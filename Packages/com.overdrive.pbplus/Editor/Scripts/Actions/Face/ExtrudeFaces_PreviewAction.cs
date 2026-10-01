using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Custom extrude methods that extend the built-in ProBuilder ExtrudeMethod enum
    /// </summary>
    public enum CustomExtrudeMethod
    {
        IndividualFaces = 0,
        VertexNormal = 1,
        FaceNormal = 2,
        Custom = 3
    }

    /// <summary>
    /// Space coordinate systems for custom extrude direction
    /// </summary>
    public enum ExtrudeSpace
    {
        Local = 0,
        Global = 1
    }

    /// <summary>
    /// Axis directions for custom extrude direction
    /// </summary>
    public enum ExtrudeAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    /// <summary>
    /// ExtrudeFaces preview action that shows cyan lines indicating where vertices will move.
    /// Supports all three extrude methods: Individual Faces, Vertex Normal, Face Normal, and Custom direction.
    /// </summary>
    [ProBuilderPlusAction("extrude_faces_preview", "Extrude",
        Tooltip = "Extrude faces with live preview",
        Instructions = "Extrude selected faces (cyan lines show new geometry)",
        IconPath = "Icons/Old/Face_Extrude",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 100)]
    public sealed class ExtrudeFacesPreviewAction : PreviewMenuAction
    {
        // Settings matching original ExtrudeFaces
        private float m_ExtrudeDistance;
        private CustomExtrudeMethod m_CustomExtrudeMethod;

        // Custom extrude settings
        private ExtrudeSpace m_ExtrudeSpace;
        private ExtrudeAxis m_ExtrudeAxis;
        
        // Preview visualization data
        private List<Vector3> m_OriginalPositions;
        private List<Vector3> m_NewPositions;
        private List<(Vector3, Vector3)> m_NewEdges; // Edges of the new geometry
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedFaces;

        public override VisualElement CreateSettingsContent()
        {
            Debug.Log("Extrude Faces Preview  - Create UI Element.");

            var root = new VisualElement();

            // Load from preferences
            m_ExtrudeDistance = UserPreferences.Tools.ExtrudeFacesDistance;
            m_CustomExtrudeMethod = (CustomExtrudeMethod)UserPreferences.Tools.ExtrudeFacesMethod;
            m_ExtrudeSpace = (ExtrudeSpace)UserPreferences.Tools.ExtrudeFacesSpace;
            m_ExtrudeAxis = (ExtrudeAxis)UserPreferences.Tools.ExtrudeFacesAxis;

            // Extrude method field
            var extrudeMethodField = new EnumField("Method", m_CustomExtrudeMethod);
            extrudeMethodField.tooltip = "Individual Faces: Each face separately. Vertex Normal: Averaged normals. Face Normal: Face normals with angle compensation. Custom: Use specified direction.";
            extrudeMethodField.RegisterCallback<ChangeEvent<System.Enum>>(evt =>
            {
                var newMethod = (CustomExtrudeMethod)evt.newValue;
                if (m_CustomExtrudeMethod != newMethod)
                {
                    m_CustomExtrudeMethod = newMethod;
                    UserPreferences.Tools.ExtrudeFacesMethod = (int)newMethod;
                    UpdateCustomControlsVisibility(root);
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            root.Add(extrudeMethodField);

            // Custom extrude controls (only visible when Custom is selected)
            
            var spaceField = new EnumField("Relative To", m_ExtrudeSpace);
            spaceField.name = "custom-space";
            spaceField.tooltip = "Local: Use object's local coordinate system. Global: Use world coordinate system.";
            spaceField.RegisterCallback<ChangeEvent<System.Enum>>(evt =>
            {
                var newSpace = (ExtrudeSpace)evt.newValue;
                if (m_ExtrudeSpace != newSpace)
                {
                    m_ExtrudeSpace = newSpace;
                    UserPreferences.Tools.ExtrudeFacesSpace = (int)newSpace;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            var axisField = new EnumField("Axis", m_ExtrudeAxis);
            axisField.name = "custom-axis";
            axisField.tooltip = "X, Y, Z: Direction along which to extrude all faces.";
            axisField.RegisterCallback<ChangeEvent<System.Enum>>(evt =>
            {
                var newAxis = (ExtrudeAxis)evt.newValue;
                if (m_ExtrudeAxis != newAxis)
                {
                    m_ExtrudeAxis = newAxis;
                    UserPreferences.Tools.ExtrudeFacesAxis = (int)newAxis;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            
            root.Add(spaceField);
            root.Add(axisField);

            // Distance field
            var distanceField = new FloatField("Distance");
            distanceField.tooltip = "Distance to extrude. Can be negative to extrude inward.";
            distanceField.SetValueWithoutNotify(m_ExtrudeDistance);
            distanceField.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                if (m_ExtrudeDistance != evt.newValue)
                {
                    m_ExtrudeDistance = evt.newValue;
                    UserPreferences.Tools.ExtrudeFacesDistance = evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(distanceField);

            // Set initial visibility
            UpdateCustomControlsVisibility(root);

            return root;
        }

        private void UpdateCustomControlsVisibility(VisualElement root)
        {
            var spaceField = root.Q<EnumField>("custom-space");
            var axisField = root.Q<EnumField>("custom-axis");
            if (spaceField != null && axisField != null)
            {
                spaceField.style.display = m_CustomExtrudeMethod == CustomExtrudeMethod.Custom ? DisplayStyle.Flex : DisplayStyle.None;
                axisField.style.display = m_CustomExtrudeMethod == CustomExtrudeMethod.Custom ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_ExtrudeDistance = UserPreferences.Tools.ExtrudeFacesDistance;
                m_CustomExtrudeMethod = (CustomExtrudeMethod)UserPreferences.Tools.ExtrudeFacesMethod;
                m_ExtrudeSpace = (ExtrudeSpace)UserPreferences.Tools.ExtrudeFacesSpace;
                m_ExtrudeAxis = (ExtrudeAxis)UserPreferences.Tools.ExtrudeFacesAxis;
            }

            // Cache current selection
            CacheCurrentSelection();

            // Subscribe to scene rendering
            SceneView.duringSceneGui += OnSceneGUI;

            // Calculate initial preview
            UpdatePreview();

        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;
            
            // Recalculate vertex movements
            CalculateVertexMovements();
            
            // Repaint scene view
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0)
                return new ActionResult(ActionResult.Status.Failure, "No faces selected");

            Undo.RecordObjects(m_CachedMeshes, "Extrude Faces");

            int extrudedFaceCount = 0;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var faces = m_CachedFaces[meshIndex];
                
                if (mesh == null || faces == null || faces.Length == 0) continue;

                mesh.ToMesh();
                mesh.Refresh(RefreshMask.Normals);

                extrudedFaceCount += faces.Length;

                // Apply extrusion based on method
                if (m_CustomExtrudeMethod == CustomExtrudeMethod.Custom)
                {
                    // Custom extrusion using specific direction
                    ApplyCustomExtrusion(mesh, faces);
                }
                else
                {
                    // Use ProBuilder's built-in Extrude method
                    ExtrudeMethod builtInMethod = (ExtrudeMethod)((int)m_CustomExtrudeMethod);
                    mesh.Extrude(faces, builtInMethod, m_ExtrudeDistance);
                }

                mesh.SetSelectedFaces(faces);
                mesh.ToMesh();
                mesh.Refresh();
            }

            ProBuilderEditor.Refresh();

            if (extrudedFaceCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Extruded {extrudedFaceCount} faces");
            else
                return new ActionResult(ActionResult.Status.Failure, "No faces to extrude");
        }

        private void ApplyCustomExtrusion(ProBuilderMesh mesh, Face[] faces)
        {
            // Step 1: Do a zero-distance extrusion to create the proper geometry structure
            // This creates the extruded faces and bridge faces but doesn't move anything
            var extrudedFaces = mesh.Extrude(faces, ExtrudeMethod.IndividualFaces, 0f);
            
            // Step 2: Move the newly extruded faces in the custom direction
            if (extrudedFaces != null && extrudedFaces.Length > 0)
            {
                Vector3 direction = GetCustomDirectionForCalculation(mesh);
                Vector3 offset = direction * m_ExtrudeDistance;
                
                // Use ProBuilder's built-in method to translate the extruded faces
                mesh.TranslateVertices(extrudedFaces, offset);
            }
        }

        private Vector3 GetCustomDirectionForApplication(ProBuilderMesh mesh)
        {
            Vector3 direction = Vector3.zero;
            
            switch (m_ExtrudeAxis)
            {
                case ExtrudeAxis.X:
                    direction = Vector3.right;
                    break;
                case ExtrudeAxis.Y:
                    direction = Vector3.up;
                    break;
                case ExtrudeAxis.Z:
                    direction = Vector3.forward;
                    break;
            }
            
            if (m_ExtrudeSpace == ExtrudeSpace.Local && mesh != null)
            {
                // Transform direction from world to local space for application
                direction = mesh.transform.InverseTransformDirection(direction).normalized;
            }
            
            return direction;
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            m_OriginalPositions = null;
            m_NewPositions = null;
            m_NewEdges = null;
            m_CachedMeshes = null;
            m_CachedFaces = null;
            
            SceneView.RepaintAll();
        }

        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedFaces = new Face[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedFaces[i] = selection[i].GetSelectedFaces();
            }
        }

        private void CalculateVertexMovements()
        {
            m_OriginalPositions = new List<Vector3>();
            m_NewPositions = new List<Vector3>();
            m_NewEdges = new List<(Vector3, Vector3)>();

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var faces = m_CachedFaces[meshIndex];
                
                if (mesh == null || faces == null || faces.Length == 0) continue;

                CalculateExtrudeGeometryForMesh(mesh, faces);
            }
        }

        private void CalculateExtrudeGeometryForMesh(ProBuilderMesh mesh, Face[] faces)
        {
            var transform = mesh.transform;
            var vertices = mesh.positions;
            
            // Ensure normals are up to date
            if (!mesh.HasArrays(MeshArrays.Normal))
                mesh.Refresh(RefreshMask.Normals);

            switch (m_CustomExtrudeMethod)
            {
                case CustomExtrudeMethod.IndividualFaces:
                    CalculateIndividualFaceExtrudeGeometry(mesh, faces, vertices, transform);
                    break;
                    
                case CustomExtrudeMethod.VertexNormal:
                case CustomExtrudeMethod.FaceNormal:
                    CalculateGroupedFaceExtrudeGeometry(mesh, faces, vertices, transform);
                    break;
                    
                case CustomExtrudeMethod.Custom:
                    CalculateCustomExtrudeGeometry(mesh, faces, vertices, transform);
                    break;
            }
        }

        private void CalculateIndividualFaceExtrudeGeometry(ProBuilderMesh mesh, Face[] faces, IList<Vector3> vertices, Transform transform)
        {
            // For individual faces, each face creates its own extruded geometry
            foreach (var face in faces)
            {
                Vector3 faceNormal = Math.Normal(mesh, face);
                Vector3 delta = faceNormal * m_ExtrudeDistance;

                // Get the face edges
                var faceEdges = face.edges;
                var faceVertices = face.distinctIndexes;
                
                // Calculate new positions for this face's vertices
                var newPositions = new Dictionary<int, Vector3>();
                foreach (var vertexIndex in faceVertices)
                {
                    newPositions[vertexIndex] = vertices[vertexIndex] + delta;
                }
                
                // Draw extruded face edges (top face)
                foreach (var edge in faceEdges)
                {
                    Vector3 start = transform.TransformPoint(newPositions[edge.a]);
                    Vector3 end = transform.TransformPoint(newPositions[edge.b]);
                    m_NewEdges.Add((start, end));
                }
                
                // Draw bridge edges (connecting original to extruded)
                foreach (var edge in faceEdges)
                {
                    Vector3 originalStart = transform.TransformPoint(vertices[edge.a]);
                    Vector3 originalEnd = transform.TransformPoint(vertices[edge.b]);
                    Vector3 extrudedStart = transform.TransformPoint(newPositions[edge.a]);
                    Vector3 extrudedEnd = transform.TransformPoint(newPositions[edge.b]);
                    
                    // Vertical edges
                    m_NewEdges.Add((originalStart, extrudedStart));
                    m_NewEdges.Add((originalEnd, extrudedEnd));
                }
            }
        }

        private void CalculateGroupedFaceExtrudeGeometry(ProBuilderMesh mesh, Face[] faces, IList<Vector3> vertices, Transform transform)
        {
            // Create a vertex-to-shared-group mapping
            var vertexToSharedGroupMap = new Dictionary<int, int>();
            for (int sharedIndex = 0; sharedIndex < mesh.sharedVertices.Count; sharedIndex++)
            {
                foreach (int vertexIndex in mesh.sharedVertices[sharedIndex])
                {
                    vertexToSharedGroupMap[vertexIndex] = sharedIndex;
                }
            }
            
            // Build normal accumulation for each shared vertex group
            var groupNormalMap = new Dictionary<int, Vector3>();
            var groupCountMap = new Dictionary<int, int>();
            var groupFirstNormalMap = new Dictionary<int, Vector3>(); // For angle compensation
            
            // Accumulate normals for each shared vertex group
            foreach (var face in faces)
            {
                Vector3 faceNormal = Math.Normal(mesh, face);
                
                foreach (var vertexIndex in face.distinctIndexes)
                {
                    // Get the shared group index for this vertex
                    int sharedGroupIndex = vertexToSharedGroupMap.ContainsKey(vertexIndex) ? vertexToSharedGroupMap[vertexIndex] : vertexIndex;
                    
                    if (!groupNormalMap.ContainsKey(sharedGroupIndex))
                    {
                        groupNormalMap[sharedGroupIndex] = Vector3.zero;
                        groupCountMap[sharedGroupIndex] = 0;
                        groupFirstNormalMap[sharedGroupIndex] = faceNormal; // Store first normal for angle calc
                    }
                    
                    groupNormalMap[sharedGroupIndex] += faceNormal;
                    groupCountMap[sharedGroupIndex]++;
                }
            }
            
            // Calculate new positions for each vertex
            var newVertexPositions = new Dictionary<int, Vector3>();
            foreach (var face in faces)
            {
                foreach (var vertexIndex in face.distinctIndexes)
                {
                    if (!newVertexPositions.ContainsKey(vertexIndex))
                    {
                        int sharedGroupIndex = vertexToSharedGroupMap.ContainsKey(vertexIndex) ? vertexToSharedGroupMap[vertexIndex] : vertexIndex;
                        
                        Vector3 accumulatedNormal = groupNormalMap[sharedGroupIndex];
                        int count = groupCountMap[sharedGroupIndex];
                        Vector3 firstNormal = groupFirstNormalMap[sharedGroupIndex];
                        
                        // Average the normal (this matches ProBuilder's calculation)
                        Vector3 averageNormal = (accumulatedNormal / count).normalized;
                        
                        // Apply angle compensation for FaceNormal method
                        float modifier = 1f;
                        if (m_CustomExtrudeMethod == CustomExtrudeMethod.FaceNormal)
                        {
                            // Use ProBuilder's secant-based angle compensation
                            float angleRad = Vector3.Angle(averageNormal, firstNormal) * Mathf.Deg2Rad;
                            modifier = Mathf.Abs(Mathf.Cos(angleRad)) > 0.001f ? 1f / Mathf.Cos(angleRad) : 1f;
                        }
                        
                        Vector3 direction = averageNormal * m_ExtrudeDistance * modifier;
                        newVertexPositions[vertexIndex] = vertices[vertexIndex] + direction;
                    }
                }
            }
            
            // Draw extruded face edges (top faces)
            foreach (var face in faces)
            {
                var faceEdges = face.edges;
                foreach (var edge in faceEdges)
                {
                    Vector3 start = transform.TransformPoint(newVertexPositions[edge.a]);
                    Vector3 end = transform.TransformPoint(newVertexPositions[edge.b]);
                    m_NewEdges.Add((start, end));
                }
            }
            
            // Draw bridge edges (connecting original to extruded)
            // For grouped extrusion, we need to find the perimeter edges
            var perimeterEdges = GetPerimeterEdges(faces, mesh);
            foreach (var edge in perimeterEdges)
            {
                Vector3 originalStart = transform.TransformPoint(vertices[edge.a]);
                Vector3 originalEnd = transform.TransformPoint(vertices[edge.b]);
                Vector3 extrudedStart = transform.TransformPoint(newVertexPositions[edge.a]);
                Vector3 extrudedEnd = transform.TransformPoint(newVertexPositions[edge.b]);
                
                // Vertical edges for the bridges
                m_NewEdges.Add((originalStart, extrudedStart));
                m_NewEdges.Add((originalEnd, extrudedEnd));
                
                // Bridge face edges (connecting the bridge faces)
                m_NewEdges.Add((originalStart, originalEnd));
                m_NewEdges.Add((extrudedStart, extrudedEnd));
            }
        }

        private List<Edge> GetPerimeterEdges(Face[] faces, ProBuilderMesh mesh)
        {
            // Find edges that are only used by one of the selected faces (perimeter edges)
            var edgeCount = new Dictionary<Edge, int>();
            var allEdges = new List<Edge>();
            
            foreach (var face in faces)
            {
                foreach (var edge in face.edges)
                {
                    var normalizedEdge = new Edge(Mathf.Min(edge.a, edge.b), Mathf.Max(edge.a, edge.b));
                    if (edgeCount.ContainsKey(normalizedEdge))
                        edgeCount[normalizedEdge]++;
                    else
                        edgeCount[normalizedEdge] = 1;
                    allEdges.Add(normalizedEdge);
                }
            }
            
            // Return edges that appear only once (perimeter)
            var perimeterEdges = new List<Edge>();
            foreach (var kvp in edgeCount)
            {
                if (kvp.Value == 1)
                    perimeterEdges.Add(kvp.Key);
            }
            
            return perimeterEdges;
        }

        private void CalculateCustomExtrudeGeometry(ProBuilderMesh mesh, Face[] faces, IList<Vector3> vertices, Transform transform)
        {
            // Calculate custom direction in local space for vertex calculations
            Vector3 customDirectionLocal = GetCustomDirectionForCalculation(mesh);
            
            foreach (var face in faces)
            {
                var faceEdges = face.edges;
                var faceVertices = face.distinctIndexes;
                
                // Calculate new positions for this face's vertices using custom direction
                var newPositions = new Dictionary<int, Vector3>();
                foreach (var vertexIndex in faceVertices)
                {
                    newPositions[vertexIndex] = vertices[vertexIndex] + customDirectionLocal * m_ExtrudeDistance;
                }
                
                // Draw extruded face edges (top face)
                foreach (var edge in faceEdges)
                {
                    Vector3 start = transform.TransformPoint(newPositions[edge.a]);
                    Vector3 end = transform.TransformPoint(newPositions[edge.b]);
                    m_NewEdges.Add((start, end));
                }
                
                // Draw bridge edges (connecting original to extruded)
                foreach (var edge in faceEdges)
                {
                    Vector3 originalStart = transform.TransformPoint(vertices[edge.a]);
                    Vector3 originalEnd = transform.TransformPoint(vertices[edge.b]);
                    Vector3 extrudedStart = transform.TransformPoint(newPositions[edge.a]);
                    Vector3 extrudedEnd = transform.TransformPoint(newPositions[edge.b]);
                    
                    // Vertical edges
                    m_NewEdges.Add((originalStart, extrudedStart));
                    m_NewEdges.Add((originalEnd, extrudedEnd));
                    
                    // Original edge
                    m_NewEdges.Add((originalStart, originalEnd));
                }
            }
        }

        private Vector3 GetCustomDirectionForCalculation(ProBuilderMesh mesh)
        {
            Vector3 direction = Vector3.zero;
            
            switch (m_ExtrudeAxis)
            {
                case ExtrudeAxis.X:
                    direction = Vector3.right;
                    break;
                case ExtrudeAxis.Y:
                    direction = Vector3.up;
                    break;
                case ExtrudeAxis.Z:
                    direction = Vector3.forward;
                    break;
            }
            
            if (m_ExtrudeSpace == ExtrudeSpace.Global)
            {
                // Global space: Convert world direction to local space for mesh calculations
                direction = mesh.transform.InverseTransformDirection(direction).normalized;
            }
            // For local space, keep the direction as-is (it's already in local space)
            
            return direction;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawExtrudePreview();
        }

        private void DrawExtrudePreview()
        {
            if (m_NewEdges == null) return;

            // Draw cyan edges showing the new geometry structure
            Handles.color = Color.cyan;
            
            foreach (var edge in m_NewEdges)
            {
                Handles.DrawAAPolyLine(3f, edge.Item1, edge.Item2);
            }
        }
    }
}

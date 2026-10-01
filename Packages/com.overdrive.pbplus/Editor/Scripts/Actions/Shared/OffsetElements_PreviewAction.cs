using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Unified Offset Elements preview action for moving selected elements by a specified vector.
    /// Supports different coordinate spaces: World, Local, and Face Normal.
    /// </summary>
    public abstract class OffsetElementsPreviewActionBase : PreviewMenuAction
    {
        // Coordinate space options
        public enum CoordinateSpace
        {
            World,
            Local,
            FaceNormal
        }
        
        protected CoordinateSpace m_CoordinateSpace = CoordinateSpace.World;
        protected Vector3 m_OffsetVector = Vector3.up;
        
        // Preview state
        protected ProBuilderMesh[] m_CachedMeshes;
        protected int[][] m_CachedVertexIndices; // Affected vertex indices per mesh
        protected Vector3[][] m_OriginalPositions; // Original positions in world space
        protected Vector3[][] m_PreviewPositions; // Target positions in world space
        protected List<(Vector3, Vector3)>[] m_PreviewLines; // Lines from original to target

        /// <summary>
        /// Derived classes must implement this to get affected vertex indices for their selection type
        /// </summary>
        protected abstract int[] GetAffectedVertices(ProBuilderMesh mesh);

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_CoordinateSpace = (CoordinateSpace)Overdrive.ProBuilderPlus.UserPreferences.Tools.OffsetCoordinateSpace;
            m_OffsetVector = Overdrive.ProBuilderPlus.UserPreferences.Tools.OffsetVector;

            // Coordinate space dropdown
            var spaceField = new EnumField("Relative To",m_CoordinateSpace);
            spaceField.tooltip = "World: Offset in world coordinates\nLocal: Offset in object's local coordinates\nFace Normal: Offset along average face normal";
            spaceField.RegisterCallback<ChangeEvent<System.Enum>>(evt =>
            {
                m_CoordinateSpace = (CoordinateSpace)evt.newValue;
                UserPreferences.Tools.OffsetCoordinateSpace = (int)m_CoordinateSpace;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(spaceField);

            // Offset vector field
            var offsetField = new Vector3Field("Offset Amount");
            offsetField.tooltip = "The vector to offset selected elements by";
            offsetField.SetValueWithoutNotify(m_OffsetVector);
            offsetField.RegisterCallback<ChangeEvent<Vector3>>(evt =>
            {
                m_OffsetVector = evt.newValue;
                UserPreferences.Tools.OffsetVector = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(offsetField);

            // Instructions are now handled by the framework via the Instructions attribute

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_CoordinateSpace = (CoordinateSpace)UserPreferences.Tools.OffsetCoordinateSpace;
            m_OffsetVector = UserPreferences.Tools.OffsetVector;

            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedVertexIndices = new int[selection.Length][];
            m_OriginalPositions = new Vector3[selection.Length][];
            m_PreviewPositions = new Vector3[selection.Length][];
            m_PreviewLines = new List<(Vector3, Vector3)>[selection.Length];

            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                var vertexIndices = GetAffectedVertices(mesh);
                m_CachedVertexIndices[i] = vertexIndices;
                
                // Cache original positions in world space
                var vertices = mesh.GetVertices();
                var originalPositions = new Vector3[vertexIndices.Length];
                for (int j = 0; j < vertexIndices.Length; j++)
                {
                    originalPositions[j] = mesh.transform.TransformPoint(vertices[vertexIndices[j]].position);
                }
                m_OriginalPositions[i] = originalPositions;
                
            }

            // Calculate initial preview
            CalculatePreviewPositions();

            // Subscribe to scene GUI for preview visualization
            SceneView.duringSceneGui += OnSceneGUI;
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            // Recalculate preview positions with new settings
            CalculatePreviewPositions();
            SceneView.RepaintAll();
        }

        private void CalculatePreviewPositions()
        {
            if (m_CachedMeshes == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var originalPositions = m_OriginalPositions[meshIndex];
                var previewPositions = new Vector3[originalPositions.Length];
                var previewLines = new List<(Vector3, Vector3)>();

                // Calculate offset based on coordinate space
                Vector3 worldOffset = CalculateWorldOffset(mesh);

                for (int i = 0; i < originalPositions.Length; i++)
                {
                    var originalPos = originalPositions[i];
                    var targetPos = originalPos + worldOffset;
                    
                    previewPositions[i] = targetPos;
                    previewLines.Add((originalPos, targetPos));
                }

                m_PreviewPositions[meshIndex] = previewPositions;
                m_PreviewLines[meshIndex] = previewLines;
            }
        }

        private Vector3 CalculateWorldOffset(ProBuilderMesh mesh)
        {
            switch (m_CoordinateSpace)
            {
                case CoordinateSpace.World:
                    return m_OffsetVector;

                case CoordinateSpace.Local:
                    return mesh.transform.TransformDirection(m_OffsetVector);

                case CoordinateSpace.FaceNormal:
                    var avgNormal = CalculateAverageFaceNormal(mesh);
                    var magnitude = m_OffsetVector.magnitude;
                    return avgNormal * magnitude;

                default:
                    return m_OffsetVector;
            }
        }

        private Vector3 CalculateAverageFaceNormal(ProBuilderMesh mesh)
        {
            var faces = GetAffectedFaces(mesh);
            if (faces.Length == 0) return Vector3.up;

            Vector3 avgNormal = Vector3.zero;
            foreach (var face in faces)
            {
                avgNormal += Math.Normal(mesh, face);
            }
            avgNormal /= faces.Length;
            
            // Transform to world space
            return mesh.transform.TransformDirection(avgNormal.normalized);
        }

        /// <summary>
        /// Get faces affected by the current selection - used for face normal calculation
        /// </summary>
        protected virtual Face[] GetAffectedFaces(ProBuilderMesh mesh)
        {
            // Default implementation: get all faces
            return mesh.faces.ToArray();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null)
                return new ActionResult(ActionResult.Status.Failure, "No cached selection");

            var result = new ActionResult(ActionResult.Status.Success, "Offset Elements completed successfully");

            foreach (var mesh in m_CachedMeshes)
            {
                // Record undo
                Undo.RecordObject(mesh, "Offset Elements");
                Undo.RecordObject(mesh.GetComponent<MeshRenderer>(), "Offset Elements");
                Undo.RecordObject(mesh.GetComponent<MeshFilter>(), "Offset Elements");

                try
                {
                    var vertexIndices = GetAffectedVertices(mesh);
                    
                    // Calculate local space offset for this mesh
                    Vector3 localOffset = CalculateLocalOffset(mesh);

                    // Apply offset using ProBuilder's TranslateVertices method
                    mesh.TranslateVertices(vertexIndices, localOffset);

                    // Refresh the mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to apply offset to {mesh.name}: {e.Message}");
                    result = new ActionResult(ActionResult.Status.Failure, $"Failed to apply offset: {e.Message}");
                }
            }

            ProBuilderEditor.Refresh();
            return result;
        }

        private Vector3 CalculateLocalOffset(ProBuilderMesh mesh)
        {
            switch (m_CoordinateSpace)
            {
                case CoordinateSpace.World:
                    // Transform world offset to local space
                    return mesh.transform.InverseTransformDirection(m_OffsetVector);

                case CoordinateSpace.Local:
                    return m_OffsetVector;

                case CoordinateSpace.FaceNormal:
                    // Calculate average face normal in local space
                    var faces = GetAffectedFaces(mesh);
                    if (faces.Length == 0) return Vector3.up * m_OffsetVector.magnitude;

                    Vector3 avgNormal = Vector3.zero;
                    foreach (var face in faces)
                    {
                        avgNormal += Math.Normal(mesh, face);
                    }
                    avgNormal /= faces.Length;
                    
                    var magnitude = m_OffsetVector.magnitude;
                    return avgNormal.normalized * magnitude;

                default:
                    return m_OffsetVector;
            }
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            // Clear all preview state
            m_CachedMeshes = null;
            m_CachedVertexIndices = null;
            m_OriginalPositions = null;
            m_PreviewPositions = null;
            m_PreviewLines = null;
            
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_PreviewLines == null || m_OriginalPositions == null || m_PreviewPositions == null) return;

            // Set z-test to always show preview on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int meshIndex = 0; meshIndex < m_PreviewLines.Length; meshIndex++)
            {
                var lines = m_PreviewLines[meshIndex];
                var originalPositions = m_OriginalPositions[meshIndex];
                var previewPositions = m_PreviewPositions[meshIndex];
                
                if (lines == null || originalPositions == null || previewPositions == null) continue;

                // Draw cyan lines showing movement direction and distance
                Handles.color = Color.cyan;
                foreach (var line in lines)
                {
                    Handles.DrawAAPolyLine(3f, line.Item1, line.Item2);
                }

                // Draw gray dots at original positions
                Handles.color = Color.gray;
                foreach (var pos in originalPositions)
                {
                    float handleSize = UnityEditor.HandleUtility.GetHandleSize(pos) * 0.06f;
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, handleSize, EventType.Repaint);
                }

                // Draw cyan dots at target positions
                Handles.color = Color.cyan;
                foreach (var pos in previewPositions)
                {
                    float handleSize = UnityEditor.HandleUtility.GetHandleSize(pos) * 0.06f;
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, handleSize, EventType.Repaint);
                }
            }
        }
    }

    /// <summary>
    /// Offset Elements action for vertex selection mode.
    /// </summary>

    [ProBuilderPlusAction("offset-vertices", "Offset",
        Tooltip = "Move selected vertices by a specified vector with live preview",
        Instructions = "Cyan lines and dots show offset results",
        IconPath = "Icons/Old/Offset_Elements",
        ValidModes = ToolMode.Vertex,
        VerticesSelectedRequired = 1,
        Order = 200)]
    public sealed class OffsetVerticesPreviewAction : OffsetElementsPreviewActionBase
    {
        protected override int[] GetAffectedVertices(ProBuilderMesh mesh)
        {
            return mesh.selectedVertices.ToArray();
        }

        protected override Face[] GetAffectedFaces(ProBuilderMesh mesh)
        {
            // For vertices, get faces that contain selected vertices
            var selectedVertices = mesh.selectedVertices;
            var affectedFaces = new List<Face>();

            foreach (var face in mesh.faces)
            {
                if (face.distinctIndexes.Any(idx => selectedVertices.Contains(idx)))
                {
                    affectedFaces.Add(face);
                }
            }

            return affectedFaces.ToArray();
        }
    }

    /// <summary>
    /// Offset Elements action for edge selection mode.
    /// </summary>

    [ProBuilderPlusAction("offset-edges", "Offset",
        Tooltip = "Move selected edges by a specified vector with live preview",
        Instructions = "Cyan lines and dots show offset results",
        IconPath = "Icons/Old/Offset_Elements",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 201)]
    public sealed class OffsetEdgesPreviewAction : OffsetElementsPreviewActionBase
    {
        protected override int[] GetAffectedVertices(ProBuilderMesh mesh)
        {
            var selectedEdges = mesh.selectedEdges;
            var vertexIndices = new HashSet<int>();

            foreach (var edge in selectedEdges)
            {
                vertexIndices.Add(edge.a);
                vertexIndices.Add(edge.b);
            }

            return vertexIndices.ToArray();
        }

        protected override Face[] GetAffectedFaces(ProBuilderMesh mesh)
        {
            // For edges, get faces that contain selected edges
            var selectedEdges = mesh.selectedEdges;
            var affectedFaces = new List<Face>();

            foreach (var face in mesh.faces)
            {
                foreach (var faceEdge in face.edges)
                {
                    if (selectedEdges.Any(selectedEdge => 
                        (selectedEdge.a == faceEdge.a && selectedEdge.b == faceEdge.b) ||
                        (selectedEdge.a == faceEdge.b && selectedEdge.b == faceEdge.a)))
                    {
                        affectedFaces.Add(face);
                        break;
                    }
                }
            }

            return affectedFaces.ToArray();
        }
    }

    /// <summary>
    /// Offset Elements action for face selection mode.
    /// </summary>
    [ProBuilderPlusAction("offset-faces", "Offset",
        Tooltip = "Move selected faces by a specified vector with live preview",
        Instructions = "Cyan lines and dots show offset results",
        IconPath = "Icons/Old/Offset_Elements",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 202)]
    public sealed class OffsetFacesPreviewAction : OffsetElementsPreviewActionBase
    {
        protected override int[] GetAffectedVertices(ProBuilderMesh mesh)
        {
            var selectedFaces = mesh.GetSelectedFaces();
            var vertexIndices = new HashSet<int>();

            foreach (var face in selectedFaces)
            {
                foreach (var index in face.distinctIndexes)
                {
                    vertexIndices.Add(index);
                }
            }

            return vertexIndices.ToArray();
        }

        protected override Face[] GetAffectedFaces(ProBuilderMesh mesh)
        {
            return mesh.GetSelectedFaces();
        }
    }
}

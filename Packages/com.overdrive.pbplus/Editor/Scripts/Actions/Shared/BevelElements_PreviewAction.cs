using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: Remove magenta lines

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Unified Bevel Elements preview action base class supporting Vertices, Edges, and Faces.
    /// Vertices: Split vertex and create center face oriented toward average normal of connected faces
    /// Edges/Faces: Use ProBuilder's edge beveling with optional perimeter-only filtering
    /// </summary>
    public abstract class BevelElementsPreviewActionBase : PreviewMenuAction
    {
        // Settings
        protected float m_BevelDistance = 0.2f;
        protected bool m_PerimeterOnly = false; // Only applies to Edge/Face modes

        // Cached data for applying changes
        protected ProBuilderMesh[] m_CachedMeshes;
        protected Edge[][] m_CachedEdges; // For Edge/Face modes
        protected int[][] m_CachedVertices; // For Vertex mode
        protected Edge[][] m_TargetEdges; // For preview highlighting
        protected List<BevelPreviewLine>[] m_PreviewBevelLines; // For bevel line preview

        /// <summary>
        /// Represents a line that will be created by the bevel operation
        /// </summary>
        public struct BevelPreviewLine
        {
            public Vector3 startPosition;
            public Vector3 endPosition;
            public bool isValidLine;
            public bool isBoundaryEdge;

            public BevelPreviewLine(Vector3 start, Vector3 end, bool valid = true, bool isBoundary = false)
            {
                startPosition = start;
                endPosition = end;
                isValidLine = valid;
                isBoundaryEdge = isBoundary;
            }
        }

        /// <summary>
        /// Returns true if this mode uses edge-based beveling (Edge/Face modes)
        /// </summary>
        protected abstract bool UsesEdgeBeveling();

        /// <summary>
        /// Returns true if this is face mode (for UI features specific to faces)
        /// </summary>
        protected abstract bool IsFaceMode();

        /// <summary>
        /// For edge-based modes, convert selection to edges
        /// </summary>
        protected virtual Edge[] GetTargetEdges(ProBuilderMesh mesh)
        {
            throw new System.NotImplementedException("Edge-based modes must implement GetTargetEdges");
        }

        /// <summary>
        /// For vertex mode, get selected vertices
        /// </summary>
        protected virtual int[] GetTargetVertices(ProBuilderMesh mesh)
        {
            throw new System.NotImplementedException("Vertex mode must implement GetTargetVertices");
        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_BevelDistance = UserPreferences.Tools.BevelDistance;
            m_PerimeterOnly = UserPreferences.Tools.BevelPerimeterOnly;

            // Distance field
            var distanceField = new FloatField("Distance");
            distanceField.tooltip = "Distance to bevel edges";
            distanceField.SetValueWithoutNotify(m_BevelDistance);
            distanceField.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                if (m_BevelDistance != evt.newValue)
                {
                    m_BevelDistance = Mathf.Max(0.001f, evt.newValue);
                    UserPreferences.Tools.BevelDistance = m_BevelDistance;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(distanceField);

            // Perimeter Only checkbox (only for Face mode)
            if (IsFaceMode())
            {
                var perimeterToggle = new Toggle("Perimeter Only");
                perimeterToggle.tooltip = "When enabled, only bevel edges on the perimeter of the selection, ignoring interior edges between selected elements. " +
                                        "Perimeter edges are highlighted in magenta when in preview mode.";
                perimeterToggle.SetValueWithoutNotify(m_PerimeterOnly);
                perimeterToggle.RegisterValueChangedCallback(evt =>
                {
                    if (m_PerimeterOnly != evt.newValue)
                    {
                        m_PerimeterOnly = evt.newValue;
                        UserPreferences.Tools.BevelPerimeterOnly = evt.newValue;
                        // Recache selection with new perimeter setting
                        CacheCurrentSelection();
                        PreviewActionFramework.RequestPreviewUpdate();
                    }
                });
                root.Add(perimeterToggle);
            }

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_BevelDistance = UserPreferences.Tools.BevelDistance;
            m_PerimeterOnly = UserPreferences.Tools.BevelPerimeterOnly;

            // Cache the current selection for later application
            CacheCurrentSelection();

            // Update preview calculations
            UpdatePreview();

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Caches the current selection state for later application.
        /// </summary>
        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;

            if (UsesEdgeBeveling())
            {
                // Cache edges for Edge/Face modes - use the same logic as preview
                m_CachedEdges = new Edge[selection.Length][];
                for (int i = 0; i < selection.Length; i++)
                {
                    // Use GetTargetEdges which already handles perimeter filtering
                    m_CachedEdges[i] = GetTargetEdges(selection[i]);
                }
            }
            else
            {
                // Cache vertices for Vertex mode (currently disabled)
                m_CachedVertices = new int[selection.Length][];
                for (int i = 0; i < selection.Length; i++)
                {
                    m_CachedVertices[i] = GetTargetVertices(selection[i]);
                }
            }
        }

        /// <summary>
        /// Override to handle selection changes during preview by refreshing cache and calculations.
        /// </summary>
        internal override void OnSelectionChangedDuringPreview()
        {
            // Update our cached selection to match the new selection
            CacheCurrentSelection();

            // Update the preview calculations for the new selection
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            try
            {
                // Update target edges for highlighting
                CacheTargetEdges();

                // Force scene view repaint to show updated preview
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating bevel preview: {ex.Message}");
            }
        }

        public override ActionResult ApplyChanges()
        {
            // Validate that we have cached data
            if (m_CachedMeshes == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached bevel data available");
            }

            if (UsesEdgeBeveling())
            {
                return ApplyEdgeBevelChanges();
            }
            else
            {
                return ApplyVertexBevelChanges();
            }
        }

        private ActionResult ApplyEdgeBevelChanges()
        {
            if (m_CachedEdges == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached edge data available");
            }

            // Record undo for the mesh modifications
            Undo.RecordObjects(m_CachedMeshes, "Bevel Elements");

            int totalBevelsCreated = 0;

            foreach (ProBuilderMesh mesh in m_CachedMeshes)
            {
                var meshIndex = System.Array.IndexOf(m_CachedMeshes, mesh);
                var edges = m_CachedEdges[meshIndex];

                if (edges == null || edges.Length == 0) continue;

                // Apply the bevel using ProBuilder's built-in method
                mesh.ToMesh();
                var beveledFaces = Bevel.BevelEdges(mesh, edges, m_BevelDistance);

                if (beveledFaces != null && beveledFaces.Count > 0)
                {
                    totalBevelsCreated++;

                    // Set the beveled faces as the new selection
                    mesh.SetSelectedFaces(beveledFaces.ToArray());

                    // Finalize the mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    mesh.Optimize();
                }
            }

            ProBuilderEditor.Refresh();

            if (totalBevelsCreated > 0)
                return new ActionResult(ActionResult.Status.Success, $"Bevel Elements Applied to {totalBevelsCreated} mesh(es)");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to apply bevel to any meshes");
        }

        private ActionResult ApplyVertexBevelChanges()
        {
            if (m_CachedVertices == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached vertex data available");
            }

            // Record undo for the mesh modifications
            Undo.RecordObjects(m_CachedMeshes, "Bevel Elements");

            int totalVerticesBeveled = 0;

            foreach (ProBuilderMesh mesh in m_CachedMeshes)
            {
                var meshIndex = System.Array.IndexOf(m_CachedMeshes, mesh);
                var vertices = m_CachedVertices[meshIndex];

                if (vertices == null || vertices.Length == 0) continue;

                // Apply vertex beveling
                var result = BevelVertices(mesh, vertices, m_BevelDistance);

                if (result > 0)
                {
                    totalVerticesBeveled += result;

                    // Finalize the mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    mesh.Optimize();
                }
            }

            ProBuilderEditor.Refresh();

            if (totalVerticesBeveled > 0)
                return new ActionResult(ActionResult.Status.Success, $"Beveled {totalVerticesBeveled} vertices");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to bevel any vertices");
        }

        /// <summary>
        /// Bevels vertices by splitting them and creating center faces.
        /// Returns the number of vertices successfully beveled.
        /// </summary>
        private int BevelVertices(ProBuilderMesh mesh, int[] vertexIndices, float distance)
        {
            return BevelElements_Helper.BevelVertices(mesh, vertexIndices, distance);
        }

        public override void CleanupPreview()
        {
            // Unsubscribe from scene GUI
            SceneView.duringSceneGui -= OnSceneGUI;

            // Clear cached data
            m_CachedMeshes = null;
            m_CachedEdges = null;
            m_CachedVertices = null;
            m_TargetEdges = null;
            m_PreviewBevelLines = null;

            // Repaint scene view
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Cache the target edges for preview highlighting based on current selection and settings
        /// </summary>
        private void CacheTargetEdges()
        {
            if (m_CachedMeshes == null) return;

            m_TargetEdges = new Edge[m_CachedMeshes.Length][];
            m_PreviewBevelLines = new List<BevelPreviewLine>[m_CachedMeshes.Length];

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                if (mesh == null) continue;

                if (UsesEdgeBeveling())
                {
                    // For edge/face modes, get the target edges
                    m_TargetEdges[i] = GetTargetEdges(mesh);

                    // Calculate bevel preview lines for these edges
                    m_PreviewBevelLines[i] = CalculateBevelPreviewLines(mesh, m_TargetEdges[i]);

                }
                else
                {
                    // For vertex mode, convert vertices to connected edges (if not disabled)
                    m_TargetEdges[i] = new Edge[0]; // Empty for now since vertex mode is disabled
                    m_PreviewBevelLines[i] = new List<BevelPreviewLine>();
                }
            }
        }

        /// <summary>
        /// Calculate bevel preview lines for the given edges.
        /// For each edge, creates two parallel lines (one for each adjacent face) pushed inward by the bevel distance.
        /// </summary>
        private List<BevelPreviewLine> CalculateBevelPreviewLines(ProBuilderMesh mesh, Edge[] edges)
        {
            var previewLines = new List<BevelPreviewLine>();

            if (edges == null || edges.Length == 0) return previewLines;

            var vertices = mesh.GetVertices();

            // Create winged edge representation for proper topology navigation
            var wingedEdges = WingedEdge.GetWingedEdges(mesh);

            foreach (var edge in edges)
            {
                if (edge.a >= vertices.Length || edge.b >= vertices.Length) continue;

                var vertexA = vertices[edge.a];
                var vertexB = vertices[edge.b];
                var edgeVector = (vertexB.position - vertexA.position).normalized;

                // Find the winged edge that corresponds to this edge
                var wingedEdge = FindWingedEdgeForEdge(wingedEdges, edge);

                if (wingedEdge == null) continue;

                // Check if this is a boundary edge (only one face) or internal edge (two faces)
                bool isBoundaryEdge = wingedEdge.opposite == null;


                // Always add the line for the primary face
                if (wingedEdge.face != null)
                {
                    var bevelLine = CalculateBevelLineForFace(mesh, edge, wingedEdge.face, vertices);
                    if (bevelLine.HasValue)
                    {
                        previewLines.Add(new BevelPreviewLine(bevelLine.Value.start, bevelLine.Value.end, true, isBoundaryEdge));
                    }
                }

                // Add the line for the opposite face if it exists (internal edge)
                if (!isBoundaryEdge && wingedEdge.opposite?.face != null)
                {
                    var bevelLine = CalculateBevelLineForFace(mesh, edge, wingedEdge.opposite.face, vertices);
                    if (bevelLine.HasValue)
                    {
                        previewLines.Add(new BevelPreviewLine(bevelLine.Value.start, bevelLine.Value.end, true, false));
                    }
                }
            }

            return previewLines;
        }

        /// <summary>
        /// Find the WingedEdge that corresponds to the given edge
        /// </summary>
        private WingedEdge FindWingedEdgeForEdge(List<WingedEdge> wingedEdges, Edge edge)
        {
            foreach (var we in wingedEdges)
            {
                if ((we.edge.local.a == edge.a && we.edge.local.b == edge.b) ||
                    (we.edge.local.a == edge.b && we.edge.local.b == edge.a))
                {
                    return we;
                }
            }
            return null;
        }

        /// <summary>
        /// Calculate a bevel line for a specific face adjacent to an edge
        /// </summary>
        private (Vector3 start, Vector3 end)? CalculateBevelLineForFace(ProBuilderMesh mesh, Edge edge, Face face, UnityEngine.ProBuilder.Vertex[] vertices)
        {
            if (edge.a >= vertices.Length || edge.b >= vertices.Length) return null;

            var vertexA = vertices[edge.a];
            var vertexB = vertices[edge.b];
            var edgeVector = (vertexB.position - vertexA.position).normalized;

            // Calculate face normal
            var faceNormal = Math.Normal(mesh, face);

            // Calculate the direction perpendicular to both the edge and face normal
            var inwardDirection = Vector3.Cross(faceNormal, edgeVector).normalized;

            // Determine which direction is "inward" for this face
            var faceCenter = GetFaceCenter(mesh, face);
            var edgeCenter = (vertexA.position + vertexB.position) * 0.5f;
            var towardsFaceCenter = (faceCenter - edgeCenter).normalized;

            // Choose the inward direction that points toward the face interior
            if (Vector3.Dot(inwardDirection, towardsFaceCenter) < 0)
            {
                inwardDirection = -inwardDirection;
            }

            // Calculate the beveled edge points
            var beveledA = vertexA.position + inwardDirection * m_BevelDistance;
            var beveledB = vertexB.position + inwardDirection * m_BevelDistance;

            return (beveledA, beveledB);
        }

        /// <summary>
        /// Calculate the center position of a face
        /// </summary>
        private Vector3 GetFaceCenter(ProBuilderMesh mesh, Face face)
        {
            var vertices = mesh.GetVertices();
            var center = Vector3.zero;

            foreach (var index in face.distinctIndexes)
            {
                if (index < vertices.Length)
                {
                    center += vertices[index].position;
                }
            }

            return center / face.distinctIndexes.Count;
        }

        /// <summary>
        /// Handle scene GUI drawing for edge preview highlighting
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_TargetEdges == null || m_CachedMeshes == null) return;

            // Set z-test to respect depth so lines are occluded by geometry
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var edges = m_TargetEdges[i];
                var bevelLines = m_PreviewBevelLines?[i];

                if (mesh == null || edges == null || edges.Length == 0) continue;

                // 1. Highlight original target edges in magenta (thinner)
                Handles.color = new Color(1f, 0f, 1f, 0.6f); // Magenta with more transparency
                var vertices = mesh.GetVertices();

                foreach (var edge in edges)
                {
                    if (edge.a < vertices.Length && edge.b < vertices.Length)
                    {
                        Vector3 startPos = mesh.transform.TransformPoint(vertices[edge.a].position);
                        Vector3 endPos = mesh.transform.TransformPoint(vertices[edge.b].position);

                        // Draw thinner line for original edges
                        Handles.DrawAAPolyLine(3f, startPos, endPos);
                    }
                }

                // 2. Draw bevel preview lines with different colors for boundary vs internal
                if (bevelLines != null && bevelLines.Count > 0)
                {
                    foreach (var bevelLine in bevelLines)
                    {
                        if (bevelLine.isValidLine)
                        {
                            // Color boundary edges magenta, internal edges cyan
                            Handles.color = bevelLine.isBoundaryEdge ?
                                new Color(1f, 0f, 1f, 0.9f) :  // Magenta for boundary
                                new Color(0f, 1f, 1f, 0.9f);   // Cyan for internal

                            Vector3 startPos = mesh.transform.TransformPoint(bevelLine.startPosition);
                            Vector3 endPos = mesh.transform.TransformPoint(bevelLine.endPosition);

                            // Draw thicker line for bevel preview
                            Handles.DrawAAPolyLine(5f, startPos, endPos);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Bevel Elements action for vertex selection mode.
    /// Splits selected vertices and creates center faces oriented toward average normal of connected faces.
    /// DISABLED: Vertex beveling is temporarily disabled - complex implementation needs more work
    /// </summary>
    // [ProBuilderPlusAction("bevel-vertices", "Bevel",
    //     Tooltip = "Bevel selected vertices by splitting them and creating center faces oriented toward the average normal of connected faces",
    //     IconPath = "Icons/Old/Edge_Bevel",
    //     ValidModes = SelectMode.Vertex,
    //     VerticesSelectedRequired = 1,
    //     Order = 120)]
    public sealed class BevelVerticesPreviewAction : BevelElementsPreviewActionBase
    {
        protected override bool UsesEdgeBeveling()
        {
            return false;
        }

        protected override bool IsFaceMode()
        {
            return false;
        }

        protected override int[] GetTargetVertices(ProBuilderMesh mesh)
        {
            return mesh.selectedVertices.ToArray();
        }
    }

    /// <summary>
    /// Bevel Elements action for edge selection mode.
    /// Uses ProBuilder's edge beveling with optional perimeter-only filtering.
    /// </summary>
    [ProBuilderPlusAction("bevel-edges", "Bevel",
        Tooltip = "Bevel selected edges using ProBuilder's edge beveling with optional perimeter-only filtering",
        IconPath = "Icons/Old/Edge_Bevel",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 121)]
    public sealed class BevelEdgesPreviewAction : BevelElementsPreviewActionBase
    {
        protected override bool UsesEdgeBeveling()
        {
            return true;
        }

        protected override bool IsFaceMode()
        {
            return false;
        }

        protected override Edge[] GetTargetEdges(ProBuilderMesh mesh)
        {
            return mesh.selectedEdges.ToArray();
        }
    }

    /// <summary>
    /// Bevel Elements action for face selection mode.
    /// Converts selected faces to edges and bevels them with optional perimeter-only filtering.
    /// </summary>
    [ProBuilderPlusAction("bevel-faces", "Bevel",
        Tooltip = "Bevel selected faces by converting them to edges and beveling with optional perimeter-only filtering",
        IconPath = "Icons/Old/Edge_Bevel",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 122)]
    public sealed class BevelFacesPreviewAction : BevelElementsPreviewActionBase
    {
        protected override bool UsesEdgeBeveling()
        {
            return true;
        }

        protected override bool IsFaceMode()
        {
            return true;
        }

        protected override Edge[] GetTargetEdges(ProBuilderMesh mesh)
        {
            // Convert selected faces to edges
            var selectedFaces = mesh.GetSelectedFaces();

            if (m_PerimeterOnly)
            {
                // Use ElementSelection.GetPerimeterEdges for perimeter-only filtering
                return ElementSelection.GetPerimeterEdges(mesh, selectedFaces).ToArray();
            }
            else
            {
                // Get all edges from selected faces
                var edges = new HashSet<Edge>();
                foreach (var face in selectedFaces)
                {
                    foreach (var edge in face.edges)
                    {
                        edges.Add(edge);
                    }
                }
                return edges.ToArray();
            }
        }
    }
}

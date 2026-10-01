using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: Mode-unique icons
// TODO: (BUG) Errors when more than 2 faces selected in Dissolve mode

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Unified Remove Elements preview action with Delete and Dissolve modes<br/>
    /// - Delete mode: Removes selected elements and dependent faces (simple deletion)<br/>
    /// - Dissolve mode: Removes interior elements while preserving topology (smart dissolution).
    /// </summary>
    public abstract class RemoveElementsPreviewActionBase : PreviewMenuAction
    {
        // Mode settings
        protected enum RemoveMode
        {
            Delete = 0,
            Dissolve = 1
        }

        protected RemoveMode m_CurrentMode = RemoveMode.Dissolve;
        protected float m_ExtrudeDistance = 1.0f; // Only used in Dissolve mode

        // Delete mode preview state
        protected ProBuilderMesh[] m_CachedMeshes;
        protected Face[][] m_CachedFacesToDelete;
        protected Vector3[][] m_FaceCenterPositions;

        // Dissolve mode preview state (from RemoveInteriorElements)
        protected List<(Vector3, Vector3)>[] m_PreviewEdges;
        protected Vector3[] m_FillVertexPositions;
        protected Face[][] m_CachedFaces;
        protected Edge[][] m_CachedPerimeterEdges;
        protected List<List<(Vector3, Vector3)>>[] m_PreviewEdgeGroups;
        protected List<(Vector3, Vector3)>[] m_SecondaryEdgeGroups;
        protected List<List<(Vector3, Vector3)>>[] m_InteriorEdgeGroups;
        protected Vector3[][] m_FillVertexPositionsByGroup;
        protected Color[] m_GroupColors;

        /// <summary>
        /// Derived classes must implement this to convert their selection to faces
        /// </summary>
        protected abstract Face[] GetTargetFaces(ProBuilderMesh mesh);

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_ExtrudeDistance = UserPreferences.Tools.RemoveExtrudeDistance;

            // Mode toggle buttons
            var modeContainer = new VisualElement();
            //modeContainer.style.marginBottom = 15;

            //var modeLabel = new Label("Mode:");
            //modeLabel.style.marginBottom = 5;
            //modeContainer.Add(modeLabel);

            // Mode toggle - use a simple Toggle instead of ToggleButtonGroup for now
            var modeToggle = new Toggle("Dissolve");
            modeToggle.tooltip = "Toggle between Delete mode (full removal) and Dissolve mode (topology-preserving)";
            modeToggle.SetValueWithoutNotify(m_CurrentMode == RemoveMode.Dissolve);

            modeToggle.RegisterValueChangedCallback(evt =>
            {
                m_CurrentMode = evt.newValue ? RemoveMode.Dissolve : RemoveMode.Delete;
                PreviewActionFramework.RequestPreviewUpdate();
            });

            modeContainer.Add(modeToggle);
            root.Add(modeContainer);

            // Mode-specific content container
            var contentContainer = new VisualElement();
            contentContainer.name = "mode-content";
            root.Add(contentContainer);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_ExtrudeDistance = UserPreferences.Tools.RemoveExtrudeDistance;

            if (m_CurrentMode == RemoveMode.Delete)
            {
                StartDeletePreview();
            }
            else
            {
                StartDissolvePreview();
            }

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;
            UpdatePreview();
        }

        private void StartDeletePreview()
        {
            // Cache the current selection for preview
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedFacesToDelete = new Face[selection.Length][];
            m_FaceCenterPositions = new Vector3[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                var facesToDelete = GetFacesToDeleteForDeleteMode(mesh);
                m_CachedFacesToDelete[i] = facesToDelete;

                // Calculate center positions for preview dots
                var centerPositions = new Vector3[facesToDelete.Length];
                for (int j = 0; j < facesToDelete.Length; j++)
                {
                    centerPositions[j] = GetFaceCenter(mesh, facesToDelete[j]);
                }
                m_FaceCenterPositions[i] = centerPositions;

            }
        }

        private void StartDissolvePreview()
        {
            // Cache the current selection for preview
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;


            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                var faces = GetTargetFaces(mesh);
            }

            m_CachedFaces = new Face[selection.Length][];
            m_CachedPerimeterEdges = new Edge[selection.Length][];
            m_PreviewEdges = new List<(Vector3, Vector3)>[selection.Length];
            m_PreviewEdgeGroups = new List<List<(Vector3, Vector3)>>[selection.Length];
            m_SecondaryEdgeGroups = new List<(Vector3, Vector3)>[selection.Length];
            m_InteriorEdgeGroups = new List<List<(Vector3, Vector3)>>[selection.Length];
            m_FillVertexPositionsByGroup = new Vector3[selection.Length][];
            m_FillVertexPositions = new Vector3[selection.Length];

            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                var faces = GetTargetFaces(mesh);
                m_CachedFaces[i] = faces;

                // Group faces into connected components
                var faceGroups = RemoveElements_Helper.GroupConnectedFaces(mesh, faces);

                // Process each face group separately
                var primaryEdgeGroups = new List<List<(Vector3, Vector3)>>();
                var interiorEdgeGroups = new List<List<(Vector3, Vector3)>>();
                var fillPositions = new Vector3[faceGroups.Count];

                for (int groupIndex = 0; groupIndex < faceGroups.Count; groupIndex++)
                {
                    var faceGroup = faceGroups[groupIndex];

                    // Get primary exterior perimeter edges (from selected faces to exterior)
                    var primaryPerimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faceGroup.ToArray()).ToList();
                    var primaryEdgePositions = RemoveElements_Helper.GetEdgePositions(mesh, primaryPerimeterEdges);
                    primaryEdgeGroups.Add(primaryEdgePositions);

                    // Get interior perimeter edges (from selected faces to other selected faces)
                    var interiorPerimeterEdges = RemoveElements_Helper.GetInteriorPerimeterEdges(mesh, faceGroup.ToArray(), faces);
                    var interiorEdgePositions = RemoveElements_Helper.GetEdgePositions(mesh, interiorPerimeterEdges);
                    interiorEdgeGroups.Add(interiorEdgePositions);

                    // Get fill vertex position for this group
                    fillPositions[groupIndex] = GetFillVertexPosition(mesh, primaryPerimeterEdges);

                }

                // Get secondary exterior edges (open edges connected to the primary perimeter)
                var allConnectedOpenEdges = RemoveElements_Helper.GetAllConnectedOpenEdges(mesh, faces);
                var allPrimaryPerimeterEdges = new List<Edge>();
                foreach (var faceGroup in faceGroups)
                {
                    allPrimaryPerimeterEdges.AddRange(ElementSelection.GetPerimeterEdges(mesh, faceGroup.ToArray()));
                }
                var secondaryEdges = allConnectedOpenEdges.Except(allPrimaryPerimeterEdges).ToList();
                var secondaryEdgePositions = RemoveElements_Helper.GetEdgePositions(mesh, secondaryEdges);

                m_PreviewEdgeGroups[i] = primaryEdgeGroups;
                m_InteriorEdgeGroups[i] = interiorEdgeGroups;
                m_SecondaryEdgeGroups[i] = secondaryEdgePositions;
                m_FillVertexPositionsByGroup[i] = fillPositions;


                // For backwards compatibility, use first group's fill position
                m_FillVertexPositions[i] = fillPositions.Length > 0 ? fillPositions[0] : Vector3.zero;

                // Also cache the connected open edge indices for later use (first group only for now)
                if (faceGroups.Count > 0)
                {
                    var firstGroupConnectedEdges = RemoveElements_Helper.GetAllConnectedOpenEdges(mesh, faceGroups[0].ToArray());
                    m_CachedPerimeterEdges[i] = firstGroupConnectedEdges.ToArray();
                }
                else
                {
                    m_CachedPerimeterEdges[i] = new Edge[0];
                }
            }

            // Generate distinct colors for face groups
            GenerateGroupColors();
        }

        public override void UpdatePreview()
        {
            // Just repaint to show the preview
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CurrentMode == RemoveMode.Delete)
            {
                return ApplyDeleteChanges();
            }
            else
            {
                return ApplyDissolveChanges();
            }
        }

        private ActionResult ApplyDeleteChanges()
        {
            // Use current selection to ensure we apply to the most up-to-date selection
            var currentSelection = MeshSelection.top.ToArray();
            var result = new ActionResult(ActionResult.Status.Success, "Delete Elements completed successfully");

            foreach (var mesh in currentSelection)
            {
                var facesToDelete = GetFacesToDeleteForDeleteMode(mesh);

                if (facesToDelete.Length == 0)
                    continue;

                // Record undo
                Undo.RecordObject(mesh, "Delete Elements");
                Undo.RecordObject(mesh.GetComponent<MeshRenderer>(), "Delete Elements");
                Undo.RecordObject(mesh.GetComponent<MeshFilter>(), "Delete Elements");

                try
                {
                    // Delete the faces
                    mesh.DeleteFaces(facesToDelete);

                    // Refresh the mesh
                    mesh.ToMesh();
                    mesh.Refresh();

                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to delete faces from {mesh.name}: {e.Message}");
                    result = new ActionResult(ActionResult.Status.Failure, $"Failed to delete faces: {e.Message}");
                }
            }

            // Clear selection since we deleted the selected elements
            MeshSelection.ClearElementSelection();

            return result;
        }

        private ActionResult ApplyDissolveChanges()
        {
            // Use current selection to ensure we apply to the most up-to-date selection
            var currentSelection = MeshSelection.top.ToArray();
            var targetFaces = new Face[currentSelection.Length][];

            // Convert current selection to target faces
            for (int i = 0; i < currentSelection.Length; i++)
            {
                targetFaces[i] = GetTargetFaces(currentSelection[i]);
            }

            // Delegate to helper for actual processing
            return RemoveElements_Helper.ProcessRemoveInteriorElements(currentSelection, targetFaces, m_ExtrudeDistance);
        }

        /// <summary>
        /// Gets faces to delete in Delete mode - includes the selected elements and their dependent faces
        /// </summary>
        protected virtual Face[] GetFacesToDeleteForDeleteMode(ProBuilderMesh mesh)
        {
            // Default implementation delegates to GetTargetFaces
            // Derived classes can override for different delete behavior
            return GetTargetFaces(mesh);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;

            // Clear all preview state
            m_CachedMeshes = null;
            m_CachedFacesToDelete = null;
            m_FaceCenterPositions = null;
            m_CachedFaces = null;
            m_CachedPerimeterEdges = null;
            m_PreviewEdges = null;
            m_PreviewEdgeGroups = null;
            m_SecondaryEdgeGroups = null;
            m_InteriorEdgeGroups = null;
            m_FillVertexPositionsByGroup = null;
            m_GroupColors = null;
            m_FillVertexPositions = null;

            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null) return;

            // Set z-test to always show lines on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null) continue;

                // Get faces connected to selected elements
                var connectedFaces = GetTargetFaces(mesh);
                if (connectedFaces == null || connectedFaces.Length == 0) continue;

                if (m_CurrentMode == RemoveMode.Dissolve)
                {
                    // Dissolve ON: highlight only interior edges
                    DrawInteriorEdgesOnly(mesh, connectedFaces);
                }
                else
                {
                    // Dissolve OFF: highlight all edges on the connected faces
                    DrawAllFaceEdges(mesh, connectedFaces);
                }
            }
        }

        /// <summary>
        /// Draws only the interior edges (edges between connected faces and non-connected faces)
        /// </summary>
        private void DrawInteriorEdgesOnly(ProBuilderMesh mesh, Face[] connectedFaces)
        {
            // Log face count
            var allVertices = mesh.GetVertices();
            var interiorEdges = new List<Edge>();

            interiorEdges = MeshHelpers_General.GetInteriorEdgesFromFaces(mesh, connectedFaces);

            // Log interior edge count

            // Draw the interior edges
            Handles.color = new Color(1f, 0.5f, 0f); // orange
            foreach (var edge in interiorEdges)
            {
                Vector3 startPos = mesh.transform.TransformPoint(allVertices[edge.a].position);
                Vector3 endPos = mesh.transform.TransformPoint(allVertices[edge.b].position);
                Handles.DrawAAPolyLine(8f, startPos, endPos);
            }
        }

        /// <summary>
        /// Draws all edges on the connected faces
        /// </summary>
        private void DrawAllFaceEdges(ProBuilderMesh mesh, Face[] connectedFaces)
        {
            var allVertices = mesh.GetVertices();
            var drawnEdges = new HashSet<Edge>();
            int edgeCount = 0;

            // Draw all edges for all connected faces
            Handles.color = Color.red;
            foreach (var face in connectedFaces)
            {
                foreach (var edge in face.edges)
                {
                    // Avoid drawing the same edge multiple times
                    if (!drawnEdges.Contains(edge))
                    {
                        drawnEdges.Add(edge);
                        Vector3 startPos = mesh.transform.TransformPoint(allVertices[edge.a].position);
                        Vector3 endPos = mesh.transform.TransformPoint(allVertices[edge.b].position);
                        Handles.DrawAAPolyLine(8f, startPos, endPos);
                        edgeCount++;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the world position of the center of a face
        /// </summary>
        private Vector3 GetFaceCenter(ProBuilderMesh mesh, Face face)
        {
            var vertices = mesh.GetVertices();
            Vector3 center = Vector3.zero;

            foreach (int index in face.distinctIndexes)
            {
                center += vertices[index].position;
            }

            center /= face.distinctIndexes.Count();

            // Transform to world space
            return mesh.transform.TransformPoint(center);
        }

        /// <summary>
        /// Gets the world position of the first vertex from the perimeter edges.
        /// Simple and reliable - just pick any vertex from the original perimeter.
        /// </summary>
        private Vector3 GetFillVertexPosition(ProBuilderMesh mesh, List<Edge> perimeterEdges)
        {
            if (perimeterEdges.Count == 0)
                return Vector3.zero;

            var allVertices = mesh.GetVertices();
            var firstEdge = perimeterEdges[0];

            // Just use the first vertex of the first perimeter edge
            var localPos = allVertices[firstEdge.a].position;
            var worldPos = mesh.transform.TransformPoint(localPos);

            return worldPos;
        }

        /// <summary>
        /// Generates distinct colors for face groups
        /// </summary>
        private void GenerateGroupColors()
        {
            // Count total groups across all meshes
            int totalGroups = 0;
            if (m_PreviewEdgeGroups != null)
            {
                foreach (var edgeGroups in m_PreviewEdgeGroups)
                {
                    if (edgeGroups != null)
                        totalGroups = Mathf.Max(totalGroups, edgeGroups.Count);
                }
            }

            // Generate distinct colors
            m_GroupColors = new Color[totalGroups];
            for (int i = 0; i < totalGroups; i++)
            {
                // Use HSV to generate distinct colors
                float hue = (i * 137.5f) % 360f / 360f; // Golden angle for good distribution
                float saturation = 0.8f + (i % 2) * 0.2f; // Alternate between 0.8 and 1.0
                float value = 0.8f + (i % 3) * 0.1f; // Vary brightness slightly

                m_GroupColors[i] = Color.HSVToRGB(hue, saturation, value);
            }
        }

        /// <summary>
        /// Gets the color for a specific group index
        /// </summary>
        private Color GetGroupColor(int groupIndex)
        {
            if (m_GroupColors == null || groupIndex < 0 || groupIndex >= m_GroupColors.Length)
            {
                // Fallback colors if we don't have enough generated colors
                Color[] fallbackColors = { Color.cyan, Color.magenta, Color.green, Color.red, Color.blue, Color.yellow };
                return fallbackColors[groupIndex % fallbackColors.Length];
            }

            return m_GroupColors[groupIndex];
        }
    }

    /// <summary>
    /// Remove Elements action for vertex selection mode.
    /// Delete mode: Finds all faces connected to selected vertices and deletes them.
    /// Dissolve mode: Finds all faces touching selected vertices and removes interior elements.
    /// </summary>
    [ProBuilderPlusAction("remove-vertices", "Remove",
        Tooltip = "Remove selected vertices using Delete (permanent removal) or Dissolve (topology-preserving) mode",
        Instructions = "Secondary elements to be removed are shown in orange, faces to be removed are shown in red",
        IconPath = "Icons/Old/Face_Delete",
        ValidModes = ToolMode.Vertex,
        VerticesSelectedRequired = 1,
        Order = 150)]
    public sealed class RemoveVerticesPreviewAction : RemoveElementsPreviewActionBase
    {
        protected override Face[] GetTargetFaces(ProBuilderMesh mesh)
        {
            if (m_CurrentMode == RemoveMode.Dissolve)
            {
                return RemoveElements_Helper.ConvertVertexSelectionToFaces(mesh);
            }
            else
            {
                // Delete mode - same logic as dissolve for vertices
                return RemoveElements_Helper.ConvertVertexSelectionToFaces(mesh);
            }
        }
    }

    /// <summary>
    /// Remove Elements action for edge selection mode.
    /// Delete mode: Finds all faces connected to selected edges and deletes them.
    /// Dissolve mode: Finds all faces touching selected edges and removes interior elements.
    /// </summary>

    [ProBuilderPlusAction("remove-edges", "Remove",
        Tooltip = "Remove selected edges using Delete (permanent removal) or Dissolve (topology-preserving) mode",
        Instructions = "Secondary elements to be removed are shown in orange, faces to be removed are shown in red",
        IconPath = "Icons/Old/Face_Delete",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 151)]
    public sealed class RemoveEdgesPreviewAction : RemoveElementsPreviewActionBase
    {
        protected override Face[] GetTargetFaces(ProBuilderMesh mesh)
        {
            if (m_CurrentMode == RemoveMode.Dissolve)
            {
                return RemoveElements_Helper.ConvertEdgeSelectionToFaces(mesh);
            }
            else
            {
                // Delete mode - same logic as dissolve for edges
                return RemoveElements_Helper.ConvertEdgeSelectionToFaces(mesh);
            }
        }
    }

    /// <summary>
    /// Remove Elements action for face selection mode.
    /// Delete mode: Deletes selected faces directly.
    /// Dissolve mode: Removes interior elements from selected faces and fills holes.
    /// </summary>

    [ProBuilderPlusAction("remove-faces", "Remove",
        Tooltip = "Remove selected faces using Delete (permanent removal) or Dissolve (topology-preserving) mode",
        Instructions = "Secondary elements to be removed are shown in orange, faces to be removed are shown in red",
        IconPath = "Icons/Old/Face_Delete",
        ValidModes = ToolMode.Face,
        FacesSelectedRequired = 1,
        Order = 152)]
    public sealed class RemoveFacesPreviewAction : RemoveElementsPreviewActionBase
    {
        protected override Face[] GetTargetFaces(ProBuilderMesh mesh)
        {
            return mesh.GetSelectedFaces();
        }
    }
}

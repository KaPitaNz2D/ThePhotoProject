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
    /// Face mode version of Triangulate that shows which selected faces will be triangulated before applying changes.
    /// Only operates on selected faces, unlike the object version which processes all faces on the object.
    /// Shows preview of triangulation result with wireframe overlay.
    /// </summary>
    [ProBuilderPlusAction("triangulate_faces_preview", "Triangulate",
        Tooltip = "Triangulate selected faces with live preview",
        Instructions = "Triangulate selected faces (cyan shows new triangle edges)",
        IconPath = "Icons/Old/Face_Triangulate",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 53)]
    public sealed class TriangulateFacesPreviewAction : PreviewMenuAction
    {
        // Cache data for preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedSelectedFaces;
        private TriangulateHelper.TriangulationPreview[] m_TriangulationPreviews;

        public override VisualElement CreateSettingsContent()
        {
            return TriangulateHelper.CreateTriangulateUI(isFaceMode: true);
        }

        public override void StartPreview()
        {
            // Cache the current selection
            CacheCurrentSelection();
            
            // Calculate previews using helper
            CalculateTriangleEdgesForSelection();

            // Subscribe to scene GUI for preview visualization
            SceneView.duringSceneGui += OnSceneGUI;

            // Initial preview update
            UpdatePreview();

            int totalSelectedFaces = m_CachedSelectedFaces?.Sum(faces => faces?.Length ?? 0) ?? 0;
        }

        /// <summary>
        /// Caches the current selection state for later application.
        /// </summary>
        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedSelectedFaces = new Face[selection.Length][];
            m_TriangulationPreviews = new TriangulateHelper.TriangulationPreview[selection.Length];

            // Initialize arrays and cache selected faces for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedSelectedFaces[i] = selection[i].GetSelectedFaces();
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
            CalculateTriangleEdgesForSelection();
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;

            // Triangulation has no parameters to update, but we can recalculate if needed
            // For now, just repaint to show the preview
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0 || m_CachedSelectedFaces == null)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No faces to process");
            }

            // Prepare undo
            var undoObjects = new List<Object>();
            foreach (var mesh in m_CachedMeshes)
            {
                undoObjects.Add(mesh);
            }
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Triangulate Faces");

            int totalTriangulated = 0;

            // Apply triangulation to selected faces on each mesh using helper
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var selectedFaces = m_CachedSelectedFaces[i];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;

                try
                {
                    totalTriangulated += TriangulateHelper.ApplyTriangulation(mesh, selectedFaces);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to triangulate faces on '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalTriangulated > 0 ?
                $"Triangulated {totalTriangulated} face{(totalTriangulated == 1 ? string.Empty : "s")}" :
                "All selected faces already triangulated";

            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedFaces = null;
            m_TriangulationPreviews = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_TriangulationPreviews == null) return;
            
            TriangulateHelper.DrawTriangulationPreviews(m_TriangulationPreviews);
        }

        /// <summary>
        /// Calculate triangle edges for all meshes based on cached selection.
        /// </summary>
        private void CalculateTriangleEdgesForSelection()
        {
            if (m_CachedMeshes == null || m_CachedSelectedFaces == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var selectedFaces = m_CachedSelectedFaces[meshIndex];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                {
                    // Create empty preview if no selected faces
                    m_TriangulationPreviews[meshIndex] = new TriangulateHelper.TriangulationPreview
                    {
                        triangleVertices = new Vector3[0],
                        newTriangleEdges = new Edge[0],
                        existingEdges = new Edge[0]
                    };
                    continue;
                }

                // Use helper to calculate preview
                m_TriangulationPreviews[meshIndex] = TriangulateHelper.CalculateTriangulationPreview(mesh, selectedFaces);
            }
        }
    }
}

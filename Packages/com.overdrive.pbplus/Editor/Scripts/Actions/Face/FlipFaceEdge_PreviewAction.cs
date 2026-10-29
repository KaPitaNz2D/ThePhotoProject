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
    /// Face mode version of Flip Face Edge that shows which diagonal edge will be flipped before applying changes.
    /// Only operates on selected faces that are quads (4-sided polygons represented as 2 triangles).
    /// Shows preview of current diagonal edge (red) and new diagonal position (cyan).
    /// </summary>
    [ProBuilderPlusAction("flip_face_edge_preview", "Swap Tri",
        Tooltip = "Flip the diagonal edge in selected quad faces with live preview",
        Instructions = "Flip diagonal edge in quad faces (cyan shows new diagonal)",
        IconPath = "Icons/Old/Face_FlipTri",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 54)]
    public sealed class FlipFaceEdgePreviewAction : PreviewMenuAction
    {
        // Cache data for preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedSelectedFaces;
        private FlipEdgeHelper.FlipEdgePreview[][] m_FlipEdgePreviews;

        public override VisualElement CreateSettingsContent()
        {
            return FlipEdgeHelper.CreateFlipEdgeUI();
        }

        public override void StartPreview()
        {
            // Cache the current selection
            CacheCurrentSelection();
            
            // Calculate previews using helper
            CalculateFlipEdgePreviews();

            // Subscribe to scene GUI for preview visualization
            SceneView.duringSceneGui += OnSceneGUI;

            // Initial preview update
            UpdatePreview();

            int totalSelectedFaces = m_CachedSelectedFaces?.Sum(faces => faces?.Length ?? 0) ?? 0;
            int flippableFaces = CountFlippableFaces();
        }

        /// <summary>
        /// Caches the current selection state for later application.
        /// </summary>
        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedSelectedFaces = new Face[selection.Length][];
            m_FlipEdgePreviews = new FlipEdgeHelper.FlipEdgePreview[selection.Length][];

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
            CalculateFlipEdgePreviews();
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;

            // Flip edge has no parameters to update, but we can recalculate if needed
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
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Flip Face Edges");

            int totalFlipped = 0;

            // Apply flip edge to selected faces on each mesh using helper
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var selectedFaces = m_CachedSelectedFaces[i];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;

                try
                {
                    totalFlipped += FlipEdgeHelper.ApplyFlipEdge(mesh, selectedFaces);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to flip edges on '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage;
            if (totalFlipped > 0)
            {
                resultMessage = $"Flipped {totalFlipped} edge{(totalFlipped == 1 ? string.Empty : "s")}";
            }
            else
            {
                resultMessage = "No edges flipped - faces must be quads (4-sided polygons)";
            }

            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedFaces = null;
            m_FlipEdgePreviews = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_FlipEdgePreviews == null) return;
            
            // Draw all flip edge previews
            for (int i = 0; i < m_FlipEdgePreviews.Length; i++)
            {
                if (m_FlipEdgePreviews[i] != null)
                {
                    FlipEdgeHelper.DrawFlipEdgePreviews(m_FlipEdgePreviews[i]);
                }
            }
        }

        /// <summary>
        /// Calculate flip edge previews for all meshes based on cached selection.
        /// </summary>
        private void CalculateFlipEdgePreviews()
        {
            if (m_CachedMeshes == null || m_CachedSelectedFaces == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var selectedFaces = m_CachedSelectedFaces[meshIndex];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                {
                    // Create empty preview if no selected faces
                    m_FlipEdgePreviews[meshIndex] = new FlipEdgeHelper.FlipEdgePreview[0];
                    continue;
                }

                // Use helper to calculate preview
                m_FlipEdgePreviews[meshIndex] = FlipEdgeHelper.CalculateFlipEdgePreview(mesh, selectedFaces);
            }
        }

        /// <summary>
        /// Count how many of the cached selected faces are actually flippable (quads).
        /// </summary>
        private int CountFlippableFaces()
        {
            if (m_FlipEdgePreviews == null) return 0;

            int count = 0;
            foreach (var meshPreviews in m_FlipEdgePreviews)
            {
                if (meshPreviews != null)
                {
                    foreach (var preview in meshPreviews)
                    {
                        if (preview.canFlip) count++;
                    }
                }
            }
            return count;
        }
    }
}

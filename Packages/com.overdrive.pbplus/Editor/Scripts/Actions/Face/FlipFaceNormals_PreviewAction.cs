using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;
using Overdrive.ProBuilderPlus;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Face mode version of FlipFaceNormals that shows which faces will be flipped before applying changes.
    /// Only operates on selected faces, providing visual preview of the flip operation.
    /// </summary>
    [ProBuilderPlusAction("flip_face_normals", "Flip Normal",
        Tooltip = "Preview which selected faces will be flipped, showing the new normal direction",
        Instructions = "Flip selected face normals (red shows flipped faces, cyan new direction)",
        IconPath = "Icons/Old/Face_FlipNormals",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 25)]
    public sealed class FlipFaceNormalsPreviewAction : PreviewMenuAction
    {
        // Cache data for preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedSelectedFaces;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            return root;
        }

        public override void StartPreview()
        {
            // Cache the current selection
            CacheCurrentSelection();

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;

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

            // Cache selected faces for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedSelectedFaces[i] = selection[i].GetSelectedFaces();
            }
        }

        /// <summary>
        /// Override to handle selection changes during preview by refreshing cache.
        /// </summary>
        internal override void OnSelectionChangedDuringPreview()
        {
            // Update our cached selection to match the new selection
            CacheCurrentSelection();
            SceneView.RepaintAll();
        }

        public override void UpdatePreview()
        {
            // No settings to change for flip operation, just repaint
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
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Flip Face Normals");

            int totalFlipped = 0;

            // Apply flip to selected faces on each mesh
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var selectedFaces = m_CachedSelectedFaces[i];

                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;

                try
                {
                    // Flip each selected face
                    foreach (var face in selectedFaces)
                    {
                        face.Reverse();
                        totalFlipped++;
                    }

                    // Update mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    mesh.Optimize();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to flip face normals for '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalFlipped > 0 ?
                $"Flipped {totalFlipped} face normal{(totalFlipped == 1 ? string.Empty : "s")}" :
                "No faces to flip";

            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedFaces = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawFlipVisualization();
        }

        private void DrawFlipVisualization()
        {
            if (m_CachedMeshes == null || m_CachedSelectedFaces == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var selectedFaces = m_CachedSelectedFaces[meshIndex];

                if (mesh == null || selectedFaces == null || selectedFaces.Length == 0) continue;

                // Use shared helper to draw flip visualization for all selected faces
                FlipNormalsHelper.DrawFlipVisualization(mesh, selectedFaces);
            }
        }
    }
}
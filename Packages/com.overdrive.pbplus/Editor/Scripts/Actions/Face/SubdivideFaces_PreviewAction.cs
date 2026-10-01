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
    /// Face mode version of Subdivide that shows which selected faces will be subdivided before applying changes.
    /// Only operates on selected faces, unlike the object version which processes all faces on the object.
    /// Shows preview of subdivision result with wireframe overlay.
    /// </summary>
    [ProBuilderPlusAction("subdivide_faces_preview", "Subdivide",
        Tooltip = "Subdivide selected faces with live preview",
        Instructions = "Subdivide selected faces (cyan shows new subdivision edges)",
        IconPath = "Icons/Old/Face_Subdivide",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 54)]
    public sealed class SubdivideFacesPreviewAction : PreviewMenuAction
    {
        // Cache data for preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedSelectedFaces;
        private SubdivideHelper.SubdivisionPreview[] m_SubdivisionPreviews;

        public override VisualElement CreateSettingsContent()
        {
            return SubdivideHelper.CreateSubdivideUI(isFaceMode: true); // true = face mode
        }

        public override void StartPreview()
        {
            // Cache the current selection
            CacheCurrentSelection();
            
            // Calculate previews using helper
            CalculateSubdivisionPreviews();

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
            m_SubdivisionPreviews = new SubdivideHelper.SubdivisionPreview[selection.Length];

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
            CalculateSubdivisionPreviews();
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;

            // Subdivision has no parameters to update, but we can recalculate if needed
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
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Subdivide Faces");

            int totalSubdivided = 0;

            // Apply subdivision to selected faces on each mesh using helper
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var selectedFaces = m_CachedSelectedFaces[i];

                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;

                try
                {
                    Face[] resultingFaces = SubdivideHelper.ApplySubdivision(mesh, selectedFaces);

                    if (resultingFaces != null)
                    {
                        totalSubdivided += selectedFaces.Length;
                        mesh.SetSelectedFaces(resultingFaces);
                        mesh.Optimize();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to subdivide faces on '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalSubdivided > 0 ?
                $"Subdivided {totalSubdivided} face{(totalSubdivided == 1 ? string.Empty : "s")}" :
                "No faces were subdivided";

            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedFaces = null;
            m_SubdivisionPreviews = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_SubdivisionPreviews == null) return;
            
            SubdivideHelper.DrawSubdivisionPreviews(m_SubdivisionPreviews);
        }

        /// <summary>
        /// Calculate subdivision previews for all meshes based on cached selection.
        /// </summary>
        private void CalculateSubdivisionPreviews()
        {
            if (m_CachedMeshes == null || m_CachedSelectedFaces == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var selectedFaces = m_CachedSelectedFaces[meshIndex];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                {
                    // Create empty preview if no selected faces
                    m_SubdivisionPreviews[meshIndex] = new SubdivideHelper.SubdivisionPreview
                    {
                        allVertices = new Vector3[0],
                        newSubdivisionEdges = new Edge[0],
                        existingEdges = new Edge[0]
                    };
                    continue;
                }

                // Use helper to calculate preview
                m_SubdivisionPreviews[meshIndex] = SubdivideHelper.CalculateSubdivisionPreview(mesh, selectedFaces);
            }
        }
    }
}

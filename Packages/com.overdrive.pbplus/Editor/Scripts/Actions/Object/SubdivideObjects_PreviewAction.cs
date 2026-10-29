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
    /// Object mode version of Subdivide that shows which faces will be subdivided before applying changes.
    /// Operates on ALL faces of selected objects, unlike the face version which only processes selected faces.
    /// Shows preview of subdivision result with wireframe overlay.
    /// </summary>
    [ProBuilderPlusAction("subdivide_objects_preview", "Subdivide",
        Tooltip = "Subdivide all faces on selected objects with live preview - shows cyan wireframe of subdivision edges that will be created",
        Instructions = "Cyan lines indicate edges that will be created.",
        IconPath = "Icons/Old/Object_Subdivide",
        ValidModes = ToolMode.Object,
        ObjectsSelectedRequired = 1,
        Order = 54)]
    public sealed class SubdivideObjectsPreviewAction : PreviewMenuAction
    {
        // Cache data for preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private SubdivideHelper.SubdivisionPreview[] m_SubdivisionPreviews;

        public override VisualElement CreateSettingsContent()
        {
            return SubdivideHelper.CreateSubdivideUI(false); // false = object mode
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

        }

        /// <summary>
        /// Caches the current selection state for later application.
        /// </summary>
        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_SubdivisionPreviews = new SubdivideHelper.SubdivisionPreview[selection.Length];
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

            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No objects to process");
            }

            // Prepare undo
            var undoObjects = new List<Object>();
            foreach (var mesh in m_CachedMeshes)
            {
                undoObjects.Add(mesh);
            }
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Subdivide Objects");

            int totalSubdivided = 0;

            // Apply subdivision to all faces on each selected object using helper
            foreach (var mesh in m_CachedMeshes)
            {
                try
                {
                    // For object mode, we subdivide ALL faces
                    var allFaces = mesh.faces.ToArray();
                    Face[] resultingFaces = SubdivideHelper.ApplySubdivision(mesh, allFaces);

                    if (resultingFaces != null)
                    {
                        totalSubdivided += allFaces.Length;
                        mesh.SetSelectedVertices(new int[0]);
                        mesh.Optimize();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to subdivide object '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalSubdivided > 0 ?
                $"Subdivided {totalSubdivided} face{(totalSubdivided == 1 ? string.Empty : "s")} across {m_CachedMeshes.Length} object{(m_CachedMeshes.Length == 1 ? string.Empty : "s")}" :
                "No faces were subdivided";

            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_SubdivisionPreviews = null;
            SceneView.RepaintAll();
            
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_SubdivisionPreviews == null) return;
            
            SubdivideHelper.DrawSubdivisionPreviews(m_SubdivisionPreviews);
        }

        /// <summary>
        /// Calculate subdivision previews for all meshes - object mode operates on ALL faces.
        /// </summary>
        private void CalculateSubdivisionPreviews()
        {
            if (m_CachedMeshes == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                
                // For object mode, subdivide ALL faces
                var allFaces = mesh.faces.ToArray();
                
                if (allFaces.Length == 0)
                {
                    // Create empty preview if no faces
                    m_SubdivisionPreviews[meshIndex] = new SubdivideHelper.SubdivisionPreview
                    {
                        allVertices = new Vector3[0],
                        newSubdivisionEdges = new Edge[0],
                        existingEdges = new Edge[0]
                    };
                    continue;
                }

                // Use helper to calculate preview
                m_SubdivisionPreviews[meshIndex] = SubdivideHelper.CalculateSubdivisionPreview(mesh, allFaces);
                
            }
        }
    }
}

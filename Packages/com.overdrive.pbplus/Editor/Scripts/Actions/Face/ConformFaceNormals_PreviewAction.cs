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
    /// Face mode version of ConformNormals that shows which faces will be flipped before applying changes.
    /// Only operates on selected faces, unlike the object version which processes all faces on the object.
    /// Allows user to choose between majority direction (default) or minority direction ("Other Way").
    /// </summary>
    [ProBuilderPlusAction("conform_face_normals", "Conform",
        Tooltip = "Preview which selected faces will be flipped to conform normals, with option to choose direction",
        Instructions = "Conform selected face normals (red shows flipped faces, cyan new direction)",
        IconPath = "Icons/Old/Face_ConformNormals",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 30)]
    public sealed class ConformFaceNormalsPreviewAction : PreviewMenuAction
    {
        // Settings
        private bool m_UseOtherDirection;

        // Cache data for preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedSelectedFaces;
        private List<Face>[] m_FacesToFlip;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_UseOtherDirection = UserPreferences.Tools.ConformNormalsOtherDirection;

            // "Other Direction" checkbox
            var otherDirectionToggle = new Toggle("Reverse");
            otherDirectionToggle.tooltip = "Choose minority direction instead of majority direction";
            otherDirectionToggle.SetValueWithoutNotify(m_UseOtherDirection);
            otherDirectionToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                if (m_UseOtherDirection != evt.newValue)
                {
                    m_UseOtherDirection = evt.newValue;
                    UserPreferences.Tools.ConformNormalsOtherDirection = evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(otherDirectionToggle);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_UseOtherDirection = UserPreferences.Tools.ConformNormalsOtherDirection;
            }

            // Cache the current selection
            CacheCurrentSelection();

            // Calculate preview for each mesh
            CalculateConformNormalsPreview();

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;

            int totalSelectedFaces = m_CachedSelectedFaces?.Sum(faces => faces?.Length ?? 0) ?? 0;
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        /// <summary>
        /// Caches the current selection state for later application.
        /// </summary>
        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedSelectedFaces = new Face[selection.Length][];
            m_FacesToFlip = new List<Face>[selection.Length];

            // Initialize arrays and cache selected faces for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedSelectedFaces[i] = selection[i].GetSelectedFaces();
                m_FacesToFlip[i] = new List<Face>();
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
            // Recalculate preview when settings change
            CalculateConformNormalsPreview();
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
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Conform Face Normals");

            int totalFlipped = 0;

            // Apply conform normals to selected faces on each mesh
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var selectedFaces = m_CachedSelectedFaces[i];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;


                try
                {
                    // Apply the conform normals operation to only the selected faces
                    ActionResult result;
                    
                    if (m_UseOtherDirection)
                    {
                        // For "other direction", use shared helper
                        result = ConformNormalsHelper.ApplyOtherDirectionConform(mesh, selectedFaces);
                    }
                    else
                    {
                        // Normal conform normals operation on selected faces only
                        result = SurfaceTopology.ConformNormals(mesh, selectedFaces);
                    }

                    // Update mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    mesh.Optimize();

                    if (result.status == ActionResult.Status.Success)
                    {
                        // Extract face count from result message if possible
                        var message = result.notification;
                        if (message.Contains("Flipped"))
                        {
                            // Try to parse number from message like "Flipped 3 faces"
                            var parts = message.Split(' ');
                            for (int j = 0; j < parts.Length - 1; j++)
                            {
                                if (parts[j] == "Flipped" && int.TryParse(parts[j + 1], out int count))
                                {
                                    totalFlipped += count;
                                    break;
                                }
                            }
                        }
                    }

                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to conform face normals for '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalFlipped > 0 ? 
                $"Conformed selected face normals: {totalFlipped} face{(totalFlipped == 1 ? string.Empty : "s")} flipped" : 
                "All selected face normals already uniform";
            
            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedFaces = null;
            m_FacesToFlip = null;
            SceneView.RepaintAll();
            
        }

        private void CalculateConformNormalsPreview()
        {
            if (m_CachedMeshes == null || m_CachedSelectedFaces == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var selectedFaces = m_CachedSelectedFaces[meshIndex];
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                {
                    m_FacesToFlip[meshIndex] = new List<Face>();
                    continue;
                }

                // Use shared helper to calculate faces to flip
                m_FacesToFlip[meshIndex] = ConformNormalsHelper.CalculateFacesToFlip(mesh, selectedFaces, m_UseOtherDirection);
                
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawNormalVisualization();
        }

        private void DrawNormalVisualization()
        {
            if (m_CachedMeshes == null || m_FacesToFlip == null) return;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null || m_FacesToFlip[meshIndex] == null) continue;

                var facesToFlip = m_FacesToFlip[meshIndex];
                
                // Use shared helper to draw edges and arrows
                ConformNormalsHelper.DrawNormalFlipArrows(mesh, facesToFlip);
            }
        }
    }
}

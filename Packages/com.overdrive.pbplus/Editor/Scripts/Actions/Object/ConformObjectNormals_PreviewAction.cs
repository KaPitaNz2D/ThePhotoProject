using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;
using Overdrive.ProBuilderPlus;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Preview version of ConformObjectNormals that shows which faces will be flipped before applying changes.
    /// Allows user to choose between majority direction (default) or minority direction ("Other Way").
    /// </summary>
    [ProBuilderPlusAction("conform_object_normals", "Conform",
        Tooltip = "Preview which faces will be flipped to conform normals, with option to choose direction",
        Instructions = "Red-outlined faces will be flipped.",
        IconPath = "Icons/Old/Object_ConformNormals",
        ValidModes = ToolMode.Object,
        ObjectsSelectedRequired = 1,
        Order = 20)]
    public sealed class ConformObjectNormalsPreviewAction : PreviewMenuAction
    {

        // Settings
        private bool m_UseOtherDirection = false;

        // Cache data for preview
        private ProBuilderMesh[] m_CachedMeshes;
        private List<Face>[] m_FacesToFlip;

        // UI elements that need updating
        private Label m_FaceCountLabel;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_UseOtherDirection = Overdrive.ProBuilderPlus.UserPreferences.Tools.ConformObjectNormalsOtherDirection;

            // "Reverse" checkbox
            var reverseToggle = new Toggle("Reverse");
            reverseToggle.tooltip = "Choose minority direction instead of majority direction";
            reverseToggle.SetValueWithoutNotify(m_UseOtherDirection);
            reverseToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                if (m_UseOtherDirection != evt.newValue)
                {
                    m_UseOtherDirection = evt.newValue;
                    Overdrive.ProBuilderPlus.UserPreferences.Tools.ConformObjectNormalsOtherDirection = evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(reverseToggle);

            // Face count label
            m_FaceCountLabel = new Label("Calculating...");
            m_FaceCountLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_FaceCountLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(m_FaceCountLabel);

            return root;
        }

        public override void StartPreview()
        {

            // Load from preferences if not already loaded
            m_UseOtherDirection = Overdrive.ProBuilderPlus.UserPreferences.Tools.ConformObjectNormalsOtherDirection;

            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_FacesToFlip = new List<Face>[selection.Length];

            // Initialize arrays
            for (int i = 0; i < selection.Length; i++)
            {
                m_FacesToFlip[i] = new List<Face>();
            }

            // Initialize face count label
            if (m_FaceCountLabel != null)
            {
                m_FaceCountLabel.text = "Calculating...";
            }

            // Calculate preview for each mesh
            CalculateConformNormalsPreview();

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;

        }

        public override void UpdatePreview()
        {
            // Recalculate preview when settings change
            CalculateConformNormalsPreview();
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
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Conform Object Normals");

            int totalFlipped = 0;

            // Apply conform normals to each mesh
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                

                try
                {
                    // Apply the conform normals operation
                    ActionResult result;
                    
                    if (m_UseOtherDirection)
                    {
                        // For "other direction", use shared helper
                        result = ConformNormalsHelper.ApplyOtherDirectionConform(mesh, mesh.faces.ToArray());
                    }
                    else
                    {
                        // Normal conform normals operation
                        result = SurfaceTopology.ConformNormals(mesh, mesh.faces);
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
                    Debug.LogError($"Failed to conform normals for '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalFlipped > 0 ? 
                $"Conformed normals: {totalFlipped} face{(totalFlipped == 1 ? string.Empty : "s")} flipped" : 
                "All normals already uniform";
            
            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_FacesToFlip = null;
            SceneView.RepaintAll();
            
        }

        private void CalculateConformNormalsPreview()
        {
            if (m_CachedMeshes == null) return;

            int totalFacesToFlip = 0;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];

                // Use shared helper to calculate faces to flip (for object mode, use all faces)
                m_FacesToFlip[meshIndex] = ConformNormalsHelper.CalculateFacesToFlip(mesh, mesh.faces.ToArray(), m_UseOtherDirection);

                totalFacesToFlip += m_FacesToFlip[meshIndex].Count;

            }

            // Update face count label
            if (m_FaceCountLabel != null)
            {
                m_FaceCountLabel.text = $"{totalFacesToFlip} face{(totalFacesToFlip == 1 ? string.Empty : "s")} to be flipped";
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

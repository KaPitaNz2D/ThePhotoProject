using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Custom overlay that provides the UI for preview actions.<br/>
    /// Shows action settings and Confirm/Cancel buttons.<br/>
    /// This overlay is dynamically created by PreviewActionFramework only when an action is active.
    /// </summary>
    // [Overlay(typeof(SceneView), "ProBuilder+ Preview", defaultDockZone = DockZone.RightColumn)]
    public sealed class PreviewActionOverlay : Overlay
    {
        public PreviewActionOverlay()
        {
            displayName = "ProBuilder+ Preview";
        }

        public override VisualElement CreatePanelContent()
        {
            // Load UXML template
            var visualTreeAsset = Resources.Load<VisualTreeAsset>("UXML/PreviewOverlayUI");
            var root = visualTreeAsset.Instantiate();

            var isElementMissing = false;

            // Get UI elements
            var contentContainer = root.QLog<VisualElement>("Content", ref isElementMissing);
            var confirmButton = root.QLog<Button>("Confirm", ref isElementMissing);
            var cancelButton = root.QLog<Button>("Cancel", ref isElementMissing);
            var instructionsLabel = root.QLog<Label>("Instructions", ref isElementMissing);

            // Set title
            displayName = PreviewActionFramework.GetCurrentActionName();

            // Set instructions from current action
            var instructions = PreviewActionFramework.GetCurrentActionInstructions();
            if (instructionsLabel != null)
            {
                instructionsLabel.text = instructions;
            }

            // Add action-specific settings content
            var settingsContent = PreviewActionFramework.GetCurrentActionSettings();
            if (settingsContent != null)
            {
                contentContainer.Add(settingsContent);
                //settingsContent.AddToClassList("stack-label-above");
            }

            // Setup button callbacks
            confirmButton.clicked += PreviewActionFramework.ConfirmAction;
            cancelButton.clicked += PreviewActionFramework.CancelAction;

            // Set button tooltips
            confirmButton.tooltip = "Apply the changes to the mesh (or press ESC to cancel)";
            cancelButton.tooltip = "Cancel and discard changes (or press ESC)";

            // Add keyboard event handling to the root element for additional ESC key detection
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Make the root element focusable and focus it to receive keyboard events
            root.focusable = true;
            root.tabIndex = 0;

            // Focus the overlay when it's created
            root.schedule.Execute(() =>
            {
                root.Focus();
            }).ExecuteLater(100); // Small delay to ensure it's fully created


            if (isElementMissing)
            {
                Debug.LogError("ProBuilder+ Preview Overlay not initialized. Field missing.");
            }

            return root;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Handle ESC and RETURN key to apply or cancel preview action
            if (evt.keyCode == KeyCode.Escape)
            {
                PreviewActionFramework.CancelAction();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Return)
            {
                PreviewActionFramework.ConfirmAction();
                evt.StopPropagation();
            }
        }
    }
}

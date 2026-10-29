using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Static helper functions shared by ProBuilderActions UIs.<br/>
    /// Used to create button lists and execute actions.
    /// </summary>
    public static class ProBuilderPlusActions
    {
        #region Action Execution

        public static void ExecuteAction(IActionInfo action, bool isInstant)
        {
            // Check if this action supports instant mode
            if (isInstant && action.SupportsInstantMode)
            {
                // Cancel any current preview action before executing instantly
                PreviewActionFramework.EndCurrentPreview(false);
                action.ExecuteInstant();
            }
            else
            {
                // Normal execution (no CTRL) - use preview framework
                action.ExecuteAction();
            }
        }

        #endregion

        #region UI Element Creation

        public static Button CreateButton(IActionInfo action)
        {
            var button = new Button();
            button.text = action.DisplayName;
            button.tooltip = action.Tooltip;

            // Todo: Corti: Avoid Closure.

            // Use pointer events to capture modifier keys properly
            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                // RightMouseButton is Context menu, so limit this to LMB
                if (evt.button == 0)
                {
                    ExecuteAction(action, evt.ctrlKey);
                }
            });

            // Always set icon (LoadIcon provides fallback for empty/null paths)
            if (action.Icon != null)
            {
                button.iconImage = action.Icon;
            }

            button.SetEnabled(action.IsEnabledForCurretMode);

            // Add CSS class based on instant mode support
            if (action.SupportsInstantMode)
            {
                button.AddToClassList("allow-instant");
            }
            else
            {
                button.AddToClassList("dont-allow-instant");
            }

            return button;
        }

        /// <summary>
        /// Populates the container Element with Bottons that can execute an Action.
        /// </summary>
        /// <param name="container">Container Element to be populated..</param>
        public static void PopulateEditorButtons(VisualElement container)
        {
            var editorActions = ActionInfoProvider.GetEditorActions();
            foreach (var action in editorActions)
            {
                var button = CreateButton(action);
                container.Add(button);
            }
        }

        public static void PopulateActionButtons(VisualElement container)
        {
            container.Clear();

            var actions = ActionInfoProvider.GetModeActions(ProBuilderPlusCore.CurrentToolMode);
            if (actions.Count == 0)
            {
                var noActionsLabel = new Label("No actions available");
                container.Add(noActionsLabel);
                return;
            }

            foreach (var action in actions)
            {
                var button = CreateButton(action);
                container.Add(button);
            }
        }

        public static Texture2D LoadIcon(string iconPath)
        {
            // Todo: Gabriel : Icon Loading
            // IconUtility.GetIcon
            // Packages\com.unity.probuilder\Editor\EditorCore\IconUtility.cs
            // ->https://github.com/Unity-Technologies/com.unity.probuilder/blob/master/Editor/EditorCore/IconUtility.cs

            if (string.IsNullOrEmpty(iconPath))
            {
                Debug.LogWarning("ProBuilderPlusActions.LoadIcon path is empty. Returning placeholder.");
                return Resources.Load<Texture2D>("Icons/Old/ProBuilderGUI_UV_ShowTexture_On");
            }

            Texture2D icon = null;

            // If path starts with "Assets/", use AssetDatabase loading
            if (iconPath.StartsWith("Assets/") || iconPath.StartsWith("Packages/"))
            {
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            }
            // Otherwise, try Resources loading
            else
            {
                icon = Resources.Load<Texture2D>(iconPath);
            }

            // Fallback to default icon if loading failed
            if (icon != null)
            {
                return icon;
            }

            if (iconPath.StartsWith("Assets/") || iconPath.StartsWith("Packages/"))
            {
                Debug.LogWarning($"LoadIcon('{iconPath}') not loaded. Returning placeholder.");
            }
            else
            {
                Debug.LogWarning($"LoadIcon('{iconPath}') not loaded. Returning placeholder. \nFile needs to be in Resources folder to be loaded by Resources.Load");
            }

            return Resources.Load<Texture2D>("Icons/Old/ProBuilderGUI_UV_ShowTexture_On");
        }

        #endregion
    }
}

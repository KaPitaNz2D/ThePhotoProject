using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// An dockable panel that contains ProBuilder tool functions shortcuts to menus as buttons.<br/>
    /// Functions open a PreviewOverlay that allows to change parameters or just apply with the hit of a button.<br/>
    /// </summary>
    /// <remarks>As an overlay, see <see cref="ProBuilderPlusActionsOverlay"/>.</remarks>
    public sealed class ProBuilderPlusActionsPanel : EditorWindow
    {
        private VisualElement _editorsContainer;
        private Label _actionsLabel;
        private VisualElement _actionsContainer;

        [MenuItem("Tools/ProBuilder/ProBuilder Plus Panel")]
        public static void ShowWindow()
        {
            var window = GetWindow<ProBuilderPlusActionsPanel>();
            window.titleContent = new GUIContent("ProBuilder Plus");
            window.Show();
        }

        public void CreateGUI()
        {
            // Retrieves the root visual element of this window hierarchy.
            var root = rootVisualElement;

            // Load the UXML template
            var template = Resources.Load<VisualTreeAsset>("UXML/ProBuilderPlus_Actions-Panel");
            if (template == null)
            {
                throw new System.Exception("ProBuilderPlus_Actions-Panel.uxml not found");
            }

            // Instantiate the template
            var panelRoot = template.Instantiate();
            root.Add(panelRoot);

            var isFieldMissing = false;

            // Get references to named elements from UXML
            _editorsContainer = root.QLog<VisualElement>("EditorButtons", ref isFieldMissing);
            _actionsContainer = root.QLog<VisualElement>("ActionButtons", ref isFieldMissing);
            _actionsLabel = root.QLog<Label>("ActionsLabel", ref isFieldMissing);

            if (isFieldMissing)
            {
                throw new System.Exception("ProBuilderPlusActionsPanel: Required UI elements not found in UXML template");
            }

            // Create buttons using Core methods
            ProBuilderPlusActions.PopulateEditorButtons(_editorsContainer);
            UpdateActions();
        }

        private void UpdateActions()
        {
            if (_actionsContainer == null || _actionsLabel == null) return;

            _actionsLabel.text = ProBuilderPlusCore.CurrentToolMode + " Actions";
            ProBuilderPlusActions.PopulateActionButtons(_actionsContainer);
        }

        private void OnEnable()
        {
            ProBuilderPlusCore.OnStatusChanged += UpdateActions;
        }

        private void OnDisable()
        {
            ProBuilderPlusCore.OnStatusChanged -= UpdateActions;
        }
    }
}

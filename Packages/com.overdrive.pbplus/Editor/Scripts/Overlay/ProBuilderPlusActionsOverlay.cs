using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// An Overlay Window, that contains ProBuilder tool functions shortcuts to menus as buttons.<br/>
    /// Functions open a PreviewOverlay that allows to change parameters or just apply with the hit of a button.<br/>
    /// </summary>
    /// <remarks>As a dockable panel, see <see cref="ProBuilderPlusActionsPanel"/>.</remarks>
    [Overlay(typeof(SceneView), "PB+", defaultDisplay = false, defaultDockZone = DockZone.LeftColumn)]
    [Icon("Packages/com.overdrive.shared/Editor/Resources/icons/ProBuilderPlus_Icon.png")]
    public sealed class ProBuilderPlusActionsOverlay : Overlay, ICreateHorizontalToolbar, ICreateVerticalToolbar
    {
        private VisualElement _root;
        private VisualElement _editorsContainer;
        private Label _actionsLabel;
        private VisualElement _actionsContainer;
        private VisualElement _mainElement;
        private ToggleButtonGroup _editModeButtons;
        private Button _objectModeButton;
        private Button _vertModeButton;
        private Button _edgeModeButton;
        private Button _faceModeButton;
        private Button _uvModeButton;

        // Cache toolbar content for dynamic updates
        private OverlayToolbar _cachedHorizontalToolbar;
        private OverlayToolbar _cachedVerticalToolbar;

        // Icon mode state
        private bool _iconMode = false;

        // CTRL key state tracking to prevent repeat spam
        private bool _ctrlKeyDown = false;

        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement();

            // Add right-click context menu to the root element
            _root.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));

            // Try to load the normal UI, if it fails show placeholder
            if (!TryInitializeUI())
            {
                CreatePlaceholderUI();
            }

            return _root;
        }

        private bool TryInitializeUI()
        {
            // Load the UXML template from Resources
            var template = Resources.Load<VisualTreeAsset>("UXML/ProBuilderPlus_Actions-Overlay");
            if (template == null)
            {
                return false; // Assets not ready yet
            }

            // Instantiate the template
            var overlayRoot = template.Instantiate();
            _root.Add(overlayRoot);

            var isFieldMissing = false;

            // Get references to named elements from UXML
            _editorsContainer = _root.QLog<VisualElement>("EditorButtons", ref isFieldMissing);
            _actionsContainer = _root.QLog<VisualElement>("ActionButtons", ref isFieldMissing);
            _actionsLabel = _root.QLog<Label>("ActionsLabel", ref isFieldMissing);
            _mainElement = _root.QLog<VisualElement>("Main", ref isFieldMissing);
            _editModeButtons = _root.QLog<ToggleButtonGroup>("EditModeButtons", ref isFieldMissing);
            _objectModeButton = _editModeButtons.QLog<Button>("ObjectMode", ref isFieldMissing);
            _vertModeButton = _editModeButtons.QLog<Button>("VertMode", ref isFieldMissing);
            _edgeModeButton = _editModeButtons.QLog<Button>("EdgeMode", ref isFieldMissing);
            _faceModeButton = _editModeButtons.QLog<Button>("FaceMode", ref isFieldMissing);
            _uvModeButton = _editModeButtons.QLog<Button>("UVMode", ref isFieldMissing);
            if (isFieldMissing)
            {
                Debug.LogError("PB+ Overlay not initialized. Field missing.");
                return false;
            }

            InitializeUIElements();
            return true;
        }

        private void InitializeUIElements()
        {
            // Create buttons using Core methods
            ProBuilderPlusActions.PopulateEditorButtons(_editorsContainer);
            UpdateActions();

            _objectModeButton.clicked += static () => ProBuilderPlusCore.SetToolMode(ToolMode.Object);
            _vertModeButton.clicked += static () => ProBuilderPlusCore.SetToolMode(ToolMode.Vertex);
            _edgeModeButton.clicked += static () => ProBuilderPlusCore.SetToolMode(ToolMode.Edge);
            _faceModeButton.clicked += static () => ProBuilderPlusCore.SetToolMode(ToolMode.Face);
            _uvModeButton.clicked += static () => ProBuilderPlusCore.SetToolMode(ToolMode.UvFace);

            // Apply initial icon mode styles
            ApplyIconModeStyles();

            // Apply initial UI element visibility
            UpdateEditModeButtons();
            UpdateEditorButtons();
            UpdateActionButtons();
        }

        private void CreatePlaceholderUI()
        {
            // Create main container
            var container = new VisualElement();
            container.style.paddingTop = 10;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingBottom = 10;
            container.style.minWidth = 200;

            // Title label
            var titleLabel = new Label("ProBuilder Plus");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            container.Add(titleLabel);

            // QuickStart Helper button
            var quickStartButton = new Button(static () =>
            {
                Application.OpenURL("https://www.overdrivetoolset.com/probuilder-plus/");
            });
            quickStartButton.text = "QuickStart Helper";
            quickStartButton.style.marginBottom = 4;
            container.Add(quickStartButton);

            // Discord button
            var discordButton = new Button(static () =>
            {
                Application.OpenURL("https://discord.gg/JVQecUp7rE");
            });
            discordButton.text = "Discord";
            discordButton.style.marginBottom = 8;
            container.Add(discordButton);

            // Ready button
            var readyButton = new Button(() =>
            {
                _root.Clear();
                if (TryInitializeUI())
                {
                    // Force a layout refresh to ensure proper sizing
                    _root.MarkDirtyRepaint();
                    _root.schedule.Execute(() =>
                    {
                        _root.MarkDirtyRepaint();
                    }).ExecuteLater(1);
                }
                else
                {
                    // Re-create placeholder since we cleared it
                    CreatePlaceholderUI();
                }
            });
            readyButton.text = "Ready!";
            readyButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f, 1f));
            container.Add(readyButton);

            _root.Add(container);
        }

        private void UpdateActions()
        {
            if (_actionsContainer == null || _actionsLabel == null) return;

            _actionsLabel.text = ProBuilderPlusCore.CurrentToolMode + " Actions";
            ProBuilderPlusActions.PopulateActionButtons(_actionsContainer);

            // Update all UI element visibility
            UpdateEditModeButtons();
            UpdateEditorButtons();
            UpdateActionButtons();
        }

        private void UpdateEditorButtons()
        {
            // Check user preference for showing editor buttons
            bool shouldShow = UserPreferences.ActionsOverlay.ShowEditorButtons;
            _editorsContainer.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateActionButtons()
        {
            // Check user preference for showing action buttons
            bool shouldShow = UserPreferences.ActionsOverlay.ShowActionButtons;
            _actionsContainer.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            _actionsLabel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateEditModeButtons()
        {

            // Check user preference for showing edit mode buttons
            bool shouldShow = UserPreferences.ActionsOverlay.ShowEditModeButtons;
            _editModeButtons.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;

            if (!shouldShow) return; // Don't update selection if hidden

            // Enable/disable based on ProBuilder object selection
            bool hasProBuilderObjects = ProBuilderFunctions.IsProBuilderMeshesSelected();
            _editModeButtons.SetEnabled(hasProBuilderObjects);

            // Get current edit mode from ProBuilderPlusCore
            var currentMode = ProBuilderPlusCore.CurrentToolMode;

            // Map edit mode to button index (based on UXML order: ObjectMode, VertMode, EdgeMode, FaceMode)
            ulong optionsBitMask = currentMode switch
            {
                ToolMode.Object => 0b_0000_0001UL,
                ToolMode.Vertex => 0b_0000_0010UL,
                ToolMode.Edge => 0b_0000_0100UL,
                ToolMode.Face => 0b_0000_1000UL,
                ToolMode.UvFace => 0b_0001_0000UL,
                _ => 0UL,
            };

            // Create a ToggleButtonGroupState with the selected index
            // Using SetValueWithoutNotify to avoid triggering events that could cause recursion
            var groupState = new ToggleButtonGroupState(optionsBitMask, length: 5); // 5 buttons total
            _editModeButtons.SetValueWithoutNotify(groupState);
        }

        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            _cachedHorizontalToolbar = new OverlayToolbar();
            PopulateToolbar(_cachedHorizontalToolbar, true);
            return _cachedHorizontalToolbar;
        }

        public OverlayToolbar CreateVerticalToolbarContent()
        {
            _cachedVerticalToolbar = new OverlayToolbar();
            PopulateToolbar(_cachedVerticalToolbar, true);
            return _cachedVerticalToolbar;
        }

        private static void PopulateToolbar(OverlayToolbar toolbar, bool iconOnly = true)
        {
            toolbar.Clear();

            // Add editor buttons
            var editorActions = ActionInfoProvider.GetEditorActions();
            foreach (var action in editorActions)
            {
                var button = CreateToolbarButton(action, iconOnly);
                toolbar.Add(button);
            }

            // Add action buttons based on current mode
            var actions = ActionInfoProvider.GetModeActions(ProBuilderPlusCore.CurrentToolMode);

            foreach (var action in actions)
            {
                var button = CreateToolbarButton(action, iconOnly);
                toolbar.Add(button);
            }
        }

        private static EditorToolbarButton CreateToolbarButton(IActionInfo action, bool iconOnly = true)
        {
            EditorToolbarButton button;

            if (iconOnly)
            {
                var icon = action.Icon; // Toolbar in Icon mode always needs an icon.
                if (icon == null)
                {
                    icon = Resources.Load<Texture2D>("Icons/Old/ProBuilderGUI_UV_ShowTexture_On");
                }

                button = new EditorToolbarButton(icon, () => {
                    bool ctrlHeld = Event.current?.control == true;
                    ProBuilderPlusActions.ExecuteAction(action, ctrlHeld);
                });

                button.tooltip = action.Tooltip;
            }
            else
            {
                button = new EditorToolbarButton(action.DisplayName, () => {
                    bool ctrlHeld = Event.current?.control == true;
                    ProBuilderPlusActions.ExecuteAction(action, ctrlHeld);
                });

                if (action.Icon != null)
                {
                    button.icon = action.Icon;
                }
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

        public override void OnCreated()
        {
            ProBuilderPlusCore.UpdateStatus();
            ProBuilderPlusCore.OnStatusChanged += OnStatusChanged;

            // Subscribe to SceneView events for keyboard handling when scene has focus
            SceneView.duringSceneGui += OnSceneGUI;

            // Ensure overlay doesn't display automatically
            displayed = false;
        }

        public override void OnWillBeDestroyed()
        {
            ProBuilderPlusCore.OnStatusChanged -= OnStatusChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnStatusChanged()
        {
            UpdateActions();

            // Update cached toolbar content
            if (_cachedHorizontalToolbar != null)
            {
                PopulateToolbar(_cachedHorizontalToolbar, true);
            }

            if (_cachedVerticalToolbar != null)
            {
                PopulateToolbar(_cachedVerticalToolbar, true);
            }
        }

        private void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Icon Mode", (a) =>
            {
                _iconMode = !_iconMode;
                ApplyIconModeStyles();
            }, (a) => _iconMode ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Show Edit Mode Buttons", (a) =>
            {
                UserPreferences.ActionsOverlay.ShowEditModeButtons = !UserPreferences.ActionsOverlay.ShowEditModeButtons;
                UpdateEditModeButtons(); // Refresh the display
            }, static (a) => UserPreferences.ActionsOverlay.ShowEditModeButtons ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Show Editor Buttons", (a) =>
            {
                UserPreferences.ActionsOverlay.ShowEditorButtons = !UserPreferences.ActionsOverlay.ShowEditorButtons;
                UpdateEditorButtons(); // Refresh the display
            }, static (a) => UserPreferences.ActionsOverlay.ShowEditorButtons ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Show Action Buttons", (a) =>
            {
                UserPreferences.ActionsOverlay.ShowActionButtons = !UserPreferences.ActionsOverlay.ShowActionButtons;
                UpdateActionButtons(); // Refresh the display
            }, static (a) => UserPreferences.ActionsOverlay.ShowActionButtons ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Help and Info ...", static (a) =>
            {
                Application.OpenURL("https://www.overdrivetoolset.com/probuilder-plus");
            });
        }

        private void ApplyIconModeStyles()
        {
            if (_mainElement == null || _actionsContainer == null) return;

            if (_iconMode)
            {
                _mainElement.AddToClassList("slim");
                _actionsContainer.AddToClassList("icon-only-button");
            }
            else
            {
                _mainElement.RemoveFromClassList("slim");
                _actionsContainer.RemoveFromClassList("icon-only-button");
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Only handle input when this overlay is displayed
            if (!displayed) return;

            Event current = Event.current;

            // Debug: Log CTRL key events when scene has focus (only on initial press, not repeats)
            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.LeftControl || current.keyCode == KeyCode.RightControl) && !_ctrlKeyDown)
            {
                _ctrlKeyDown = true;
                _root?.AddToClassList("instant-mode");
                DisableNonInstantButtons(true);
            }
            if (current.type == EventType.KeyUp && (current.keyCode == KeyCode.LeftControl || current.keyCode == KeyCode.RightControl) && _ctrlKeyDown)
            {
                _ctrlKeyDown = false;
                _root?.RemoveFromClassList("instant-mode");
                DisableNonInstantButtons(false);
            }
        }

        private void DisableNonInstantButtons(bool disable)
        {
            if (_root == null) return;

            // Find all buttons with "dont-allow-instant" class
            var nonInstantButtons = _root.Query<Button>(className: "dont-allow-instant").ToList();
            foreach (var button in nonInstantButtons)
            {
                if (disable)
                {
                    // Disable them when CTRL is held
                    button.SetEnabled(false);
                }
                else
                {
                    // When CTRL is released, restore proper enabled state
                    RestoreButtonEnabledState(button);
                }
            }
        }

        private void RestoreButtonEnabledState(Button button)
        {
            // Todo: CortiWins: Awfully complicated way to do this.

            // Find the ActionInfo for this button to check its proper enabled state
            var actions = ActionInfoProvider.GetModeActions(ProBuilderPlusCore.CurrentToolMode);
            var editorActions = ActionInfoProvider.GetEditorActions();

            // Check both action lists for the button text match
            foreach (var action in actions.Concat(editorActions))
            {
                if (button.text == action.DisplayName)
                {
                    button.SetEnabled(action.IsEnabledForCurretMode);
                    return;
                }
            }

            // If we can't find the action, default to enabled
            button.SetEnabled(true);
        }
    }
}

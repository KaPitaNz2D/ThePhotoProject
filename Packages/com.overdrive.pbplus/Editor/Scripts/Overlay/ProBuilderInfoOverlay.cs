using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;

/* SETTING                  Edit Mode               UV Mode
-----------------------------------------------------------
Selection Statistics    |   All                 |
Vertex Color            |   Vertex,Edge,Face    |   
Material                |   Face                |   
Smooth                  |   Face                | 
-----------------------------------------------------------
UV Mode                 |   Face                |   
Projection Planar/Box   |   Face                |   UVMode Manual
Flip UV                 |   Face                |   Both Modes
Fill                    |   Face                |   UVMode AUTO
Anchor                  |   Face                |   UVMode AUTO 
TextureGroup            |   Face                |   UVMode AUTO
Rotation                |   Face                |   UVMode AUTO
Scale                   |   Face                |   UVMode AUTO
Offset                  |   Face                |   UVMode AUTO
Pan UV                  |   Face                |   Both Modes
Pan UV Texel            |   Face                |   Both Modes
----------------------------------------------------------- */

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Shows useful actions for the current selection of faces/edges/vertices.
    /// </summary>
    //// [Icon("Packages/com.overdrive.shared/Editor/Resources/icons/ProBuilderPlus_Icon.png")] // Todo: Gabriel: make pretty icon.
    public sealed class ProBuilderInfoOverlay : Overlay // Note: No overlay attribute, controlled by code.
    {
        private ToolMode _currentToolMode = ToolMode.Object;

        /// <summary>
        /// Flag to prevent recursive updates when setting values programmatically
        /// </summary>
        private bool _isUpdatingValues = false;
        private bool _pendingUpdate = false;
        private int _updateValueDepth = 0;
        private int memorizedTabIndex = -1;
        private bool memorizedPanUVVisibity = false;

        #region Fields: UI Elements
        private VisualElement _root;
        private VisualElement _elementSelectedContainer;
        private VisualElement _noElementSelectedContainer;
        private TabView _tabView;

        private VisualElement _statisticsContainer;
        private TextField _statisticsLabelFace;
        private TextField _statisticsLabelVert;
        private TextField _statisticsLabelEdge;
        private TextField _statisticsLabelTris;
        private TextField _statisticsLabelObjs;

        private UnityEditor.UIElements.ColorField _vertexColorField;
        private UnityEditor.UIElements.ObjectField _materialField;
        private IntegerField _smoothingGroupField;

        private EnumField _uvModeField;
        private VisualElement _uvManualItemsContainer;
        private VisualElement _uvFlipContainer;
        private VisualElement _uvAutoItemsContainer;
        private EnumField _uvFillModeField;
        private EnumField _uvAnchorField;
        private IntegerField _uvTextureGroupField;
        private Button _textureGroupSelectButton;
        private Button _textureGroupUnselectButton;
        private EnumField _uvUseWorldSpaceField;
        private FloatField _uvRotationField;
        private Vector2Field _uvOffsetField;
        private Vector2Field _uvScaleField;
        private Foldout _panUVGroup;
        #endregion Fields: UI Elements

        #region Fields: Track which fields are showing mixed values
        private bool _materialShowingMixed = false;
        private bool _smoothingGroupShowingMixed = false;
        private bool _uvAnchorShowingMixed = false;
        private bool _uvFillModeShowingMixed = false;
        private bool _uvGroupShowingMixed = false;
        private bool _uvModeShowingMixed = false;
        private bool _uvOffsetShowingMixed = false;
        private bool _uvRotationShowingMixed = false;
        private bool _uvScaleShowingMixed = false;
        private bool _uvUseWorldSpaceShowingMixed = false;
        private bool _vertexColorShowingMixed = false;
        #endregion Fields: mixed malues

        internal ProBuilderInfoOverlay()
        {
            // Todo: Pretty Icon.
            //// this.collapsedIcon = ProBuilderPlusActions.LoadIcon("Icons/Old/Panel_Smoothing");
            this.displayName = "PBi";
            this.minSize = new Vector2(225, 0);
            this.maxSize = new Vector2(350, float.MaxValue);
            this.defaultSize = new Vector2(225, 0);
        }

        public void MemorizeUiState()
        {
            this.memorizedTabIndex = this._tabView.selectedTabIndex;
            this.memorizedPanUVVisibity = this._panUVGroup.value;
        }

        public override VisualElement CreatePanelContent()
        {
            // Load UXML from Resources
            var visualTreeAsset = Resources.Load<VisualTreeAsset>("UXML/ProBuilderPlus_Inspector");
            if (visualTreeAsset == null)
            {
                var errorRoot = new VisualElement();
                errorRoot.Add(new Label("Could not load UXML file"));
                return errorRoot;
            }

            _root = visualTreeAsset.Instantiate();
            _root.schedule.Execute(UpdateSettingsScheduled).Every(intervalMs: 500);

            // Add right-click context menu to the root element
            _root.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));

            var isFieldMissing = false;

            try
            {
                // Query for main containers
                _elementSelectedContainer = _root.QLog<VisualElement>("ElementSelected", ref isFieldMissing);
                _noElementSelectedContainer = _root.QLog<VisualElement>("NoElementSelected", ref isFieldMissing);
                _tabView = _root.QLog<TabView>("TabView", ref isFieldMissing);

                // Statistics
                _statisticsContainer = _root.QLog<VisualElement>("SelectionStatistics", ref isFieldMissing);
                _statisticsLabelFace = _statisticsContainer.QLog<TextField>("StatisticsFacesLabel", ref isFieldMissing);
                _statisticsLabelVert = _statisticsContainer.QLog<TextField>("StatisticsVerticesLabel", ref isFieldMissing);
                _statisticsLabelEdge = _statisticsContainer.QLog<TextField>("StatisticsEdgesLabel", ref isFieldMissing);
                _statisticsLabelTris = _statisticsContainer.QLog<TextField>("StatisticsTrianglesLabel", ref isFieldMissing);
                _statisticsLabelObjs = _statisticsContainer.QLog<TextField>("StatisticsObjectsLabel", ref isFieldMissing);

                // Query for UI elements within ElementSelected container
                _vertexColorField = _elementSelectedContainer?.QLog<UnityEditor.UIElements.ColorField>("VertexColor", ref isFieldMissing);
                _materialField = _elementSelectedContainer?.QLog<UnityEditor.UIElements.ObjectField>("Material", ref isFieldMissing);
                _smoothingGroupField = _elementSelectedContainer?.QLog<IntegerField>("SmoothingGroup", ref isFieldMissing);
                _uvModeField = _elementSelectedContainer?.QLog<EnumField>("UV-Mode", ref isFieldMissing);

                _uvManualItemsContainer = _elementSelectedContainer?.QLog<VisualElement>("UV-Mode-ManualGroup", ref isFieldMissing);
                _uvAutoItemsContainer = _elementSelectedContainer?.QLog<VisualElement>("UV-Mode-AutoGroup", ref isFieldMissing);

                _uvFillModeField = _uvAutoItemsContainer?.QLog<EnumField>("UV-FillMode", ref isFieldMissing);
                _uvAnchorField = _uvAutoItemsContainer?.QLog<EnumField>("UV-Anchor", ref isFieldMissing);
                _uvTextureGroupField = _uvAutoItemsContainer?.QLog<IntegerField>("UV-Group", ref isFieldMissing);
                _textureGroupSelectButton = _uvAutoItemsContainer?.QLog<Button>("UV-TextureGroupSelected", ref isFieldMissing);
                _textureGroupUnselectButton = _uvAutoItemsContainer?.QLog<Button>("UV-TextureUngroupSelected", ref isFieldMissing);
                _uvUseWorldSpaceField = _uvAutoItemsContainer?.QLog<EnumField>("UV-UseWorldSpace", ref isFieldMissing);
                _uvRotationField = _uvAutoItemsContainer?.QLog<FloatField>("UV-Rotation", ref isFieldMissing);
                _uvScaleField = _uvAutoItemsContainer?.QLog<Vector2Field>("UV-Scale", ref isFieldMissing);
                _uvOffsetField = _uvAutoItemsContainer?.QLog<Vector2Field>("UV-Offset", ref isFieldMissing);
                _uvFlipContainer = _elementSelectedContainer.QLog<VisualElement>("FlipUVsGroup", ref isFieldMissing);
                _panUVGroup = _elementSelectedContainer.QLog<Foldout>("PanAndTexels", ref isFieldMissing);

                // Type init
                _materialField.SetObjectType(typeof(Material));
                _uvModeField?.Init(UVMode.Auto);
                _uvFillModeField?.Init(AutoUnwrapSettings.Fill.Fit);
                _uvAnchorField?.Init(AutoUnwrapSettings.Anchor.MiddleCenter);
                _uvUseWorldSpaceField.Init(BooleanValues.False);

                // Set up event handlers for value changes
                {
                    _vertexColorField?.RegisterValueChangedCallback(OnVertexColorChanged);
                    var _vertexColorReset = _elementSelectedContainer?.QLog<Button>("VertexColorReset", ref isFieldMissing);
                    _vertexColorReset?.RegisterCallback<ClickEvent>(OnVertexColorResetButtonClicked);

                    _textureGroupSelectButton?.RegisterCallback<ClickEvent>(OnGroupButtonClicked);
                    _textureGroupUnselectButton?.RegisterCallback<ClickEvent>(OnUngroupButtonClicked);
                    _materialField?.RegisterValueChangedCallback(OnMaterialChanged);
                    _smoothingGroupField?.RegisterValueChangedCallback(OnSmoothingGroupChanged);
                    _uvModeField?.RegisterValueChangedCallback(OnUVModeChanged);

                    _uvFillModeField?.RegisterValueChangedCallback(OnUVFillModeChanged);
                    _uvTextureGroupField?.RegisterValueChangedCallback(OnUVGroupChanged);
                    _uvAnchorField?.RegisterValueChangedCallback(OnUVAnchorChanged);
                    _uvRotationField?.RegisterValueChangedCallback(OnUVRotationChanged);

                    _uvUseWorldSpaceField?.RegisterValueChangedCallback(OnUvWorldSpaceChanged);

                    _uvScaleField?.RegisterValueChangedCallback(OnUVScaleChanged);
                    var resetUVScaleButton = _uvAutoItemsContainer?.QLog<Button>("UV-ScaleResetButton", ref isFieldMissing);
                    resetUVScaleButton.RegisterCallback<ClickEvent>(OnUVCommandButtonsClicked);

                    _uvOffsetField?.RegisterValueChangedCallback(OnUVOffsetChanged);
                    var resetUVOffsetButton = _uvAutoItemsContainer?.QLog<Button>("UV-OffsetResetButton", ref isFieldMissing);
                    resetUVOffsetButton.RegisterCallback<ClickEvent>(OnUVCommandButtonsClicked);

                    // UV Operations Buttons
                    {
                        var button1 = _uvFlipContainer.QLog<Button>("ButtonFlipU", ref isFieldMissing);
                        var button2 = _uvFlipContainer.QLog<Button>("ButtonFlipV", ref isFieldMissing);
                        button1?.RegisterCallback<ClickEvent>(OnUVCommandButtonsClicked);
                        button2?.RegisterCallback<ClickEvent>(OnUVCommandButtonsClicked);
                    }

                    // UV Operations Buttons
                    {
                        var buttonHost = _uvManualItemsContainer.QLog<VisualElement>("ProjectUVsGroup", ref isFieldMissing);
                        var button1 = buttonHost.QLog<Button>("ButtonProjectPlanar", ref isFieldMissing);
                        var button2 = buttonHost.QLog<Button>("ButtonProjectBox", ref isFieldMissing);
                        button1?.RegisterCallback<ClickEvent>(OnUVCommandButtonsClicked);
                        button2?.RegisterCallback<ClickEvent>(OnUVCommandButtonsClicked);
                    }
         
                    // Pan U Buttons
                    {
                        var buttonHost = _elementSelectedContainer.QLog<VisualElement>("UV-PanButtons_U", ref isFieldMissing);
                        var button1 = buttonHost.QLog<Button>("Button1", ref isFieldMissing);
                        var button2 = buttonHost.QLog<Button>("Button2", ref isFieldMissing);
                        var button3 = buttonHost.QLog<Button>("Button3", ref isFieldMissing);
                        var button4 = buttonHost.QLog<Button>("Button4", ref isFieldMissing);
                        var button5 = buttonHost.QLog<Button>("Button5", ref isFieldMissing);

                        UnityEngine.Assertions.Assert.AreEqual(button1.parent.name, "PanU", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button2.parent.name, "PanU", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button3.parent.name, "PanU", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button4.parent.name, "PanU", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button5.parent.name, "PanU", "Buttons require specific parent name in click handler");

                        button1.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button2.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button3.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button4.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button5.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                    }

                    // Pan V Buttons
                    {
                        var buttonHost = _elementSelectedContainer.QLog<VisualElement>("UV-PanButtons_V", ref isFieldMissing);
                        var button1 = buttonHost.QLog<Button>("Button1", ref isFieldMissing);
                        var button2 = buttonHost.QLog<Button>("Button2", ref isFieldMissing);
                        var button3 = buttonHost.QLog<Button>("Button3", ref isFieldMissing);
                        var button4 = buttonHost.QLog<Button>("Button4", ref isFieldMissing);
                        var button5 = buttonHost.QLog<Button>("Button5", ref isFieldMissing);

                        UnityEngine.Assertions.Assert.AreEqual(button1.parent.name, "PanV", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button2.parent.name, "PanV", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button3.parent.name, "PanV", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button4.parent.name, "PanV", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button5.parent.name, "PanV", "Buttons require specific parent name in click handler");

                        button1?.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button2?.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button3?.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button4?.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                        button5?.RegisterCallback<ClickEvent>(OnPanUVButtonsClicked);
                    }

                    // Pan U Texels Buttons
                    {
                        var buttonHost = _elementSelectedContainer.QLog<VisualElement>("UV-PanButtons_UTexels", ref isFieldMissing);
                        var button1 = buttonHost.QLog<Button>("Button1", ref isFieldMissing);
                        var button2 = buttonHost.QLog<Button>("Button2", ref isFieldMissing);
                        var button3 = buttonHost.QLog<Button>("Button3", ref isFieldMissing);
                        var button4 = buttonHost.QLog<Button>("Button4", ref isFieldMissing);
                        var button5 = buttonHost.QLog<Button>("Button5", ref isFieldMissing);

                        UnityEngine.Assertions.Assert.AreEqual(button1.parent.name, "PanUT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button2.parent.name, "PanUT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button3.parent.name, "PanUT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button4.parent.name, "PanUT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button5.parent.name, "PanUT", "Buttons require specific parent name in click handler");

                        button1?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button2?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button3?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button4?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button5?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                    }

                    // Pan V Texels Buttons
                    {
                        var buttonHost = _elementSelectedContainer.QLog<VisualElement>("UV-PanButtons_VTexels", ref isFieldMissing);
                        var button1 = buttonHost.QLog<Button>("Button1", ref isFieldMissing);
                        var button2 = buttonHost.QLog<Button>("Button2", ref isFieldMissing);
                        var button3 = buttonHost.QLog<Button>("Button3", ref isFieldMissing);
                        var button4 = buttonHost.QLog<Button>("Button4", ref isFieldMissing);
                        var button5 = buttonHost.QLog<Button>("Button5", ref isFieldMissing);

                        UnityEngine.Assertions.Assert.AreEqual(button1.parent.name, "PanVT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button2.parent.name, "PanVT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button3.parent.name, "PanVT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button4.parent.name, "PanVT", "Buttons require specific parent name in click handler");
                        UnityEngine.Assertions.Assert.AreEqual(button5.parent.name, "PanVT", "Buttons require specific parent name in click handler");

                        button1?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button2?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button3?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button4?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                        button5?.RegisterCallback<ClickEvent>(OnPanUVTexelButtonsClicked);
                    }

                    // Todo: CortiWins: Focus on TextFields inside does not work.
                    ////{
                    ////    _uvRotationField.Q<TextField>()?.RegisterCallback<FocusInEvent>(evt =>
                    ////    {
                    ////        Debug.Log("Focus in '_uvRotationFloatField' text field");
                    ////        _uvRotationField.ClearMixedStateOnFocus(ref _uvRotationShowingMixed);
                    ////    });

                    ////    if (_uvScaleField != null)
                    ////    {
                    ////        var xField = _uvScaleField.QLog<FloatField>("unity-x-input", ref isFieldMissing);
                    ////        var yField = _uvScaleField.QLog<FloatField>("unity-y-input", ref isFieldMissing);
                    ////        xField?.Q<TextField>()?.RegisterCallback<FocusInEvent>(evt =>
                    ////        {
                    ////            Debug.LogWarning("Focus in '_uvScaleField' text field");
                    ////            _uvScaleField.ClearMixedStateOnFocus(ref _uvScaleShowingMixed);
                    ////        });
                    ////        yField?.Q<TextField>()?.RegisterCallback<FocusInEvent>(evt =>
                    ////        {
                    ////            Debug.LogWarning("Focus in '_uvScaleField' text field");
                    ////            _uvScaleField.ClearMixedStateOnFocus(ref _uvScaleShowingMixed);
                    ////        });
                    ////    }

                    ////    if (_uvOffsetField != null)
                    ////    {
                    ////        var xField = _uvOffsetField.QLog<FloatField>("unity-x-input", ref isFieldMissing);
                    ////        var yField = _uvOffsetField.QLog<FloatField>("unity-y-input", ref isFieldMissing);
                    ////        xField?.Q<TextField>()?.RegisterCallback<FocusInEvent>(evt =>
                    ////        {
                    ////            Debug.LogWarning("Focus in '_uvOffsetField' text field");
                    ////            _uvOffsetField.ClearMixedStateOnFocus(ref _uvOffsetShowingMixed);
                    ////        });
                    ////        yField?.Q<TextField>()?.RegisterCallback<FocusInEvent>(evt =>
                    ////        {
                    ////            Debug.LogWarning("Focus in '_uvOffsetField' text field");
                    ////            _uvOffsetField.ClearMixedStateOnFocus(ref _uvOffsetShowingMixed);
                    ////        });
                    ////    }
                    ////}
                }

                // reapply memorized state
                this._panUVGroup.value = this.memorizedPanUVVisibity;
                this._tabView.selectedTabIndex = this.memorizedTabIndex != 0 ? this.memorizedTabIndex : 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("ProBuilderInfoOverlay.CreatePanelContent Exception");
                Debug.LogException(ex);
            }

            if (isFieldMissing)
            {
                Debug.LogError("ProBuilderInfoOverlay (PBi) not initialized. Field missing.");
                return null;
            }

            // Initialize UI visibility based on user preferences
            UpdateElementVisibility();
            UpdateElementValues();
            return _root;
        }

        public void UpdateOverlay()
        {
            _currentToolMode = ProBuilderEditor.selectMode.ToToolMode();

            // Use delayed update to prevent rapid-fire updates during drag selection
            if (!_pendingUpdate)
            {
                _pendingUpdate = true;
                EditorApplication.delayCall += () =>
                {
                    _pendingUpdate = false;
                    if (this != null) // Check if overlay still exists
                    {
                        if (_root == null) return;
                        UpdateElementVisibility();
                        UpdateSelectionStatistics();
                        UpdateElementValues();
                    }
                };
            }
        }

        private void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Show Selection Info", (a) =>
            {
                UserPreferences.InfoOverlay.ShowSelectionInfo = !UserPreferences.InfoOverlay.ShowSelectionInfo;
                ProBuilderPlusCore.ForceOnStatusChangedEvent();
            }, static (a) => UserPreferences.InfoOverlay.ShowSelectionInfo ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Show Pan UV Buttons", (a) =>
            {
                UserPreferences.InfoOverlay.ShowPanUVButtons = !UserPreferences.InfoOverlay.ShowPanUVButtons;
                ProBuilderPlusCore.ForceOnStatusChangedEvent();
            }, static (a) => UserPreferences.InfoOverlay.ShowPanUVButtons ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Show Smoothness Group", (a) =>
            {
                UserPreferences.InfoOverlay.ShowSmoothnessGroup = !UserPreferences.InfoOverlay.ShowSmoothnessGroup;
                ProBuilderPlusCore.ForceOnStatusChangedEvent();
            }, static (a) => UserPreferences.InfoOverlay.ShowSmoothnessGroup ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Show Disabled UV Settings", (a) =>
            {
                UserPreferences.InfoOverlay.ShowDisabledUvSettings = !UserPreferences.InfoOverlay.ShowDisabledUvSettings;
                ProBuilderPlusCore.ForceOnStatusChangedEvent();
            }, static (a) => UserPreferences.InfoOverlay.ShowDisabledUvSettings ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("X Ray | Hidden selections are visible", (a) =>
            {
                ProBuilderInternals.EditorHandleDrawingXRay = !ProBuilderInternals.EditorHandleDrawingXRay;
                SceneView.RepaintAll();
            }, static (a) => ProBuilderInternals.EditorHandleDrawingXRay ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Help and Info ...", static (a) =>
            {
                Application.OpenURL("https://www.overdrivetoolset.com/probuilder-plus");
            });
        }

        private void UpdateElementValues()
        {
            // Only update values when ElementSelected container is visible
            if (_elementSelectedContainer.style.display != DisplayStyle.Flex)
                return;

            bool isFaceMode = _currentToolMode.IsFaceMode();

            // Set flags to prevent recursive updates during programmatic value changes
            _updateValueDepth++;
            _isUpdatingValues = true;
            try
            {
                var (vertexColor, hasMixedVertexColor) = ProBuilderFunctions.GetCurrentSelectionColorWithMixed();
                _vertexColorField.SetColorFieldMixed(hasMixedVertexColor, vertexColor, ref _vertexColorShowingMixed);

                if (isFaceMode)
                {
                    var (material, hasMixedMaterial) = ProBuilderFunctions.GetCurrentFaceMaterialWithMixed();
                    _materialField.SetObjectFieldMixed(hasMixedMaterial, material, ref _materialShowingMixed);

                    var (smoothingGroup, hasMixedSmoothingGroup) = ProBuilderFunctions.GetCurrentFaceSmoothingGroupWithMixed();
                    _smoothingGroupField.SetIntegerFieldMixed(hasMixedSmoothingGroup, smoothingGroup, ref _smoothingGroupShowingMixed);

                    var (uvMode, hasMixedUVMode) = ProBuilderFunctions.GetCurrentUVModeWithMixed();
                    _uvModeField.SetEnumFieldMixed(hasMixedUVMode, uvMode, ref _uvModeShowingMixed);

                    var uvValues = ProBuilderFunctions.GetUVValuesWithMixedDetection();

                    // 7 Werte!
                    _uvAnchorField.SetEnumFieldMixed(uvValues.hasMixedAnchor, uvValues.anchor, ref _uvAnchorShowingMixed);
                    _uvFillModeField.SetEnumFieldMixed(uvValues.hasMixedFill, uvValues.fill, ref _uvFillModeShowingMixed);
                    _uvOffsetField.SetVector2FieldMixed(uvValues.hasMixedOffset, uvValues.offset, ref _uvOffsetShowingMixed);
                    _uvRotationField.SetFloatFieldMixed(uvValues.hasMixedRotation, uvValues.rotation, ref _uvRotationShowingMixed);
                    _uvScaleField.SetVector2FieldMixed(uvValues.hasMixedScale, uvValues.scale, ref _uvScaleShowingMixed);
                    _uvUseWorldSpaceField.SetEnumFieldMixed(uvValues.hasMixedUseWorldSpace, uvValues.GetUvUseWorld(), ref _uvUseWorldSpaceShowingMixed);
                    _uvTextureGroupField.SetIntegerFieldMixed(uvValues.hasMixedTextureGroup, uvValues.textureGroup, ref _uvGroupShowingMixed);

                    // Use Auto for visibility when mixed
                    UpdateUVModeVisibility(hasMixedUVMode ? UVMode.Auto : uvMode);
                }
            }
            finally
            {
                // Always reset flags after updating values
                _isUpdatingValues = false;
                _updateValueDepth--;
            }
        }

        private void UpdateElementVisibility()
        {
            bool isFaceMode = _currentToolMode.IsFaceMode();
            bool isEditMode = _currentToolMode.IsEditMode();
            bool hasElementSelection = isEditMode && ProBuilderFunctions.GetProBuilderSelectionCount(_currentToolMode) > 0;

            _elementSelectedContainer.SetDisplay(isEditMode && hasElementSelection);
            _noElementSelectedContainer.SetDisplay(isEditMode && !hasElementSelection);

            _statisticsContainer.SetDisplay(UserPreferences.InfoOverlay.ShowSelectionInfo);
            _vertexColorField.SetDisplay(true);
            _materialField.SetDisplay(isFaceMode);
            _smoothingGroupField.SetDisplay(UserPreferences.InfoOverlay.ShowSmoothnessGroup && isFaceMode);
            _uvModeField.SetDisplay(isFaceMode);
            _uvAutoItemsContainer.SetDisplay(isFaceMode);
            _uvManualItemsContainer.SetDisplay(isFaceMode);

            _uvFlipContainer.SetDisplay(isFaceMode);
            _panUVGroup.SetDisplay(UserPreferences.InfoOverlay.ShowPanUVButtons && isFaceMode);
        }

        private void UpdateSelectionStatistics()
        {
            if (UserPreferences.InfoOverlay.ShowSelectionInfo)
            {
                // Shared Vertices
                var selectedCountV = MeshSelection.selectedSharedVertexCount;
                var totalCountV = MeshSelection.totalCommonVertexCount;

                var selectedCountE = MeshSelection.selectedEdgeCount.ToString();
                var totalCountE = MeshSelection.totalEdgeCount;

                var totalCountF = MeshSelection.totalFaceCount;
                var selectedCountF = MeshSelection.selectedFaceCount.ToString();

                var totalSelectedCountT = MeshSelection.totalTriangleCountCompiled;
                var selectedCountO = MeshSelection.selectedObjectCount;

                _statisticsLabelFace.value = $"{selectedCountF} / {totalCountF}";
                _statisticsLabelVert.value = $"{selectedCountV} / {totalCountV}";
                _statisticsLabelEdge.value = $"{selectedCountE} / {totalCountE}";
                _statisticsLabelTris.value = $"{totalSelectedCountT}";
                _statisticsLabelObjs.value = $"{selectedCountO}";

                // Note: Real Vertices ( this are the resulting vertices after transformation into native UnityMesh
                // -> var selectedCountV_Real = MeshSelection.selectedVertexCount;
                // -> var totalCountV_Real = MeshSelection.totalVertexCountOptimized;
                // reflection -> ProBuilderInternals.ProBuilderMeshSelectionGetTotalVertexCountOptimizedMethod
            }
        }

        /// <summary>
        /// Method is called every 500ms by the scheduler to update the UV settings.
        /// Their changes are not detected by the ProBuilder and Unity change events.
        /// </summary>
        private void UpdateSettingsScheduled()
        {
            if (!this._currentToolMode.IsFaceMode() || MeshSelection.selectedFaceCount <= 0) return;

            var uvValues = ProBuilderFunctions.GetUVValuesWithMixedDetection();
            var currentUvOffset = this._uvOffsetField.value;
            if (!currentUvOffset.Approximately(uvValues.offset))
            {
                _uvOffsetField.SetVector2FieldMixed(uvValues.hasMixedOffset, uvValues.offset, ref _uvOffsetShowingMixed);
            }

            var currentUvScale = this._uvScaleField.value;
            if (!currentUvScale.Approximately(uvValues.scale))
            {
                _uvScaleField.SetVector2FieldMixed(uvValues.hasMixedScale, uvValues.scale, ref _uvScaleShowingMixed);
            }

            var currentUvRotation = this._uvRotationField.value;
            if (!currentUvRotation.Approximately(uvValues.rotation))
            {
                _uvRotationField.SetFloatFieldMixed(uvValues.hasMixedRotation, uvValues.rotation, ref _uvScaleShowingMixed);
            }
        }
        #region Value Change Handlers

        private void OnGroupButtonClicked(ClickEvent evt)
        {
            if (!_currentToolMode.IsFaceMode()) return;
            ProBuilderFunctions.SetSelectedFacesToUnusedTextureGroup();
        }

        private void OnMaterialChanged(ChangeEvent<Object> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;
            ProBuilderFunctions.ApplyMaterial(evt.newValue as Material);
        }

        private void OnPanUVButtonsClicked(ClickEvent evt)
        {
            if (evt.target is not Button btn)
                return;

            var shiftU = btn.parent.name == "PanU" ? btn.name switch
            {
                "Button1" => 0.25f,
                "Button2" => 0.125f,
                "Button3" => 0.0625f,
                "Button4" => 0.015625f,
                "Button5" => 0.0078125f,
                _ => 0.0f,
            } : 0.0f;

            var shiftV = btn.parent.name == "PanV" ? btn.name switch
            {
                "Button1" => 0.25f,
                "Button2" => 0.125f,
                "Button3" => 0.0625f,
                "Button4" => 0.015625f,
                "Button5" => 0.0078125f,
                _ => 0.0f,
            } : 0.0f;

            ProBuilderFunctions.AppplyUVOffsetPan(new Vector2(
                evt.shiftKey ? -shiftU : shiftU,
                evt.shiftKey ? -shiftV : shiftV));
            this.UpdateSettingsScheduled();
        }

        private void OnPanUVTexelButtonsClicked(ClickEvent evt)
        {
            if (evt.target is not Button btn)
                return;

            var shiftU = btn.parent.name == "PanUT" ? btn.name switch
            {
                "Button1" => 256,
                "Button2" => 128,
                "Button3" => 32,
                "Button4" => 8,
                "Button5" => 1,
                _ => 0,
            } : 0;

            var shiftV = btn.parent.name == "PanVT" ? btn.name switch
            {
                "Button1" => 256,
                "Button2" => 128,
                "Button3" => 32,
                "Button4" => 8,
                "Button5" => 1,
                _ => 0,
            } : 0;

            ProBuilderFunctions.ApplyUVOffsetPanTexels(new Vector2(
                (evt.shiftKey ? -shiftU : shiftU),
                (evt.shiftKey ? -shiftV : shiftV)));
            this.UpdateSettingsScheduled();
        }

        private void OnSmoothingGroupChanged(ChangeEvent<int> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;
            ProBuilderFunctions.ApplySmoothingGroup(evt.newValue);
        }

        private void OnUngroupButtonClicked(ClickEvent evt)
        {
            if (!_currentToolMode.IsFaceMode()) return;
            ProBuilderFunctions.UngroupSelectedFaces();
        }

        private void OnUVAnchorChanged(ChangeEvent<System.Enum> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            ProBuilderFunctions.ApplyUVAnchor((AutoUnwrapSettings.Anchor)evt.newValue);
        }

        private void OnUVCommandButtonsClicked(ClickEvent evt)
        {
            if (evt.target is not Button btn)
                return;

            if (btn.name == "ButtonProjectPlanar")
            {
                ProBuilderFunctions.ProjectUV(UVProjectionMode.Planar);
            }
            else if (btn.name == "ButtonProjectBox")
            {
                ProBuilderFunctions.ProjectUV(UVProjectionMode.Box);
            }
            else if (btn.name == "ButtonFlipU")
            {
                ProBuilderFunctions.FlipUV(UVAxis.U);
            }
            else if (btn.name == "ButtonFlipV")
            {
                ProBuilderFunctions.FlipUV(UVAxis.V);
            }
            else if (btn.name == "UV-ScaleResetButton")
            {
                this._uvScaleField.value = Vector2.one;
            }
            else if (btn.name == "UV-OffsetResetButton")
            {
                this._uvOffsetField.value = Vector2.zero;
            }
        }

        private void OnUVFillModeChanged(ChangeEvent<System.Enum> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            ProBuilderFunctions.ApplyUVFillMode((AutoUnwrapSettings.Fill)evt.newValue);
        }

        private void OnUVGroupChanged(ChangeEvent<int> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            ProBuilderFunctions.ApplyUVGroup(evt.newValue);
        }

        private void OnUVModeChanged(ChangeEvent<System.Enum> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            var uvMode = (UVMode)evt.newValue;
            ProBuilderFunctions.ApplyUVMode(uvMode);
            UpdateUVModeVisibility(uvMode);
        }

        private void OnUVOffsetChanged(ChangeEvent<Vector2> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            ProBuilderFunctions.ApplyUVOffset(evt.newValue);
        }

        private void OnUVRotationChanged(ChangeEvent<float> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            ProBuilderFunctions.ApplyUVRotation(evt.newValue);
        }

        private void OnUVScaleChanged(ChangeEvent<Vector2> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            ProBuilderFunctions.ApplyUVScale(evt.newValue);
        }

        private void OnUvWorldSpaceChanged(ChangeEvent<System.Enum> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            if (!_currentToolMode.IsFaceMode()) return;

            var useUvWorld = (BooleanValues)evt.newValue;
            ProBuilderFunctions.ApplyUVUseWorldSpace(useUvWorld == BooleanValues.True);
        }

        private void OnVertexColorChanged(ChangeEvent<Color> evt)
        {
            if (_isUpdatingValues || _updateValueDepth > 0) return;
            ProBuilderFunctions.ApplyVertexColor(evt.newValue);
        }

        private void OnVertexColorResetButtonClicked(ClickEvent evt)
        {
            ProBuilderFunctions.ClearVertexColor();
            var (vertexColor, hasMixedVertexColor) = ProBuilderFunctions.GetCurrentSelectionColorWithMixed();
            _vertexColorField.SetColorFieldMixed(hasMixedVertexColor, vertexColor, ref _vertexColorShowingMixed);
        }
        #endregion
        private void UpdateUVModeVisibility(UVMode uvMode)
        {
            _uvAutoItemsContainer.SetEnabled(uvMode == UVMode.Auto);
            _uvManualItemsContainer.SetEnabled(uvMode == UVMode.Manual);
            _uvAutoItemsContainer.SetDisplay(uvMode == UVMode.Auto || UserPreferences.InfoOverlay.ShowDisabledUvSettings);
            _uvManualItemsContainer.SetDisplay(uvMode == UVMode.Manual || UserPreferences.InfoOverlay.ShowDisabledUvSettings);
        }
    }
}

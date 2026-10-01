using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// UI Element that is shown in the Unity User Preferences as 'Preferences/Overdrive/ProBuilder Plus'
    /// </summary>
    internal static class UserPreferencesProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProBuilderPlusUserPreferencesProvider()
        {
            SettingsProvider provider = new SettingsProvider("Preferences/Overdrive/ProBuilder Plus", SettingsScope.User)
            {
                label = "ProBuilder Plus",
                activateHandler = static (searchContext, rootElement) =>
                {
                    VisualTreeAsset settings = Resources.Load<VisualTreeAsset>("UXML/ProBuilderPlus_UserPreferences");

                    if (settings != null)
                    {
                        TemplateContainer settingsContainer = settings.Instantiate();

                        // Setup UI elements and bind to preferences
                        SetupUI(settingsContainer);
                        SetupToolSettings(settingsContainer);
                        rootElement.Add(settingsContainer);
                    }
                    else
                    {
                        Debug.LogError("UserPreferencesProvider: Could not load ProBuilderPlus_UserPreferences.Tools.uxml from Resources");
                        var errorLabel = new Label("ProBuilderPlus_UserPreferences.Tools.uxml not found");
                        errorLabel.style.color = Color.red;
                        rootElement.Add(errorLabel);
                    }
                },
                keywords = new HashSet<string>(new[] { "ProBuilder", "ProBuilder Plus", "vertex", "collapse", "weld", "preferences" })
            };

            return provider;
        }

        private static void SetupToolSettings(TemplateContainer container)
        {
            var isUIElementMissing = false;

            // Setup Collapse to First toggle
            var collapseToFirstToggle = container.QLog<Toggle>("CollapseToFirst", ref isUIElementMissing);
            collapseToFirstToggle?.SetValueWithoutNotify(UserPreferences.Tools.CollapseToFirst);
            collapseToFirstToggle?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.CollapseToFirst = evt.newValue;
            });

            // Setup Weld Distance field
            var weldDistanceField = container.QLog<FloatField>("WeldDistance", ref isUIElementMissing);
            weldDistanceField?.SetValueWithoutNotify(UserPreferences.Tools.WeldDistance);
            weldDistanceField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.WeldDistance = Mathf.Max(0.00001f, evt.newValue);
            });

            // Setup Extrude Distance field
            var extrudeDistanceField = container.QLog<FloatField>("ExtrudeDistance", ref isUIElementMissing);
            extrudeDistanceField?.SetValueWithoutNotify(UserPreferences.Tools.ExtrudeDistance);
            extrudeDistanceField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ExtrudeDistance = evt.newValue;
            });

            // Setup Extrude As Group toggle
            var extrudeAsGroupToggle = container.QLog<Toggle>("ExtrudeAsGroup", ref isUIElementMissing);
            extrudeAsGroupToggle?.SetValueWithoutNotify(UserPreferences.Tools.ExtrudeAsGroup);
            extrudeAsGroupToggle?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ExtrudeAsGroup = evt.newValue;
            });

            // Setup Loop Position field
            var loopPositionField = container.QLog<FloatField>("LoopPosition", ref isUIElementMissing);
            loopPositionField?.SetValueWithoutNotify(UserPreferences.Tools.LoopPosition);
            loopPositionField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.LoopPosition = evt.newValue;
            });

            // Setup Loop Direction field
            var loopDirectionField = container.QLog<EnumField>("LoopDirection", ref isUIElementMissing);
            loopDirectionField?.Init((InsertEdgeLoopPreviewAction.ConnectionDirection)UserPreferences.Tools.LoopDirection);
            loopDirectionField?.SetValueWithoutNotify((InsertEdgeLoopPreviewAction.ConnectionDirection)UserPreferences.Tools.LoopDirection);
            loopDirectionField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.LoopDirection = (int)(InsertEdgeLoopPreviewAction.ConnectionDirection)evt.newValue;
            });

            // Setup Loop Mode field
            var loopModeField = container.QLog<EnumField>("LoopMode", ref isUIElementMissing);
            loopModeField?.Init((InsertEdgeLoopPreviewAction.ConnectionMode)UserPreferences.Tools.LoopMode);
            loopModeField?.SetValueWithoutNotify((InsertEdgeLoopPreviewAction.ConnectionMode)UserPreferences.Tools.LoopMode);
            loopModeField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.LoopMode = (int)(InsertEdgeLoopPreviewAction.ConnectionMode)evt.newValue;
            });

            // Setup Bridge Rotation Offset field
            var bridgeRotationOffsetField = container.QLog<IntegerField>("BridgeRotationOffset", ref isUIElementMissing);
            bridgeRotationOffsetField?.SetValueWithoutNotify(UserPreferences.Tools.BridgeRotationOffset);
            bridgeRotationOffsetField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.BridgeRotationOffset = evt.newValue;
            });

            // Setup Bridge Reverse Order toggle
            var bridgeReverseOrderToggle = container.QLog<Toggle>("BridgeReverseOrder", ref isUIElementMissing);
            bridgeReverseOrderToggle?.SetValueWithoutNotify(UserPreferences.Tools.BridgeReverseOrder);
            bridgeReverseOrderToggle?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.BridgeReverseOrder = evt.newValue;
            });

            // Setup Bridge Use Full Borders toggle
            var bridgeUseFullBordersToggle = container.QLog<Toggle>("BridgeUseFullBorders", ref isUIElementMissing);
            bridgeUseFullBordersToggle?.SetValueWithoutNotify(UserPreferences.Tools.BridgeUseFullBorders);
            bridgeUseFullBordersToggle?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.BridgeUseFullBorders = evt.newValue;
            });

            // Setup Connection Position field
            var connectionPositionField = container.QLog<FloatField>("ConnectionPosition", ref isUIElementMissing);
            connectionPositionField?.SetValueWithoutNotify(UserPreferences.Tools.ConnectionPosition);
            connectionPositionField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ConnectionPosition = evt.newValue;
            });

            // Setup Connection Direction field
            var connectionDirectionField = container.QLog<EnumField>("ConnectionDirection", ref isUIElementMissing);
            connectionDirectionField?.Init((ConnectEdgesPreviewAction.ConnectionDirection)UserPreferences.Tools.ConnectionDirection);
            connectionDirectionField?.SetValueWithoutNotify((ConnectEdgesPreviewAction.ConnectionDirection)UserPreferences.Tools.ConnectionDirection);
            connectionDirectionField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ConnectionDirection = (int)(ConnectEdgesPreviewAction.ConnectionDirection)evt.newValue;
            });

            // Setup Connection Mode field
            var connectionModeField = container.QLog<EnumField>("ConnectionMode", ref isUIElementMissing);
            connectionModeField?.Init((ConnectEdgesPreviewAction.ConnectionMode)UserPreferences.Tools.ConnectionMode);
            connectionModeField?.SetValueWithoutNotify((ConnectEdgesPreviewAction.ConnectionMode)UserPreferences.Tools.ConnectionMode);
            connectionModeField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ConnectionMode = (int)(ConnectEdgesPreviewAction.ConnectionMode)evt.newValue;
            });

            // === FACE ACTIONS ===

            // Setup Extrude Faces Method field
            var extrudeFacesMethodField = container.QLog<EnumField>("ExtrudeFacesMethod", ref isUIElementMissing);
            extrudeFacesMethodField?.Init((CustomExtrudeMethod)UserPreferences.Tools.ExtrudeFacesMethod);
            extrudeFacesMethodField?.SetValueWithoutNotify((CustomExtrudeMethod)UserPreferences.Tools.ExtrudeFacesMethod);
            extrudeFacesMethodField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ExtrudeFacesMethod = (int)(CustomExtrudeMethod)evt.newValue;
            });

            // Setup Extrude Faces Space field
            var extrudeFacesSpaceField = container.QLog<EnumField>("ExtrudeFacesSpace", ref isUIElementMissing);
            extrudeFacesSpaceField?.Init((ExtrudeSpace)UserPreferences.Tools.ExtrudeFacesSpace);
            extrudeFacesSpaceField?.SetValueWithoutNotify((ExtrudeSpace)UserPreferences.Tools.ExtrudeFacesSpace);
            extrudeFacesSpaceField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ExtrudeFacesSpace = (int)(ExtrudeSpace)evt.newValue;
            });

            // Setup Extrude Faces Axis field
            var extrudeFacesAxisField = container.QLog<EnumField>("ExtrudeFacesAxis", ref isUIElementMissing);
            extrudeFacesAxisField?.Init((ExtrudeAxis)UserPreferences.Tools.ExtrudeFacesAxis);
            extrudeFacesAxisField?.SetValueWithoutNotify((ExtrudeAxis)UserPreferences.Tools.ExtrudeFacesAxis);
            extrudeFacesAxisField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ExtrudeFacesAxis = (int)(ExtrudeAxis)evt.newValue;
            });

            // === SHARED ACTIONS ===

            // Setup Remove Extrude Distance field
            var removeExtrudeDistanceField = container.QLog<FloatField>("RemoveExtrudeDistance", ref isUIElementMissing);
            removeExtrudeDistanceField?.SetValueWithoutNotify(UserPreferences.Tools.RemoveExtrudeDistance);
            removeExtrudeDistanceField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.RemoveExtrudeDistance = evt.newValue;
            });

            // Setup Fill Entire Path field
            var fillEntirePathField = container.QLog<Toggle>("FillEntirePath", ref isUIElementMissing);
            fillEntirePathField?.SetValueWithoutNotify(UserPreferences.Tools.FillEntirePath);
            fillEntirePathField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.FillEntirePath = evt.newValue;
            });

            // Setup Bevel Distance field
            var bevelDistanceField = container.QLog<FloatField>("BevelDistance", ref isUIElementMissing);
            bevelDistanceField?.SetValueWithoutNotify(UserPreferences.Tools.BevelDistance);
            bevelDistanceField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.BevelDistance = evt.newValue;
            });

            // Setup Bevel Perimeter Only field
            var bevelPerimeterOnlyField = container.QLog<Toggle>("BevelPerimeterOnly", ref isUIElementMissing);
            bevelPerimeterOnlyField?.SetValueWithoutNotify(UserPreferences.Tools.BevelPerimeterOnly);
            bevelPerimeterOnlyField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.BevelPerimeterOnly = evt.newValue;
            });

            // Setup Offset Coordinate Space field
            var offsetCoordinateSpaceField = container.QLog<EnumField>("OffsetCoordinateSpace", ref isUIElementMissing);
            offsetCoordinateSpaceField?.Init((OffsetElementsPreviewActionBase.CoordinateSpace)UserPreferences.Tools.OffsetCoordinateSpace);
            offsetCoordinateSpaceField?.SetValueWithoutNotify((OffsetElementsPreviewActionBase.CoordinateSpace)UserPreferences.Tools.OffsetCoordinateSpace);
            offsetCoordinateSpaceField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.OffsetCoordinateSpace = (int)(OffsetElementsPreviewActionBase.CoordinateSpace)evt.newValue;
            });

            // Setup Offset Vector field
            var offsetVectorField = container.QLog<Vector3Field>("OffsetVector", ref isUIElementMissing);
            offsetVectorField?.SetValueWithoutNotify(UserPreferences.Tools.OffsetVector);
            offsetVectorField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.OffsetVector = evt.newValue;
            });

            // === OBJECT ACTIONS ===

            // Setup Mirror Objects fields
            var mirrorXField = container.QLog<Toggle>("MirrorX", ref isUIElementMissing);
            mirrorXField?.SetValueWithoutNotify((UserPreferences.Tools.MirrorSettings & 1) != 0);
            mirrorXField?.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    UserPreferences.Tools.MirrorSettings |= 1;
                else
                    UserPreferences.Tools.MirrorSettings &= ~1;
            });

            var mirrorYField = container.QLog<Toggle>("MirrorY", ref isUIElementMissing);
            mirrorYField?.SetValueWithoutNotify((UserPreferences.Tools.MirrorSettings & 2) != 0);
            mirrorYField?.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    UserPreferences.Tools.MirrorSettings |= 2;
                else
                    UserPreferences.Tools.MirrorSettings &= ~2;
            });


            var mirrorZField = container.QLog<Toggle>("MirrorZ", ref isUIElementMissing);
            mirrorZField?.SetValueWithoutNotify((UserPreferences.Tools.MirrorSettings & 4) != 0);
            mirrorZField?.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    UserPreferences.Tools.MirrorSettings |= 4;
                else
                    UserPreferences.Tools.MirrorSettings &= ~4;
            });

            var mirrorDuplicateField = container.QLog<Toggle>("MirrorDuplicate", ref isUIElementMissing);
            mirrorDuplicateField?.SetValueWithoutNotify((UserPreferences.Tools.MirrorSettings & 8) != 0);
            mirrorDuplicateField?.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    UserPreferences.Tools.MirrorSettings |= 8;
                else
                    UserPreferences.Tools.MirrorSettings &= ~8;
            });

            // Setup Apply Transform fields
            var applyPositionField = container.QLog<Toggle>("ApplyPosition", ref isUIElementMissing);
            applyPositionField?.SetValueWithoutNotify(UserPreferences.Tools.ApplyPosition);
            applyPositionField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ApplyPosition = evt.newValue;
            });

            var applyRotationField = container.QLog<Toggle>("ApplyRotation", ref isUIElementMissing);
            applyRotationField?.SetValueWithoutNotify(UserPreferences.Tools.ApplyRotation);
            applyRotationField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ApplyRotation = evt.newValue;
            });

            var applyScaleField = container.QLog<Toggle>("ApplyScale", ref isUIElementMissing);
            applyScaleField?.SetValueWithoutNotify(UserPreferences.Tools.ApplyScale);
            applyScaleField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ApplyScale = evt.newValue;
            });

            // Setup Conform Object Normals field
            var conformObjectNormalsOtherDirectionField = container.QLog<Toggle>("ConformObjectNormalsOtherDirection", ref isUIElementMissing);
            conformObjectNormalsOtherDirectionField?.SetValueWithoutNotify(UserPreferences.Tools.ConformObjectNormalsOtherDirection);
            conformObjectNormalsOtherDirectionField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ConformObjectNormalsOtherDirection = evt.newValue;
            });

            // Setup ProBuilderize fields
            var importQuadsField = container.QLog<Toggle>("ImportQuads", ref isUIElementMissing);
            importQuadsField?.SetValueWithoutNotify(UserPreferences.Tools.ImportQuads);
            importQuadsField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ImportQuads = evt.newValue;
            });

            var importSmoothingField = container.QLog<Toggle>("ImportSmoothing", ref isUIElementMissing);
            importSmoothingField?.SetValueWithoutNotify(UserPreferences.Tools.ImportSmoothing);
            importSmoothingField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.ImportSmoothing = evt.newValue;
            });

            var smoothingAngleField = container.QLog<FloatField>("SmoothingAngle", ref isUIElementMissing);
            smoothingAngleField?.SetValueWithoutNotify(UserPreferences.Tools.SmoothingAngle);
            smoothingAngleField?.RegisterValueChangedCallback(evt =>
            {
                UserPreferences.Tools.SmoothingAngle = evt.newValue;
            });
        }

        private static void SetupUI(TemplateContainer container)
        {
            var isUIElementMissing = false;

            // Action Overlay
            {
                var showEditModeButtonsField = container.QLog<Toggle>("ShowEditModeButtons", ref isUIElementMissing);
                showEditModeButtonsField?.SetValueWithoutNotify(UserPreferences.ActionsOverlay.ShowEditModeButtons);
                showEditModeButtonsField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.ActionsOverlay.ShowEditModeButtons = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });

                var showEditorButtonsField = container.QLog<Toggle>("ShowEditorButtons", ref isUIElementMissing);
                showEditorButtonsField?.SetValueWithoutNotify(UserPreferences.ActionsOverlay.ShowEditorButtons);
                showEditorButtonsField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.ActionsOverlay.ShowEditorButtons = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });

                var showActionButtonsField = container.QLog<Toggle>("ShowActionButtons", ref isUIElementMissing);
                showActionButtonsField?.SetValueWithoutNotify(UserPreferences.ActionsOverlay.ShowActionButtons);
                showActionButtonsField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.ActionsOverlay.ShowActionButtons = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });
            }

            // Info Overlay
            {
                var showPanButtonsField = container.QLog<Toggle>("ShowPanButtons", ref isUIElementMissing);
                showPanButtonsField?.SetValueWithoutNotify(UserPreferences.InfoOverlay.ShowPanUVButtons);
                showPanButtonsField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.InfoOverlay.ShowPanUVButtons = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });

                var ShowSelectionInfoField = container.QLog<Toggle>("ShowSelectionInfo", ref isUIElementMissing);
                ShowSelectionInfoField?.SetValueWithoutNotify(UserPreferences.InfoOverlay.ShowSelectionInfo);
                ShowSelectionInfoField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.InfoOverlay.ShowSelectionInfo = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });

                var ShowDisabledUvSettingsField = container.QLog<Toggle>("ShowDisabledUvSettings", ref isUIElementMissing);
                ShowDisabledUvSettingsField?.SetValueWithoutNotify(UserPreferences.InfoOverlay.ShowDisabledUvSettings);
                ShowDisabledUvSettingsField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.InfoOverlay.ShowDisabledUvSettings = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });

                var ShowSmoothnessGroupField = container.QLog<Toggle>("ShowSmoothnessGroup", ref isUIElementMissing);
                ShowSmoothnessGroupField?.SetValueWithoutNotify(UserPreferences.InfoOverlay.ShowSmoothnessGroup);
                ShowSmoothnessGroupField?.RegisterValueChangedCallback(static evt =>
                {
                    UserPreferences.InfoOverlay.ShowSmoothnessGroup = evt.newValue;
                    ProBuilderPlusCore.ForceOnStatusChangedEvent();
                });
            }

            // Setup more info button
            var moreInfoButton = container.QLog<Button>("Btn_Collections-Web", ref isUIElementMissing);
            if (moreInfoButton != null)
            {
                moreInfoButton.clicked += static () =>
                {
                    Application.OpenURL("https://overdrivetoolset.com/probuilder-plus");
                };
            }
        }
    }
}
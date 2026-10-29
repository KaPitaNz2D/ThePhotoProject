using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Overdrive.Framework;

/// <summary>
/// OverdriveSharedPreferencesProvider provides a custom settings provider for the Overdrive toolset user preferences.
/// It provides an overview of all Overdrive tools and links to more information for each.
/// </summary>
internal static class OverdriveSharedPreferencesProvider
{
    private static Toggle opaqueLeftToggle;
    private static Toggle opaqueRightToggle;
    private static Toggle transparentLeftToggle;
    private static Toggle transparentRightToggle;

    // Button references removed - buttons are only in ToolList, not UserPreferences

    [SettingsProvider]
    public static SettingsProvider CreateOverdriveSharedPreferencesProvider()
    {
        SettingsProvider provider = new SettingsProvider("Preferences/Overdrive", SettingsScope.User)
        {
            label = "Overdrive",
            activateHandler = (searchContext, rootElement) =>
            {
                VisualTreeAsset settings = Resources.Load<VisualTreeAsset>("UI/OverdriveShared_UserPreferences");

                if (settings != null)
                {
                    TemplateContainer settingsContainer = settings.Instantiate();

                    // Setup container toggles
                    opaqueLeftToggle = settingsContainer.Q<Toggle>("Toggle_OpaqueLeft");
                    if (opaqueLeftToggle != null)
                    {
                        opaqueLeftToggle.value = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Opaque, DataPanelSide.Left);
                        opaqueLeftToggle.RegisterValueChangedCallback(evt =>
                        {
                            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Opaque, DataPanelSide.Left, evt.newValue);
                        });
                    }

                    opaqueRightToggle = settingsContainer.Q<Toggle>("Toggle_OpaqueRight");
                    if (opaqueRightToggle != null)
                    {
                        opaqueRightToggle.value = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Opaque, DataPanelSide.Right);
                        opaqueRightToggle.RegisterValueChangedCallback(evt =>
                        {
                            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Opaque, DataPanelSide.Right, evt.newValue);
                        });
                    }

                    transparentLeftToggle = settingsContainer.Q<Toggle>("Toggle_TransparentLeft");
                    if (transparentLeftToggle != null)
                    {
                        transparentLeftToggle.value = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Transparent, DataPanelSide.Left);
                        transparentLeftToggle.RegisterValueChangedCallback(evt =>
                        {
                            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Transparent, DataPanelSide.Left, evt.newValue);
                        });
                    }

                    transparentRightToggle = settingsContainer.Q<Toggle>("Toggle_TransparentRight");
                    if (transparentRightToggle != null)
                    {
                        transparentRightToggle.value = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Transparent, DataPanelSide.Right);
                        transparentRightToggle.RegisterValueChangedCallback(evt =>
                        {
                            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Transparent, DataPanelSide.Right, evt.newValue);
                        });
                    }

                    // Note: UserPreferences UXML doesn't contain web buttons, 
                    // they're only in the ToolList which is added separately

                    rootElement.Add(settingsContainer);
                }
                else
                {
                    Debug.LogError("OverdriveSharedPreferencesProvider: Could not load OverdriveShared_UserPreferences.uxml");
                    var errorLabel = new Label("OverdriveShared_UserPreferences.uxml not found");
                    errorLabel.style.color = Color.red;
                    rootElement.Add(errorLabel);
                }

                // Add ToolList at the end using helper
                OverdriveUIHelper.AddToolList(rootElement, "OverdriveSharedPreferencesProvider");
            },
            deactivateHandler = OnDeactivate,
            keywords = new HashSet<string>(new[] { "Overdrive", "Pins", "Scene Blocks", "Multiverse", "Collections", "Todo", "EasyEdit", "toolset" })
        };

        return provider;
    }

    private static void OnDeactivate()
    {
        opaqueLeftToggle = null;
        opaqueRightToggle = null;
        transparentLeftToggle = null;
        transparentRightToggle = null;

        // Button cleanup removed - buttons are handled by ToolList helper
    }
}
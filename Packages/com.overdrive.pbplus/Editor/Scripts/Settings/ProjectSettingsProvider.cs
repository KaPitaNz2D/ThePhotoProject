using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// UI Element that is shown in the Unity User Preferences as 'Project/Overdrive/ProBuilder Plus'
    /// </summary>
    internal static class ProjectSettingsProvider
    {
        private static Button moreInfoButton;

        [SettingsProvider]
        public static SettingsProvider CreateProBuilderPlusSettingsProvider()
        {
            SettingsProvider provider = new SettingsProvider("Project/Overdrive/ProBuilder Plus", SettingsScope.Project)
            {
                label = "ProBuilder Plus",
                activateHandler = static (searchContext, rootElement) =>
                {
                    VisualTreeAsset settings = Resources.Load<VisualTreeAsset>("UXML/ProBuilderPlus_ProjectSettings");

                    if (settings != null)
                    {
                        TemplateContainer settingsContainer = settings.Instantiate();

                        // Setup more info button
                        moreInfoButton = settingsContainer.Q<Button>("Btn_ProBuilderPlus-Web");
                        if (moreInfoButton != null)
                        {
                            moreInfoButton.clicked += static () =>
                            {
                                Application.OpenURL("https://overdrivetoolset.com/probuilder-plus");
                            };
                        }

                        rootElement.Add(settingsContainer);
                    }
                    else
                    {
                        Debug.LogError("ProBuilderPlusSettingsProvider: Could not load ProBuilderPlus_ProjectSettings.uxml from Resources");
                        var errorLabel = new Label("ProBuilderPlus_ProjectSettings.uxml not found");
                        errorLabel.style.color = Color.red;
                        rootElement.Add(errorLabel);
                    }
                },
                deactivateHandler = OnDeactivate,
                keywords = new HashSet<string>(new[] { "ProBuilder", "ProBuilder Plus", "mesh", "modeling" })
            };

            return provider;
        }

        private static void OnDeactivate()
        {
            moreInfoButton = null;
        }
    }
}
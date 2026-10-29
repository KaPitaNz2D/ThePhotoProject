using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// Helper class for shared Overdrive UI functionality across different settings providers.
/// Provides common methods for loading tool lists and setting up web buttons.
/// </summary>
internal static class OverdriveUIHelper
{
    /// <summary>
    /// Loads and adds the ToolList UXML to the specified root element.
    /// </summary>
    /// <param name="rootElement">The root element to add the tool list to</param>
    /// <param name="errorPrefix">Prefix for error messages (e.g., "OverdriveSharedPreferencesProvider")</param>
    public static void AddToolList(VisualElement rootElement, string errorPrefix = "OverdriveUIHelper")
    {
        var toolList = Resources.Load<VisualTreeAsset>("UI/OverdriveShared_ToolList");
        if (toolList != null)
        {
            var toolListContainer = toolList.CloneTree();
            toolListContainer.style.flexGrow = 1;
            SetupToolListButtons(toolListContainer, errorPrefix);
            
            // Look for an element named "ToolList" to add the content to
            var toolListElement = rootElement.Q<VisualElement>("ToolList");
            if (toolListElement != null)
            {
                toolListElement.Add(toolListContainer);
            }
            else
            {
                // Fallback to adding to root element if "ToolList" element not found
                Debug.LogWarning($"{errorPrefix}: ToolList element not found, adding to root element instead");
                rootElement.Add(toolListContainer);
            }
        }
        else
        {
            Debug.LogError($"{errorPrefix}: Could not load OverdriveShared_ToolList.uxml");
            var toolListErrorLabel = new Label("OverdriveShared_ToolList.uxml not found");
            toolListErrorLabel.style.color = Color.red;
            rootElement.Add(toolListErrorLabel);
        }
    }

    /// <summary>
    /// Sets up common web buttons found in Overdrive UI containers.
    /// </summary>
    /// <param name="container">The container to search for buttons in</param>
    /// <param name="errorPrefix">Prefix for error messages</param>
    public static void SetupWebButtons(VisualElement container, string errorPrefix = "OverdriveUIHelper")
    {
        SetupWebButton(container, "Btn-Web-Pins", "https://www.overdrivetoolset.com/pins", "Pins", errorPrefix);
        SetupWebButton(container, "Btn-Web-SmartScenes", "https://www.overdrivetoolset.com/smart-scenes", "Smart Scenes", errorPrefix);
        SetupWebButton(container, "Btn-Web-PrefabEditor", "https://www.overdrivetoolset.com/prefab-editor", "Prefab Editor", errorPrefix);
        SetupWebButton(container, "Btn-Web-Collections", "https://www.overdrivetoolset.com/collections", "Collections", errorPrefix);
        SetupWebButton(container, "Btn-Web-Todo", "https://www.overdrivetoolset.com/todo", "Todo", errorPrefix);
        SetupWebButton(container, "Btn-Web-ProBuilderPlus", "https://www.overdrivetoolset.com/probuilder-plus", "ProBuilder Plus", errorPrefix);
        SetupWebButton(container, "Btn-Web-KeyControl", "https://www.overdrivetoolset.com/key-control", "Key Control", errorPrefix);
        SetupWebButton(container, "Btn-Web-ActionsPalette", "https://www.overdrivetoolset.com/actions-palette", "Actions Palette", errorPrefix);
    }

    /// <summary>
    /// Sets up buttons specifically for the ToolList container.
    /// </summary>
    /// <param name="container">The ToolList container</param>
    /// <param name="errorPrefix">Prefix for error messages</param>
    private static void SetupToolListButtons(VisualElement container, string errorPrefix)
    {
        // Add any ToolList-specific button setup here
        // For now, we'll use the same web buttons setup
        SetupWebButtons(container, errorPrefix);
    }

    /// <summary>
    /// Sets up a single web button with URL navigation.
    /// </summary>
    /// <param name="container">The container to search for the button in</param>
    /// <param name="buttonName">The name/ID of the button</param>
    /// <param name="url">The URL to open when clicked</param>
    /// <param name="displayName">The display name for error messages</param>
    /// <param name="errorPrefix">Prefix for error messages</param>
    private static void SetupWebButton(VisualElement container, string buttonName, string url, string displayName, string errorPrefix)
    {
        var button = container.Q<Button>(buttonName);
        if (button != null)
        {
            button.clicked += () =>
            {
                Application.OpenURL(url);
            };
        }
        else
        {
            Debug.LogError($"{errorPrefix}: {displayName} button not found in UI.");
        }
    }
}
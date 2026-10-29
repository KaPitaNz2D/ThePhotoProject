using System;
using UnityEditor;
using UnityEngine;

namespace Overdrive.Framework
{
    /// <summary>
    /// Manages user preferences for Overdrive Shared package.
    /// Handles visibility and width state for data panel containers.
    /// </summary>
    public static class OverdriveSharedPreferences
    {
        public static event Action<string, bool> onPreferenceChanged;

        // Visibility preference keys
        public const string OpaqueLeftVisiblePref = "OverdriveDataPanel_OpaqueLeft_Visible";
        public const string OpaqueRightVisiblePref = "OverdriveDataPanel_OpaqueRight_Visible";
        public const string TransparentLeftVisiblePref = "OverdriveDataPanel_TransparentLeft_Visible";
        public const string TransparentRightVisiblePref = "OverdriveDataPanel_TransparentRight_Visible";

        // Width preference keys
        public const string OpaqueLeftWidthPref = "OverdriveDataPanel_OpaqueLeft_Width";
        public const string OpaqueRightWidthPref = "OverdriveDataPanel_OpaqueRight_Width";
        public const string TransparentLeftWidthPref = "OverdriveDataPanel_TransparentLeft_Width";
        public const string TransparentRightWidthPref = "OverdriveDataPanel_TransparentRight_Width";

        // Get visibility state for container
        public static bool GetContainerVisible(DataPanelType type, DataPanelSide side)
        {
            string key = GetVisibleKey(type, side);
            return EditorPrefs.GetBool(key, false); // Default to hidden
        }

        // Check if user has explicitly set visibility preference
        public static bool HasVisibilityPreference(DataPanelType type, DataPanelSide side)
        {
            string key = GetVisibleKey(type, side);
            return EditorPrefs.HasKey(key);
        }

        // Set visibility state for container
        public static void SetContainerVisible(DataPanelType type, DataPanelSide side, bool visible)
        {
            string key = GetVisibleKey(type, side);
            if (EditorPrefs.GetBool(key, false) == visible)
                return;

            EditorPrefs.SetBool(key, visible);
            onPreferenceChanged?.Invoke(key, visible);
        }

        // Get saved width for container
        public static float GetContainerWidth(DataPanelType type, DataPanelSide side)
        {
            string key = GetWidthKey(type, side);
            return EditorPrefs.GetFloat(key, 120f); // Default width
        }

        // Set saved width for container
        public static void SetContainerWidth(DataPanelType type, DataPanelSide side, float width)
        {
            string key = GetWidthKey(type, side);
            if (Mathf.Approximately(EditorPrefs.GetFloat(key, 120f), width))
                return;

            EditorPrefs.SetFloat(key, width);
        }

        private static string GetVisibleKey(DataPanelType type, DataPanelSide side)
        {
            if (type == DataPanelType.Opaque && side == DataPanelSide.Left)
                return OpaqueLeftVisiblePref;
            if (type == DataPanelType.Opaque && side == DataPanelSide.Right)
                return OpaqueRightVisiblePref;
            if (type == DataPanelType.Transparent && side == DataPanelSide.Left)
                return TransparentLeftVisiblePref;
            return TransparentRightVisiblePref;
        }

        private static string GetWidthKey(DataPanelType type, DataPanelSide side)
        {
            if (type == DataPanelType.Opaque && side == DataPanelSide.Left)
                return OpaqueLeftWidthPref;
            if (type == DataPanelType.Opaque && side == DataPanelSide.Right)
                return OpaqueRightWidthPref;
            if (type == DataPanelType.Transparent && side == DataPanelSide.Left)
                return TransparentLeftWidthPref;
            return TransparentRightWidthPref;
        }
    }
}

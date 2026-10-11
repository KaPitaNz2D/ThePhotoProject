using UnityEditor;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Reads or Writes ProBuilderPlus Settings in/from Unity EditorPrefs. 
    /// </summary>
    public static partial class UserPreferences
    {
        private const string k_PreferencePrefix = "ProBuilderPlus.";

        public static class ActionsOverlay
        {
            // UI Preferences
            private const string k_ShowEditModeButtonsKey = k_PreferencePrefix + "ShowEditModeButtons";
            private const string k_ShowEditorButtonsKey = k_PreferencePrefix + "ShowEditorButtons";
            private const string k_ShowActionButtonsKey = k_PreferencePrefix + "ShowActionButtons";

            public static bool ShowEditModeButtons
            {
                get { return EditorPrefs.GetBool(k_ShowEditModeButtonsKey, defaultValue: true); }
                set { EditorPrefs.SetBool(k_ShowEditModeButtonsKey, value); }
            }

            public static bool ShowEditorButtons
            {
                get { return EditorPrefs.GetBool(k_ShowEditorButtonsKey, defaultValue: true); }
                set { EditorPrefs.SetBool(k_ShowEditorButtonsKey, value); }
            }

            public static bool ShowActionButtons
            {
                get { return EditorPrefs.GetBool(k_ShowActionButtonsKey, defaultValue: true); }
                set { EditorPrefs.SetBool(k_ShowActionButtonsKey, value); }
            }
        }

        public static class InfoOverlay
        {
            private const string k_ShowPanButtonsKey = k_PreferencePrefix + "ShowPanButtons";
            private const string k_ShowSelectionInfoKey = k_PreferencePrefix + "ShowSelectionInfo";
            private const string k_ShowDisabledUvSettingsKey = k_PreferencePrefix + "ShowDisabledUvSettings";
            private const string k_ShowSmoothnessGroupKey = k_PreferencePrefix + "ShowSmoothnessGroup";

            public static bool ShowPanUVButtons
            {
                get { return EditorPrefs.GetBool(k_ShowPanButtonsKey, defaultValue: true); }
                set { EditorPrefs.SetBool(k_ShowPanButtonsKey, value); }
            }

            public static bool ShowSelectionInfo
            {
                get { return EditorPrefs.GetBool(k_ShowSelectionInfoKey, defaultValue: true); }
                set { EditorPrefs.SetBool(k_ShowSelectionInfoKey, value); }
            }

            public static bool ShowDisabledUvSettings
            {
                get { return EditorPrefs.GetBool(k_ShowDisabledUvSettingsKey, defaultValue: false); } // On by default
                set { EditorPrefs.SetBool(k_ShowDisabledUvSettingsKey, value); }
            }

            public static bool ShowSmoothnessGroup
            {
                get { return EditorPrefs.GetBool(k_ShowSmoothnessGroupKey, defaultValue: true); } // On by default
                set { EditorPrefs.SetBool(k_ShowSmoothnessGroupKey, value); }
            }
        }
    }
}
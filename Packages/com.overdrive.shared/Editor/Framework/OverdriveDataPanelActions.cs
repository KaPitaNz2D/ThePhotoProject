using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Overdrive.Framework
{
    public static class OverdriveDataPanelActions
    {
        //[MenuItem("Tools/Overdrive Actions/Toggle Left Opaque Container")]
        [Shortcut("Overdrive/Toggle Left Opaque Container", typeof(SceneView), KeyCode.LeftBracket, ShortcutModifiers.Control)]
        public static void ToggleLeftOpaque()
        {
            bool current = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Opaque, DataPanelSide.Left);
            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Opaque, DataPanelSide.Left, !current);
            Debug.Log($"[OverdriveDataPanel] Opaque Left container {(!current ? "shown" : "hidden")}");
        }

        //[MenuItem("Tools/Overdrive Actions/Toggle Left Transparent Container")]
        [Shortcut("Overdrive/Toggle Left Transparent Container", typeof(SceneView), KeyCode.LeftBracket, ShortcutModifiers.Shift)]
        public static void ToggleLeftTransparent()
        {
            bool current = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Transparent, DataPanelSide.Left);
            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Transparent, DataPanelSide.Left, !current);
            Debug.Log($"[OverdriveDataPanel] Transparent Left container {(!current ? "shown" : "hidden")}");
        }

        //[MenuItem("Tools/Overdrive Actions/Toggle Right Opaque Container")]
        [Shortcut("Overdrive/Toggle Right Opaque Container", typeof(SceneView), KeyCode.RightBracket, ShortcutModifiers.Control)]
        public static void ToggleRightOpaque()
        {
            bool current = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Opaque, DataPanelSide.Right);
            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Opaque, DataPanelSide.Right, !current);
            Debug.Log($"[OverdriveDataPanel] Opaque Right container {(!current ? "shown" : "hidden")}");
        }

        //[MenuItem("Tools/Overdrive Actions/Toggle Right Transparent Container")]
        [Shortcut("Overdrive/Toggle Right Transparent Container", typeof(SceneView), KeyCode.RightBracket, ShortcutModifiers.Shift)]
        public static void ToggleRightTransparent()
        {
            bool current = OverdriveSharedPreferences.GetContainerVisible(DataPanelType.Transparent, DataPanelSide.Right);
            OverdriveSharedPreferences.SetContainerVisible(DataPanelType.Transparent, DataPanelSide.Right, !current);
            Debug.Log($"[OverdriveDataPanel] Transparent Right container {(!current ? "shown" : "hidden")}");
        }
    }
}

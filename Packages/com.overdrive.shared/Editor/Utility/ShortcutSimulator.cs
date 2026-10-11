using UnityEditor;
using UnityEngine;

namespace Overdrive
{
    /// <summary>
    /// Simulates a shortcut by temporarily rebinding it to a key and sending the key event.
    /// </summary>
    public static class ShortcutSimulator
    {
        public static void SimulateShortcut(string shortcutId)
        {
            var shortcutManager = UnityEditor.ShortcutManagement.ShortcutManager.instance;
            var originalBinding = shortcutManager.GetShortcutBinding(shortcutId);

            //Debug.Log($"(1) [ShortcutSimulator] Shortcut: {shortcutId} | Original Binding: {originalBinding}");

            var tempBinding = new UnityEditor.ShortcutManagement.ShortcutBinding(new UnityEditor.ShortcutManagement.KeyCombination(KeyCode.F12));
            shortcutManager.RebindShortcut(shortcutId, tempBinding);

            //Debug.Log($"(2) [ShortcutSimulator] Rebound to F12, sending key event...");
            SimulateKeyPress(KeyCode.F12);

            shortcutManager.RebindShortcut(shortcutId, originalBinding);
            //Debug.Log($"(4) [ShortcutSimulator] Restored original binding");
        }

        private static void SimulateKeyPress(KeyCode keyCode)
        {
            var keyEvent = new Event { type = EventType.KeyDown, keyCode = keyCode };

            // Try SceneView first (for ProBuilder shortcuts)
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                //Debug.Log($"(3) [ShortcutSimulator] Sending to SceneView: {sceneView.titleContent.text}");
                sceneView.SendEvent(keyEvent);
                keyEvent.type = EventType.KeyUp;
                sceneView.SendEvent(keyEvent);
                return;
            }

            // Fallback to focused window
            var focused = EditorWindow.focusedWindow;
            //Debug.Log($"(fallback 3) [ShortcutSimulator] Sending to focused window: {focused?.titleContent.text ?? "null"}");
            focused?.SendEvent(keyEvent);
            keyEvent.type = EventType.KeyUp;
            focused?.SendEvent(keyEvent);
        }
    }
}

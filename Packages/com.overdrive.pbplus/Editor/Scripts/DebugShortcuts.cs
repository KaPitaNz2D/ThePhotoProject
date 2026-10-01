using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Overdrive.ProBuilderPlus
{
    public static class DebugShortcuts
    {
        [MenuItem("Tools/Overdrive Actions/Debug/List All ProBuilder Shortcuts")]
        public static void ListProBuilderShortcuts()
        {
            var shortcutManager = UnityEditor.ShortcutManagement.ShortcutManager.instance;
            var allShortcuts = shortcutManager.GetAvailableShortcutIds();

            Debug.Log("=== All ProBuilder Shortcuts ===");
            foreach (var shortcutId in allShortcuts.Where(static s => s.Contains("ProBuilder") || s.Contains("Edit")))
            {
                var binding = shortcutManager.GetShortcutBinding(shortcutId);
                Debug.Log($"Shortcut ID: {shortcutId} | Binding: {binding}");
            }
            Debug.Log("=== End of ProBuilder Shortcuts ===");
        }
    }
}

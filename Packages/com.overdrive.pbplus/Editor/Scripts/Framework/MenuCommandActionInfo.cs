using UnityEditor;
using UnityEngine;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Type of action meant for buttons that call up menus.
    /// </summary>
    public sealed class MenuCommandActionInfo : IActionInfo
    {
        public string DisplayName { get; set; }

        public Texture2D Icon { get; set; }

        public string Id { get; set; }

        /// <summary>
        /// Optional function that allows to do an additional check to determine
        /// if an Action should be shown as 'enabled' on the menus.<br/>
        /// Leave it as null if you want <see cref="IsEnabledForCurretMode"/> to return true.
        /// </summary>
        public System.Func<bool> IsEnabledCheckFunction { get; set; }

        public bool IsEnabledForCurretMode => IsEnabledCheckFunction == null || IsEnabledCheckFunction.Invoke();

        /// <summary>
        /// The name of the menu item.<br/>
        /// Example: 'Tools/ProBuilder/Editors/Open Material Editor'.
        /// </summary>
        public string MenuCommand { get; set; }

        public int Order { get; set; }

        public bool SupportsInstantMode => false;

        public string Tooltip { get; set; }

        public void ExecuteAction()
        {
            EditorApplication.ExecuteMenuItem(this.MenuCommand);
        }

        public void ExecuteInstant()
        {
            ExecuteAction();
        }
    }
}

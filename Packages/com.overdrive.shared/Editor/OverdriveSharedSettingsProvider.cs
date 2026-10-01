using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

    public class OverdriveSharedSettingsProvider:SettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateOverdriveSharedSettingsProvider()
        {
            return new OverdriveSharedSettingsProvider("Project/Overdrive",SettingsScope.Project)
            {
                keywords = new HashSet<string>(new[] { "Overdrive","Scene","Discard","Child","Order","Blocks","Todo","Clear","EasyEdit" })
            };
        }

        private OverdriveSharedSettingsProvider(string path,SettingsScope scope) : base(path,scope) { }

        public override void OnActivate(string searchContext,VisualElement rootElement)
        {
            var visualTree = Resources.Load<VisualTreeAsset>("UI/OverdriveShared_ProjectSettings");

            var container = visualTree.CloneTree();

            // Note: ProjectSettings UXML doesn't contain web buttons, 
            // they're only in the ToolList which is added separately

            rootElement.Add(container);

            // Add ToolList at the end using helper
            OverdriveUIHelper.AddToolList(rootElement, "OverdriveSharedSettingsProvider");
        }
    }

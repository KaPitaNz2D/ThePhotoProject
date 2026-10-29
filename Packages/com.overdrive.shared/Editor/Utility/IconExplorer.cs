using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Overdrive.Framework
{
    public class IconExplorer : EditorWindow
    {
        private TextField searchField;
        private ListView resultsListView;
        private Label statusLabel;

        private List<IconEntry> allEditorIcons = new List<IconEntry>();
        private bool isLoadingIcons = false;

        [MenuItem("Tools/Icon Explorer")]
        public static void ShowWindow()
        {
            GetWindow<IconExplorer>("Icon Explorer");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingBottom = 10;

            // Search section
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.marginBottom = 10;

            var searchLabel = new Label("Search Icon:");
            searchLabel.style.width = 100;
            searchLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            searchField = new TextField();
            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(OnSearchChanged);

            searchContainer.Add(searchLabel);
            searchContainer.Add(searchField);

            rootVisualElement.Add(searchContainer);

            // Status label
            statusLabel = new Label("Loading all editor icons...");
            statusLabel.style.marginBottom = 10;
            rootVisualElement.Add(statusLabel);

            // Separator
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            separator.style.marginBottom = 10;
            rootVisualElement.Add(separator);

            // Results ListView
            resultsListView = new ListView();
            resultsListView.makeItem = CreateIconListItem;
            resultsListView.bindItem = (element, index) =>
            {
                var results = resultsListView.itemsSource as List<IconEntry>;
                if (results != null && index < results.Count)
                {
                    BindIconListItem(element, results[index]);
                }
            };
            resultsListView.selectionType = SelectionType.None;
            resultsListView.style.flexGrow = 1;

            rootVisualElement.Add(resultsListView);

            // Load all icons asynchronously
            EditorApplication.delayCall += LoadAllEditorIcons;
        }

        private void LoadAllEditorIcons()
        {
            if (isLoadingIcons) return;
            isLoadingIcons = true;

            allEditorIcons.Clear();

            // Get the editor asset bundle using reflection
            var editorAssetBundle = GetEditorAssetBundle();
            if (editorAssetBundle != null)
            {
                var allAssetNames = editorAssetBundle.GetAllAssetNames();

                var previousLogType = Application.GetStackTraceLogType(LogType.Warning);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

                // Use a HashSet to track unique icon names and avoid duplicates
                var seenIconNames = new HashSet<string>();
                int count = 0;

                foreach (var assetName in allAssetNames)
                {
                    // Only process texture assets
                    if (assetName.EndsWith(".png") || assetName.EndsWith(".asset"))
                    {
                        var iconName = System.IO.Path.GetFileNameWithoutExtension(assetName);

                        // Skip if we've already loaded this icon name
                        if (seenIconNames.Contains(iconName))
                            continue;

                        try
                        {
                            var texture = EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
                            if (texture != null)
                            {
                                allEditorIcons.Add(new IconEntry
                                {
                                    iconName = iconName,
                                    icon = texture
                                });
                                seenIconNames.Add(iconName);
                                count++;
                            }
                        }
                        catch
                        {
                            // Skip icons that fail to load
                        }
                    }
                }

                Application.SetStackTraceLogType(LogType.Warning, previousLogType);

                statusLabel.text = $"Loaded {count} editor icons. Type to search...";
            }
            else
            {
                statusLabel.text = "Could not access editor asset bundle. Showing limited icon set.";
                LoadKnownIcons();
            }

            resultsListView.itemsSource = allEditorIcons;
            resultsListView.Rebuild();

            isLoadingIcons = false;
        }

        private void LoadKnownIcons()
        {
            // Fallback: load known UI Toolkit icons
            var knownIconNames = new[]
            {
                "VisualElement", "Label", "Button", "Toggle Icon", "Text Field",
                "Image", "ScrollView", "ListView", "TreeView", "Foldout",
                "Slider", "IntegerField", "FloatField", "MultiColumnListView",
                "MultiColumnTreeView", "GroupBox", "IMGUIContainer", "TabView",
                "Tab", "ToggleButtonGroup", "Scroller", "SliderInt", "MinMaxSlider",
                "ProgressBar", "DropdownField", "EnumField", "RadioButton",
                "RadioButtonGroup", "LongField", "DoubleField", "Hash128Field",
                "Vector2Field", "Vector3Field", "Vector4Field", "RectField",
                "BoundsField", "UnsignedIntegerField", "UnsignedLongField",
                "Vector2IntField", "Vector3IntField", "RectIntField", "BoundsIntField",
                "ColorField", "CurveField", "GradientField", "TagField", "MaskField",
                "LayerField", "LayerMaskField", "EnumFlagsField", "Toolbar",
                "ToolbarMenu", "ToolbarButton", "ToolbarSpacer", "ToolbarToggle",
                "ToolbarBreadcrumbs", "ToolbarSearchField", "ToolbarPopupSearchField",
                "ObjectField", "PropertyField"
            };

            var previousLogType = Application.GetStackTraceLogType(LogType.Warning);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            foreach (var iconName in knownIconNames)
            {
                try
                {
                    var texture = EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
                    if (texture != null)
                    {
                        allEditorIcons.Add(new IconEntry
                        {
                            iconName = iconName,
                            icon = texture
                        });
                    }
                }
                catch
                {
                    // Skip
                }
            }

            Application.SetStackTraceLogType(LogType.Warning, previousLogType);
            statusLabel.text = $"Loaded {allEditorIcons.Count} known icons. Type to search...";
        }

        private AssetBundle GetEditorAssetBundle()
        {
            try
            {
                var method = typeof(EditorGUIUtility).GetMethod("GetEditorAssetBundle",
                    BindingFlags.NonPublic | BindingFlags.Static);
                return (AssetBundle)method?.Invoke(null, new object[] { });
            }
            catch
            {
                return null;
            }
        }

        private VisualElement CreateIconListItem()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.alignItems = Align.Center;

            var icon = new Image();
            icon.name = "Icon";
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 8;

            var iconNameLabel = new Label();
            iconNameLabel.name = "IconName";

            container.Add(icon);
            container.Add(iconNameLabel);

            return container;
        }

        private void BindIconListItem(VisualElement element, IconEntry entry)
        {
            var icon = element.Q<Image>("Icon");
            var iconNameLabel = element.Q<Label>("IconName");

            if (icon != null) icon.image = entry.icon;
            if (iconNameLabel != null) iconNameLabel.text = entry.iconName;
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            var searchText = evt.newValue;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                resultsListView.itemsSource = allEditorIcons;
                resultsListView.Rebuild();
                statusLabel.text = $"Showing all {allEditorIcons.Count} icons";
                return;
            }

            // Use fuzzy search to filter icons
            var results = allEditorIcons
                .Select(icon => new
                {
                    icon = icon,
                    score = FuzzySearchHelper.FuzzyMatchScore(searchText, icon.iconName)
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Select(x => x.icon)
                .ToList();

            resultsListView.itemsSource = results;
            resultsListView.Rebuild();
            statusLabel.text = $"Found {results.Count} matching icons";
        }

        private class IconEntry
        {
            public string iconName;
            public Texture2D icon;
        }
    }
}

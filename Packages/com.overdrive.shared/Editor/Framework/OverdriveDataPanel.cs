using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Overdrive.Framework
{
    /// <summary>
    /// Defines the visual style and positioning behavior of data panel containers.
    /// </summary>
    public enum DataPanelType
    {
        /// <summary>
        /// Full opacity panel that displaces overlays by pushing them inward.
        /// Added to unity-overlay-canvas with relative positioning.
        /// </summary>
        Opaque,

        /// <summary>
        /// Semi-transparent panel that overlays on top of scene content.
        /// Added to overlay-scene-containers with absolute positioning.
        /// </summary>
        Transparent
    }

    /// <summary>
    /// Defines which side of the SceneView the container appears on.
    /// </summary>
    public enum DataPanelSide
    {
        Left,
        Right
    }

    /// <summary>
    /// Delegate for creating panel content. Similar to Unity's Overlay.CreatePanelContent pattern.
    /// </summary>
    public delegate VisualElement ContentFactory();

    /// <summary>
    /// Delegate that returns the current type and side for a registered content.
    /// </summary>
    public delegate (DataPanelType type, DataPanelSide side) LocationProvider();

    /// <summary>
    /// Delegate for receiving opacity updates (for IMGUI content that doesn't respect UIToolkit opacity).
    /// </summary>
    public delegate void OpacityChangedCallback(float opacity);

    /// <summary>
    /// Manages persistent data panel containers in SceneView.
    /// Uses a factory-based registration system where clients provide a CreateContent callback,
    /// and the system handles all lifecycle management automatically.
    /// </summary>
    [InitializeOnLoad]
    public static class OverdriveDataPanel
    {
        private class ContainerInfo
        {
            public VisualElement contentRoot;
            public Dictionary<string, VisualElement> contentItems = new Dictionary<string, VisualElement>();
        }

        private class ContentRegistration
        {
            public string id;
            public ContentFactory factory;
            public LocationProvider locationProvider;
            public float? width;
            public OpacityChangedCallback opacityCallback;
        }

        private static Dictionary<(DataPanelType, DataPanelSide), ContainerInfo> containers = new Dictionary<(DataPanelType, DataPanelSide), ContainerInfo>();
        private static Dictionary<string, ContentRegistration> registrations = new Dictionary<string, ContentRegistration>();
        private static SceneView currentSceneView = null;

        static OverdriveDataPanel()
        {
            EditorApplication.delayCall += OnDomainReloadComplete;
            EditorApplication.update += OnEditorUpdate;
            OverdriveSharedPreferences.onPreferenceChanged += OnPreferenceChanged;
        }

        public static void NotifyContentLocationChanged(string id, DataPanelType oldType, DataPanelSide oldSide)
        {
            if (!registrations.ContainsKey(id))
            {
                Debug.LogWarning($"[OverdriveDataPanel] No registration found for ID: {id}");
                return;
            }

            var registration = registrations[id];
            var (newType, newSide) = registration.locationProvider();

            if (oldType != newType || oldSide != newSide)
            {
                MoveContent(id, oldType, oldSide, newType, newSide);
            }
        }

        private static void MoveContent(string id, DataPanelType oldType, DataPanelSide oldSide, DataPanelType newType, DataPanelSide newSide)
        {
            //Debug.Log($"[OverdriveDataPanel] Moving content {id} from {oldType} {oldSide} to {newType} {newSide}");

            var oldKey = (oldType, oldSide);
            var newKey = (newType, newSide);

            // Remove from old container
            if (containers.ContainsKey(oldKey))
            {
                var oldContainer = containers[oldKey];
                if (oldContainer.contentItems.ContainsKey(id))
                {
                    var content = oldContainer.contentItems[id];
                    oldContainer.contentRoot.Remove(content);
                    oldContainer.contentItems.Remove(id);
                }
            }

            // Create at new location
            if (containers.ContainsKey(newKey))
            {
                CreateRegisteredContent(id);
            }
        }

        private static void OnDomainReloadComplete()
        {
            EditorApplication.delayCall -= OnDomainReloadComplete;
            //Debug.Log("[OverdriveDataPanel] Domain reload complete - starting container recreation");

            // Clear all existing containers
            ClearAllContainers();

            // Create containers for current active SceneView
            if (SceneView.lastActiveSceneView != null)
            {
                currentSceneView = SceneView.lastActiveSceneView;
                CreateContainersForSceneView(currentSceneView);

                // Automatically recreate content from all registrations
                RecreateAllRegisteredContent();
            }
        }

        private static void OnEditorUpdate()
        {
            // Check if the active SceneView has changed
            var activeSceneView = SceneView.lastActiveSceneView;
            
            // Only switch if we actually have a different SceneView
            if (activeSceneView != null && currentSceneView != activeSceneView)
            {
                SwitchToSceneView(activeSceneView);
            }
            // Handle case where current SceneView was destroyed
            else if (currentSceneView != null && activeSceneView == null)
            {
                ClearAllContainers();
            }
        }

        private static void ClearAllContainers()
        {
            //Debug.Log($"[OverdriveDataPanel] Clearing {containers.Count} containers");
            // Clear all container references and remove from UI
            foreach (var kvp in containers)
            {
                if (kvp.Value.contentRoot != null && kvp.Value.contentRoot.parent != null)
                {
                    //Debug.Log($"[OverdriveDataPanel] Clearing container {kvp.Key} with {kvp.Value.contentRoot.childCount} children");
                    kvp.Value.contentRoot.Clear();
                    kvp.Value.contentRoot.parent.Remove(kvp.Value.contentRoot);
                }
            }
            
            containers.Clear();
            currentSceneView = null;
            //Debug.Log("[OverdriveDataPanel] All containers cleared");
        }

        private static void SwitchToSceneView(SceneView sceneView)
        {
            if (sceneView == null) return;

            var oldId = currentSceneView?.GetInstanceID().ToString() ?? "null";
            var newId = sceneView.GetInstanceID().ToString();
            //Debug.Log($"[OverdriveDataPanel] Switching from SceneView {oldId} to {newId}");

            // Clear existing containers
            ClearAllContainers();

            // Set new active SceneView and create containers
            currentSceneView = sceneView;
            CreateContainersForSceneView(sceneView);

            // Automatically recreate content from all registrations
            RecreateAllRegisteredContent();
        }

        private static void CreateContainersForSceneView(SceneView sceneView)
        {
            //Debug.Log("[OverdriveDataPanel] Creating containers for active SceneView");
            // Create all four containers: Opaque Left/Right, Transparent Left/Right
            CreateContainer(sceneView, DataPanelType.Opaque, DataPanelSide.Left);
            CreateContainer(sceneView, DataPanelType.Opaque, DataPanelSide.Right);
            CreateContainer(sceneView, DataPanelType.Transparent, DataPanelSide.Left);
            CreateContainer(sceneView, DataPanelType.Transparent, DataPanelSide.Right);
        }

        private static void CreateContainer(SceneView sceneView, DataPanelType type, DataPanelSide side)
        {
            var key = (type, side);

            // Create container programmatically
            var contentRoot = new VisualElement();
            contentRoot.name = $"overdrive-datapanel-{type.ToString().ToLower()}-{side.ToString().ToLower()}";
            contentRoot.style.height = Length.Percent(100);
            contentRoot.style.flexDirection = FlexDirection.Column;
            contentRoot.style.position = Position.Relative;
            contentRoot.style.flexGrow = 0;
            contentRoot.style.flexShrink = 0;

            // Respect saved visibility and width preferences
            float savedWidth = OverdriveSharedPreferences.GetContainerWidth(type, side);
            if (OverdriveSharedPreferences.HasVisibilityPreference(type, side))
            {
                bool shouldBeVisible = OverdriveSharedPreferences.GetContainerVisible(type, side);
                contentRoot.style.display = shouldBeVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (shouldBeVisible)
                {
                    contentRoot.style.width = savedWidth;
                }
            }
            else
            {
                contentRoot.style.display = DisplayStyle.None;
            }

            VisualElement parentElement = null;

            if (type == DataPanelType.Opaque)
            {
                // Navigate to unity-overlay-canvas
                parentElement = sceneView.rootVisualElement;
                while (parentElement.parent != null && !parentElement.name.StartsWith("unity-overlay-canvas"))
                {
                    parentElement = parentElement.parent;
                }

                // Set flex-direction to row
                parentElement.style.flexDirection = FlexDirection.Row;

                // Insert based on side
                if (side == DataPanelSide.Left)
                {
                    parentElement.Insert(0, contentRoot);
                }
                else
                {
                    parentElement.Add(contentRoot);
                }
            }
            else // Transparent
            {
                // Navigate to overlay-scene-containers
                parentElement = sceneView.rootVisualElement.parent;
                if (parentElement == null)
                {
                    Debug.LogWarning("[OverdriveDataPanel] Parent element not found for transparent container");
                    return;
                }

                // Set flex direction to row for proper left/right layout
                parentElement.style.flexDirection = FlexDirection.Row;

                // Find overlay-window-root to insert relative to it
                var overlayWindowRoot = parentElement.Q("overlay-window-root");

                if (side == DataPanelSide.Left)
                {
                    // Insert after overlay-window-root so it's not hidden by it
                    if (overlayWindowRoot != null)
                    {
                        int windowRootIndex = parentElement.IndexOf(overlayWindowRoot);
                        parentElement.Insert(windowRootIndex + 1, contentRoot);
                    }
                    else
                    {
                        parentElement.Insert(0, contentRoot);
                    }
                }
                else
                {
                    // Right side - add at end
                    parentElement.Add(contentRoot);
                }
            }

            // Store container info
            containers[key] = new ContainerInfo
            {
                contentRoot = contentRoot
            };

            //Debug.Log($"[OverdriveDataPanel] Created {type} {side} container for SceneView {sceneView.GetInstanceID()}");
        }

        /// <summary>
        /// Registers a content factory for automatic lifecycle management.
        /// The factory will be called automatically when containers are created or recreated.
        /// The locationProvider delegate is called each time to get the current type and side.
        /// </summary>
        /// <param name="id">Unique identifier for this content registration</param>
        /// <param name="factory">Function that creates and returns the VisualElement content</param>
        /// <param name="locationProvider">Function that returns current (type, side) location</param>
        /// <param name="width">Width in pixels (default: uses saved preference width)</param>
        /// <param name="opacityCallback">Optional callback for receiving opacity updates (for IMGUI content)</param>
        public static void RegisterContent(string id, ContentFactory factory, LocationProvider locationProvider, float? width = null, OpacityChangedCallback opacityCallback = null)
        {
            registrations[id] = new ContentRegistration
            {
                id = id,
                factory = factory,
                locationProvider = locationProvider,
                width = width,
                opacityCallback = opacityCallback
            };

            //Debug.Log($"[OverdriveDataPanel] Registered content factory: {id}");

            // Create content immediately at current location
            var (type, side) = locationProvider();
            if (containers.ContainsKey((type, side)))
            {
                bool isVisible = OverdriveSharedPreferences.GetContainerVisible(type, side);
                if (isVisible || !OverdriveSharedPreferences.HasVisibilityPreference(type, side))
                {
                    CreateRegisteredContent(id);
                }
            }
        }

        /// <summary>
        /// Unregisters a content factory by ID. Content will be removed from the container.
        /// </summary>
        public static void UnregisterContent(string id)
        {
            if (!registrations.ContainsKey(id))
            {
                Debug.LogWarning($"[OverdriveDataPanel] No registration found for ID: {id}");
                return;
            }

            var registration = registrations[id];
            var (type, side) = registration.locationProvider();
            var key = (type, side);

            registrations.Remove(id);
            //Debug.Log($"[OverdriveDataPanel] Unregistered content factory: {id}");

            // Clear the content from container
            if (containers.ContainsKey(key))
            {
                var container = containers[key];
                if (container.contentItems.ContainsKey(id))
                {
                    var content = container.contentItems[id];
                    container.contentRoot.Remove(content);
                    container.contentItems.Remove(id);
                }
            }
        }

        private static void RecreateAllRegisteredContent()
        {
            //Debug.Log($"[OverdriveDataPanel] Recreating content for {registrations.Count} registrations");

            foreach (var kvp in registrations)
            {
                var id = kvp.Key;
                var registration = kvp.Value;
                var (type, side) = registration.locationProvider();

                // Only create content if container should be visible
                bool isVisible = OverdriveSharedPreferences.GetContainerVisible(type, side);
                if (isVisible || !OverdriveSharedPreferences.HasVisibilityPreference(type, side))
                {
                    CreateRegisteredContent(id);
                }
            }
        }

        private static void CreateRegisteredContent(string id)
        {
            if (!registrations.ContainsKey(id))
            {
                Debug.LogWarning($"[OverdriveDataPanel] No registration found for ID: {id}");
                return;
            }

            var registration = registrations[id];
            var (type, side) = registration.locationProvider();
            var key = (type, side);

            if (!containers.ContainsKey(key))
            {
                Debug.LogWarning($"[OverdriveDataPanel] Container not found for {type} {side}");
                return;
            }

            var container = containers[key];

            // Remove existing content for this ID if any
            if (container.contentItems.ContainsKey(id))
            {
                var oldContent = container.contentItems[id];
                container.contentRoot.Remove(oldContent);
                container.contentItems.Remove(id);
            }

            // Call factory to create new content
            try
            {
                VisualElement content = registration.factory();
                if (content != null)
                {
                    // Style TemplateContainer for equal vertical distribution
                    content.style.flexGrow = 1;
                    content.style.flexShrink = 1;
                    content.style.flexBasis = 0;
                    content.style.paddingTop = 5;
                    content.style.paddingBottom = 5;
                    content.style.paddingLeft = 5;
                    content.style.paddingRight = 5;

                    // only Transparent containers need color background added
                    if (type == DataPanelType.Transparent)
                    {
                        content.style.backgroundColor = new Color(0, 0, 0, 0.7f);
                    }

                    // Hover opacity behavior (only for Transparent containers)
                    if (type == DataPanelType.Transparent)
                    {
                        content.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
                        content.style.transitionDuration = new List<TimeValue> { new TimeValue(0.2f, TimeUnit.Second) };
                        content.style.opacity = 0.3f;

                        // Notify callback of initial opacity
                        registration.opacityCallback?.Invoke(0.3f);

                        content.RegisterCallback<MouseEnterEvent>(evt => {
                            var element = evt.currentTarget as VisualElement;
                            if (element != null)
                            {
                                element.style.opacity = 1.0f;
                                registration.opacityCallback?.Invoke(1.0f);
                            }
                        });

                        content.RegisterCallback<MouseLeaveEvent>(evt => {
                            var element = evt.currentTarget as VisualElement;
                            if (element != null)
                            {
                                EditorApplication.delayCall += () => {
                                    if (element != null)
                                    {
                                        element.style.opacity = 0.3f;
                                        registration.opacityCallback?.Invoke(0.3f);
                                    }
                                };
                            }
                        });
                    }
                    else // Opaque - always 100% opacity
                    {
                        content.style.opacity = 1.0f;
                        registration.opacityCallback?.Invoke(1.0f);
                    }

                    container.contentRoot.Add(content);
                    container.contentItems[id] = content;

                    // Set width
                    float targetWidth = registration.width ?? OverdriveSharedPreferences.GetContainerWidth(type, side);
                    OverdriveSharedPreferences.SetContainerWidth(type, side, targetWidth);

                    // Show container if first time or user preference says visible
                    if (!OverdriveSharedPreferences.HasVisibilityPreference(type, side))
                    {
                        OverdriveSharedPreferences.SetContainerVisible(type, side, true);
                        container.contentRoot.style.display = DisplayStyle.Flex;
                        container.contentRoot.style.width = targetWidth;
                    }
                    else
                    {
                        bool isVisible = OverdriveSharedPreferences.GetContainerVisible(type, side);
                        if (isVisible)
                        {
                            container.contentRoot.style.display = DisplayStyle.Flex;
                            container.contentRoot.style.width = targetWidth;
                        }
                    }

                    //Debug.Log($"[OverdriveDataPanel] Created content for registered factory: {id} at {type} {side}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[OverdriveDataPanel] Error creating content for {id}: {ex}");
            }
        }

        private static void OnPreferenceChanged(string key, bool value)
        {
            // Determine which container changed based on the preference key
            DataPanelType? type = null;
            DataPanelSide? side = null;

            if (key == OverdriveSharedPreferences.OpaqueLeftVisiblePref)
            {
                type = DataPanelType.Opaque;
                side = DataPanelSide.Left;
            }
            else if (key == OverdriveSharedPreferences.OpaqueRightVisiblePref)
            {
                type = DataPanelType.Opaque;
                side = DataPanelSide.Right;
            }
            else if (key == OverdriveSharedPreferences.TransparentLeftVisiblePref)
            {
                type = DataPanelType.Transparent;
                side = DataPanelSide.Left;
            }
            else if (key == OverdriveSharedPreferences.TransparentRightVisiblePref)
            {
                type = DataPanelType.Transparent;
                side = DataPanelSide.Right;
            }

            if (!type.HasValue || !side.HasValue)
                return;

            var containerKey = (type.Value, side.Value);
            if (!containers.ContainsKey(containerKey))
                return;

            var info = containers[containerKey];

            if (value)
            {
                // Show container
                float savedWidth = OverdriveSharedPreferences.GetContainerWidth(type.Value, side.Value);
                info.contentRoot.style.display = DisplayStyle.Flex;
                info.contentRoot.style.width = savedWidth;

                // Create registered content for all registrations at this location
                foreach (var kvp in registrations)
                {
                    var registration = kvp.Value;
                    var (regType, regSide) = registration.locationProvider();
                    if (regType == type.Value && regSide == side.Value)
                    {
                        CreateRegisteredContent(kvp.Key);
                    }
                }
            }
            else
            {
                // Hide container and clear all content
                info.contentRoot.style.display = DisplayStyle.None;
                info.contentRoot.Clear();
                info.contentItems.Clear();
                //Debug.Log($"[OverdriveDataPanel] Closed {type.Value} {side.Value} container");
            }
        }
    }
}

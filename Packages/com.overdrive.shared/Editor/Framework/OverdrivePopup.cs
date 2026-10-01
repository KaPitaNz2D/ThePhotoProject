using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Overdrive.Framework
{
    /// <summary>
    /// Delegate for creating popup content. Called when popup is shown.
    /// </summary>
    public delegate VisualElement PopupContentFactory();

    /// <summary>
    /// Delegate for getting focus target after content creation. Called when popup is shown.
    /// </summary>
    public delegate VisualElement FocusTargetCallback();

    /// <summary>
    /// Delegate for determining if a popup should close. Called when auto-close events occur.
    /// </summary>
    public delegate bool ShouldCloseCallback();

    /// <summary>
    /// Framework for displaying UITK popups at mouse position in SceneView.
    /// Uses a registration system where clients provide a content factory,
    /// and the framework handles all lifecycle management automatically.
    /// </summary>
    [InitializeOnLoad]
    public static class OverdrivePopup
    {
        private class PopupInstance
        {
            public VisualElement popupElement;
            public SceneView targetSceneView;
            public bool isActive;
            public System.Action onClosed;
            public bool useManualClose;
            public ShouldCloseCallback shouldCloseCallback;
        }

        private class PopupRegistration
        {
            public string id;
            public PopupContentFactory factory;
            public FocusTargetCallback focusTargetCallback;
        }

        private static Dictionary<string, PopupRegistration> registrations = new Dictionary<string, PopupRegistration>();
        private static Dictionary<string, PopupInstance> activePopups = new Dictionary<string, PopupInstance>();
        private static SceneView currentSceneView = null;

        static OverdrivePopup()
        {
            EditorApplication.delayCall += OnDomainReloadComplete;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnDomainReloadComplete()
        {
            EditorApplication.delayCall -= OnDomainReloadComplete;
            //Debug.Log("[OverdrivePopup] Domain reload complete - clearing active popups");

            activePopups.Clear();

            if (SceneView.lastActiveSceneView != null)
            {
                currentSceneView = SceneView.lastActiveSceneView;
            }
        }

        private static void OnEditorUpdate()
        {
            var activeSceneView = SceneView.lastActiveSceneView;

            if (activeSceneView != null && currentSceneView != activeSceneView)
            {
                SwitchToSceneView(activeSceneView);
            }
            else if (currentSceneView != null && activeSceneView == null)
            {
                CloseAllPopups();
            }
        }

        private static void SwitchToSceneView(SceneView sceneView)
        {
            if (sceneView == null) return;

            //Debug.Log($"[OverdrivePopup] Switching SceneView - closing all active popups");
            CloseAllPopups();
            currentSceneView = sceneView;
        }

        private static void CloseAllPopups()
        {
            var popupIds = new List<string>(activePopups.Keys);
            foreach (var id in popupIds)
            {
                Close(id);
            }
        }

        /// <summary>
        /// Registers a popup content factory for automatic lifecycle management.
        /// </summary>
        /// <param name="id">Unique identifier for this popup registration</param>
        /// <param name="factory">Function that creates and returns the VisualElement content</param>
        /// <param name="focusTargetCallback">Optional function that returns the element to focus after positioning</param>
        public static void RegisterContent(string id, PopupContentFactory factory, FocusTargetCallback focusTargetCallback = null)
        {
            registrations[id] = new PopupRegistration
            {
                id = id,
                factory = factory,
                focusTargetCallback = focusTargetCallback
            };

            //Debug.Log($"[OverdrivePopup] Registered content factory: {id}");
        }

        /// <summary>
        /// Unregisters a popup content factory by ID. If popup is active, it will be closed.
        /// </summary>
        public static void UnregisterContent(string id)
        {
            if (!registrations.ContainsKey(id))
            {
                Debug.LogWarning($"[OverdrivePopup] No registration found for ID: {id}");
                return;
            }

            registrations.Remove(id);
            //Debug.Log($"[OverdrivePopup] Unregistered content factory: {id}");

            if (activePopups.ContainsKey(id))
            {
                Close(id);
            }
        }

        /// <summary>
        /// Shows a registered popup at the specified mouse position.
        /// </summary>
        /// <param name="id">The registered popup ID to show</param>
        /// <param name="mousePosition">Optional mouse position override. If null, uses Event.current.mousePosition</param>
        /// <param name="manualClose">If true, shows close button and disables auto-close. Default is false.</param>
        /// <param name="onClosed">Optional callback when the popup is closed</param>
        /// <param name="shouldCloseCallback">Optional callback to determine if popup should close on auto-close events</param>
        /// <returns>True if popup was shown successfully</returns>
        public static bool Show(string id, Vector2? mousePosition = null, bool manualClose = false, System.Action onClosed = null, ShouldCloseCallback shouldCloseCallback = null)
        {
            if (!registrations.ContainsKey(id))
            {
                Debug.LogWarning($"[OverdrivePopup] No registration found for ID: {id}");
                return false;
            }

            if (activePopups.ContainsKey(id))
            {
                Debug.LogWarning($"[OverdrivePopup] Popup {id} is already active");
                return false;
            }

            var registration = registrations[id];
            var targetSceneView = SceneView.lastActiveSceneView;

            if (targetSceneView == null)
            {
                Debug.LogWarning("[OverdrivePopup] No active SceneView found");
                return false;
            }

            var containerTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.overdrive.shared/Editor/Resources/UI/OverdrivePopup_Container.uxml");

            if (containerTemplate == null)
            {
                Debug.LogError("[OverdrivePopup] Failed to load OverdrivePopup_Container.uxml");
                return false;
            }

            var container = containerTemplate.Instantiate();
            container.name = $"overdrive-popup-container-{id}";
            var popupRoot = container.Q("popup-root");

            container.style.position = Position.Absolute;
            container.style.visibility = Visibility.Hidden;

            VisualElement content = null;
            try
            {
                content = registration.factory();
                if (content == null)
                {
                    Debug.LogError($"[OverdrivePopup] Factory for {id} returned null content");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[OverdrivePopup] Error creating content for {id}: {ex}");
                return false;
            }

            popupRoot.Add(content);

            var parentElement = targetSceneView.rootVisualElement;
            while (parentElement.parent != null && !parentElement.name.StartsWith("unity-overlay-canvas"))
            {
                parentElement = parentElement.parent;
            }

            parentElement.Add(container);

            // Add close button if manual close mode
            if (manualClose)
            {
                var closeButton = new Button(() => Close(id));
                closeButton.text = "X";
                closeButton.AddToClassList("popup-close-button");
                container.Insert(0, closeButton);
            }

            var instance = new PopupInstance
            {
                popupElement = container,
                targetSceneView = targetSceneView,
                isActive = true,
                onClosed = onClosed,
                useManualClose = manualClose,
                shouldCloseCallback = shouldCloseCallback
            };

            activePopups[id] = instance;

            Vector2 desiredPosition = mousePosition ?? (Event.current != null ? Event.current.mousePosition : Vector2.zero);
            desiredPosition -= new Vector2(3, 22);

            EditorApplication.delayCall += () =>
            {
                if (!instance.isActive || container == null) return;

                PositionWithinContainer(container, parentElement, desiredPosition);

                // Get focus target from callback
                VisualElement targetToFocus = null;
                if (registration.focusTargetCallback != null)
                {
                    try
                    {
                        targetToFocus = registration.focusTargetCallback();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[OverdrivePopup] Focus target callback failed for {id}: {ex}");
                    }
                }

                if (targetToFocus != null)
                {
                    targetToFocus.focusable = true;
                    container.schedule.Execute(() =>
                    {
                        if (instance.isActive && targetToFocus != null)
                        {
                            targetToFocus.Focus();
                        }
                    });
                }
            };

            if (!manualClose)
            {
                SetupCloseHandlers(id, instance);
            }

            //Debug.Log($"[OverdrivePopup] Showed popup: {id}");
            return true;
        }

        private static void SetupCloseHandlers(string id, PopupInstance instance)
        {
            if (instance.popupElement == null) return;

            // Only use MouseDownEvent to close popup, not FocusOutEvent
            // This prevents right-clicks and other interactions from closing the popup
            instance.targetSceneView.rootVisualElement.RegisterCallback<MouseDownEvent>(evt =>
            {
                OnRootMouseDown(id, instance, evt);
            }, TrickleDown.TrickleDown);
        }

        private static void OnRootMouseDown(string id, PopupInstance instance, MouseDownEvent evt)
        {
            if (instance.popupElement == null || !instance.isActive) return;

            // Only close on left mouse button clicks outside the popup
            if (evt.button != 0) return;

            if (!instance.popupElement.worldBound.Contains(evt.mousePosition))
            {
                if (instance.shouldCloseCallback == null || instance.shouldCloseCallback())
                {
                    Close(id);
                }
            }
        }

        private static void PositionWithinContainer(VisualElement popup, VisualElement container, Vector2 desiredPosition)
        {
            var popupRect = popup.layout;
            var containerRect = container.layout;

            float x = desiredPosition.x;
            float y = desiredPosition.y;

            if (x + popupRect.width > containerRect.width)
            {
                x = containerRect.width - popupRect.width;
            }

            if (y + popupRect.height > containerRect.height)
            {
                y = containerRect.height - popupRect.height;
            }

            x = Mathf.Max(0, x);
            y = Mathf.Max(0, y);

            popup.style.left = x;
            popup.style.top = y;
            popup.style.visibility = Visibility.Visible;
        }

        /// <summary>
        /// Closes a specific popup by ID.
        /// </summary>
        public static void Close(string id)
        {
            if (!activePopups.ContainsKey(id))
            {
                return;
            }

            var instance = activePopups[id];

            if (!instance.isActive || instance.popupElement == null)
            {
                activePopups.Remove(id);
                return;
            }

            instance.isActive = false;

            if (instance.popupElement.parent != null)
            {
                instance.popupElement.parent.Remove(instance.popupElement);
            }

            activePopups.Remove(id);

            instance.onClosed?.Invoke();

            //Debug.Log($"[OverdrivePopup] Closed popup: {id}");
        }

        /// <summary>
        /// Gets the root element of an active popup by ID.
        /// </summary>
        public static VisualElement GetRootElement(string id)
        {
            if (activePopups.ContainsKey(id))
            {
                return activePopups[id].popupElement;
            }
            return null;
        }

        /// <summary>
        /// Checks if a popup is currently active.
        /// </summary>
        public static bool IsActive(string id)
        {
            return activePopups.ContainsKey(id) && activePopups[id].isActive;
        }
    }
}

using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Core framework that manages preview actions - handles lifecycle, overlay management, and event coordination.
    /// </summary>
    public static class PreviewActionFramework
    {
        private static PreviewMenuAction _currentAction;
        private static PreviewActionOverlay s_CurrentOverlay;
        private static bool s_IsActive;
        private static bool s_SelectionUpdateDisabled;

        static PreviewActionFramework()
        {
            // Todo: CortiWins: Rewrite to use ProBuilderCore for state based events.

            // Subscribe to selection change events for automatic preview updates
            Selection.selectionChanged += OnSelectionChanged;
            ProBuilderEditor.selectionUpdated += OnProBuilderSelectionUpdated;

            // Subscribe to events that should auto-cancel (user must explicitly confirm)
            ToolManager.activeToolChanged += OnToolChanged;
            ToolManager.activeContextChanged += OnContextChanged;
            ProBuilderEditor.selectModeChanged += OnSelectModeChanged;

            // Subscribe to SceneView events for ESC key handling
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Check if any preview action is currently active
        /// </summary>
        public static bool IsAnyActionActive => s_IsActive;

        /// <summary>
        /// Called when user clicks Cancel button
        /// </summary>
        public static void CancelAction()
        {
            EndCurrentPreview(false); // Just cleanup, no application
        }

        /// <summary>
        /// Called when user clicks Confirm button
        /// </summary>
        public static void ConfirmAction()
        {
            if (_currentAction != null)
            {
                try
                {
                    // Todo: CortiWins : Log this. UnityEngine.ProBuilder.ActionResult
                    var result = _currentAction.ApplyChanges();
                    if (result.status == ActionResult.Status.Failure)
                    {
                        Debug.LogErrorFormat("Action '{0}' ApplyChanges execute Instant with 'Status.Failure' Message: {1}", _currentAction.GetName(), result.notification);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error applying changes: {ex.Message}");
                }
            }

            EndCurrentPreview(false); // Don't apply again
        }

        /// <summary>
        /// Ends the current preview session
        /// </summary>
        /// <param name="apply">Whether to apply changes before ending (default: false - cancel)</param>
        public static void EndCurrentPreview(bool apply = false)
        {
            if (!s_IsActive) return;

            try
            {
                if (apply && _currentAction != null)
                {
                    // Todo: CortiWins : Log this. UnityEngine.ProBuilder.ActionResult
                    var result = _currentAction.ApplyChanges();
                    if (result.status == ActionResult.Status.Failure)
                    {
                        Debug.LogErrorFormat("Action '{0}' ApplyChanges execute Instant with 'Status.Failure' Message: {1}", _currentAction.GetName(), result.notification);
                    }
                }

                // Cleanup action
                _currentAction?.CleanupPreview();

                // Cleanup overlay
                if (s_CurrentOverlay != null)
                {
                    SceneView.RemoveOverlayFromActiveView(s_CurrentOverlay);
                    s_CurrentOverlay.displayed = false;
                    s_CurrentOverlay = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during preview cleanup: {ex.Message}");
            }
            finally
            {
                _currentAction = null;
                s_IsActive = false;
                s_SelectionUpdateDisabled = false;
            }
        }

        /// <summary>
        /// Gets the instructions text for the current action
        /// </summary>
        public static string GetCurrentActionInstructions()
        {
            return _currentAction?.Instructions ?? "Configure settings and click Apply to confirm.";
        }

        /// <summary>
        /// Gets the display name for the current action
        /// </summary>
        public static string GetCurrentActionName()
        {
            return _currentAction?.MenuTitle ?? "Preview Action";
        }

        /// <summary>
        /// Gets the settings UI from the current action
        /// </summary>
        public static VisualElement GetCurrentActionSettings()
        {
            return _currentAction?.CreateSettingsContent() ?? new Label("No active preview");
        }

        /// <summary>
        /// Main entry point - called by PreviewMenuAction.PerformActionImplementation()
        /// </summary>
        public static void HandleAction(PreviewMenuAction action)
        {
            try
            {
                // If we have a different action active, end it first
                if (_currentAction != null && _currentAction != action)
                {
                    EndCurrentPreview(false); // Don't apply the previous action
                }

                if (!s_IsActive)
                {
                    // Starting new preview
                    StartPreview(action);
                }
                else if (_currentAction == action)
                {
                    // Updating existing preview (parameter changed)
                    _currentAction.UpdatePreview();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Preview framework error: {ex.Message}");
                EndCurrentPreview(false);
            }
        }

        /// <summary>
        /// Check if a specific action is currently active
        /// </summary>
        public static bool IsActionActive(PreviewMenuAction action)
        {
            return s_IsActive && _currentAction == action;
        }

        /// <summary>
        /// Requests a preview update (called when settings change)
        /// </summary>
        public static void RequestPreviewUpdate()
        {
            if (s_IsActive && _currentAction != null)
            {
                _currentAction.UpdatePreview();
            }
        }

        private static void OnContextChanged()
        {
            // Auto-cancel when context changes (user must explicitly confirm)
            if (s_IsActive)
            {
                EndCurrentPreview(false);
            }
        }

        private static void OnProBuilderSelectionUpdated(System.Collections.Generic.IEnumerable<ProBuilderMesh> selection)
        {
            if (s_IsActive && !s_SelectionUpdateDisabled)
            {
                // Check if ProBuilder selection is now empty - auto-cancel if so
                if (!selection.Any())
                {
                    EndCurrentPreview(false);
                    return;
                }

                // Check if element selection is empty based on current select mode - auto-cancel if so
                bool hasElementSelection = false;
                var currentToolMode = ProBuilderPlusCore.CurrentToolMode;

                foreach (var mesh in selection)
                {
                    if (mesh == null) continue;

                    switch (currentToolMode)
                    {
                        case ToolMode.Vertex:
                            if (mesh.selectedVertices != null && mesh.selectedVertices.Count > 0)
                                hasElementSelection = true;
                            break;
                        case ToolMode.Edge:
                            if (mesh.selectedEdges != null && mesh.selectedEdges.Count > 0)
                                hasElementSelection = true;
                            break;
                        case ToolMode.Face:
                        case ToolMode.UvFace:
                            var selectedFaces = mesh.GetSelectedFaces();
                            if (selectedFaces != null && selectedFaces.Length > 0)
                                hasElementSelection = true;
                            break;
                        default:
                            hasElementSelection = false; // These modes don't need element selection
                            break;
                    }

                    if (hasElementSelection) break;
                }

                if (!hasElementSelection)
                {
                    EndCurrentPreview(false);
                    return;
                }

                s_SelectionUpdateDisabled = true;
                EditorApplication.delayCall += static () =>
                {
                    if (s_IsActive && _currentAction != null)
                    {
                        _currentAction.OnSelectionChangedDuringPreview();
                    }
                    s_SelectionUpdateDisabled = false;
                };
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // Only handle input when we have an active preview
            if (!s_IsActive) return;

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown)
            {
                // Handle ESC and RETURN key to apply or cancel preview action
                if (evt.keyCode == KeyCode.Escape)
                {
                    CancelAction();
                    evt.Use(); // Consume the event so it doesn't propagate
                }
                else if (evt.keyCode == KeyCode.Return)
                {
                    ConfirmAction();
                    evt.Use();
                }
            }
        }

        private static void OnSelectionChanged()
        {
            if (s_IsActive && !s_SelectionUpdateDisabled)
            {
                // Check if selection is now empty - auto-cancel if so
                if (Selection.transforms.Length == 0)
                {
                    EndCurrentPreview(false);
                    return;
                }

                s_SelectionUpdateDisabled = true;
                EditorApplication.delayCall += static () =>
                {
                    if (s_IsActive && _currentAction != null)
                    {
                        _currentAction.OnSelectionChangedDuringPreview();
                    }
                    s_SelectionUpdateDisabled = false;
                };
            }
        }

        private static void OnSelectModeChanged(SelectMode mode)
        {
            // Auto-cancel when ProBuilder select mode changes (user must explicitly confirm)
            if (s_IsActive)
            {
                EndCurrentPreview(false);
            }
        }

        private static void OnToolChanged()
        {
            // Auto-cancel when tool changes (user must explicitly confirm)
            if (s_IsActive)
            {
                EndCurrentPreview(false);
            }
        }

        private static void StartPreview(PreviewMenuAction action)
        {
            _currentAction = action;
            s_IsActive = true;
            s_SelectionUpdateDisabled = false;

            // Create and show overlay
            s_CurrentOverlay = new PreviewActionOverlay();
            SceneView.AddOverlayToActiveView(s_CurrentOverlay);
            s_CurrentOverlay.displayed = true;

            // Start the action's preview
            _currentAction.StartPreview();
        }
    }
}
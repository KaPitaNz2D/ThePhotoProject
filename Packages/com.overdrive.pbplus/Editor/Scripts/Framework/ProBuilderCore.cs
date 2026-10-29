using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Central tracking of Unity and ProBuider state.<br/>
    /// When state has changed, the <see cref="OnStatusChanged"/> event is invoked.
    /// </summary>
    public static class ProBuilderPlusCore
    {
        /// <summary>
        /// Suppresses the status update calculated in <see cref="UpdateStatus"/>.
        /// Used in cases where changing the state requires a change in more than one Context/Tool/SelectMode
        /// and we don't want multiple updates.
        /// </summary>
        private static bool isUpdateSuppressed;

        /// <summary>
        /// Initialize the core system. Auto-executed on domain reload.
        /// </summary>
        static ProBuilderPlusCore()
        {
            ToolManager.activeContextChanged += UpdateStatus;
            ToolManager.activeToolChanged += UpdateStatus;
            ProBuilderEditor.selectModeChanged += OnSelectModeChanged;
            ProBuilderEditor.selectionUpdated += OnProBuilderSelectionUpdated; //// Note: This thing is often called TWICE. 
            Selection.selectionChanged += UpdateStatus;
            //// Important: 'MeshSelection.objectSelectionChanged' duplicates 'Selection.selectionChanged'

            ProBuilderInfo.Initialize();
        }

        /// <summary>
        /// Invoked when...<br/>
        /// - Unity active context<br/>
        /// - Unity selection<br/>
        /// - ProBuilder mode<br/>
        /// - ProBuilder selection<br/>
        /// changed.
        /// </summary>
        public static event System.Action OnStatusChanged;

        /// <summary>
        /// Gets the current tool mode supported by ProBuilderPlus.
        /// </summary>
        public static ToolMode CurrentToolMode => CachedState.toolMode;

        /// <summary>
        /// Forces an <see cref="OnStatusChanged"/> event be to invoked.<br/>
        /// Can be used to have all controls update themselves and should
        /// <b>ONLY</b> be used when the state management of the ProBuilderCore type can not handle it.<br/>
        /// For example: When UserPreferences change.
        /// </summary>
        public static void ForceOnStatusChangedEvent()
        {
            OnStatusChanged?.Invoke();
        }

        public static void SetActiveToolContext(ToolContext toolContext)
        {
            if (toolContext == ToolContext.Object)
            {
                if (ToolManager.activeContextType != typeof(GameObjectToolContext))
                {
                    ToolManager.SetActiveContext<GameObjectToolContext>();
                }
            }
            else if (toolContext == ToolContext.ProBuilderPosition)
            {
                if (ToolManager.activeContextType != ProBuilderInternals.ProBuilderPositionToolContext)
                {
                    ToolManager.SetActiveContext(ProBuilderInternals.ProBuilderPositionToolContext);
                }
            }
            else if (toolContext == ToolContext.ProBuilderTexture)
            {
                if (ToolManager.activeContextType != ProBuilderInternals.ProBuilderTextureToolContext)
                {
                    ToolManager.SetActiveContext(ProBuilderInternals.ProBuilderTextureToolContext);
                }
            }
        }

        public static void SetToolMode(ToolMode toolMode)
        {
            try
            {
                isUpdateSuppressed = true;
                if (toolMode.IsEditMode())
                {
                    if (toolMode == ToolMode.UvFace)
                    {
                        SetActiveToolContext(ToolContext.ProBuilderTexture);
                        ProBuilderEditor.selectMode = SelectMode.TextureFace;
                    }
                    else
                    {
                        SetActiveToolContext(ToolContext.ProBuilderPosition);
                        ProBuilderEditor.selectMode = toolMode.ToSelectMode();
                    }   
                }
                else if (toolMode == ToolMode.Object)
                {
                    SetActiveToolContext(ToolContext.Object);
                }
                else
                {
                    Debug.LogError("Invalid ToolMode " + toolMode);
                }
            }
            finally
            {
                isUpdateSuppressed = false;
                UpdateStatus();
            }
        }

        public static void UpdateStatus()
        {
            if (isUpdateSuppressed)
            {
                return;
            }

            // Update the edit mode info
            {
                var previousEditMode = CachedState.toolMode;

                var currentToolMode = ToolMode.None;

                if (ToolManager.activeContextType == ProBuilderInternals.ProBuilderPositionToolContext)
                {
                    // Get current selection mode
                    currentToolMode = ProBuilderEditor.selectMode.ToToolMode();
                }
                else if (ToolManager.activeContextType == ProBuilderInternals.ProBuilderTextureToolContext)
                {
                    // TextureToolContext ist der UV Move Modus aus dem UV Editor.
                    currentToolMode = ProBuilderEditor.selectMode == SelectMode.TextureFace
                        ? ToolMode.UvFace
                        : ToolMode.Object;
                }
                else if (ToolManager.activeContextType == typeof(GameObjectToolContext))
                {
                    currentToolMode = ToolMode.Object;
                }

                if (currentToolMode != previousEditMode)
                {
                    CachedState.toolMode = currentToolMode;
                    CachedState.isDirty = true;
                }
            }

            if (!CachedState.isDirty && CachedState.selectedTransformsCount != Selection.transforms.Length)
            {
                CachedState.isDirty = true;
            }

            if (!CachedState.isDirty && CachedState.selectedActiveTransform != Selection.activeTransform)
            {
                CachedState.isDirty = true;
            }

            if (!CachedState.isDirty && CachedState.selectedPbEdgesCount != MeshSelection.selectedEdgeCount)
            {
                CachedState.isDirty = true;
            }

            if (!CachedState.isDirty && CachedState.selectedPbFacesCount != MeshSelection.selectedFaceCount)
            {
                CachedState.isDirty = true;
            }

            if (!CachedState.isDirty && CachedState.selectedPbVerticesCount != MeshSelection.selectedVertexCount)
            {
                CachedState.isDirty = true;
            }

            if (!CachedState.isDirty && CachedState.selectedPbMeshesCount != MeshSelection.selectedObjectCount)
            {
                CachedState.isDirty = true;
            }

            if (!CachedState.isDirty && CachedState.selectedActiveMesh != MeshSelection.activeMesh)
            {
                CachedState.isDirty = true;
            }

            if (CachedState.isDirty)
            {
                CachedState.selectedTransformsCount = Selection.transforms.Length;
                CachedState.selectedActiveTransform = Selection.activeTransform;
                CachedState.selectedPbEdgesCount = MeshSelection.selectedEdgeCount;
                CachedState.selectedPbFacesCount = MeshSelection.selectedFaceCount;
                CachedState.selectedPbVerticesCount = MeshSelection.selectedVertexCount;
                CachedState.selectedPbMeshesCount = MeshSelection.selectedObjectCount;
                CachedState.selectedActiveMesh = MeshSelection.activeMesh;
                CachedState.isDirty = false;

                //// Debug Outout of number of hooked delegates.
                //// var statusChangedInvocationList = OnStatusChanged?.GetInvocationList();
                //// Debug.Log("CoreUpdateStatus -> OnStatusChanged event fired. Targets: " + (statusChangedInvocationList != null ? statusChangedInvocationList.Length : -1));
                OnStatusChanged?.Invoke();
            }
        }

        private static void OnProBuilderSelectionUpdated(System.Collections.Generic.IEnumerable<ProBuilderMesh> selection)
        {
            // Directly set dirty, we can't check whats changed internally
            CachedState.isDirty = true;
            UpdateStatus();
        }

        private static void OnSelectModeChanged(SelectMode mode)
        {
            UpdateStatus();
        }

        private static class CachedState
        {
            public static bool isDirty = false;
            public static ProBuilderMesh selectedActiveMesh = null;
            public static Transform selectedActiveTransform = null;
            public static int selectedPbEdgesCount = 0;
            public static int selectedPbFacesCount = 0;
            public static int selectedPbMeshesCount = 0;
            public static int selectedPbVerticesCount = 0;
            public static int selectedTransformsCount = 0;
            public static ToolMode toolMode;
        }
    }
}

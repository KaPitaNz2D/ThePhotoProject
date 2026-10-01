using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Base class for menu actions that provide live preview functionality.<br/>
    /// Actions inherit from this to get automatic preview-then-confirm behavior.<br/>
    /// Automatically reads metadata from ProBuilderPlusAction attribute.
    /// </summary>
    public abstract class PreviewMenuAction
    {
        private ProBuilderPlusActionAttribute attribute;

        /// <summary>
        /// Automatically reads menu title from ProBuilderPlusAction attribute
        /// </summary>
        public string MenuTitle => attribute?.DisplayName ?? GetType().Name;

        /// <summary>
        /// Instructions text to display in the overlay. Automatically reads from ProBuilderPlusAction attribute.
        /// </summary>
        public string Instructions => attribute?.Instructions ?? "Configure settings and click Apply to confirm.";

        public bool IsInValidMode
        {
            get
            {
                var toolMode = ProBuilderPlusCore.CurrentToolMode;
                if (!this.attribute.ValidModes.ContainsFlag(toolMode))
                {
                    return false;
                }

                if (ProBuilderEditor.selectMode.ContainsFlag(SelectMode.InputTool))
                {
                    return false;
                }

                if ((toolMode == ToolMode.Face || toolMode == ToolMode.UvFace)
                    && this.attribute.FacesSelectedRequired > 0
                    && MeshSelection.selectedFaceCount < this.attribute.FacesSelectedRequired)
                {
                    return false;
                }

                if (toolMode == ToolMode.Edge
                    && this.attribute.EdgesSelectedRequired > 0
                    && MeshSelection.selectedEdgeCount < this.attribute.EdgesSelectedRequired)
                {
                    return false;
                }

                if (toolMode == ToolMode.Vertex
                    && this.attribute.VerticesSelectedRequired > 0
                    && MeshSelection.selectedSharedVertexCount < this.attribute.VerticesSelectedRequired)
                {
                    return false;
                }

                if (toolMode == ToolMode.Object
                   &&this.attribute.ObjectsSelectedRequired > 0
                   && MeshSelection.selectedObjectCount < this.attribute.ObjectsSelectedRequired)
                {
                    return false;
                }

                if (!this.IsSpecialConditionsMet())
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Sets the cached attribute. Called by ActionAutoDiscovery to avoid double reflection.
        /// </summary>
        internal void SetCachedAttribute(ProBuilderPlusActionAttribute attribute)
        {
            this.attribute = attribute ?? throw new System.ArgumentNullException(nameof(attribute));
        }

        /// <summary>
        /// Gets the readable name. For error messages.
        /// </summary>
        public string GetName()
        {
            return this.attribute != null ? $"{this.attribute.Id}.{this.attribute.DisplayName}" : nameof(PreviewMenuAction);
        }

        /// <summary>
        /// Called when selection changes during preview. Override to handle selection changes differently.
        /// Default behavior is to update preview for new selection.
        /// </summary>
        internal virtual void OnSelectionChangedDuringPreview()
        {
            // Default: Update preview for new selection
            StartPreview(); // Restart with new selection
        }

        protected virtual bool IsSpecialConditionsMet() => true;

        #region Abstract Members

        /// <summary>
        /// Called when the preview is first started. Set up your preview state here.
        /// </summary>
        public abstract void StartPreview();

        /// <summary>
        /// Called when preview parameters change or selection updates. Recalculate and redraw preview.
        /// </summary>
        public abstract void UpdatePreview();

        /// <summary>
        /// Called when user confirms the action. Apply the actual changes to the mesh here.
        /// </summary>
        /// <returns>ActionResult indicating success/failure</returns>
        public abstract ActionResult ApplyChanges();

        /// <summary>
        /// Called when preview ends (confirm or cancel). Clean up preview state here.
        /// </summary>
        public abstract void CleanupPreview();

        /// <summary>
        /// Override this to provide settings UI. Don't add Confirm/Cancel buttons - the framework handles those.
        /// </summary>
        public abstract VisualElement CreateSettingsContent();

        #endregion Abstract Members
    }
}

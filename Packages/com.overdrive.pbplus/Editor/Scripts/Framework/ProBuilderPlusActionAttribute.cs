using System;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Attribute for ProBuilderPlus actions that provides metadata for automatic discovery.<br/>
    /// Actions can use this instead of manual registration in ActionInfoProvider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ProBuilderPlusActionAttribute : Attribute
    {
        /// <summary>
        /// Unique identifier for this action
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Display name shown in the UI
        /// </summary>
        public string DisplayName { get; }
        
        /// <summary>
        /// Tooltip text for the action button
        /// </summary>
        public string Tooltip { get; set; }

        /// <summary>
        /// Instructions text displayed in the preview overlay
        /// </summary>
        public string Instructions { get; set; }

        /// <summary>
        /// Path to icon resource (relative to Assets folder)
        /// </summary>
        public string IconPath { get; set; }
        
        /// <summary>
        /// Selection modes this action is valid for
        /// </summary>
        public ToolMode ValidModes { get; set; } = ToolMode.Face | ToolMode.Edge | ToolMode.Vertex;

        /// <summary>
        /// Gets the number of faces required to be selected for the tool to be used in <see cref="ToolMode.Face"/> or <see cref="ToolMode.UvFace"/>.<br/>
        /// 0/Zero means the check is disabled.
        /// </summary>
        public int FacesSelectedRequired { get; set; } = 0;

        /// <summary>
        /// Gets the number of edges required to be selected for the tool to be used in <see cref="ToolMode.Edge"/>.<br/>
        /// 0/Zero means the check is disabled.
        /// </summary>
        public int EdgesSelectedRequired { get; set; } = 0;

        /// <summary>
        /// Gets the number of vertices required to be selected for the tool to be used in <see cref="ToolMode.Vertex"/>.<br/>
        /// 0/Zero means the check is disabled.
        /// </summary>
        public int VerticesSelectedRequired { get; set; } = 0;

        /// <summary>
        /// Gets the number of ProBuilderMeshes required to be selected for the tool to be used in <see cref="ToolMode.Object"/>.<br/>
        /// 0/Zero means the check is disabled.
        /// </summary>
        public int ObjectsSelectedRequired { get; set; } = 0;

        /// <summary>
        /// Order for displaying in toolbar (lower numbers appear first)
        /// </summary>
        public int Order { get; set; } = 100;

        /// <summary>
        /// Type of action for UI placement (defaults to Action)
        /// </summary>
        public ProBuilderPlusActionType ActionType { get; set; } = ProBuilderPlusActionType.Action;

        /// <summary>
        /// Whether this action supports instant mode (CTRL+click execution with default settings)
        /// </summary>
        public bool SupportsInstantMode { get; set; } = true;

        public ProBuilderPlusActionAttribute(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }
}

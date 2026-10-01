namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// ToolContexts supported by PBPlus.
    /// </summary>
    public enum ToolContext
    {
        /// <summary>
        /// Unity Object Context.<br/>
        /// </summary>
        Object = 0,

        /// <summary>
        /// Unity ProBuilder Context for editing vertices/edges/faces.
        /// </summary>
        ProBuilderPosition,

        /// <summary>
        /// Unity ProBuilder Context for editing UVs. Hidden away in UV window.
        /// </summary>
        ProBuilderTexture,
    }
}
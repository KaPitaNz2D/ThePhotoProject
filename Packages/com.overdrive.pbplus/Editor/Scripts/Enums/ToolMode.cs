using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Tool Modes Supported by ProBuilder Plus.
    /// </summary>
    [System.Flags]
    public enum ToolMode
    {
        /// <summary>
        /// No tool Selected.<br/>
        /// </summary>
        None = 0,

        /// <summary>
        /// Unity is in <see cref="UnityEditor.EditorTools.GameObjectToolContext"/><br/>
        /// </summary>
        Object = 1 << 0,

        /// <summary>
        /// Unity is in ProBuilder FACE selection context.<br/>
        /// -> Context: UnityEditor.ProBuilder.PositionToolContext<br/>
        /// -> ProBuilder SelectMode: <see cref="SelectMode.Face"/>.<br/>
        /// </summary>
        Face = 1 << 1,

        /// <summary>
        /// Unity is in ProBuilder EDGE selection context.<br/>
        /// -> Context: UnityEditor.ProBuilder.PositionToolContext<br/>
        /// -> ProBuilder SelectMode: <see cref="SelectMode.Edge"/>.<br/>
        /// </summary>
        Edge = 1 << 2,

        /// <summary>
        /// Unity is in ProBuilder VERTEX selection context.<br/>
        /// -> Context: UnityEditor.ProBuilder.PositionToolContext<br/>
        /// -> ProBuilder SelectMode: <see cref="SelectMode.Vertex"/>.<br/>
        /// </summary>
        Vertex = 1 << 3,

        /// <summary>
        /// Unity is in ProBuilder FACE-UV selection context.<br/>
        /// -> Context: UnityEditor.ProBuilder.TextureToolContext<br/>
        /// -> ProBuilder SelectMode: <see cref="SelectMode.TextureFace"/>.<br/>
        /// </summary>
        UvFace = 1 << 4,
    }
}

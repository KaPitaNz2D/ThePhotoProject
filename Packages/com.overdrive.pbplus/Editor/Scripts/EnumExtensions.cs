using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Extensionsmethods for ProBuilder and ProBuilderPlus enums.
    /// </summary>
    public static class EnumExtensions
    {
        public static bool ContainsFlag(this SelectMode target, SelectMode value)
        {
            return (target & value) != 0;
        }

        public static bool ContainsFlag(this ToolMode target, ToolMode value)
        {
            return (target & value) != 0;
        }

        public static bool IsEditMode(this ToolMode toolMode)
        {
            return toolMode switch
            {
                ToolMode.Face => true,
                ToolMode.Vertex => true,
                ToolMode.Edge => true,
                ToolMode.UvFace => true,
                _ => false,
            };
        }

        public static bool IsFaceMode(this ToolMode toolMode)
        {
            return toolMode switch
            {
                ToolMode.Face => true,
                ToolMode.UvFace => true,
                _ => false,
            };
        }

        public static SelectMode ToSelectMode(this ToolMode toolMode)
        {
            return toolMode switch
            {
                ToolMode.Face => SelectMode.Face,
                ToolMode.Edge => SelectMode.Edge,
                ToolMode.Vertex => SelectMode.Vertex,
                _ => SelectMode.Face,
            };
        }

        public static ToolMode ToToolMode(this SelectMode proBuilderSelectMode)
        {
            return proBuilderSelectMode switch
            {
                SelectMode.Face => ToolMode.Face,
                SelectMode.Edge => ToolMode.Edge,
                SelectMode.Vertex => ToolMode.Vertex,
                SelectMode.TextureFace => ToolMode.UvFace,
                _ => ToolMode.None,
            };
        }
    }
}

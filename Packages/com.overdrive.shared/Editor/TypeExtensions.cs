using System;

namespace Overdrive
{
    /// <summary>
    /// Extension methods for the Type-type. I typed this so you can type less. So nice that Net is not typeless.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Tools that may exist in UnityEditor.EditorTools.ToolManager.activeToolType as a type.
        /// </summary>
        public enum UnityTool
        {
            None,
            MoveTool,
            RotateTool,
            ScaleTool,
            RectTool,
            TransformTool,
            SplinesCreateSplineTool,
            ProBuilderCreateCubeTool,
            ProBuilderDrawPolyShapeTool,
            ProBuilderCutTool,
        }

        /// <summary>
        /// Returns which Unity tool the given Type is. 
        /// </summary>
        /// <param name="type">UnityEditor.EditorTools.ToolManager.activeToolType</param>
        /// <returns>Type or UnityTool.None or no match.</returns>
        public static UnityTool AsToolType(this Type type)
        {
            if (type == null)
            {
                return UnityTool.None;
            }

            var fullName = type.FullName;

           return fullName switch
            {
                "UnityEditor.MoveTool" => UnityTool.MoveTool,
                "UnityEditor.RotateTool" => UnityTool.RotateTool,
                "UnityEditor.ScaleTool" => UnityTool.ScaleTool,
                "UnityEditor.RectTool" => UnityTool.RectTool,
                "UnityEditor.TransformTool" => UnityTool.TransformTool,
                "UnityEditor.Splines.CreateSplineTool" => UnityTool.SplinesCreateSplineTool,
                "UnityEditor.ProBuilder.CreateCubeTool" => UnityTool.ProBuilderCreateCubeTool,
                "UnityEditor.ProBuilder.DrawPolyShapeTool" => UnityTool.ProBuilderDrawPolyShapeTool,
                "UnityEditor.ProBuilder.CutTool" => UnityTool.ProBuilderCutTool,
                _ => UnityTool.None,
            };
        }

        /// <summary>
        /// Return if the given Type matches a UnityTool.
        /// </summary>
        /// <param name="type">UnityEditor.EditorTools.ToolManager.activeToolType</param>
        /// <returns>False if no match or unknown Type.</returns>
        public static bool IsToolType(this Type type, UnityTool unityTool)
        {
            if (type == null)
                return true;

            return unityTool switch
            {
                UnityTool.MoveTool => type.FullName == "UnityEditor.MoveTool",
                UnityTool.RotateTool => type.FullName == "UnityEditor.RotateTool",
                UnityTool.ScaleTool => type.FullName == "UnityEditor.ScaleTool",
                UnityTool.RectTool => type.FullName == "UnityEditor.RectTool",
                UnityTool.TransformTool => type.FullName == "UnityEditor.TransformTool",
                UnityTool.SplinesCreateSplineTool => type.FullName == "UnityEditor.Splines.CreateSplineTool",
                UnityTool.ProBuilderCreateCubeTool => type.FullName == "UnityEditor.ProBuilder.CreateCubeTool",
                UnityTool.ProBuilderDrawPolyShapeTool => type.FullName == "UnityEditor.ProBuilder.DrawPolyShapeTool",
                UnityTool.ProBuilderCutTool => type.FullName == "UnityEditor.ProBuilder.CutTool",
                _ => false,
            };
        }
    }
}

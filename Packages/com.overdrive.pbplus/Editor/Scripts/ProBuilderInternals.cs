using UnityEngine;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Contains internal types of ProBuilder to check/set the active context and tools acquired by reflection.<br/>
    /// For use with:
    ///     <br/>-ToolManager.activeToolType
    ///     <br/>-ToolManager.activeToolType
    ///     <br/>-ToolManager.SetActiveContext(...)
    ///     <br/>-ToolManager.SetActiveTool(...)
    /// </summary>
    public static class ProBuilderInternals
    {
        /// <summary>
        /// Type of the Unity ProBuilder Cut Tool. Acquired via Reflection.
        /// </summary>
        public static readonly System.Type ProBuilderCutTool;

        /// <summary>
        /// Getter-Method for access to property <see cref="UnityEditor.ProBuilder.MeshSelection.totalVertexCountOptimized"/>.
        /// </summary>
        public static readonly System.Reflection.MethodInfo ProBuilderMeshSelectionGetTotalVertexCountOptimizedMethod;

        /// <summary>
        /// Type of the Unity ProBuilder Tool Context. Acquired via Reflection.
        /// </summary>
        public static readonly System.Type ProBuilderPositionToolContext;

        /// <summary>
        /// Type of the Unity ProBuilder Texture Tool Context. Acquired via Reflection.
        /// </summary>
        public static readonly System.Type ProBuilderTextureToolContext;
        /// <summary>
        /// Method for PLANAR projection.<br/>
        /// -> UnityEngine.ProBuilder.MeshOperations.UVEditing<br/>
        /// -> internal static void ProjectFacesAuto(ProBuilderMesh mesh, Face[] faces, int channel)
        /// </summary>

        public static readonly System.Reflection.MethodInfo ProjectFacesAutoMethod;

        /// <summary>
        /// Method for BOX projection.<br/>
        /// -> UnityEngine.ProBuilder.MeshOperations.UVEditing<br/>
        /// -> public static void ProjectFacesBox(ProBuilderMesh mesh, Face[] faces, Vector2 lowerLeftAnchor, int channel = 0)
        /// </summary>
        public static readonly System.Reflection.MethodInfo ProjectFacesBoxMethod;

        /// <summary>
        /// -> UnityEditor.ProBuilder.EditorHandleDrawing.xRay
        /// </summary>
        private static readonly System.Reflection.PropertyInfo EditorHandleDrawingXRayProperty;

        static ProBuilderInternals()
        {
            var proBuilderEditorAssembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.ProBuilder.Actions.CutToolAction));
            var proBuilderEditorTypes = proBuilderEditorAssembly.GetTypes();
            foreach (var type in proBuilderEditorTypes)
            {
                if (type.FullName == "UnityEditor.ProBuilder.PositionToolContext")
                {
                    ProBuilderPositionToolContext = type;
                }
                else if (type.FullName == "UnityEditor.ProBuilder.TextureToolContext")
                {
                    ProBuilderTextureToolContext = type;
                }
                else if (type.FullName == "UnityEditor.ProBuilder.CutTool")
                {
                    ProBuilderCutTool = type;
                }
                else if (type.FullName == "UnityEditor.ProBuilder.EditorHandleDrawing")
                {
                    EditorHandleDrawingXRayProperty = type.GetProperty("xRay", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                }
            }
            
            var proBuilderRuntimeAssembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEngine.ProBuilder.ActionResult));
            var proBuilderRuntimeTypes = proBuilderRuntimeAssembly.GetTypes();
            foreach (var type in proBuilderRuntimeTypes)
            {
                if (type.FullName == "UnityEngine.ProBuilder.MeshOperations.UVEditing")
                {
                    // Enumerating all methods because of methods of the same name, so we yoink the one with the matching parameter count.
                    var publicMethods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    foreach (var publicMethod in publicMethods)
                    {
                        if (publicMethod.Name == "ProjectFacesBox")
                        {
                            var parameters = publicMethod.GetParameters();
                            if (parameters.Length == 4)
                            {
                                ProjectFacesBoxMethod = publicMethod;
                                break;
                            }
                        }
                        else if (publicMethod.Name == "ProjectFacesAuto")
                        {
                            var parameters = publicMethod.GetParameters();
                            if (parameters.Length == 3)
                            {
                                ProjectFacesAutoMethod = publicMethod;
                            }
                        }
                    }

                    break;
                }
            }

            Debug.Assert(ProBuilderPositionToolContext != null, "Reflection failed. UnityEditor.ProBuilder.PositionToolContext is null.");
            Debug.Assert(ProBuilderTextureToolContext != null, "Reflection failed. UnityEditor.ProBuilder.TextureToolContext is null.");
            Debug.Assert(ProBuilderCutTool != null, "Reflection failed. UnityEditor.ProBuilder.ProBuilderCutTool is null.");
            Debug.Assert(ProjectFacesAutoMethod != null, "Reflection failed. UnityEngine.ProBuilder.MeshOperations.UVEditing method 'ProjectFacesAuto' is null.");
            Debug.Assert(ProjectFacesBoxMethod != null, "Reflection failed. UnityEngine.ProBuilder.MeshOperations.UVEditing method 'ProjectFacesBox' is null.");
            Debug.Assert(EditorHandleDrawingXRayProperty != null, "Reflection failed. UnityEditor.ProBuilder.EditorHandleDrawing property 'xRay' is null.");

            // Currently unused access to an internal static property MeshSelection.totalVertexCountOptimized
            var meshSelectionType = typeof(UnityEditor.ProBuilder.MeshSelection);
            var property = meshSelectionType.GetProperty("totalVertexCountOptimized", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            ProBuilderMeshSelectionGetTotalVertexCountOptimizedMethod = property?.GetMethod;
            Debug.Assert(property != null, "Reflection failed. Property UnityEditor.ProBuilder.MeshSelection.totalVertexCountOptimized is null.");
            Debug.Assert(ProBuilderMeshSelectionGetTotalVertexCountOptimizedMethod != null, "Reflection failed. Property UnityEditor.ProBuilder.MeshSelection.totalVertexCountOptimized.GetMethod is null.");
        }

        /// <summary>
        /// Gets or sets the ProBuilder X-Ray value.
        /// </summary>
        public static bool EditorHandleDrawingXRay
        {
            get { return (bool)EditorHandleDrawingXRayProperty.GetValue(null); }
            set { EditorHandleDrawingXRayProperty.SetValue(null, value); }
        }
    }
}

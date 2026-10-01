using UnityEditor;
using UnityEditor.ShortcutManagement;
using ToolManager = UnityEditor.EditorTools.ToolManager;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Additional Shortcuts for ProBuilder.
    /// </summary>
    public static class ProBuilderPlusShortcuts
    {
        [Shortcut("Overdrive/Toogle Probuilder Cut/Knife Tool <-> Object", typeof(SceneView))]
        public static void ToggleCutTool()
        {
            if (ToolManager.activeToolType == ProBuilderInternals.ProBuilderCutTool)
            {
                ToolManager.RestorePreviousTool();
            }
            else
            {
                if (UnityEditor.ProBuilder.MeshSelection.selectedObjectCount == 1)
                {
                    ToolManager.SetActiveTool(ProBuilderInternals.ProBuilderCutTool);
                }
            }

            //Give the focus back to scene view to handle key inputs directly
            SceneView.lastActiveSceneView.Focus();
        }

        [Shortcut("Overdrive/Toogle Probuilder Edit Edge <-> Object", typeof(SceneView))]
        public static void ToggleEditEdges()
        {
            if (ProBuilderPlusCore.CurrentToolMode != ToolMode.Edge)
            {
                if (UnityEditor.ProBuilder.MeshSelection.selectedObjectCount == 0)
                    return;

                ProBuilderPlusCore.SetToolMode(ToolMode.Edge);
            }
            else
            {
                ProBuilderPlusCore.SetActiveToolContext(ToolContext.Object);
            }
            SceneView.lastActiveSceneView.Focus();
        }

        [Shortcut("Overdrive/Toogle Probuilder Edit Face <-> Object", typeof(SceneView))]
        public static void ToggleEditFaces()
        {
            if (ProBuilderPlusCore.CurrentToolMode != ToolMode.Face)
            {
                if (UnityEditor.ProBuilder.MeshSelection.selectedObjectCount == 0)
                    return;

                ProBuilderPlusCore.SetToolMode(ToolMode.Face);
            }
            else
            {
                ProBuilderPlusCore.SetActiveToolContext(ToolContext.Object);
            }

            SceneView.lastActiveSceneView.Focus();
        }

        [Shortcut("Overdrive/Toogle Probuilder Edit Vertex <-> Object", typeof(SceneView))]
        public static void ToggleEditVertices()
        {
            if (ProBuilderPlusCore.CurrentToolMode != ToolMode.Vertex)
            {
                if (UnityEditor.ProBuilder.MeshSelection.selectedObjectCount == 0)
                    return;

                ProBuilderPlusCore.SetToolMode(ToolMode.Vertex);
            }
            else
            {
                ProBuilderPlusCore.SetActiveToolContext(ToolContext.Object);
            }

            SceneView.lastActiveSceneView.Focus();
        }

        [Shortcut("Overdrive/Toogle Probuilder UV-Texture Face Mode", typeof(SceneView))]
        public static void ToTextureUV()
        {
            if (ProBuilderPlusCore.CurrentToolMode != ToolMode.UvFace)
            {
                if (UnityEditor.ProBuilder.MeshSelection.selectedObjectCount == 0)
                    return;

                ProBuilderPlusCore.SetToolMode(ToolMode.UvFace);
            }
            else
            {
                ProBuilderPlusCore.SetActiveToolContext(ToolContext.Object);
            }

            SceneView.lastActiveSceneView.Focus();
        }
    }
}

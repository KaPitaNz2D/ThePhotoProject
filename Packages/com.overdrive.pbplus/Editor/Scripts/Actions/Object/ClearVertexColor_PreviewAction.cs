using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Removes vertex colors from meshes.
    /// </summary>
    [ProBuilderPlusAction("clear-Vertex_color_objects_preview", "Clear Vert.Color",
        Tooltip = "Removes vertex colors from meshes",
        Instructions =
            "In <b>object mode</b>, this removes all vertex colors from the mesh data.\n" +
            "In <b>edit modes</b>, this sets the color of selected vertices to <b>white/neutral</b>.",
        IconPath = "Icons/Old/Panel_VertColors",
        ValidModes = ToolMode.Object | ToolMode.Face | ToolMode.Edge | ToolMode.Vertex | ToolMode.UvFace,
        ObjectsSelectedRequired = 1,
        FacesSelectedRequired = 1,
        EdgesSelectedRequired = 1,
        VerticesSelectedRequired =1,
        Order = 53)]
    public sealed class ClearVertexColorPreviewAction : PreviewMenuAction
    {
        public override ActionResult ApplyChanges()
        {
            ProBuilderFunctions.ClearVertexColor();
            return new ActionResult(ActionResult.Status.Success, "okay");
        }
        public override void CleanupPreview()
        {

        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            return root;
        }

        public override void StartPreview()
        {

        }

        public override void UpdatePreview()
        {

        }
    }
}

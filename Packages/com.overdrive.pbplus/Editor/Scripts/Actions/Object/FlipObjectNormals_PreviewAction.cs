using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Preview version of FlipObjectNormals that shows which faces will be flipped before applying changes.
    /// Operates on all faces of selected objects, providing visual preview of the flip operation.
    /// </summary>
    [ProBuilderPlusAction("flip_object_normals", "Flip",
        Tooltip = "Preview which faces will be flipped on selected objects, showing the new normal direction",
        Instructions = "Flip all faces (red)",
        IconPath = "Icons/Old/Object_ConformNormals",
        ValidModes = ToolMode.Object,
        ObjectsSelectedRequired = 1,
        Order = 25)]
    public sealed class FlipObjectNormalsPreviewAction : PreviewMenuAction
    {
        // Cache data for preview
        private ProBuilderMesh[] m_CachedMeshes;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            // Instructions are now handled by the framework via the Instructions attribute
            return root;
        }

        public override void StartPreview()
        {

            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;

        }

        public override void UpdatePreview()
        {
            // No settings to change for flip operation, just repaint
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {

            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No objects to process");
            }

            // Prepare undo
            var undoObjects = new List<Object>();
            foreach (var mesh in m_CachedMeshes)
            {
                undoObjects.Add(mesh);
            }
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Flip Object Normals");

            int totalFlipped = 0;

            // Apply flip to all faces on each mesh
            foreach (var mesh in m_CachedMeshes)
            {

                try
                {
                    // Flip all faces on the object
                    foreach (var face in mesh.faces)
                    {
                        face.Reverse();
                        totalFlipped++;
                    }

                    // Update mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    mesh.Optimize();

                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to flip object normals for '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            string resultMessage = totalFlipped > 0 ?
                $"Flipped {totalFlipped} face normal{(totalFlipped == 1 ? string.Empty : "s")}" :
                "No faces to flip";

            return new ActionResult(ActionResult.Status.Success, resultMessage);
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            SceneView.RepaintAll();

        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawFlipVisualization();
        }

        private void DrawFlipVisualization()
        {
            if (m_CachedMeshes == null) return;

            foreach (var mesh in m_CachedMeshes)
            {
                if (mesh == null) continue;

                // Use shared helper to draw flip visualization for all faces
                FlipNormalsHelper.DrawFlipVisualization(mesh, mesh.faces.ToArray());
            }
        }
    }
}
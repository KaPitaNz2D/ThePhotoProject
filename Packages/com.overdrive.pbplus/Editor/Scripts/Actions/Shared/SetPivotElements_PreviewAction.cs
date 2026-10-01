using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Base class for Set Pivot actions that work with selected elements (vertices, edges, faces).
    /// Shows axis tripod at the center of selected elements and moves pivot there on confirm.
    /// </summary>
    public abstract class SetPivotElementsPreviewActionBase : PreviewMenuAction
    {
        // Preview state
        protected ProBuilderMesh[] m_CachedMeshes;
        protected Vector3[] m_PivotPositions; // Target pivot positions in world space

        // Visual settings
        private const float AXIS_LENGTH = 1.5f;

        /// <summary>
        /// Derived classes must implement this to calculate the center position of their selection
        /// </summary>
        protected abstract Vector3 CalculateSelectionCenter(ProBuilderMesh mesh);

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            return root;
        }

        public override void StartPreview()
        {
            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_PivotPositions = new Vector3[selection.Length];

            // Calculate pivot positions for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                m_PivotPositions[i] = CalculateSelectionCenter(mesh);
            }

            // Subscribe to scene GUI for preview visualization
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public override void UpdatePreview()
        {
            // Recalculate pivot positions in case selection changed
            if (m_CachedMeshes != null)
            {
                for (int i = 0; i < m_CachedMeshes.Length; i++)
                {
                    var mesh = m_CachedMeshes[i];
                    m_PivotPositions[i] = CalculateSelectionCenter(mesh);
                }
            }
            
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            
            if (m_CachedMeshes == null || m_PivotPositions == null)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No selection cached");
            }

            // Prepare undo - register both mesh and transform objects for undo
            var undoObjects = new List<Object>();
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                undoObjects.Add(m_CachedMeshes[i]);
                undoObjects.Add(m_CachedMeshes[i].transform);
            }

            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Set Pivot to Selection Center");

            int successCount = 0;
            
            // Apply pivot changes to each mesh
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var pivotWorldPos = m_PivotPositions[i];
                

                try
                {
                    // Use ProBuilder's SetPivot method which handles everything internally
                    mesh.SetPivot(pivotWorldPos);
                    
                    // Optimize the mesh
                    mesh.Optimize();
                    
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to set pivot for '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            if (successCount > 0)
            {
                string message = successCount == 1 ? 
                    $"Set pivot for 1 object" : 
                    $"Set pivot for {successCount} objects";
                return new ActionResult(ActionResult.Status.Success, message);
            }
            else
            {
                return new ActionResult(ActionResult.Status.Failure, "Failed to set pivot for any objects");
            }
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            // Clear all preview state
            m_CachedMeshes = null;
            m_PivotPositions = null;
            
            SceneView.RepaintAll();
            
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawAxisTripods();
        }

        private void DrawAxisTripods()
        {
            if (m_CachedMeshes == null || m_PivotPositions == null) return;

            // Set z-test to always show elements on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            var drawnPositions = new HashSet<Vector3>();

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var pivotPosition = m_PivotPositions[meshIndex];
                
                if (mesh == null) continue;

                // Avoid drawing duplicate tripods at the same position
                var roundedPos = new Vector3(
                    Mathf.Round(pivotPosition.x * 1000f) / 1000f,
                    Mathf.Round(pivotPosition.y * 1000f) / 1000f,
                    Mathf.Round(pivotPosition.z * 1000f) / 1000f
                );

                if (drawnPositions.Add(roundedPos))
                {
                    DrawAxisTripod(pivotPosition, mesh.transform.rotation);
                }
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        private void DrawAxisTripod(Vector3 position, Quaternion rotation)
        {
            float handleSize = UnityEditor.HandleUtility.GetHandleSize(position);
            float axisLength = handleSize * AXIS_LENGTH;

            // Use standard RGB colors for XYZ axes
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            
            // X axis (Red)
            Handles.color = Color.red;
            Handles.DrawLine(position, position + right * axisLength);
            
            // Y axis (Green)
            Handles.color = Color.green;
            Handles.DrawLine(position, position + up * axisLength);
            
            // Z axis (Blue)
            Handles.color = Color.blue;
            Handles.DrawLine(position, position + forward * axisLength);
        }
    }

    /// <summary>
    /// Set Pivot action for vertex selection mode.
    /// Sets pivot to the center of selected vertices.
    /// </summary>

    [ProBuilderPlusAction("setpivot-vertices", "Set Pivot",
        Tooltip = "Set pivot to the center of selected vertices with preview",
        Instructions = "Axis tripod shows where the new pivot will be positioned",
        IconPath = "Icons/Old/SetPivot",
        ValidModes = ToolMode.Vertex,
        VerticesSelectedRequired = 1,
        Order = 250)]
    public sealed class SetPivotVerticesPreviewAction : SetPivotElementsPreviewActionBase
    {
        protected override Vector3 CalculateSelectionCenter(ProBuilderMesh mesh)
        {
            var selectedVertices = mesh.selectedVertices;
            if (selectedVertices.Count == 0) return mesh.transform.position;

            var vertices = mesh.GetVertices();
            Vector3 center = Vector3.zero;
            
            foreach (var index in selectedVertices)
            {
                center += mesh.transform.TransformPoint(vertices[index].position);
            }
            
            return center / selectedVertices.Count;
        }
    }

    /// <summary>
    /// Set Pivot action for edge selection mode.
    /// Sets pivot to the center of selected edges.
    /// </summary>

    [ProBuilderPlusAction("setpivot-edges", "Set Pivot",
        Tooltip = "Set pivot to the center of selected edges with preview",
        Instructions = "Axis tripod shows where the new pivot will be positioned",
        IconPath = "Icons/Old/SetPivot",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 251)]
    public sealed class SetPivotEdgesPreviewAction : SetPivotElementsPreviewActionBase
    {
        protected override Vector3 CalculateSelectionCenter(ProBuilderMesh mesh)
        {
            var selectedEdges = mesh.selectedEdges;
            if (selectedEdges.Count == 0) return mesh.transform.position;

            var vertices = mesh.GetVertices();
            Vector3 center = Vector3.zero;
            int pointCount = 0;
            
            foreach (var edge in selectedEdges)
            {
                center += mesh.transform.TransformPoint(vertices[edge.a].position);
                center += mesh.transform.TransformPoint(vertices[edge.b].position);
                pointCount += 2;
            }
            
            return center / pointCount;
        }
    }

    /// <summary>
    /// Set Pivot action for face selection mode.
    /// Sets pivot to the center of selected faces.
    /// </summary>

    [ProBuilderPlusAction("setpivot-faces", "Set Pivot",
        Tooltip = "Set pivot to the center of selected faces with preview",
        Instructions = "Axis tripod shows where the new pivot will be positioned",
        IconPath = "Icons/Old/SetPivot",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 252)]
    public sealed class SetPivotFacesPreviewAction : SetPivotElementsPreviewActionBase
    {
        protected override Vector3 CalculateSelectionCenter(ProBuilderMesh mesh)
        {
            var selectedFaces = mesh.GetSelectedFaces();
            if (selectedFaces.Length == 0) return mesh.transform.position;

            var vertices = mesh.GetVertices();
            Vector3 center = Vector3.zero;
            int pointCount = 0;
            
            foreach (var face in selectedFaces)
            {
                foreach (var index in face.distinctIndexes)
                {
                    center += mesh.transform.TransformPoint(vertices[index].position);
                    pointCount++;
                }
            }
            
            return center / pointCount;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;
using UHandleUtility = UnityEditor.HandleUtility;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Interactive Set Pivot action for Object mode.
    /// Allows user to hover over vertices and click to set pivot position.
    /// </summary>
    [ProBuilderPlusAction("setpivot_interactive", "Pivot",
        Tooltip = "Interactive pivot setting - hover over vertices or center to choose pivot position",
        Instructions = "Click vertex or center dots to set pivot position.",
        IconPath = "Icons/Old/CenterPivot", // Reuse ProBuilder's pivot icon
        ValidModes = ToolMode.Object,
        SupportsInstantMode = false,
        ObjectsSelectedRequired = 1,
        Order = 50)]
    public sealed class SetPivotInteractivePreviewAction : PreviewMenuAction
    {
        // Interactive state
        private ProBuilderMesh[] m_CachedMeshes;
        private Vector3[] m_ObjectCenters;
        private List<Vector3>[] m_VertexPositions;
        private bool[] m_IsHoveringCenter;
        private int[] m_HoveredVertexIndex;
        private Vector3[] m_SelectedPivotPositions;

        // Visual settings
        private const float CENTER_DOT_SIZE = 20f;
        private const float VERTEX_DOT_SIZE = 8f;
        private const float HOVER_DISTANCE = 15f; // pixels
        private const float AXIS_LENGTH = 1.5f;
        
        private static readonly Color DEFAULT_COLOR = Color.white;
        private static readonly Color HOVER_COLOR = Color.cyan;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            // Instructions are now handled by the framework via the Instructions property
            return root;
        }

        public override void StartPreview()
        {
            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_ObjectCenters = new Vector3[selection.Length];
            m_VertexPositions = new List<Vector3>[selection.Length];
            m_IsHoveringCenter = new bool[selection.Length];
            m_HoveredVertexIndex = new int[selection.Length];
            m_SelectedPivotPositions = new Vector3[selection.Length];

            // Initialize arrays
            for (int i = 0; i < selection.Length; i++)
            {
                m_IsHoveringCenter[i] = false;
                m_HoveredVertexIndex[i] = -1;
                m_SelectedPivotPositions[i] = Vector3.zero;
            }

            // Calculate positions for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                
                // Calculate object center (bounds center in world space)
                var bounds = mesh.GetComponent<Renderer>().bounds;
                m_ObjectCenters[i] = bounds.center;

                // Get all unique vertex positions in world space
                var vertices = mesh.GetVertices();
                var vertexPositions = new List<Vector3>();
                var processedPositions = new HashSet<Vector3>();

                foreach (var vertex in vertices)
                {
                    var worldPos = mesh.transform.TransformPoint(vertex.position);
                    // Round to avoid floating point precision issues for duplicate detection
                    var roundedPos = new Vector3(
                        Mathf.Round(worldPos.x * 1000f) / 1000f,
                        Mathf.Round(worldPos.y * 1000f) / 1000f,
                        Mathf.Round(worldPos.z * 1000f) / 1000f
                    );
                    
                    if (processedPositions.Add(roundedPos))
                    {
                        vertexPositions.Add(worldPos);
                    }
                }

                m_VertexPositions[i] = vertexPositions;

            }

            // Subscribe to scene GUI for interactive preview
            SceneView.duringSceneGui += OnSceneGUI;

        }

        public override void UpdatePreview()
        {
            // For this action, we just need to repaint to update visual feedback
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            
            // Check if any pivot positions were selected
            bool anyPivotSelected = false;
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                if (m_SelectedPivotPositions[i] != Vector3.zero)
                {
                    anyPivotSelected = true;
                    break;
                }
            }

            if (!anyPivotSelected)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No pivot position selected");
            }

            // Prepare undo - register both mesh and transform objects for undo
            var undoObjects = new List<Object>();
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                if (m_SelectedPivotPositions[i] != Vector3.zero)
                {
                    undoObjects.Add(m_CachedMeshes[i]);
                    undoObjects.Add(m_CachedMeshes[i].transform);
                }
            }

            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Set Pivot (Interactive)");

            int successCount = 0;
            
            // Apply pivot changes to each mesh that has a selected position
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var selectedWorldPos = m_SelectedPivotPositions[i];
                
                if (selectedWorldPos == Vector3.zero)
                {
                    continue;
                }

                // Convert world position to local space
                var selectedLocalPos = mesh.transform.InverseTransformPoint(selectedWorldPos);
                

                try
                {
                    // Use ProBuilder's SetPivot method which handles everything internally
                    mesh.SetPivot(selectedWorldPos);
                    
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
            m_CachedMeshes = null;
            m_ObjectCenters = null;
            m_VertexPositions = null;
            m_IsHoveringCenter = null;
            m_HoveredVertexIndex = null;
            m_SelectedPivotPositions = null;
            SceneView.RepaintAll();
            
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            HandleMouseEvents();
            DrawInteractiveElements();
            DrawAxisTripods();
        }

        private void HandleMouseEvents()
        {
            var evt = Event.current;
            var mousePos = evt.mousePosition;

            if (m_CachedMeshes == null) return;

            // Update hover states
            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null) continue;

                // Check hover over center
                var centerScreenPos = UHandleUtility.WorldToGUIPoint(m_ObjectCenters[meshIndex]);
                var centerDistance = Vector2.Distance(mousePos, centerScreenPos);
                m_IsHoveringCenter[meshIndex] = centerDistance <= HOVER_DISTANCE;

                // Check hover over vertices
                m_HoveredVertexIndex[meshIndex] = -1;
                var vertexPositions = m_VertexPositions[meshIndex];
                
                for (int vertexIndex = 0; vertexIndex < vertexPositions.Count; vertexIndex++)
                {
                    var vertexScreenPos = UHandleUtility.WorldToGUIPoint(vertexPositions[vertexIndex]);
                    var vertexDistance = Vector2.Distance(mousePos, vertexScreenPos);
                    
                    if (vertexDistance <= HOVER_DISTANCE)
                    {
                        m_HoveredVertexIndex[meshIndex] = vertexIndex;
                        break; // Take the first one found (closest in the list)
                    }
                }
            }

            // Handle mouse clicks
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                bool clickHandled = false;
                
                for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
                {
                    if (m_IsHoveringCenter[meshIndex])
                    {
                        // Clicked on center
                        m_SelectedPivotPositions[meshIndex] = m_ObjectCenters[meshIndex];
                        clickHandled = true;
                        break;
                    }
                    else if (m_HoveredVertexIndex[meshIndex] >= 0)
                    {
                        // Clicked on vertex
                        var vertexPos = m_VertexPositions[meshIndex][m_HoveredVertexIndex[meshIndex]];
                        m_SelectedPivotPositions[meshIndex] = vertexPos;
                        clickHandled = true;
                        break;
                    }
                }

                if (clickHandled)
                {
                    evt.Use();
                    SceneView.RepaintAll();
                }
            }

            // Repaint on mouse move to update hover states
            if (evt.type == EventType.MouseMove)
            {
                SceneView.RepaintAll();
            }
        }

        private void DrawInteractiveElements()
        {
            if (m_CachedMeshes == null) return;

            // Set z-test to always show elements on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null) continue;

                // Draw center dot
                var centerColor = m_IsHoveringCenter[meshIndex] ? HOVER_COLOR : DEFAULT_COLOR;
                Handles.color = centerColor;
                
                var centerPos = m_ObjectCenters[meshIndex];
                float centerSize = UHandleUtility.GetHandleSize(centerPos) * 0.12f; // 4x larger for better visibility
                Handles.SphereHandleCap(0, centerPos, Quaternion.identity, centerSize, EventType.Repaint);

                // Draw vertex dots
                var vertexPositions = m_VertexPositions[meshIndex];
                for (int vertexIndex = 0; vertexIndex < vertexPositions.Count; vertexIndex++)
                {
                    var vertexPos = vertexPositions[vertexIndex];
                    var isHovered = m_HoveredVertexIndex[meshIndex] == vertexIndex;
                    var vertexColor = isHovered ? HOVER_COLOR : DEFAULT_COLOR;
                    
                    Handles.color = vertexColor;
                    float vertexSize = UHandleUtility.GetHandleSize(vertexPos) * 0.06f; // 4x larger for better visibility
                    Handles.SphereHandleCap(0, vertexPos, Quaternion.identity, vertexSize, EventType.Repaint);
                }
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        private void DrawAxisTripods()
        {
            if (m_CachedMeshes == null || m_SelectedPivotPositions == null) return;

            // Set z-test to always show elements on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            var drawnPositions = new HashSet<Vector3>();

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var selectedPosition = m_SelectedPivotPositions[meshIndex];
                
                if (mesh == null || selectedPosition == Vector3.zero) continue;

                // Avoid drawing duplicate tripods at the same position
                var roundedPos = new Vector3(
                    Mathf.Round(selectedPosition.x * 1000f) / 1000f,
                    Mathf.Round(selectedPosition.y * 1000f) / 1000f,
                    Mathf.Round(selectedPosition.z * 1000f) / 1000f
                );

                if (drawnPositions.Add(roundedPos))
                {
                    DrawAxisTripod(selectedPosition, mesh.transform.rotation);
                }
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        private void DrawAxisTripod(Vector3 position, Quaternion rotation)
        {
            float handleSize = UHandleUtility.GetHandleSize(position);
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
}

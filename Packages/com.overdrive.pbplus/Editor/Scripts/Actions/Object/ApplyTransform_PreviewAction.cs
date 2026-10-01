using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;
using UHandleUtility = UnityEditor.HandleUtility;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Apply Transform action with selective Position, Rotation, Scale application.
    /// Shows preview of final transform state before applying.
    /// </summary>
    [ProBuilderPlusAction("apply_transform", "Apply Transform",
        Tooltip = "Selectively apply transform components (Position, Rotation, Scale) with preview",
        Instructions = "Select which transform components to apply and click Apply to confirm.",
        IconPath = "Icons/Old/Freeze_Transform",
        ValidModes = ToolMode.Object,
        ObjectsSelectedRequired = 1,
        Order = 51)]
    public sealed class ApplyTransformPreviewAction : PreviewMenuAction
    {
        // Settings
        private bool m_ApplyPosition = true;
        private bool m_ApplyRotation = true;
        private bool m_ApplyScale = true;

        // Cached data
        private ProBuilderMesh[] m_CachedMeshes;

        // Visual settings
        private const float AXIS_LENGTH = 2.0f;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_ApplyPosition = UserPreferences.Tools.ApplyPosition;
            m_ApplyRotation = UserPreferences.Tools.ApplyRotation;
            m_ApplyScale = UserPreferences.Tools.ApplyScale;

            var positionToggle = new Toggle("Apply Position") { value = m_ApplyPosition };
            positionToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyPosition = evt.newValue;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.ApplyPosition = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(positionToggle);

            var rotationToggle = new Toggle("Apply Rotation") { value = m_ApplyRotation };
            rotationToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyRotation = evt.newValue;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.ApplyRotation = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(rotationToggle);

            var scaleToggle = new Toggle("Apply Scale") { value = m_ApplyScale };
            scaleToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyScale = evt.newValue;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.ApplyScale = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(scaleToggle);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_ApplyPosition = Overdrive.ProBuilderPlus.UserPreferences.Tools.ApplyPosition;
            m_ApplyRotation = Overdrive.ProBuilderPlus.UserPreferences.Tools.ApplyRotation;
            m_ApplyScale = Overdrive.ProBuilderPlus.UserPreferences.Tools.ApplyScale;

            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;

            // Subscribe to scene GUI for preview visualization
            SceneView.duringSceneGui += OnSceneGUI;

        }

        public override void UpdatePreview()
        {
            // Repaint to update visual feedback based on checkbox changes
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            
            // Check if any components are selected for application
            if (!m_ApplyPosition && !m_ApplyRotation && !m_ApplyScale)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No transform components selected");
            }

            // Prepare undo - register both mesh and transform objects for undo
            var undoObjects = new List<Object>();
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                undoObjects.Add(m_CachedMeshes[i]);
                undoObjects.Add(m_CachedMeshes[i].transform);
            }

            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Apply Transform");

            int successCount = 0;
            
            // Apply transform changes to each mesh
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                

                try
                {
                    // Apply each transform component separately in the correct order
                    if (m_ApplyScale)
                    {
                        ApplyScaleTransform(mesh);
                    }
                    
                    if (m_ApplyRotation)
                    {
                        ApplyRotationTransform(mesh);
                    }
                    
                    if (m_ApplyPosition)
                    {
                        ApplyPositionTransform(mesh);
                    }

                    // Update the mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                    mesh.Optimize();
                    
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to apply transform to '{mesh.name}': {ex.Message}");
                }
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();
            SceneView.RepaintAll();

            // Return result
            if (successCount > 0)
            {
                var components = new List<string>();
                if (m_ApplyPosition) components.Add("Position");
                if (m_ApplyRotation) components.Add("Rotation");
                if (m_ApplyScale) components.Add("Scale");
                
                string componentsList = string.Join(", ", components);
                string message = successCount == 1 ? 
                    $"Applied {componentsList} to 1 object" : 
                    $"Applied {componentsList} to {successCount} objects";
                    
                return new ActionResult(ActionResult.Status.Success, message);
            }
            else
            {
                return new ActionResult(ActionResult.Status.Failure, "Failed to apply transform to any objects");
            }
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            SceneView.RepaintAll();
            
        }

        private void ApplyScaleTransform(ProBuilderMesh mesh)
        {
            
            // Get current vertices in local space
            var vertices = mesh.GetVertices();
            var transform = mesh.transform;
            var currentScale = transform.localScale;
            
            // Check if we need to flip faces due to negative scale
            bool flipFaces = ShouldFlipFaces(currentScale);
            
            // Convert each vertex to world space, then back to local space with unit scale
            for (int i = 0; i < vertices.Length; i++)
            {
                // Transform vertex to world space using current transform
                Vector3 worldPos = transform.TransformPoint(vertices[i].position);
                
                // Convert back to local space with only position and rotation (scale = one)
                Matrix4x4 newTransformMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Vector3 newLocalPos = newTransformMatrix.inverse.MultiplyPoint3x4(worldPos);
                
                vertices[i].position = newLocalPos;
            }
            
            // Set vertices back to mesh
            mesh.SetVertices(vertices);
            
            // Reset the transform scale
            mesh.transform.localScale = Vector3.one;
            
            // Handle face flipping if needed
            if (flipFaces)
            {
                foreach (Face face in mesh.faces)
                {
                    face.manualUV = true;
                    face.Reverse();
                }
            }
        }

        private void ApplyRotationTransform(ProBuilderMesh mesh)
        {
            
            // Get current vertices in local space
            var vertices = mesh.GetVertices();
            var transform = mesh.transform;
            
            // Convert each vertex to world space, then back to local space with identity rotation
            for (int i = 0; i < vertices.Length; i++)
            {
                // Transform vertex to world space using current transform
                Vector3 worldPos = transform.TransformPoint(vertices[i].position);
                
                // Convert back to local space with only position and scale (rotation = identity)
                Matrix4x4 newTransformMatrix = Matrix4x4.TRS(transform.position, Quaternion.identity, transform.localScale);
                Vector3 newLocalPos = newTransformMatrix.inverse.MultiplyPoint3x4(worldPos);
                
                vertices[i].position = newLocalPos;
            }
            
            // Set vertices back to mesh
            mesh.SetVertices(vertices);
            
            // Reset the transform rotation
            mesh.transform.rotation = Quaternion.identity;
        }

        private void ApplyPositionTransform(ProBuilderMesh mesh)
        {
            
            // Get current vertices in local space
            var vertices = mesh.GetVertices();
            var transform = mesh.transform;
            
            // Convert each vertex to world space, then back to local space with zero position
            // This properly handles arbitrary pivot positions
            for (int i = 0; i < vertices.Length; i++)
            {
                // Transform vertex to world space using current transform
                Vector3 worldPos = transform.TransformPoint(vertices[i].position);
                
                // Convert back to local space with only rotation and scale (position = zero)
                Matrix4x4 newTransformMatrix = Matrix4x4.TRS(Vector3.zero, transform.rotation, transform.localScale);
                Vector3 newLocalPos = newTransformMatrix.inverse.MultiplyPoint3x4(worldPos);
                
                vertices[i].position = newLocalPos;
            }
            
            // Set vertices back to mesh
            mesh.SetVertices(vertices);
            
            // Reset the transform position
            mesh.transform.position = Vector3.zero;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawAxisTripods();
        }

        private void DrawAxisTripods()
        {
            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0) return;

            // Only draw if rotation or scale will be applied (otherwise tripods don't make sense)
            if (!m_ApplyRotation && !m_ApplyScale) return;

            // Set z-test to always show elements on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            var tripodPositions = new HashSet<Vector3>();

            foreach (var mesh in m_CachedMeshes)
            {
                if (mesh == null) continue;

                Vector3 tripodPosition;
                Quaternion tripodRotation;

                if (m_ApplyPosition)
                {
                    // If position will be applied, show tripod at world origin
                    tripodPosition = Vector3.zero;
                    tripodRotation = m_ApplyRotation ? Quaternion.identity : mesh.transform.rotation;
                }
                else
                {
                    // If position won't be applied, show tripod at current object position
                    tripodPosition = mesh.transform.position;
                    tripodRotation = m_ApplyRotation ? Quaternion.identity : mesh.transform.rotation;
                }

                // Avoid drawing duplicate tripods at the same position/rotation
                var tripodKey = new Vector3(
                    Mathf.Round(tripodPosition.x * 1000f) / 1000f,
                    Mathf.Round(tripodPosition.y * 1000f) / 1000f,
                    Mathf.Round(tripodPosition.z * 1000f) / 1000f
                );

                if (tripodPositions.Add(tripodKey))
                {
                    DrawAxisTripod(tripodPosition, tripodRotation);
                }
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        private void DrawAxisTripod(Vector3 position, Quaternion rotation)
        {
            float handleSize = UHandleUtility.GetHandleSize(position);
            float axisLength = handleSize * AXIS_LENGTH;

            if (m_ApplyScale)
            {
                // If scale will be applied, use orange for all axes
                Handles.color = Color.HSVToRGB(0.08f, 1f, 1f); // Orange
                
                // Draw all three axes in orange
                Vector3 right = rotation * Vector3.right;
                Vector3 up = rotation * Vector3.up;
                Vector3 forward = rotation * Vector3.forward;
                
                Handles.DrawLine(position, position + right * axisLength);
                Handles.DrawLine(position, position + up * axisLength);
                Handles.DrawLine(position, position + forward * axisLength);
            }
            else
            {
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

        private bool ShouldFlipFaces(Vector3 scale)
        {
            var globalSign = Mathf.Sign(scale.x) * Mathf.Sign(scale.y) * Mathf.Sign(scale.z);
            return globalSign < 0;
        }
    }
}

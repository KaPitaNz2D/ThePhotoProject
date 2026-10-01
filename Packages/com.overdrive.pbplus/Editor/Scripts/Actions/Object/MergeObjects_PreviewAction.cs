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
    /// Merge Objects action with live wireframe preview and pivot selection.
    /// Shows preview of merge result with selectable pivot object.
    /// </summary>
    [ProBuilderPlusAction("merge_objects_preview", "Merge",
        Tooltip = "Merge objects with live preview - select which object's transform to use as pivot",
        Instructions = "Select which object's transform to use as pivot and click Apply to merge.",
        IconPath = "Icons/Old/Object_Merge",
        ValidModes = ToolMode.Object,
        ObjectsSelectedRequired = 1,
        Order = 50)]
    public sealed class MergeObjectsPreviewAction : PreviewMenuAction
    {
        // Settings
        private ProBuilderMesh m_PivotMesh; // Which object's transform to use

        // Preview state
        private ProBuilderMesh[] m_CachedMeshes;
        private Edge[][] m_MeshEdges;              // Edges for each mesh
        
        // Visual settings
        private static readonly Color PIVOT_COLOR = Color.cyan;        // Pivot object wireframe
        private static readonly Color MERGE_COLOR = Color.white;       // Objects to merge wireframe
        private const float WIREFRAME_THICKNESS = 2.0f;
        private const float AXIS_LENGTH = 2.0f;

        // UI elements
        private DropdownField m_PivotDropdown;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            var objectNames = GetObjectNames();
            var currentPivotName = m_PivotMesh != null ? m_PivotMesh.gameObject.name :
                                  (objectNames.Count > 0 ? objectNames[0] : "No objects");

            m_PivotDropdown = new DropdownField("Pivot Object", objectNames, currentPivotName);
            m_PivotDropdown.RegisterValueChangedCallback(evt =>
            {
                var selectedMesh = m_CachedMeshes?.FirstOrDefault(mesh => mesh.gameObject.name == evt.newValue);
                if (selectedMesh != null && selectedMesh != m_PivotMesh)
                {
                    m_PivotMesh = selectedMesh;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(m_PivotDropdown);

            return root;
        }

        public override void StartPreview()
        {
            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_MeshEdges = new Edge[selection.Length][];

            // Set initial pivot to active mesh or first selection
            m_PivotMesh = MeshSelection.activeMesh ?? selection[0];

            // Cache mesh edges for wireframe drawing
            for (int i = 0; i < selection.Length; i++)
            {
                m_MeshEdges[i] = GetMeshEdges(selection[i]);
            }

            // Update dropdown if it exists
            if (m_PivotDropdown != null)
            {
                var objectNames = GetObjectNames();
                m_PivotDropdown.choices = objectNames;
                m_PivotDropdown.value = m_PivotMesh.gameObject.name;
            }

            // Subscribe to scene GUI for preview visualization and interaction
            SceneView.duringSceneGui += OnSceneGUI;

        }

        public override void UpdatePreview()
        {
            // Update dropdown to reflect current pivot
            if (m_PivotDropdown != null && m_PivotMesh != null)
            {
                m_PivotDropdown.SetValueWithoutNotify(m_PivotMesh.gameObject.name);
            }

            // Repaint to show updated preview
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            
            if (m_CachedMeshes == null || m_CachedMeshes.Length < 2)
            {
                return new ActionResult(ActionResult.Status.Canceled, "Need at least 2 objects to merge");
            }

            if (m_PivotMesh == null)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No pivot object selected");
            }

            try
            {
                // Use the original MergeObjects implementation but with our selected pivot
                var selected = m_CachedMeshes;
                var currentMesh = m_PivotMesh; // Use our selected pivot instead of activeMesh
                
                Undo.RecordObject(currentMesh, "Merge Objects");
                
                // Convert to the internal selection format expected by CombineMeshes.Combine
                var internalSelection = selected.ToList();
                
                List<ProBuilderMesh> result = CombineMeshes.Combine(internalSelection, currentMesh);

                if (result != null && result.Count > 0)
                {
                    foreach (var mesh in result)
                    {
                        mesh.Optimize();
                        if (mesh != currentMesh)
                        {
                            mesh.gameObject.name = m_PivotMesh.gameObject.name + "-Merged";
                            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Merge Objects");
                        }
                        
                        // Remove PolyShape and ProBuilderShape components if any are present post-merge
                        var polyShapeComp = mesh.gameObject.GetComponent<PolyShape>();
                        if (polyShapeComp != null)
                            Undo.DestroyObjectImmediate(polyShapeComp);
                        
                        // Try to remove ProBuilderShape using reflection since it's internal
                        var proBuilderShapeComp = mesh.gameObject.GetComponent("ProBuilderShape");
                        if (proBuilderShapeComp != null)
                            Undo.DestroyObjectImmediate(proBuilderShapeComp);
                    }

                    // Delete donor objects if they are not part of the result
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (selected[i] != null && result.Contains(selected[i]) == false)
                            Undo.DestroyObjectImmediate(selected[i].gameObject);
                    }

                    // Update selection to the result
                    Selection.objects = result.Select(x => x.gameObject).ToArray();
                }

                // Refresh ProBuilder
                ProBuilderEditor.Refresh();

                return new ActionResult(ActionResult.Status.Success, $"Merged {selected.Length} objects");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to merge objects: {ex.Message}");
                return new ActionResult(ActionResult.Status.Failure, $"Failed to merge objects: {ex.Message}");
            }
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_MeshEdges = null;
            m_PivotMesh = null;
            m_PivotDropdown = null;
            SceneView.RepaintAll();
            
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawWireframePreviews();
            DrawAxisTripod();
        }

        private void DrawWireframePreviews()
        {
            if (m_CachedMeshes == null || m_MeshEdges == null) return;

            // Set z-test to always show wireframes on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null) continue;

                var edges = m_MeshEdges[meshIndex];
                
                // Use different colors for pivot vs merge objects
                Handles.color = (mesh == m_PivotMesh) ? PIVOT_COLOR : MERGE_COLOR;
                
                DrawWireframeForMesh(mesh, edges);
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        private void DrawWireframeForMesh(ProBuilderMesh mesh, Edge[] edges)
        {
            var vertices = mesh.GetVertices();
            var transform = mesh.transform;

            foreach (var edge in edges)
            {
                if (edge.a < vertices.Length && edge.b < vertices.Length)
                {
                    Vector3 worldPosA = transform.TransformPoint(vertices[edge.a].position);
                    Vector3 worldPosB = transform.TransformPoint(vertices[edge.b].position);
                    Handles.DrawLine(worldPosA, worldPosB, WIREFRAME_THICKNESS);
                }
            }
        }

        private void DrawAxisTripod()
        {
            if (m_PivotMesh == null) return;

            // Set z-test to always show tripod on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            Vector3 position = m_PivotMesh.transform.position;
            Quaternion rotation = m_PivotMesh.transform.rotation;

            float handleSize = UHandleUtility.GetHandleSize(position);
            float axisLength = handleSize * AXIS_LENGTH;

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

            // Reset handles color
            Handles.color = Color.white;
        }

        /// <summary>
        /// Get all edges from all faces in the mesh for wireframe drawing
        /// </summary>
        private Edge[] GetMeshEdges(ProBuilderMesh mesh)
        {
            var edges = new List<Edge>();
            var addedEdges = new HashSet<Edge>();

            // Collect all edges from all faces
            foreach (var face in mesh.faces)
            {
                var faceEdges = face.edges;
                foreach (var edge in faceEdges)
                {
                    // Avoid duplicate edges (since faces share edges)
                    if (addedEdges.Add(edge))
                    {
                        edges.Add(edge);
                    }
                }
            }

            return edges.ToArray();
        }

        private List<string> GetObjectNames()
        {
            // If we have cached meshes, use those
            if (m_CachedMeshes != null)
                return m_CachedMeshes.Select(mesh => mesh.gameObject.name).ToList();
            
            // Otherwise, use current selection (for when CreateSettingsContent is called before StartPreview)
            var selection = MeshSelection.top.ToArray();
            if (selection.Length > 0)
                return selection.Select(mesh => mesh.gameObject.name).ToList();
            
            // Fallback to empty list
            return new List<string>();
        }
    }
}

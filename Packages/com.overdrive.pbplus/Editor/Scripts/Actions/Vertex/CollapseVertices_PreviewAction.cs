using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: Replace "First" with "Active" functionality, so user can choose which vertex to collapse to

namespace Overdrive.ProBuilderPlus
{
    [ProBuilderPlusAction("collapse_vertices_preview", "Collapse",
        Tooltip = "Collapse selected vertices to a single point with live preview",
        Instructions = "Collapse vertices to single point (orange target, cyan lines)",
        IconPath = "Icons/Old/Vert_Collapse",
        ValidModes = ToolMode.Vertex,
        VerticesSelectedRequired = 2,
        Order = 160)]
    public sealed class CollapseVerticesPreviewAction : PreviewMenuAction
    {
        // Settings
        private bool m_CollapseToFirst;

        // Cached data for applying changes
        private ProBuilderMesh[] m_CachedMeshes;
        private int[][] m_CachedVertices;
        private Vector3[] m_PreviewCollapsePositions;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_CollapseToFirst = UserPreferences.Tools.CollapseToFirst;

            // Instructions are now handled by the framework via the Instructions attribute

            // Collapse to First toggle
            var collapseToggle = new Toggle("Collapse to First");
            collapseToggle.tooltip = "If enabled, collapse to the first selected vertex position. If disabled, collapse to the average position of all selected vertices.";
            collapseToggle.SetValueWithoutNotify(m_CollapseToFirst);
            collapseToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_CollapseToFirst != evt.newValue)
                {
                    m_CollapseToFirst = evt.newValue;
                    UserPreferences.Tools.CollapseToFirst = evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(collapseToggle);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
                m_CollapseToFirst = UserPreferences.Tools.CollapseToFirst;

            CacheCurrentSelection();
            UpdatePreview();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedVertices = new int[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null)
                {
                    var selectedVertices = selection[i].selectedVertices;
                    m_CachedVertices[i] = selectedVertices.ToArray();
                }
                else
                {
                    m_CachedVertices[i] = new int[0];
                }
            }
        }

        internal override void OnSelectionChangedDuringPreview()
        {
            CacheCurrentSelection();
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            try
            {
                CalculateCollapsePositions();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating collapse vertices preview: {ex.Message}");
            }
        }

        private void CalculateCollapsePositions()
        {
            if (m_CachedMeshes == null) return;

            m_PreviewCollapsePositions = new Vector3[m_CachedMeshes.Length];

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];

                if (mesh == null || vertices == null || vertices.Length < 2)
                {
                    m_PreviewCollapsePositions[i] = Vector3.zero;
                    continue;
                }

                var meshVertices = mesh.GetVertices();

                // Calculate collapse position based on setting
                Vector3 collapsePosition;
                if (m_CollapseToFirst && vertices.Length > 0 && vertices[0] < meshVertices.Length)
                {
                    // Collapse to first selected vertex
                    collapsePosition = meshVertices[vertices[0]].position;
                }
                else
                {
                    // Collapse to average position
                    Vector3 averagePosition = Vector3.zero;
                    int validVertexCount = 0;

                    foreach (var vertexIndex in vertices)
                    {
                        if (vertexIndex < meshVertices.Length)
                        {
                            averagePosition += meshVertices[vertexIndex].position;
                            validVertexCount++;
                        }
                    }

                    if (validVertexCount > 0)
                        collapsePosition = averagePosition / validVertexCount;
                    else
                        collapsePosition = Vector3.zero;
                }

                m_PreviewCollapsePositions[i] = collapsePosition;
            }
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached vertex data available");
            }

            Undo.RecordObjects(m_CachedMeshes, "Collapse Vertices");

            int successCount = 0;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];

                if (mesh == null || vertices == null || vertices.Length < 2)
                    continue;

                try
                {
                    mesh.ToMesh();
                    int newIndex = mesh.MergeVertices(vertices, m_CollapseToFirst);

                    if (newIndex > -1)
                    {
                        mesh.SetSelectedVertices(new int[] { newIndex });
                        mesh.ToMesh();
                        mesh.Refresh();
                        mesh.Optimize();
                        successCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to collapse vertices on mesh {i}: {ex.Message}");
                }
            }

            ProBuilderEditor.Refresh();

            if (successCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Collapsed vertices on {successCount} mesh(es)");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to collapse vertices");
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedVertices = null;
            m_PreviewCollapsePositions = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_PreviewCollapsePositions == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];
                var collapsePosition = m_PreviewCollapsePositions[i];

                if (mesh == null || vertices == null || vertices.Length < 2) continue;

                var meshVertices = mesh.GetVertices();
                var worldCollapsePosition = mesh.transform.TransformPoint(collapsePosition);

                // Draw cyan lines from each selected vertex to the collapse position
                Handles.color = Color.cyan;
                foreach (var vertexIndex in vertices)
                {
                    if (vertexIndex < meshVertices.Length)
                    {
                        var worldVertexPosition = mesh.transform.TransformPoint(meshVertices[vertexIndex].position);
                        Handles.DrawAAPolyLine(2f, worldVertexPosition, worldCollapsePosition);
                    }
                }

                // Draw orange dot at the collapse position
                Handles.color = new Color(1f, 0.5f, 0f); // Orange
                Handles.SphereHandleCap(0, worldCollapsePosition, Quaternion.identity,
                    UnityEditor.HandleUtility.GetHandleSize(worldCollapsePosition) * 0.15f, EventType.Repaint);
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: (BUG) Weld distance isn't working
// TODO: Visuals are poor - need better way to show which vertices will be welded and where
// TODO: Option to weld all, regardless of selection
// TODO: Option to weld to exact vertex per group, instead of average

namespace Overdrive.ProBuilderPlus
{
    [ProBuilderPlusAction("weld_vertices_preview", "Weld",
        Tooltip = "Weld selected vertices within a specified distance with live preview",
        Instructions = "Weld selected vertices within a specified distance (orange highlights)",
        IconPath = "Icons/Old/Vert_Weld",
        ValidModes = ToolMode.Vertex,
        VerticesSelectedRequired = 1,
        Order = 150)]
    public sealed class WeldVerticesPreviewAction : PreviewMenuAction
    {
        private const float k_MinWeldDistance = 0.00001f;

        // Cached data for applying changes
        private ProBuilderMesh[] m_CachedMeshes;

        private int[][] m_CachedVertices;

        private List<WeldGroup>[] m_PreviewWeldGroups;

        // Settings
        private float m_WeldDistance;
        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached vertex data available");
            }

            Undo.RecordObjects(m_CachedMeshes, "Weld Vertices");

            int successCount = 0;
            int totalWeldCount = 0;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];

                if (mesh == null || vertices == null || vertices.Length < 2)
                    continue;

                try
                {
                    int initialSharedVertexCount = mesh.sharedVertices.Count;

                    mesh.ToMesh();
                    int[] newVertices = mesh.WeldVertices(vertices, m_WeldDistance);

                    if (newVertices != null)
                    {
                        // Calculate how many vertices were welded
                        int finalSharedVertexCount = mesh.sharedVertices.Count;
                        int weldedCount = initialSharedVertexCount - finalSharedVertexCount;
                        totalWeldCount += weldedCount;

                        // Handle degenerate triangles (copied from original implementation)
                        var selectedVertices = mesh.GetCoincidentVertices(mesh.selectedVertices);
                        var newSelection = newVertices ?? new int[0];

                        if (MeshValidation.ContainsDegenerateTriangles(mesh))
                        {
                            List<int> removedIndices = new List<int>();
                            var vertexCount = mesh.vertexCount;

                            if (MeshValidation.RemoveDegenerateTriangles(mesh, removedIndices))
                            {
                                if (removedIndices.Count < vertexCount)
                                {
                                    var newlySelectedVertices = new List<int>();
                                    selectedVertices.Sort();
                                    removedIndices.Sort();

                                    int count = 0;

                                    for (int j = 0; j < selectedVertices.Count; j++)
                                    {
                                        if (count >= removedIndices.Count || selectedVertices[j] != removedIndices[count])
                                        {
                                            newlySelectedVertices.Add(selectedVertices[j] - NearestIndexPriorToValue(removedIndices, selectedVertices[j]) - 1);
                                        }
                                        else
                                        {
                                            ++count;
                                        }
                                    }

                                    newSelection = newlySelectedVertices.ToArray();
                                }
                                else
                                {
                                    newSelection = new int[0];
                                }
                            }
                            mesh.ToMesh();
                        }

                        mesh.SetSelectedVertices(newSelection);
                        mesh.Refresh();
                        mesh.Optimize();
                        successCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to weld vertices on mesh {i}: {ex.Message}");
                }
            }

            ProBuilderEditor.Refresh();

            if (successCount > 0 && totalWeldCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Welded {totalWeldCount} vertices on {successCount} mesh(es)");
            else if (successCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Processed {successCount} mesh(es) - no vertices needed welding");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to weld vertices");
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedVertices = null;
            m_PreviewWeldGroups = null;
            SceneView.RepaintAll();
        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_WeldDistance = UserPreferences.Tools.WeldDistance;

            // Instructions are now handled by the framework via the Instructions attribute

            // Weld Distance
            var distanceField = new FloatField("Distance");
            distanceField.tooltip = "Exact weld distance value";
            distanceField.SetValueWithoutNotify(m_WeldDistance);
            distanceField.RegisterValueChangedCallback(evt =>
            {
                float newValue = Mathf.Max(k_MinWeldDistance, evt.newValue);
                if (m_WeldDistance != newValue)
                {
                    m_WeldDistance = newValue;
                    UserPreferences.Tools.WeldDistance = newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            root.Add(distanceField);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
                m_WeldDistance = UserPreferences.Tools.WeldDistance;

            CacheCurrentSelection();
            UpdatePreview();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public override void UpdatePreview()
        {
            try
            {
                CalculateWeldGroups();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating weld vertices preview: {ex.Message}");
            }
        }

        internal override void OnSelectionChangedDuringPreview()
        {
            CacheCurrentSelection();
            UpdatePreview();
        }

        /// <summary>
        /// Helper function to replace the inaccessible UnityEngine.ProBuilder.ArrayUtility.NearestIndexPriorToValue
        /// Returns the number of indices in the sorted array that are less than the target value
        /// </summary>
        private static int NearestIndexPriorToValue(List<int> sortedArray, int targetValue)
        {
            int count = 0;
            foreach (int value in sortedArray)
            {
                if (value < targetValue)
                    count++;
                else
                    break;
            }
            return count;
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

        private void CalculateWeldGroups()
        {
            if (m_CachedMeshes == null) return;

            m_PreviewWeldGroups = new List<WeldGroup>[m_CachedMeshes.Length];

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];

                if (mesh == null || vertices == null || vertices.Length < 2)
                {
                    m_PreviewWeldGroups[i] = new List<WeldGroup>();
                    continue;
                }

                var weldGroups = new List<WeldGroup>();
                var meshVertices = mesh.GetVertices();
                var processedVertices = new HashSet<int>();

                // Simple distance-based grouping (simplified version of the KdTree algorithm)
                foreach (var vertexIndex in vertices)
                {
                    if (processedVertices.Contains(vertexIndex) || vertexIndex >= meshVertices.Length)
                        continue;

                    var group = new List<int> { vertexIndex };
                    var basePosition = meshVertices[vertexIndex].position;
                    var totalPosition = basePosition;

                    processedVertices.Add(vertexIndex);

                    // Find all other vertices within weld distance
                    foreach (var otherVertexIndex in vertices)
                    {
                        if (processedVertices.Contains(otherVertexIndex) || otherVertexIndex >= meshVertices.Length)
                            continue;

                        var otherPosition = meshVertices[otherVertexIndex].position;
                        var distance = Vector3.Distance(basePosition, otherPosition);

                        if (distance <= m_WeldDistance)
                        {
                            group.Add(otherVertexIndex);
                            totalPosition += otherPosition;
                            processedVertices.Add(otherVertexIndex);
                        }
                    }

                    // Only create groups with more than one vertex
                    if (group.Count > 1)
                    {
                        var averagePosition = totalPosition / group.Count;
                        weldGroups.Add(new WeldGroup(group, averagePosition));
                    }
                }

                m_PreviewWeldGroups[i] = weldGroups;
            }
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_PreviewWeldGroups == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = new Color(1f, 0.5f, 0f); // Orange

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var weldGroups = m_PreviewWeldGroups[i];

                if (mesh == null || weldGroups == null || weldGroups.Count == 0) continue;

                var meshVertices = mesh.GetVertices();

                // Draw orange dots at each vertex that will be welded
                foreach (var group in weldGroups)
                {
                    if (!group.isValidGroup) continue;

                    foreach (var vertexIndex in group.vertexIndices)
                    {
                        if (vertexIndex < meshVertices.Length)
                        {
                            var worldPos = mesh.transform.TransformPoint(meshVertices[vertexIndex].position);
                            Handles.SphereHandleCap(0, worldPos, Quaternion.identity, UnityEditor.HandleUtility.GetHandleSize(worldPos) * 0.15f, EventType.Repaint);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Represents a group of vertices that will be welded together
        /// </summary>
        public struct WeldGroup
        {
            public Vector3 averagePosition;
            public bool isValidGroup;
            public List<int> vertexIndices;
            public WeldGroup(List<int> indices, Vector3 avgPos)
            {
                vertexIndices = indices;
                averagePosition = avgPos;
                isValidGroup = indices.Count > 1;
            }
        }
    }
}
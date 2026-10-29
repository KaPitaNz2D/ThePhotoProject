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
    [ProBuilderPlusAction("connect_vertices_preview", "Connect",
        Tooltip = "Connect selected vertices with live preview",
        Instructions = "Connect selected vertices with new edges (cyan)",
        IconPath = "Icons/Old/Vert_Connect",
        ValidModes = ToolMode.Vertex,
        VerticesSelectedRequired = 2,
        Order = 140)]
    public sealed class ConnectVerticesPreviewAction : PreviewMenuAction
    {
        // Cached data for applying changes
        private ProBuilderMesh[] m_CachedMeshes;
        private int[][] m_CachedVertices;
        private List<Edge>[] m_PreviewConnections;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            // Instructions are now handled by the framework via the Instructions attribute
            return root;
        }

        public override void StartPreview()
        {
            CacheCurrentSelection();
            UpdatePreview();
            SceneView.duringSceneGui += OnSceneGUI;
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
                CalculatePreviewConnections();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating connect vertices preview: {ex.Message}");
            }
        }

        private void CalculatePreviewConnections()
        {
            if (m_CachedMeshes == null) return;

            m_PreviewConnections = new List<Edge>[m_CachedMeshes.Length];

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];

                if (mesh == null || vertices == null || vertices.Length < 2)
                {
                    m_PreviewConnections[i] = new List<Edge>();
                    continue;
                }

                var connections = new List<Edge>();

                // For preview, we'll show connections between all selected vertices within each face
                // This gives a visual indication of what the Connect operation will do
                var sharedVertices = mesh.sharedVertices;
                var selectedSharedIndices = new HashSet<int>();

                // Find all shared vertex indices for selected vertices
                foreach (var vertexIndex in vertices)
                {
                    foreach (var sharedGroup in sharedVertices)
                    {
                        if (sharedGroup.Contains(vertexIndex))
                        {
                            foreach (var coincidentIndex in sharedGroup)
                            {
                                selectedSharedIndices.Add(coincidentIndex);
                            }
                            break;
                        }
                    }
                }

                // For each face, find selected vertices and show connections between them
                foreach (Face face in mesh.faces)
                {
                    var faceVertices = new List<int>();

                    foreach (var vertexIndex in face.distinctIndexes)
                    {
                        if (selectedSharedIndices.Contains(vertexIndex))
                        {
                            faceVertices.Add(vertexIndex);
                        }
                    }

                    // If this face has 2+ selected vertices, show connections between them
                    if (faceVertices.Count >= 2)
                    {
                        for (int j = 0; j < faceVertices.Count; j++)
                        {
                            for (int k = j + 1; k < faceVertices.Count; k++)
                            {
                                connections.Add(new Edge(faceVertices[j], faceVertices[k]));
                            }
                        }
                    }
                }

                m_PreviewConnections[i] = connections;
            }
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached vertex data available");
            }

            Undo.RecordObjects(m_CachedMeshes, "Connect Vertices");

            int successCount = 0;
            var allNewVertices = new List<int>();
            var meshesWithNewVertices = new List<ProBuilderMesh>();

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var vertices = m_CachedVertices[i];

                if (mesh == null || vertices == null || vertices.Length < 2)
                    continue;

                try
                {
                    mesh.ToMesh();
                    int[] newVertices = mesh.Connect(vertices);

                    if (newVertices != null && newVertices.Length > 0)
                    {
                        mesh.Refresh();
                        mesh.Optimize();
                        successCount++;

                        // Collect new vertices for selection
                        allNewVertices.AddRange(newVertices);
                        meshesWithNewVertices.Add(mesh);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to connect vertices on mesh {i}: {ex.Message}");
                }
            }

            // Select the new vertices
            if (allNewVertices.Count > 0 && meshesWithNewVertices.Count > 0)
            {
                // Clear current element selection but keep object selection
                MeshSelection.ClearElementSelection();

                // Select the new vertices
                foreach (var mesh in meshesWithNewVertices)
                {
                    var meshSpecificVertices = allNewVertices.Where(v => IsVertexInMesh(mesh, v)).ToArray();
                    if (meshSpecificVertices.Length > 0)
                    {
                        mesh.SetSelectedVertices(meshSpecificVertices);
                    }
                }

            }

            ProBuilderEditor.Refresh();

            if (successCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Connected vertices on {successCount} mesh(es)");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to connect vertices");
        }

        private bool IsVertexInMesh(ProBuilderMesh mesh, int vertexIndex)
        {
            var vertices = mesh.GetVertices();
            return vertexIndex < vertices.Length;
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedVertices = null;
            m_PreviewConnections = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_PreviewConnections == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var connections = m_PreviewConnections[i];

                if (mesh == null || connections == null || connections.Count == 0) continue;

                var vertices = mesh.GetVertices();

                // Draw preview connections in cyan
                Handles.color = Color.cyan;
                foreach (var edge in connections)
                {
                    if (edge.a < vertices.Length && edge.b < vertices.Length)
                    {
                        var startPos = mesh.transform.TransformPoint(vertices[edge.a].position);
                        var endPos = mesh.transform.TransformPoint(vertices[edge.b].position);
                        Handles.DrawAAPolyLine(3f, startPos, endPos);
                    }
                }
            }
        }
    }
}
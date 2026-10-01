using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: Add Direction options like Face Extrude (Normal, X Axis, Y Axis, Z Axis, Local vs Global)

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// ExtrudeEdges preview action that shows cyan lines indicating the new edge geometry that will be created.
    /// Supports both individual edge extrusion and grouped edge extrusion.
    /// </summary>
    [ProBuilderPlusAction("extrude_edges_preview", "Extrude",
        Tooltip = "Extrude edges with live preview",
        Instructions = "Extrude selected edges (cyan shows new geometry)",
        IconPath = "Icons/Old/Edge_Extrude",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 100)]
    public sealed class ExtrudeEdgesPreviewAction : PreviewMenuAction
    {
        // Settings matching original ExtrudeEdges - mirror ProBuilder's settings
        private float m_ExtrudeDistance;
        private bool m_ExtrudeAsGroup;

        // Preview visualization data
        private List<(Vector3, Vector3)> m_NewEdges; // Edges of the new geometry
        private ProBuilderMesh[] m_CachedMeshes;
        private Edge[][] m_CachedEdges;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_ExtrudeDistance = Overdrive.ProBuilderPlus.UserPreferences.Tools.ExtrudeDistance;
            m_ExtrudeAsGroup = Overdrive.ProBuilderPlus.UserPreferences.Tools.ExtrudeAsGroup;

            // Instructions are now handled by the framework via the Instructions attribute

            // As Group toggle
            var asGroupToggle = new Toggle("As Group");
            asGroupToggle.tooltip = "Extrude as Group determines whether adjacent edges stay connected when extruding.";
            asGroupToggle.SetValueWithoutNotify(m_ExtrudeAsGroup);
            asGroupToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                if (m_ExtrudeAsGroup != evt.newValue)
                {
                    m_ExtrudeAsGroup = evt.newValue;
                    Overdrive.ProBuilderPlus.UserPreferences.Tools.ExtrudeAsGroup = evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(asGroupToggle);

            // Distance field
            var distanceField = new FloatField("Distance");
            distanceField.tooltip = "Distance to extrude edges. Can be negative to extrude inward.";
            distanceField.SetValueWithoutNotify(m_ExtrudeDistance);
            distanceField.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                if (m_ExtrudeDistance != evt.newValue)
                {
                    m_ExtrudeDistance = evt.newValue;
                    Overdrive.ProBuilderPlus.UserPreferences.Tools.ExtrudeDistance = evt.newValue;
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
            {
                m_ExtrudeDistance = Overdrive.ProBuilderPlus.UserPreferences.Tools.ExtrudeDistance;
                m_ExtrudeAsGroup = Overdrive.ProBuilderPlus.UserPreferences.Tools.ExtrudeAsGroup;
            }

            // Cache current selection
            CacheCurrentSelection();

            // Subscribe to scene rendering
            SceneView.duringSceneGui += OnSceneGUI;

            // Calculate initial preview
            UpdatePreview();
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;

            // Recalculate edge geometry
            CalculateExtrudeGeometry();

            // Repaint scene view
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0)
                return new ActionResult(ActionResult.Status.Failure, "No edges selected");

            Undo.RecordObjects(m_CachedMeshes, "Extrude Edges");

            int extrudedEdgeCount = 0;
            bool success = false;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var edges = m_CachedEdges[meshIndex];

                if (mesh == null || edges == null || edges.Length == 0) continue;

                mesh.ToMesh();
                mesh.Refresh(RefreshMask.Normals);

                extrudedEdgeCount += edges.Length;

                // Use ProBuilder's built-in Extrude method  
                Edge[] newEdges = mesh.Extrude(edges, m_ExtrudeDistance, m_ExtrudeAsGroup, true);

                success |= newEdges != null;

                if (success)
                    mesh.SetSelectedEdges(newEdges);
                else
                    extrudedEdgeCount -= edges.Length;

                mesh.ToMesh();
                mesh.Refresh();
            }

            ProBuilderEditor.Refresh();

            if (extrudedEdgeCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Extruded {extrudedEdgeCount} edges");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to extrude edges");
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;

            m_NewEdges = null;
            m_CachedMeshes = null;
            m_CachedEdges = null;

            SceneView.RepaintAll();
        }

        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedEdges = new Edge[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedEdges[i] = selection[i].selectedEdges.ToArray();
            }
        }

        private void CalculateExtrudeGeometry()
        {
            m_NewEdges = new List<(Vector3, Vector3)>();

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                var edges = m_CachedEdges[meshIndex];

                if (mesh == null || edges == null || edges.Length == 0) continue;

                CalculateExtrudeGeometryForMesh(mesh, edges);
            }
        }

        private void CalculateExtrudeGeometryForMesh(ProBuilderMesh mesh, Edge[] edges)
        {
            var transform = mesh.transform;
            var vertices = mesh.positions;

            // Ensure normals are up to date
            if (!mesh.HasArrays(MeshArrays.Normal))
                mesh.Refresh(RefreshMask.Normals);
            var normals = mesh.normals;

            // Filter valid edges exactly like ProBuilder does
            var validEdges = new List<Edge>();
            var edgeFaces = new List<Face>();

            foreach (Edge edge in edges)
            {
                int faceCount = 0;
                Face face = null;

                foreach (Face meshFace in mesh.faces)
                {
                    if (meshFace.edges.Contains(edge))
                    {
                        face = meshFace;
                        if (++faceCount > 1)
                            break;
                    }
                }

                // Only include non-manifold edges (exactly like ProBuilder)
                if (faceCount < 2)
                {
                    validEdges.Add(edge);
                    edgeFaces.Add(face);
                }
            }

            if (validEdges.Count < 1) return;

            // Prepare for ProBuilder-style normal calculation
            int[] allEdgeIndexes = null;
            IList<SharedVertex> sharedIndexes = null;

            if (m_ExtrudeAsGroup)
            {
                // Collect all vertex indices for shared vertex averaging
                allEdgeIndexes = new int[validEdges.Count * 2];
                int c = 0;
                for (int i = 0; i < validEdges.Count; i++)
                {
                    allEdgeIndexes[c++] = validEdges[i].a;
                    allEdgeIndexes[c++] = validEdges[i].b;
                }
                sharedIndexes = mesh.sharedVertices;
            }

            // Calculate geometry for each edge exactly like ProBuilder
            for (int i = 0; i < validEdges.Count; i++)
            {
                Edge edge = validEdges[i];
                Face face = edgeFaces[i];

                Vector3 xnorm, ynorm;

                if (m_ExtrudeAsGroup)
                {
                    // Use ProBuilder's shared vertex normal averaging approach
                    xnorm = AverageNormalWithSharedVertices(mesh, edge.a, allEdgeIndexes, normals, sharedIndexes);
                    ynorm = AverageNormalWithSharedVertices(mesh, edge.b, allEdgeIndexes, normals, sharedIndexes);
                }
                else
                {
                    // Use face normal for both vertices (exactly like ProBuilder)
                    Vector3 faceNormal = face != null ? Math.Normal(mesh, face) : Vector3.up;
                    xnorm = faceNormal;
                    ynorm = faceNormal;
                }

                // Calculate the extruded positions
                Vector3 startPos = vertices[edge.a];
                Vector3 endPos = vertices[edge.b];
                Vector3 extrudedStartPos = startPos + xnorm.normalized * m_ExtrudeDistance;
                Vector3 extrudedEndPos = endPos + ynorm.normalized * m_ExtrudeDistance;

                // Transform to world space
                Vector3 worldStart = transform.TransformPoint(startPos);
                Vector3 worldEnd = transform.TransformPoint(endPos);
                Vector3 worldExtrudedStart = transform.TransformPoint(extrudedStartPos);
                Vector3 worldExtrudedEnd = transform.TransformPoint(extrudedEndPos);

                // Add the new face edges (this creates a quad)
                // Original edge (at bottom)
                m_NewEdges.Add((worldStart, worldEnd));
                // Extruded edge (at top)
                m_NewEdges.Add((worldExtrudedStart, worldExtrudedEnd));
                // Side edges (connecting original to extruded)
                m_NewEdges.Add((worldStart, worldExtrudedStart));
                m_NewEdges.Add((worldEnd, worldExtrudedEnd));
            }
        }

        /// <summary>
        /// Approximates ProBuilder's InternalMeshUtility.AverageNormalWithIndexes behavior
        /// </summary>
        private Vector3 AverageNormalWithSharedVertices(ProBuilderMesh mesh, int vertexIndex, int[] allEdgeIndexes, IList<Vector3> normals, IList<SharedVertex> sharedIndexes)
        {
            // Find the shared vertex group that contains this vertex
            SharedVertex sharedGroup = null;
            foreach (var sv in sharedIndexes)
            {
                if (sv.Contains(vertexIndex))
                {
                    sharedGroup = sv;
                    break;
                }
            }

            if (sharedGroup == null)
                return normals[vertexIndex];

            // Average normals from vertices in the shared group that are also in our edge selection
            Vector3 avgNormal = Vector3.zero;
            int count = 0;

            foreach (int sharedVertexIndex in sharedGroup)
            {
                // Only include vertices that are part of the selected edges
                for (int i = 0; i < allEdgeIndexes.Length; i++)
                {
                    if (allEdgeIndexes[i] == sharedVertexIndex && sharedVertexIndex < normals.Count)
                    {
                        avgNormal += normals[sharedVertexIndex];
                        count++;
                        break; // Don't double-count
                    }
                }
            }

            return count > 0 ? (avgNormal / count).normalized : normals[vertexIndex];
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawExtrudePreview();
        }

        private void DrawExtrudePreview()
        {
            if (m_NewEdges == null) return;

            // Draw cyan edges showing the new geometry structure
            Handles.color = Color.cyan;

            foreach (var edge in m_NewEdges)
            {
                Handles.DrawAAPolyLine(3f, edge.Item1, edge.Item2);
            }
        }
    }
}

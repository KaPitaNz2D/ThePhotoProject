using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: re-enable settings UI for offset and reverse order

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Bridge Edges preview action for connecting exactly 2 selected open edges.
    /// </summary>
    [ProBuilderPlusAction("bridge-edges", "Bridge",
        Tooltip = "Bridge between open edges, using 2 selected open edges as anchors for complex bridging.",
        Instructions = "Select 2 open edges to bridge between",
        IconPath = "Icons/Old/Edge_Bridge",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 2,
        Order = 130)]
    public sealed class BridgeEdgesPreviewAction : PreviewMenuAction
    {
        // Settings
        private int m_RotationOffset;
        private bool m_ReverseOrder;
        private bool m_UseFullBorders;

        // Preview data
        private List<(Vector3, Vector3)> m_ConnectionLines; // Cyan connection lines

        protected override bool IsSpecialConditionsMet()
        {
            // Count total selected open edges across all meshes
            int totalSelectedOpenEdges = 0;

            foreach (var mesh in MeshSelection.top)
            {
                if (mesh.selectedEdges.Count == 0) continue;

                var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
                var openEdges = allWingedEdges.Where(static x => x.opposite == null).Select(static y => y.edge.local).ToHashSet();

                totalSelectedOpenEdges += mesh.selectedEdges.Count(edge => openEdges.Contains(edge));
            }

            return totalSelectedOpenEdges == 2;
        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_RotationOffset = UserPreferences.Tools.BridgeRotationOffset;
            m_ReverseOrder = UserPreferences.Tools.BridgeReverseOrder;
            m_UseFullBorders = UserPreferences.Tools.BridgeUseFullBorders;

            // Full borders toggle
            var fullBordersToggle = new Toggle("Use Full Borders")
            {
                value = m_UseFullBorders
            };
            fullBordersToggle.tooltip = "When enabled, bridges between entire open edge borders. When disabled, bridges only the 2 selected edges directly.";
            fullBordersToggle.RegisterValueChangedCallback(evt =>
            {
                m_UseFullBorders = evt.newValue;
                UserPreferences.Tools.BridgeUseFullBorders = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(fullBordersToggle);

            /*
            // Rotation offset control
            var rotationContainer = new VisualElement();
            rotationContainer.style.flexDirection = FlexDirection.Row;
            rotationContainer.style.alignItems = Align.Center;
            rotationContainer.style.marginTop = 10;

            var rotationLabel = new Label("Rotation Offset:");
            rotationLabel.style.minWidth = 100;
            rotationContainer.Add(rotationLabel);

            var decrementButton = new Button(() => {
                m_RotationOffset = Mathf.Max(0, m_RotationOffset - 1);
                PreviewActionFramework.RequestPreviewUpdate();
                // Update the label
                var label = rotationContainer.Q<Label>("rotationOffsetLabel");
                if (label != null) label.text = $"{m_RotationOffset}";
            }) { text = "-" };
            decrementButton.style.width = 25;
            rotationContainer.Add(decrementButton);

            var offsetLabel = new Label($"{m_RotationOffset}");
            offsetLabel.style.minWidth = 30;
            offsetLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            offsetLabel.name = "rotationOffsetLabel";
            rotationContainer.Add(offsetLabel);

            var incrementButton = new Button(() => {
                m_RotationOffset++;
                PreviewActionFramework.RequestPreviewUpdate();
                // Update the label
                var label = rotationContainer.Q<Label>("rotationOffsetLabel");
                if (label != null) label.text = $"{m_RotationOffset}";
            }) { text = "+" };
            incrementButton.style.width = 25;
            rotationContainer.Add(incrementButton);

            root.Add(rotationContainer);

            // Reverse order toggle
            var reverseOrderToggle = new Toggle("Reverse Order")
            {
                value = m_ReverseOrder
            };
            reverseOrderToggle.style.marginTop = 15;
            reverseOrderToggle.RegisterValueChangedCallback(evt =>
            {
                m_ReverseOrder = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(reverseOrderToggle);
            */

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_RotationOffset = UserPreferences.Tools.BridgeRotationOffset;
                m_ReverseOrder = UserPreferences.Tools.BridgeReverseOrder;
                m_UseFullBorders = UserPreferences.Tools.BridgeUseFullBorders;
            }

            UpdatePreview();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        internal override void OnSelectionChangedDuringPreview()
        {
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            // Reset all preview data
            m_ConnectionLines = new List<(Vector3, Vector3)>();

            // Collect all selected open edges across all meshes
            var selectedOpenEdges = new List<(ProBuilderMesh mesh, Edge edge)>();

            foreach (var mesh in MeshSelection.top)
            {
                if (mesh.selectedEdges.Count == 0) continue;

                var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
                var openEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToHashSet();

                foreach (var edge in mesh.selectedEdges)
                {
                    if (openEdges.Contains(edge))
                    {
                        selectedOpenEdges.Add((mesh, edge));
                    }
                }
            }

            // We should have exactly 2 selected open edges based on HasValidSelection
            if (selectedOpenEdges.Count != 2) return;

            var (mesh1, edge1) = selectedOpenEdges[0];
            var (mesh2, edge2) = selectedOpenEdges[1];

            var allVertices1 = mesh1.GetVertices();
            var allVertices2 = mesh2.GetVertices();

            // Get edge positions
            var edge1Start = mesh1.transform.TransformPoint(allVertices1[edge1.a].position);
            var edge1End = mesh1.transform.TransformPoint(allVertices1[edge1.b].position);
            var edge2Start = mesh2.transform.TransformPoint(allVertices2[edge2.a].position);
            var edge2End = mesh2.transform.TransformPoint(allVertices2[edge2.b].position);

            if (!m_UseFullBorders)
            {
                // Direct mode: cyan connection between edge midpoints
                var edge1Mid = (edge1Start + edge1End) * 0.5f;
                var edge2Mid = (edge2Start + edge2End) * 0.5f;
                m_ConnectionLines.Add((edge1Mid, edge2Mid));
            }
            else
            {
                // Full borders mode
                if (mesh1 == mesh2)
                {
                    var allWingedEdges = WingedEdge.GetWingedEdges(mesh1);
                    var allOpenEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToList();

                    if (BridgeEdges_Helper.AreEdgesOnSameBorder(mesh1, edge1, edge2, allOpenEdges))
                    {
                        // Same border: cyan connection
                        var connectionLine = BridgeEdges_Helper.GetDirectConnectionLine(mesh1, edge1, edge2);
                        m_ConnectionLines.Add(connectionLine);
                    }
                    else
                    {
                        // Different borders: cyan connection lines between loops
                        var bridgeableLoops = BridgeEdges_Helper.FindBridgeableLoops(mesh1, allOpenEdges, mesh1.selectedEdges.ToArray());
                        foreach (var (loopA, loopB, anchorA, anchorB) in bridgeableLoops)
                        {
                            var connections = BridgeEdges_Helper.CalculateBridgeConnections(mesh1, loopA, loopB, anchorA, anchorB, m_RotationOffset, m_ReverseOrder);
                            m_ConnectionLines.AddRange(connections);
                        }
                    }
                }
                else
                {
                    // Different meshes: cyan connection lines between loops
                    foreach (var mesh in MeshSelection.top)
                    {
                        if (mesh.selectedEdges.Count == 0) continue;

                        var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
                        var allOpenEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToList();
                        var bridgeableLoops = BridgeEdges_Helper.FindBridgeableLoops(mesh, allOpenEdges, mesh.selectedEdges.ToArray());

                        foreach (var (loopA, loopB, anchorA, anchorB) in bridgeableLoops)
                        {
                            var connections = BridgeEdges_Helper.CalculateBridgeConnections(mesh, loopA, loopB, anchorA, anchorB, m_RotationOffset, m_ReverseOrder);
                            m_ConnectionLines.AddRange(connections);
                        }
                    }
                }
            }

            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (MeshSelection.selectedObjectCount < 1)
                return ActionResult.NoSelection;

            var meshes = MeshSelection.top.ToArray();
            Undo.RecordObjects(meshes, "Bridge Edges");

            int bridgedCount = 0;
            bool hasFailures = false;

            // Collect all selected open edges across all meshes (same logic as preview)
            var selectedOpenEdges = new List<(ProBuilderMesh mesh, Edge edge)>();

            foreach (var mesh in MeshSelection.top)
            {
                if (mesh.selectedEdges.Count == 0) continue;

                var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
                var openEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToHashSet();

                foreach (var edge in mesh.selectedEdges)
                {
                    if (openEdges.Contains(edge))
                    {
                        selectedOpenEdges.Add((mesh, edge));
                    }
                }
            }

            // We should have exactly 2 selected open edges based on HasValidSelection
            if (selectedOpenEdges.Count != 2)
            {
                return new ActionResult(ActionResult.Status.Failure, "Exactly 2 open edges must be selected.");
            }

            var (mesh1, edge1) = selectedOpenEdges[0];
            var (mesh2, edge2) = selectedOpenEdges[1];

            if (!m_UseFullBorders)
            {
                // Direct mode: Just bridge the two selected edges directly
                if (mesh1 == mesh2)
                {
                    // Same mesh: Direct bridge
                    var newFace = mesh1.Bridge(edge1, edge2, true);
                    if (newFace != null)
                    {
                        bridgedCount++;
                        mesh1.ToMesh();
                        mesh1.Refresh();
                        mesh1.Optimize();
                    }
                    else
                    {
                        hasFailures = true;
                    }
                }
                else
                {
                    // Different meshes: Cannot bridge directly between different meshes
                    return new ActionResult(ActionResult.Status.Failure, "Cannot bridge edges from different meshes in Direct mode.");
                }
            }
            else
            {
                // Full borders mode: Original complex logic
                // Check if both edges are on the same mesh and same border (same logic as preview)
                if (mesh1 == mesh2)
                {
                    var allWingedEdges = WingedEdge.GetWingedEdges(mesh1);
                    var allOpenEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToList();

                    if (BridgeEdges_Helper.AreEdgesOnSameBorder(mesh1, edge1, edge2, allOpenEdges))
                    {
                        // Same border scenario: Direct bridge between the 2 selected edges
                        var newFace = mesh1.Bridge(edge1, edge2, true);
                        if (newFace != null)
                        {
                            bridgedCount++;
                            mesh1.ToMesh();
                            mesh1.Refresh();
                            mesh1.Optimize();
                        }
                        else
                        {
                            hasFailures = true;
                        }
                    }
                    else
                    {
                        // Different borders on same mesh: Use bridgeable loops logic
                        var bridgeableLoops = BridgeEdges_Helper.FindBridgeableLoops(mesh1, allOpenEdges, mesh1.selectedEdges.ToArray());

                        bridgedCount += ProcessBridgeableLoops(mesh1, bridgeableLoops, ref hasFailures);

                        mesh1.ToMesh();
                        mesh1.Refresh();
                        mesh1.Optimize();
                    }
                }
                else
                {
                    // Different meshes: Process each mesh separately using bridgeable loops logic
                    foreach (var mesh in MeshSelection.top)
                    {
                        if (mesh.selectedEdges.Count == 0) continue;

                        var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
                        var allOpenEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToList();
                        var bridgeableLoops = BridgeEdges_Helper.FindBridgeableLoops(mesh, allOpenEdges, mesh.selectedEdges.ToArray());

                        bridgedCount += ProcessBridgeableLoops(mesh, bridgeableLoops, ref hasFailures);

                        mesh.ToMesh();
                        mesh.Refresh();
                        mesh.Optimize();
                    }
                }
            }

            MeshSelection.ClearElementSelection();
            ProBuilderEditor.Refresh();

            if (bridgedCount > 0)
            {
                string message = $"Bridged {bridgedCount} edge connection{(bridgedCount == 1 ? string.Empty : "s")}";
                if (hasFailures) message += " (some bridges failed)";
                return new ActionResult(ActionResult.Status.Success, message);
            }
            else
            {
                return new ActionResult(ActionResult.Status.Failure, "Failed to bridge edges. Ensure edges are open and bridgeable.");
            }
        }

        private int ProcessBridgeableLoops(ProBuilderMesh mesh, List<(List<Edge> loopA, List<Edge> loopB, Edge anchorA, Edge anchorB)> bridgeableLoops, ref bool hasFailures)
        {
            int bridgedCount = 0;

            foreach (var (loopA, loopB, anchorA, anchorB) in bridgeableLoops)
            {
                var allVertices = mesh.GetVertices();

                // Get sorted loops - replicate the preview logic
                var sortedLoopA = new List<Edge>(loopA);
                var sortedLoopB = new List<Edge>(loopB);

                // Sort loops spatially to match preview behavior
                if (sortedLoopA.Count > 1)
                {
                    sortedLoopA = SortEdgesSpatially(mesh, sortedLoopA, allVertices);
                }
                if (sortedLoopB.Count > 1)
                {
                    sortedLoopB = SortEdgesSpatially(mesh, sortedLoopB, allVertices);
                }

                int anchorIndexA = sortedLoopA.IndexOf(anchorA);
                int anchorIndexB = sortedLoopB.IndexOf(anchorB);

                if (anchorIndexA == -1 || anchorIndexB == -1) continue;

                // Apply rotation offset to loop B anchor position
                anchorIndexB = (anchorIndexB + m_RotationOffset) % sortedLoopB.Count;

                // Determine the best walking direction for loop B
                bool autoDetectedReverse = DetermineReverseDirection(mesh, sortedLoopA, sortedLoopB, anchorIndexA, anchorIndexB, allVertices);
                bool reverseB = m_ReverseOrder ? !autoDetectedReverse : autoDetectedReverse;

                int maxConnections = Mathf.Min(sortedLoopA.Count, sortedLoopB.Count);

                // Create and bridge all edge pairs
                for (int i = 0; i < maxConnections; i++)
                {
                    int indexA = (anchorIndexA + i) % sortedLoopA.Count;
                    int indexB = reverseB ?
                        (anchorIndexB - i + sortedLoopB.Count) % sortedLoopB.Count :
                        (anchorIndexB + i) % sortedLoopB.Count;

                    var edgeA = sortedLoopA[indexA];
                    var edgeB = sortedLoopB[indexB];

                    var newFace = mesh.Bridge(edgeA, edgeB, true); // Allow non-manifold for now
                    if (newFace != null)
                    {
                        bridgedCount++;
                    }
                    else
                    {
                        hasFailures = true;
                    }
                }
            }

            return bridgedCount;
        }

        public override void CleanupPreview()
        {
            m_ConnectionLines = null;

            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Set z-test to respect depth so lines are occluded by geometry
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            // Draw cyan connection lines
            if (m_ConnectionLines != null && m_ConnectionLines.Count > 0)
            {
                Handles.color = Color.cyan;
                foreach (var connection in m_ConnectionLines)
                {
                    Handles.DrawAAPolyLine(4f, connection.Item1, connection.Item2);
                }
            }
        }

        /// <summary>
        /// Sorts edges around their perimeter to ensure proper sequential spatial order.
        /// </summary>
        private static List<Edge> SortEdgesSpatially(ProBuilderMesh mesh, List<Edge> edges, UnityEngine.ProBuilder.Vertex[] allVertices)
        {
            if (edges.Count <= 2) return new List<Edge>(edges);

            // Build adjacency map to understand edge connections
            var adjacencyMap = new Dictionary<int, List<int>>();
            foreach (var edge in edges)
            {
                if (!adjacencyMap.ContainsKey(edge.a))
                    adjacencyMap[edge.a] = new List<int>();
                if (!adjacencyMap.ContainsKey(edge.b))
                    adjacencyMap[edge.b] = new List<int>();

                adjacencyMap[edge.a].Add(edge.b);
                adjacencyMap[edge.b].Add(edge.a);
            }

            // Start from first edge and walk around the perimeter
            var sortedEdges = new List<Edge>();
            var usedEdges = new HashSet<Edge>();

            var currentEdge = edges[0];
            sortedEdges.Add(currentEdge);
            usedEdges.Add(currentEdge);

            int currentVertex = currentEdge.b; // Move to the end of first edge

            // Walk around the perimeter
            while (sortedEdges.Count < edges.Count)
            {
                Edge nextEdge = default;
                bool foundNext = false;

                // Find the next edge that connects to current vertex
                foreach (var edge in edges)
                {
                    if (usedEdges.Contains(edge)) continue;

                    if (edge.a == currentVertex)
                    {
                        nextEdge = edge;
                        currentVertex = edge.b;
                        foundNext = true;
                        break;
                    }
                    else if (edge.b == currentVertex)
                    {
                        nextEdge = edge;
                        currentVertex = edge.a;
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext) break; // Can't continue the walk

                sortedEdges.Add(nextEdge);
                usedEdges.Add(nextEdge);
            }

            // If we couldn't sort all edges properly, fall back to original order
            if (sortedEdges.Count != edges.Count)
                return new List<Edge>(edges);

            return sortedEdges;
        }

        /// <summary>
        /// Simplified direction detection based on distance minimization.
        /// </summary>
        private static bool DetermineReverseDirection(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB,
            int anchorIndexA, int anchorIndexB, UnityEngine.ProBuilder.Vertex[] allVertices)
        {
            if (loopA.Count < 2 || loopB.Count < 2) return false;

            // Get next positions in both directions
            int nextIndexA = (anchorIndexA + 1) % loopA.Count;
            int nextIndexB_forward = (anchorIndexB + 1) % loopB.Count;
            int nextIndexB_reverse = (anchorIndexB - 1 + loopB.Count) % loopB.Count;

            Vector3 nextA = GetEdgeMidpoint(mesh, loopA[nextIndexA], allVertices);
            Vector3 nextB_forward = GetEdgeMidpoint(mesh, loopB[nextIndexB_forward], allVertices);
            Vector3 nextB_reverse = GetEdgeMidpoint(mesh, loopB[nextIndexB_reverse], allVertices);

            // Choose direction that gives shorter distance for the next connection
            float distanceForward = Vector3.Distance(nextA, nextB_forward);
            float distanceReverse = Vector3.Distance(nextA, nextB_reverse);

            return distanceReverse < distanceForward;
        }

        /// <summary>
        /// Gets the world space midpoint of an edge.
        /// </summary>
        private static Vector3 GetEdgeMidpoint(ProBuilderMesh mesh, Edge edge, UnityEngine.ProBuilder.Vertex[] allVertices)
        {
            var startPos = allVertices[edge.a].position;
            var endPos = allVertices[edge.b].position;
            var midpoint = (startPos + endPos) * 0.5f;
            return mesh.transform.TransformPoint(midpoint);
        }
    }
}
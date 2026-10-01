using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Helper class containing core algorithms for bridge elements functionality.
    /// </summary>
    public static class BridgeEdges_Helper
    {
        /// <summary>
        /// Calculates the average center position of a collection of world space positions.
        /// </summary>
        public static Vector3? CalculateAverageCenter(IEnumerable<Vector3> worldPositions)
        {
            var positions = worldPositions.ToList();
            if (positions.Count == 0)
                return null;

            var sum = Vector3.zero;
            foreach (var pos in positions)
            {
                sum += pos;
            }
            return sum / positions.Count;
        }

        /// <summary>
        /// Gets the world space center positions of selected edges.
        /// </summary>
        public static List<Vector3> GetEdgeCenterPositions(IEnumerable<ProBuilderMesh> meshes)
        {
            var worldPositions = new List<Vector3>();
            
            foreach (var mesh in meshes)
            {
                if (mesh.selectedEdges.Count == 0) continue;
                
                var positions = mesh.positions;
                
                foreach (var edge in mesh.selectedEdges)
                {
                    // Get the center point of each edge
                    var edgeCenter = (positions[edge.a] + positions[edge.b]) * 0.5f;
                    // Transform to world space
                    var worldPos = mesh.transform.TransformPoint(edgeCenter);
                    worldPositions.Add(worldPos);
                }
            }
            
            return worldPositions;
        }

        /// <summary>
        /// Gets the world space positions of the complete open perimeter that contains the selected edges.
        /// Traces the entire open boundary loop rather than just directly connected edges.
        /// </summary>
        public static List<(Vector3, Vector3)> GetConnectedOpenEdgePositions(IEnumerable<ProBuilderMesh> meshes)
        {
            var edgePositions = new List<(Vector3, Vector3)>();
            
            foreach (var mesh in meshes)
            {
                if (mesh.selectedEdges.Count == 0) continue;
                
                // Get all open edges in the mesh
                var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
                var allOpenEdges = allWingedEdges.Where(static x => x.opposite == null).Select(static y => y.edge.local).ToList();
                
                if (allOpenEdges.Count == 0) continue;
                
                var allVertices = mesh.GetVertices();
                var processedEdges = new HashSet<Edge>();
                
                // For each selected edge, trace its complete open perimeter
                foreach (var selectedEdge in mesh.selectedEdges)
                {
                    // Skip if this edge is not actually open
                    if (!allOpenEdges.Contains(selectedEdge)) continue;
                    
                    // Skip if we've already processed this edge as part of another perimeter
                    if (processedEdges.Contains(selectedEdge)) continue;
                    
                    // Trace the complete open boundary starting from this edge
                    var perimeterEdges = TraceOpenPerimeter(allOpenEdges, selectedEdge, allVertices);
                    
                    // Mark all edges in this perimeter as processed
                    foreach (var edge in perimeterEdges)
                    {
                        processedEdges.Add(edge);
                    }
                    
                    // Convert to world positions
                    foreach (var edge in perimeterEdges)
                    {
                        Vector3 startPos = mesh.transform.TransformPoint(allVertices[edge.a].position);
                        Vector3 endPos = mesh.transform.TransformPoint(allVertices[edge.b].position);
                        edgePositions.Add((startPos, endPos));
                    }
                }
            }
            
            return edgePositions;
        }
        
        /// <summary>
        /// Traces a complete open perimeter starting from a given edge.
        /// Returns all edges that form the connected open boundary.
        /// </summary>
        private static List<Edge> TraceOpenPerimeter(List<Edge> allOpenEdges, Edge startEdge, Vertex[] allVertices)
        {
            var perimeterEdges = new List<Edge>();
            var usedEdges = new HashSet<Edge>();
            
            // Start with the given edge
            perimeterEdges.Add(startEdge);
            usedEdges.Add(startEdge);
            
            // Trace forward from the end of startEdge
            TraceDirection(allOpenEdges, startEdge.b, usedEdges, perimeterEdges, allVertices, false);
            
            // Trace backward from the start of startEdge
            TraceDirection(allOpenEdges, startEdge.a, usedEdges, perimeterEdges, allVertices, true);
            
            return perimeterEdges;
        }
        
        /// <summary>
        /// Traces in one direction along the open perimeter.
        /// </summary>
        private static void TraceDirection(List<Edge> allOpenEdges, int startVertex, HashSet<Edge> usedEdges, 
            List<Edge> perimeterEdges, Vertex[] allVertices, bool insertAtBeginning)
        {
            const float POSITION_TOLERANCE = 0.0001f;
            int currentVertex = startVertex;
            
            while (true)
            {
                // Find the next open edge connected to currentVertex
                Edge? nextEdge = null;
                
                var currentPos = allVertices[currentVertex].position;
                
                foreach (var edge in allOpenEdges)
                {
                    if (usedEdges.Contains(edge)) continue;
                    
                    var edgeStartPos = allVertices[edge.a].position;
                    var edgeEndPos = allVertices[edge.b].position;
                    
                    // Check if this edge connects to our current vertex by position
                    if (Vector3.Distance(currentPos, edgeStartPos) <= POSITION_TOLERANCE)
                    {
                        nextEdge = edge;
                        currentVertex = edge.b; // Move to the other end
                        break;
                    }
                    else if (Vector3.Distance(currentPos, edgeEndPos) <= POSITION_TOLERANCE)
                    {
                        nextEdge = edge;
                        currentVertex = edge.a; // Move to the other end
                        break;
                    }
                }
                
                // If no next edge found, we've reached the end of this direction
                if (!nextEdge.HasValue) break;
                
                // Add the edge to our perimeter
                usedEdges.Add(nextEdge.Value);
                
                if (insertAtBeginning)
                    perimeterEdges.Insert(0, nextEdge.Value);
                else
                    perimeterEdges.Add(nextEdge.Value);
                
                // Check if we've completed a loop (back to start edge's vertices)
                var startEdge = perimeterEdges[insertAtBeginning ? perimeterEdges.Count - 1 : 0];
                var startPos1 = allVertices[startEdge.a].position;
                var startPos2 = allVertices[startEdge.b].position;
                var currentPosCheck = allVertices[currentVertex].position;
                
                if (Vector3.Distance(currentPosCheck, startPos1) <= POSITION_TOLERANCE ||
                    Vector3.Distance(currentPosCheck, startPos2) <= POSITION_TOLERANCE)
                {
                    break; // Completed the loop
                }
            }
        }
        
        /// <summary>
        /// Finds the best anchor pair between two edge loops.
        /// For edge mode, uses the selected edges as initial anchors if they're in different loops.
        /// </summary>
        public static (Edge edgeA, Edge edgeB)? FindBestAnchorPair(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, Edge[] selectedEdges = null)
        {
            if (loopA.Count == 0 || loopB.Count == 0) return null;
            
            // For edge mode: try to use selected edges as anchors if they're in different loops
            if (selectedEdges != null && selectedEdges.Length >= 2)
            {
                var selectedInLoopA = selectedEdges.Where(e => loopA.Contains(e)).ToList();
                var selectedInLoopB = selectedEdges.Where(e => loopB.Contains(e)).ToList();
                
                if (selectedInLoopA.Count > 0 && selectedInLoopB.Count > 0)
                {
                    // Use the first selected edge from each loop as anchors
                    return (selectedInLoopA[0], selectedInLoopB[0]);
                }
            }
            
            // Fallback to closest-distance detection
            var allVertices = mesh.GetVertices();
            float minDistance = float.MaxValue;
            Edge bestEdgeA = default;
            Edge bestEdgeB = default;
            
            foreach (var edgeA in loopA)
            {
                Vector3 midA = GetEdgeMidpoint(mesh, edgeA, allVertices);
                
                foreach (var edgeB in loopB)
                {
                    Vector3 midB = GetEdgeMidpoint(mesh, edgeB, allVertices);
                    float distance = Vector3.Distance(midA, midB);
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestEdgeA = edgeA;
                        bestEdgeB = edgeB;
                    }
                }
            }
            
            return (bestEdgeA, bestEdgeB);
        }
        
        /// <summary>
        /// Calculates all bridge connections between two edge loops starting from anchor pair.
        /// Returns list of (pointA, pointB) world space positions for drawing connection lines.
        /// </summary>
        public static List<(Vector3, Vector3)> CalculateBridgeConnections(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, Edge anchorA, Edge anchorB)
        {
            return CalculateBridgeConnections(mesh, loopA, loopB, anchorA, anchorB, false);
        }
        
        /// <summary>
        /// Calculates all bridge connections between two edge loops starting from anchor pair.
        /// Returns list of (pointA, pointB) world space positions for drawing connection lines.
        /// </summary>
        /// <param name="forceReverseOrder">If true, forces the reverse order for loop B (inverts the automatic direction detection). 
        /// This affects only one loop, allowing manual correction when automatic ordering creates unwanted twists.</param>
        public static List<(Vector3, Vector3)> CalculateBridgeConnections(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, Edge anchorA, Edge anchorB, bool forceReverseOrder)
        {
            return CalculateBridgeConnections(mesh, loopA, loopB, anchorA, anchorB, 0, forceReverseOrder);
        }
        
        /// <summary>
        /// Calculates all bridge connections between two edge loops starting from anchor pair.
        /// Returns list of (pointA, pointB) world space positions for drawing connection lines.
        /// </summary>
        /// <param name="rotationOffset">Rotation offset for loop B starting position (0=default, +1=shift by 1, etc.)</param>
        public static List<(Vector3, Vector3)> CalculateBridgeConnections(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, Edge anchorA, Edge anchorB, int rotationOffset)
        {
            return CalculateBridgeConnections(mesh, loopA, loopB, anchorA, anchorB, rotationOffset, false);
        }
        
        /// <summary>
        /// Calculates all bridge connections between two edge loops starting from anchor pair.
        /// Returns list of (pointA, pointB) world space positions for drawing connection lines.
        /// </summary>
        /// <param name="rotationOffset">Rotation offset for loop B starting position (0=default, +1=shift by 1, etc.)</param>
        /// <param name="forceReverseOrder">If true, forces the reverse order for loop B (inverts the automatic direction detection). 
        /// This affects only one loop, allowing manual correction when automatic ordering creates unwanted twists.</param>
        public static List<(Vector3, Vector3)> CalculateBridgeConnections(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, Edge anchorA, Edge anchorB, int rotationOffset, bool forceReverseOrder)
        {
            var connections = new List<(Vector3, Vector3)>();
            if (loopA.Count == 0 || loopB.Count == 0) return connections;
            
            var allVertices = mesh.GetVertices();
            
            // Sort edges around their perimeters to ensure proper sequential order
            var sortedLoopA = SortEdgesAroundPerimeter(mesh, loopA, allVertices);
            var sortedLoopB = SortEdgesAroundPerimeter(mesh, loopB, allVertices);
            
            // Find anchor positions in sorted loops
            int anchorIndexA = sortedLoopA.IndexOf(anchorA);
            int anchorIndexB = sortedLoopB.IndexOf(anchorB);
            
            if (anchorIndexA == -1 || anchorIndexB == -1) return connections;
            
            // Apply rotation offset to loop B anchor position
            anchorIndexB = (anchorIndexB + rotationOffset) % sortedLoopB.Count;
            
            // Determine the best walking direction for loop B to minimize crossings
            bool autoDetectedReverse = ShouldReverseBridgeDirection(mesh, sortedLoopA, sortedLoopB, anchorIndexA, anchorIndexB, allVertices);
            bool reverseB = forceReverseOrder ? !autoDetectedReverse : autoDetectedReverse;
            
            // Calculate how many connections we can make
            int maxConnections = Mathf.Min(sortedLoopA.Count, sortedLoopB.Count);
            
            // Debug log the walking order
            var loopAOrder = new List<int>();
            var loopBOrder = new List<int>();
            for (int i = 0; i < maxConnections; i++)
            {
                int indexA = (anchorIndexA + i) % sortedLoopA.Count;
                int indexB = reverseB ? (anchorIndexB - i + sortedLoopB.Count) % sortedLoopB.Count : (anchorIndexB + i) % sortedLoopB.Count;
                loopAOrder.Add(indexA);
                loopBOrder.Add(indexB);
            }
            
            // Walk from anchors and create connections
            for (int i = 0; i < maxConnections; i++)
            {
                int indexA = (anchorIndexA + i) % sortedLoopA.Count;
                int indexB = reverseB ? 
                    (anchorIndexB - i + sortedLoopB.Count) % sortedLoopB.Count : 
                    (anchorIndexB + i) % sortedLoopB.Count;
                
                Vector3 pointA = GetEdgeMidpoint(mesh, sortedLoopA[indexA], allVertices);
                Vector3 pointB = GetEdgeMidpoint(mesh, sortedLoopB[indexB], allVertices);
                
                connections.Add((pointA, pointB));
            }
            
            return connections;
        }
        
        /// <summary>
        /// Sorts edges around their perimeter to ensure proper sequential spatial order.
        /// This prevents criss-cross patterns when bridging.
        /// </summary>
        private static List<Edge> SortEdgesAroundPerimeter(ProBuilderMesh mesh, List<Edge> edges, Vertex[] allVertices)
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
        /// Determines if loop B should be walked in reverse direction to minimize bridge crossings.
        /// Uses ProBuilder's WindingOrder API to properly align loops based on their geometric orientation.
        /// </summary>
        private static bool ShouldReverseBridgeDirection(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, 
            int anchorIndexA, int anchorIndexB, Vertex[] allVertices)
        {
            if (loopA.Count < 3 || loopB.Count < 3) 
            {
                // Fallback to distance-based method for small loops
                return ShouldReverseBridgeDirection_Distance(mesh, loopA, loopB, anchorIndexA, anchorIndexB, allVertices);
            }
            
            try
            {
                // Convert edge loops to ordered vertex positions for winding order analysis
                var positionsA = GetLoopVertexPositions(mesh, loopA, allVertices);
                var positionsB = GetLoopVertexPositions(mesh, loopB, allVertices);
                
                if (positionsA.Count < 3 || positionsB.Count < 3)
                {
                    return ShouldReverseBridgeDirection_Distance(mesh, loopA, loopB, anchorIndexA, anchorIndexB, allVertices);
                }
                
                // Get winding orders using ProBuilder's API
                var windingA = UnityEngine.ProBuilder.MeshOperations.SurfaceTopology.GetWindingOrder(positionsA);
                var windingB = UnityEngine.ProBuilder.MeshOperations.SurfaceTopology.GetWindingOrder(positionsB);
                
                // If both loops have the same winding order, we should reverse one to align properly
                if (windingA != WindingOrder.Unknown && windingB != WindingOrder.Unknown)
                {
                    return windingA == windingB;
                }
            }
            catch (System.Exception)
            {
                // Fall back to distance method if winding order calculation fails
            }
            
            // Fallback to distance-based method
            return ShouldReverseBridgeDirection_Distance(mesh, loopA, loopB, anchorIndexA, anchorIndexB, allVertices);
        }
        
        /// <summary>
        /// Fallback distance-based direction detection method.
        /// </summary>
        private static bool ShouldReverseBridgeDirection_Distance(ProBuilderMesh mesh, List<Edge> loopA, List<Edge> loopB, 
            int anchorIndexA, int anchorIndexB, Vertex[] allVertices)
        {
            // Test both directions and see which produces shorter bridge lengths for the first few connections
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
        /// Converts an edge loop to ordered vertex positions for winding order calculation.
        /// </summary>
        private static List<Vector2> GetLoopVertexPositions(ProBuilderMesh mesh, List<Edge> edgeLoop, Vertex[] allVertices)
        {
            var positions = new List<Vector2>();
            if (edgeLoop.Count == 0) return positions;
            
            // Build ordered vertex positions from edge loop
            var vertexPositions3D = new List<Vector3>();
            var usedVertices = new HashSet<int>();
            
            // Start with first edge
            vertexPositions3D.Add(allVertices[edgeLoop[0].a].position);
            usedVertices.Add(edgeLoop[0].a);
            
            int currentVertex = edgeLoop[0].b;
            vertexPositions3D.Add(allVertices[currentVertex].position);
            usedVertices.Add(currentVertex);
            
            // Follow the edge chain
            for (int i = 1; i < edgeLoop.Count; i++)
            {
                var edge = edgeLoop[i];
                
                if (edge.a == currentVertex && !usedVertices.Contains(edge.b))
                {
                    currentVertex = edge.b;
                    vertexPositions3D.Add(allVertices[currentVertex].position);
                    usedVertices.Add(currentVertex);
                }
                else if (edge.b == currentVertex && !usedVertices.Contains(edge.a))
                {
                    currentVertex = edge.a;
                    vertexPositions3D.Add(allVertices[currentVertex].position);
                    usedVertices.Add(currentVertex);
                }
            }
            
            // Project to 2D for winding order calculation
            // Transform positions to world space first
            var worldPositions = vertexPositions3D.Select(pos => mesh.transform.TransformPoint(pos)).ToArray();
            var projected2D = Projection.PlanarProject(worldPositions);
            
            return projected2D.ToList();
        }
        
        /// <summary>
        /// Gets the world space midpoint of an edge.
        /// </summary>
        private static Vector3 GetEdgeMidpoint(ProBuilderMesh mesh, Edge edge, Vertex[] allVertices)
        {
            var startPos = allVertices[edge.a].position;
            var endPos = allVertices[edge.b].position;
            var midpoint = (startPos + endPos) * 0.5f;
            return mesh.transform.TransformPoint(midpoint);
        }
        
        /// <summary>
        /// Checks if two edges are on the same open border/hole.
        /// </summary>
        public static bool AreEdgesOnSameBorder(ProBuilderMesh mesh, Edge edgeA, Edge edgeB, List<Edge> allOpenEdges)
        {
            if (!allOpenEdges.Contains(edgeA) || !allOpenEdges.Contains(edgeB))
                return false;

            var allVertices = mesh.GetVertices();
            var loopA = TraceOpenPerimeter(allOpenEdges, edgeA, allVertices);

            return loopA.Contains(edgeB);
        }

        /// <summary>
        /// Gets the direct connection line between two edges (for same-border scenario).
        /// Returns the world space positions of the edge midpoints.
        /// </summary>
        public static (Vector3, Vector3) GetDirectConnectionLine(ProBuilderMesh mesh, Edge edgeA, Edge edgeB)
        {
            var allVertices = mesh.GetVertices();
            var midpointA = GetEdgeMidpoint(mesh, edgeA, allVertices);
            var midpointB = GetEdgeMidpoint(mesh, edgeB, allVertices);
            return (midpointA, midpointB);
        }

        public static List<(List<Edge> loopA, List<Edge> loopB, Edge anchorA, Edge anchorB)> FindBridgeableLoops(ProBuilderMesh mesh, List<Edge> allOpenEdges, Edge[] selectedEdges = null)
        {
            var bridgeablePairs = new List<(List<Edge>, List<Edge>, Edge, Edge)>();
            var processedEdges = new HashSet<Edge>();
            var allVertices = mesh.GetVertices();

            // Group edges into separate loops
            var edgeLoops = new List<List<Edge>>();

            foreach (var edge in allOpenEdges)
            {
                if (processedEdges.Contains(edge)) continue;

                var loop = TraceOpenPerimeter(allOpenEdges, edge, allVertices);
                edgeLoops.Add(loop);

                foreach (var loopEdge in loop)
                {
                    processedEdges.Add(loopEdge);
                }
            }

            // Find pairs of loops that could be bridged
            for (int i = 0; i < edgeLoops.Count - 1; i++)
            {
                for (int j = i + 1; j < edgeLoops.Count; j++)
                {
                    var anchorPair = FindBestAnchorPair(mesh, edgeLoops[i], edgeLoops[j], selectedEdges);
                    if (anchorPair.HasValue)
                    {
                        bridgeablePairs.Add((edgeLoops[i], edgeLoops[j], anchorPair.Value.edgeA, anchorPair.Value.edgeB));
                    }
                }
            }

            return bridgeablePairs;
        }
    }
}

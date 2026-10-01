using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Advanced inset calculator and debug visualizer for ProBuilder face selections.
    /// Calculates precise inset positions for vertices in complex selections including interior vertices.
    /// Provides both debug visualization and production-ready inset point data.
    /// </summary>
    [InitializeOnLoad]
    public static class InsetFaces_MathHelper
    {
        #region Public Configuration
        
        /// <summary>
        /// The distance to inset edges inward from their original positions.
        /// Used for calculating inset lines and final vertex positions.
        /// </summary>
        public static float InsetAmount { get; set; } = 0.3f;
        
        /// <summary>
        /// Controls whether debug visualization should be shown in the scene.
        /// When false, no debug drawing occurs.
        /// </summary>
        public static bool ShowDebugVisualization { get; set; } = false;
        
        /// <summary>
        /// Public access to calculated inset points, ordered to match the original vertex order.
        /// Each Vector3? represents the calculated inset position for the corresponding vertex.
        /// Null values indicate vertices that don't need inset positions (interior vertices).
        /// </summary>
        public static List<Vector3?> OrderedInsetPoints { get; private set; } = new List<Vector3?>();
        
        /// <summary>
        /// Event fired when face selection changes and inset calculation completes.
        /// Provides access to old selection, new selection, and calculated inset points.
        /// </summary>
        public static System.Action<HashSet<Face>, HashSet<Face>, List<Vector3?>> InsetCalculationCompleted;
        
        #endregion
        
        #region Private Constants
        
        private const float VERTEX_DOT_SIZE_MULTIPLIER = 0.15f;
        private const float POSITION_TOLERANCE = 0.0001f;
        private const float MAX_INTERSECTION_DISTANCE = 2.0f;
        private const float PARALLEL_LINE_THRESHOLD = 0.001f;
        
        #endregion
        
        #region Data Structures
        
        /// <summary>
        /// Represents a vertex point with comprehensive connection data for inset calculations.
        /// </summary>
        public struct VertexPoint
        {
            public int vertexIndex;
            public Vector3 worldPosition;
            public bool isInterior;
            public List<EdgeInfo> perimeterEdges;    // Exterior edges connected to this vertex
            public List<EdgeInfo> interiorEdges;     // Interior edges connected to this vertex
            public List<int> partnerVertices;        // List of indices in currentVertexPoints list of all vertices at same position
            
            public VertexPoint(int index, Vector3 world, bool interior = false)
            {
                this.vertexIndex = index;
                this.worldPosition = world;
                this.isInterior = interior;
                this.perimeterEdges = new List<EdgeInfo>();
                this.interiorEdges = new List<EdgeInfo>();
                this.partnerVertices = new List<int>();
            }
        }
        
        /// <summary>
        /// Represents an edge with references to its connected vertices and interior/exterior classification.
        /// </summary>
        public struct EdgeInfo
        {
            public Edge edge;
            public VertexPoint vertexPointA;
            public VertexPoint vertexPointB;
            public bool isInterior;
            
            public EdgeInfo(Edge e, VertexPoint vertA, VertexPoint vertB, bool interior = false)
            {
                this.edge = e;
                this.vertexPointA = vertA;
                this.vertexPointB = vertB;
                this.isInterior = interior;
            }
        }
        
        /// <summary>
        /// Represents an inset line calculated from perimeter edges for intersection calculations.
        /// </summary>
        public struct InsetLine
        {
            public Vector3 startPosition;
            public Vector3 endPosition;
            
            public InsetLine(Vector3 start, Vector3 end)
            {
                this.startPosition = start;
                this.endPosition = end;
            }
        }
        
        #endregion
        
        #region Private State
        
        // Core data collections
        private static List<VertexPoint> currentVertexPoints = new List<VertexPoint>();
        private static List<EdgeInfo> currentEdges = new List<EdgeInfo>();
        private static List<InsetLine> currentInsetLines = new List<InsetLine>();
        private static Dictionary<Edge, InsetLine> edgeToInsetLine = new Dictionary<Edge, InsetLine>();
        private static List<Face> currentSelectedFaces = new List<Face>();
        
        // Calculation results
        private static Dictionary<int, Vector3> calculatedInsetPoints = new Dictionary<int, Vector3>();
        private static List<Vector3> currentInsetPoints = new List<Vector3>(); // For visualization only
        private static List<Vector3> currentFallbackInsetPoints = new List<Vector3>();
        
        // Processing state
        private static HashSet<int> processedVertexIndices = new HashSet<int>();
        private static HashSet<Face> previousSelectedFaces = new HashSet<Face>();
        
        // Performance optimization: vertex lookup dictionary and cached sorted list
        private static Dictionary<int, VertexPoint> vertexIndexLookup = new Dictionary<int, VertexPoint>();
        private static List<VertexPoint> sortedVerticesByIndex = new List<VertexPoint>();
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Checks if the face selection has changed and updates calculations accordingly.
        /// Returns true if selection changed, false if not.
        /// </summary>
        public static bool CheckForSelectionChange()
        {
            // Collect all currently selected faces
            var currentSelectedFaces = new HashSet<Face>();
            
            if (MeshSelection.selectedFaceCount > 0)
            {
                var selection = MeshSelection.top.ToArray();
                foreach (var mesh in selection)
                {
                    var selectedFaces = mesh.GetSelectedFaces();
                    if (selectedFaces != null)
                    {
                        foreach (var face in selectedFaces)
                        {
                            currentSelectedFaces.Add(face);
                        }
                    }
                }
            }
            
            // Check if selection has changed
            if (!currentSelectedFaces.SetEquals(previousSelectedFaces))
            {
                // Store old selection before updating
                var oldSelection = new HashSet<Face>(previousSelectedFaces);
                
                // Update tracking
                previousSelectedFaces = new HashSet<Face>(currentSelectedFaces);
                
                // Trigger calculation update
                UpdateSelectionData();
                
                // Invoke completion event with results
                InsetCalculationCompleted?.Invoke(oldSelection, currentSelectedFaces, OrderedInsetPoints);
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the current inset points in the same order as the original vertices.
        /// Returns null for vertices that don't need inset positions (interior vertices).
        /// </summary>
        public static List<Vector3?> GetOrderedInsetPoints()
        {
            return new List<Vector3?>(OrderedInsetPoints);
        }
        
        /// <summary>
        /// Forces a recalculation of inset points for the current selection.
        /// Useful when InsetAmount has been changed programmatically.
        /// </summary>
        public static void RecalculateInsetPoints()
        {
            UpdateSelectionData();
        }
        
        /// <summary>
        /// Updates the inset amount and recalculates inset points.
        /// This is the main API for external tools to update calculations.
        /// </summary>
        /// <param name="newInsetAmount">The new inset distance</param>
        /// <returns>The updated list of ordered inset points</returns>
        public static List<Vector3?> UpdateInsetAmountAndRecalculate(float newInsetAmount)
        {
            InsetAmount = newInsetAmount;
            RecalculateInsetPoints();
            return GetOrderedInsetPoints();
        }
        
        /// <summary>
        /// Enables or disables debug visualization display.
        /// When disabled, no debug drawing occurs in the scene view.
        /// </summary>
        /// <param name="enabled">Whether to show debug visualization</param>
        public static void SetDebugVisualizationEnabled(bool enabled)
        {
            ShowDebugVisualization = enabled;
            SceneView.RepaintAll();
        }
        

        
        /// <summary>
        /// Gets the vertex indices in the same order as OrderedInsetPoints.
        /// This allows external code to correctly map cached inset points back to vertex indices.
        /// </summary>
        /// <returns>List of vertex indices corresponding to OrderedInsetPoints positions</returns>
        public static List<int> GetOrderedVertexIndices()
        {
            var result = new List<int>();
            
            if (currentVertexPoints.Count == 0)
                return result;
                
            // Use cached sorted vertices instead of sorting again
            foreach (var vertex in sortedVerticesByIndex)
            {
                result.Add(vertex.vertexIndex);
            }
            
            return result;
        }
        
        #endregion

        #region Initialization and Events
        
        static InsetFaces_MathHelper()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            MeshSelection.objectSelectionChanged += OnSelectionChanged;
            
            // Initialize ordered inset points list
            OrderedInsetPoints = new List<Vector3?>();
        }
        
        private static void OnSelectionChanged()
        {
            // Repaint all scene views when selection changes
            SceneView.RepaintAll();
        }
        
        #endregion
        
        #region Core Calculation Pipeline
        
        /// <summary>
        /// Updates all selection data and calculates inset points in the proper hierarchical order.
        /// </summary>
        private static void UpdateSelectionData()
        {
            // Clear all collections
            ClearAllCollections();
            
            if (MeshSelection.selectedFaceCount == 0)
            {
                OrderedInsetPoints.Clear();
                return;
            }
            
            // Validate inset amount
            if (InsetAmount <= 0)
            {
                throw new System.ArgumentException($"Inset amount must be positive, got: {InsetAmount}");
            }
            
            try
            {
                // Execute calculation pipeline in proper order
                BuildVertices();
                BuildEdges();
                CheckInteriorEdges();
                BuildInsetLines();
                CheckInteriorVertices();
                BuildFaces();
                PopulateVertexEdgeConnections();
                CalculateInsetPoints();
                BuildOrderedInsetPointsList();
                
                LogCalculationSummary();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error during inset calculation: {ex.Message}");
                OrderedInsetPoints.Clear();
            }
        }
        
        /// <summary>
        /// Clears all internal collections to prepare for new calculation.
        /// </summary>
        private static void ClearAllCollections()
        {
            currentVertexPoints.Clear();
            currentEdges.Clear();
            currentInsetLines.Clear();
            edgeToInsetLine.Clear();
            calculatedInsetPoints.Clear();
            currentInsetPoints.Clear();
            currentFallbackInsetPoints.Clear();
            processedVertexIndices.Clear();
            currentSelectedFaces.Clear();
            vertexIndexLookup.Clear();
            sortedVerticesByIndex.Clear();
            OrderedInsetPoints.Clear();
        }
        
        /// <summary>
        /// Builds the ordered inset points list using already calculated results.
        /// </summary>
        private static void BuildOrderedInsetPointsList()
        {
            OrderedInsetPoints.Clear();
            
            if (currentVertexPoints.Count == 0)
                return;
            
            // Build ordered list using cached sorted vertices
            foreach (var vertex in sortedVerticesByIndex)
            {
                if (vertex.isInterior)
                {
                    OrderedInsetPoints.Add(null); // Interior vertex doesn't need inset position
                }
                else if (calculatedInsetPoints.TryGetValue(vertex.vertexIndex, out Vector3 insetPoint))
                {
                    OrderedInsetPoints.Add(insetPoint); // Use already calculated result
                }
                else
                {
                    OrderedInsetPoints.Add(null); // Failed calculation
                }
            }
        }
        
        #endregion
        
        #region Calculation Methods
        
        /// <summary>
        /// Build vertices from selected faces.
        /// </summary>
        private static void BuildVertices()
        {
            var selection = MeshSelection.top.ToArray();
            foreach (var mesh in selection)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;
                    
                var allVertices = mesh.GetVertices();
                var uniqueVertices = new HashSet<int>();
                
                // Collect all unique vertex indices from the selected faces
                foreach (var face in selectedFaces)
                {
                    foreach (var index in face.distinctIndexes)
                    {
                        uniqueVertices.Add(index);
                    }
                }
                
                // Build the vertex points list
                var vertexList = new List<int>(uniqueVertices);
                for (int i = 0; i < vertexList.Count; i++)
                {
                    int vertexIndex = vertexList[i];
                    Vector3 localPosition = allVertices[vertexIndex].position;
                    Vector3 worldPosition = mesh.transform.TransformPoint(localPosition);
                    
                    var vertexPoint = new VertexPoint(vertexIndex, worldPosition);
                    currentVertexPoints.Add(vertexPoint);
                }
            }
            
            // Find ALL partner vertices (vertices within tolerance of the same world position)
            for (int i = 0; i < currentVertexPoints.Count; i++)
            {
                var vertexPoint = currentVertexPoints[i];
                var partners = new List<int>();
                
                // Look for ALL other vertices at the same world position (within tolerance)
                for (int j = 0; j < currentVertexPoints.Count; j++)
                {
                    if (i == j) continue; // Skip self
                    
                    var otherVertex = currentVertexPoints[j];
                    
                    // Check if positions are within tolerance
                    if (Vector3.Distance(vertexPoint.worldPosition, otherVertex.worldPosition) <= POSITION_TOLERANCE)
                    {
                        partners.Add(j);
                    }
                }
                
                // Update the vertex point with partner information
                var updatedVertexPoint = new VertexPoint(vertexPoint.vertexIndex, vertexPoint.worldPosition, vertexPoint.isInterior);
                updatedVertexPoint.perimeterEdges.AddRange(vertexPoint.perimeterEdges);
                updatedVertexPoint.interiorEdges.AddRange(vertexPoint.interiorEdges);
                updatedVertexPoint.partnerVertices.AddRange(partners);
                
                currentVertexPoints[i] = updatedVertexPoint;
            }
            
            // Build vertex index lookup dictionary for O(1) access
            vertexIndexLookup.Clear();
            foreach (var vertex in currentVertexPoints)
            {
                vertexIndexLookup[vertex.vertexIndex] = vertex;
            }
            
            // Cache sorted vertices to avoid redundant sorting operations
            sortedVerticesByIndex = currentVertexPoints.OrderBy(v => v.vertexIndex).ToList();
        }
        
        /// <summary>
        /// 2. Build edges from selected faces (referencing vertices)
        /// </summary>
        private static void BuildEdges()
        {
            var selection = MeshSelection.top.ToArray();
            foreach (var mesh in selection)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;
                    
                var uniqueEdges = new HashSet<Edge>();
                
                // Collect all unique edges from the selected faces
                foreach (var face in selectedFaces)
                {
                    foreach (var edge in face.edges)
                    {
                        var normalizedEdge = new Edge(Mathf.Min(edge.a, edge.b), Mathf.Max(edge.a, edge.b));
                        uniqueEdges.Add(normalizedEdge);
                    }
                }
                
                // Build the edges list (referencing vertex points)
                foreach (var edge in uniqueEdges)
                {
                    // Use O(1) dictionary lookup instead of O(n) linear search
                    if (vertexIndexLookup.TryGetValue(edge.a, out VertexPoint vertexPointA) && 
                        vertexIndexLookup.TryGetValue(edge.b, out VertexPoint vertexPointB))
                    {
                        var edgeInfo = new EdgeInfo(edge, vertexPointA, vertexPointB);
                        currentEdges.Add(edgeInfo);
                    }
                    else
                    {
                        Debug.LogWarning($"Edge {edge.a}-{edge.b} references vertices not found in selection");
                    }
                }
            }
        }
        
        /// <summary>
        /// 2.5. Check for interior edges by comparing vertex positions
        /// </summary>
        private static void CheckInteriorEdges()
        {
            // Update edges to mark interior ones (edges with same vertex positions as another)
            for (int i = 0; i < currentEdges.Count; i++)
            {
                var edgeInfo = currentEdges[i];
                bool isInterior = false;
                
                // Check if any other edge has the same two vertex positions
                for (int j = 0; j < currentEdges.Count; j++)
                {
                    if (i == j) continue; // Skip self
                    
                    var otherEdge = currentEdges[j];
                    
                    // Check if edges have same vertex positions (in either order)
                    bool samePositions = (edgeInfo.vertexPointA.worldPosition == otherEdge.vertexPointA.worldPosition && 
                                         edgeInfo.vertexPointB.worldPosition == otherEdge.vertexPointB.worldPosition) ||
                                        (edgeInfo.vertexPointA.worldPosition == otherEdge.vertexPointB.worldPosition && 
                                         edgeInfo.vertexPointB.worldPosition == otherEdge.vertexPointA.worldPosition);
                    
                    if (samePositions)
                    {
                        isInterior = true;
                        break;
                    }
                }
                
                // Create new EdgeInfo with updated interior status
                var updatedEdgeInfo = new EdgeInfo(edgeInfo.edge, edgeInfo.vertexPointA, edgeInfo.vertexPointB, isInterior);
                currentEdges[i] = updatedEdgeInfo;
            }
        }
        
        /// <summary>
        /// Build inset lines for non-interior edges using the configured inset amount.
        /// </summary>
        private static void BuildInsetLines()
        {
            
            var selection = MeshSelection.top.ToArray();
            foreach (var mesh in selection)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;
                
                var allVertices = mesh.GetVertices();
                
                // For each non-interior edge, create an inset line
                foreach (var edgeInfo in currentEdges)
                {
                    if (edgeInfo.isInterior) continue; // Skip interior edges
                    
                    // Find a triangle that contains this edge to get the third vertex
                    Vector3 thirdVertexPosition = Vector3.zero;
                    bool foundTriangle = false;
                    
                    foreach (var face in selectedFaces)
                    {
                        // Check if this face contains our edge first
                        bool faceContainsEdge = false;
                        foreach (var faceEdge in face.edges)
                        {
                            var normalizedFaceEdge = new Edge(Mathf.Min(faceEdge.a, faceEdge.b), Mathf.Max(faceEdge.a, faceEdge.b));
                            var normalizedCurrentEdge = new Edge(Mathf.Min(edgeInfo.edge.a, edgeInfo.edge.b), Mathf.Max(edgeInfo.edge.a, edgeInfo.edge.b));
                            
                            if (normalizedFaceEdge.Equals(normalizedCurrentEdge))
                            {
                                faceContainsEdge = true;
                                break;
                            }
                        }
                        
                        if (!faceContainsEdge) continue;
                        
                        // Look through face indices to find a triangle containing our edge
                        for (int i = 0; i < face.indexes.Count - 2; i += 3)
                        {
                            int[] triangle = { face.indexes[i], face.indexes[i + 1], face.indexes[i + 2] };
                            
                            // Check if this triangle contains our edge
                            bool hasVertexA = System.Array.IndexOf(triangle, edgeInfo.edge.a) >= 0;
                            bool hasVertexB = System.Array.IndexOf(triangle, edgeInfo.edge.b) >= 0;
                            
                            if (hasVertexA && hasVertexB)
                            {
                                // Find the third vertex (not part of our edge)
                                foreach (var vertexIndex in triangle)
                                {
                                    if (vertexIndex != edgeInfo.edge.a && vertexIndex != edgeInfo.edge.b)
                                    {
                                        Vector3 localPosition = allVertices[vertexIndex].position;
                                        thirdVertexPosition = mesh.transform.TransformPoint(localPosition);
                                        foundTriangle = true;
                                        break;
                                    }
                                }
                                
                                if (foundTriangle) break;
                            }
                        }
                        
                        if (foundTriangle) break;
                    }
                    
                    if (!foundTriangle) continue;
                    
                    // Calculate edge direction and edge midpoint
                    Vector3 edgeDirection = (edgeInfo.vertexPointB.worldPosition - edgeInfo.vertexPointA.worldPosition).normalized;
                    Vector3 edgeMidpoint = (edgeInfo.vertexPointA.worldPosition + edgeInfo.vertexPointB.worldPosition) * 0.5f;
                    
                    // Calculate vector from edge midpoint to third vertex
                    Vector3 toThirdVertex = (thirdVertexPosition - edgeMidpoint);
                    
                    // Project the third vertex vector onto the plane perpendicular to the edge
                    Vector3 projectedToThird = toThirdVertex - Vector3.Project(toThirdVertex, edgeDirection);
                    
                    // This gives us the inward direction (toward the triangle interior)
                    Vector3 inwardDirection = projectedToThird.normalized;
                    
                    // Fallback: if projection fails, use cross product method
                    if (inwardDirection.magnitude < 0.001f)
                    {
                        Vector3 arbitrary = Vector3.up;
                        if (Vector3.Dot(edgeDirection, arbitrary) > 0.9f)
                            arbitrary = Vector3.forward;
                        inwardDirection = Vector3.Cross(edgeDirection, arbitrary).normalized;
                    }
                    
                    // Create inset line by moving the original edge inward using the configured amount
                    Vector3 insetStart = edgeInfo.vertexPointA.worldPosition + (inwardDirection * InsetAmount);
                    Vector3 insetEnd = edgeInfo.vertexPointB.worldPosition + (inwardDirection * InsetAmount);
                    
                    var insetLine = new InsetLine(insetStart, insetEnd);
                    currentInsetLines.Add(insetLine);
                    edgeToInsetLine[edgeInfo.edge] = insetLine;
                }
            }
        }
        
        /// <summary>
        /// 2.75. Check for interior vertices - vertices that are only connected to interior edges
        /// </summary>
        private static void CheckInteriorVertices()
        {
            // Update vertices to mark interior ones
            for (int i = 0; i < currentVertexPoints.Count; i++)
            {
                var vertexPoint = currentVertexPoints[i];
                bool isInterior = true;
                
                // Check if this vertex is connected to any exterior edges
                foreach (var edgeInfo in currentEdges)
                {
                    // If this vertex is part of an edge that's not interior (exterior edge)
                    if ((edgeInfo.vertexPointA.vertexIndex == vertexPoint.vertexIndex || 
                         edgeInfo.vertexPointB.vertexIndex == vertexPoint.vertexIndex) && 
                        !edgeInfo.isInterior)
                    {
                        isInterior = false;
                        break;
                    }
                }
                
                // Create new VertexPoint with updated interior status
                var updatedVertexPoint = new VertexPoint(vertexPoint.vertexIndex, vertexPoint.worldPosition, isInterior);
                updatedVertexPoint.perimeterEdges.AddRange(vertexPoint.perimeterEdges);
                updatedVertexPoint.interiorEdges.AddRange(vertexPoint.interiorEdges);
                updatedVertexPoint.partnerVertices.AddRange(vertexPoint.partnerVertices);
                currentVertexPoints[i] = updatedVertexPoint;
            }
        }
        
        /// <summary>
        /// 3. Build faces from selection (now that vertices and edges are ready)
        /// </summary>
        private static void BuildFaces()
        {
            var selection = MeshSelection.top.ToArray();
            foreach (var mesh in selection)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;
                    
                // Store all selected faces
                foreach (var face in selectedFaces)
                {
                    currentSelectedFaces.Add(face);
                }
            }
        }
        
        /// <summary>
        /// 4. Populate vertex edge connections - assign perimeter and interior edges to each vertex
        /// </summary>
        private static void PopulateVertexEdgeConnections()
        {
            // Update each vertex with its connected edges
            for (int i = 0; i < currentVertexPoints.Count; i++)
            {
                var vertexPoint = currentVertexPoints[i];
                var perimeterEdges = new List<EdgeInfo>();
                var interiorEdges = new List<EdgeInfo>();
                
                // Find all edges connected to this vertex
                foreach (var edgeInfo in currentEdges)
                {
                    // Check if this vertex is part of the edge
                    bool isConnected = (edgeInfo.vertexPointA.vertexIndex == vertexPoint.vertexIndex || 
                                       edgeInfo.vertexPointB.vertexIndex == vertexPoint.vertexIndex);
                    
                    if (isConnected)
                    {
                        if (edgeInfo.isInterior)
                        {
                            interiorEdges.Add(edgeInfo);
                        }
                        else
                        {
                            perimeterEdges.Add(edgeInfo);
                        }
                    }
                }
                
                // Create new VertexPoint with populated edge lists
                var updatedVertexPoint = new VertexPoint(vertexPoint.vertexIndex, vertexPoint.worldPosition, vertexPoint.isInterior);
                updatedVertexPoint.perimeterEdges.AddRange(perimeterEdges);
                updatedVertexPoint.interiorEdges.AddRange(interiorEdges);
                updatedVertexPoint.partnerVertices.AddRange(vertexPoint.partnerVertices);
                
                currentVertexPoints[i] = updatedVertexPoint;
            }
        }
        
        /// <summary>
        /// Calculate inset points for all exterior vertices using intersection methods.
        /// </summary>
        private static void CalculateInsetPoints()
        {
            processedVertexIndices.Clear();
            
            // Process all vertices
            foreach (var vertex in currentVertexPoints)
            {
                // Skip if already processed
                if (processedVertexIndices.Contains(vertex.vertexIndex)) continue;
                
                // Skip interior vertices as they don't need inset points
                if (vertex.isInterior) continue;
                
                // Calculate inset point based on vertex characteristics
                Vector3? insetPoint = null;
                
                if (vertex.partnerVertices.Count > 0)
                {
                    insetPoint = ProcessVertexWithPartners(vertex);
                }
                else
                {
                    insetPoint = ProcessVertexWithoutPartners(vertex);
                }
                
                // Store the result if successful
                if (insetPoint.HasValue)
                {
                    calculatedInsetPoints[vertex.vertexIndex] = insetPoint.Value;
                    currentInsetPoints.Add(insetPoint.Value); // Keep for visualization
                }
            }
        }
        
        /// <summary>
        /// Process a vertex that has no partners using inset line intersection.
        /// </summary>
        private static Vector3? ProcessVertexWithoutPartners(VertexPoint vertex)
        {
            // Get all perimeter edges connected to this vertex
            var connectedPerimeterEdges = vertex.perimeterEdges;
            
            // We need at least 2 perimeter edges to find an intersection
            if (connectedPerimeterEdges.Count < 2)
            {
                processedVertexIndices.Add(vertex.vertexIndex);
                Debug.LogWarning($"Vertex {vertex.vertexIndex} has insufficient perimeter edges ({connectedPerimeterEdges.Count}) for inset calculation");
                return null;
            }
            
            // Find inset lines directly from the dictionary
            var connectedInsetLines = new List<InsetLine>();
            foreach (var edgeInfo in connectedPerimeterEdges)
            {
                if (edgeToInsetLine.TryGetValue(edgeInfo.edge, out InsetLine insetLine))
                {
                    connectedInsetLines.Add(insetLine);
                }
            }
            
            // Find intersection point of the first two inset lines
            if (connectedInsetLines.Count >= 2)
            {
                Vector3 intersectionPoint = FindLineIntersection(
                    connectedInsetLines[0].startPosition,
                    connectedInsetLines[0].endPosition,
                    connectedInsetLines[1].startPosition,
                    connectedInsetLines[1].endPosition
                );
                
                // Mark as processed
                processedVertexIndices.Add(vertex.vertexIndex);
                return intersectionPoint;
            }
            
            // Mark as processed even if failed
            processedVertexIndices.Add(vertex.vertexIndex);
            Debug.LogWarning($"Vertex {vertex.vertexIndex} failed inset calculation: insufficient connected inset lines ({connectedInsetLines.Count})");
            return null;
        }
        
        /// <summary>
        /// Process a vertex that has partners using advanced intersection logic.
        /// </summary>
        private static Vector3? ProcessVertexWithPartners(VertexPoint vertex)
        {
            // Get the vertex and all its partners
            var vertexGroup = new List<VertexPoint> { vertex };
            foreach (int partnerIndex in vertex.partnerVertices)
            {
                if (partnerIndex < currentVertexPoints.Count)
                    vertexGroup.Add(currentVertexPoints[partnerIndex]);
            }
            
            bool foundIntersection = false;
            Vector3 intersectionPoint = Vector3.zero;
            
            // Try each vertex in the group
            foreach (var testVertex in vertexGroup)
            {
                // Check if this vertex has exactly one perimeter edge and at least one interior edge
                if (testVertex.perimeterEdges.Count != 1 || testVertex.interiorEdges.Count == 0)
                    continue;
                
                // Find the inset line for the single perimeter edge
                var perimeterEdge = testVertex.perimeterEdges[0];
                if (!edgeToInsetLine.TryGetValue(perimeterEdge.edge, out InsetLine insetLine))
                    continue;
                
                // Try intersecting with each interior edge
                foreach (var interiorEdge in testVertex.interiorEdges)
                {
                    Vector3 intersection = FindLineIntersection(
                        insetLine.startPosition,
                        insetLine.endPosition,
                        interiorEdge.vertexPointA.worldPosition,
                        interiorEdge.vertexPointB.worldPosition
                    );
                    
                    // Check if intersection is valid (not too far from the vertex)
                    if (Vector3.Distance(intersection, testVertex.worldPosition) <= MAX_INTERSECTION_DISTANCE)
                    {
                        intersectionPoint = intersection;
                        foundIntersection = true;
                        break;
                    }
                }
                
                if (foundIntersection) break;
            }
            
            // Mark all vertices in the group as processed
            foreach (var processedVertex in vertexGroup)
            {
                processedVertexIndices.Add(processedVertex.vertexIndex);
            }
            
            if (foundIntersection)
            {
                return intersectionPoint;
            }
            else
            {
                // Fallback: find closest approach point of all inset lines
                var allInsetLines = new List<InsetLine>();
                foreach (var testVertex in vertexGroup)
                {
                    foreach (var edgeInfo in testVertex.perimeterEdges)
                    {
                        if (edgeToInsetLine.TryGetValue(edgeInfo.edge, out InsetLine line))
                        {
                            allInsetLines.Add(line);
                        }
                    }
                }
                
                if (allInsetLines.Count > 0)
                {
                    Vector3 fallbackPoint = FindClosestApproachPoint(allInsetLines, vertex.worldPosition);
                    currentFallbackInsetPoints.Add(fallbackPoint);
                    return fallbackPoint;
                }
                
                return null;
            }
        }
        

        
        /// <summary>
        /// Find the closest approach point among multiple inset lines to a reference position
        /// </summary>
        private static Vector3 FindClosestApproachPoint(List<InsetLine> insetLines, Vector3 referencePosition)
        {
            if (insetLines.Count == 0) return referencePosition;
            
            // For simplicity, return the midpoint of the first inset line
            // Could be enhanced to find actual closest approach between multiple lines
            var firstLine = insetLines[0];
            return (firstLine.startPosition + firstLine.endPosition) * 0.5f;
        }
        
        /// <summary>
        /// Find intersection point between two 3D lines (or closest point if they don't intersect).
        /// </summary>
        private static Vector3 FindLineIntersection(Vector3 line1Start, Vector3 line1End, Vector3 line2Start, Vector3 line2End)
        {
            Vector3 line1Dir = (line1End - line1Start).normalized;
            Vector3 line2Dir = (line2End - line2Start).normalized;
            
            Vector3 w0 = line1Start - line2Start;
            
            float a = Vector3.Dot(line1Dir, line1Dir);
            float b = Vector3.Dot(line1Dir, line2Dir);
            float c = Vector3.Dot(line2Dir, line2Dir);
            float d = Vector3.Dot(line1Dir, w0);
            float e = Vector3.Dot(line2Dir, w0);
            
            float denominator = a * c - b * b;
            
            // Lines are parallel or nearly parallel
            if (Mathf.Abs(denominator) < PARALLEL_LINE_THRESHOLD)
            {
                // Return the midpoint between the closest points on each line
                return (line1Start + line2Start) * 0.5f;
            }
            
            float t1 = (b * e - c * d) / denominator;
            float t2 = (a * e - b * d) / denominator;
            
            Vector3 point1 = line1Start + t1 * line1Dir;
            Vector3 point2 = line2Start + t2 * line2Dir;
            
            // Return the midpoint between the closest points
            return (point1 + point2) * 0.5f;
        }
        

        
        /// <summary>
        /// Logs a summary of the calculation results for debugging purposes.
        /// </summary>
        private static void LogCalculationSummary()
        {
            if (currentVertexPoints.Count == 0) return;
            
            int interiorVertices = currentVertexPoints.Count(v => v.isInterior);
            int exteriorVertices = currentVertexPoints.Count - interiorVertices;
            int interiorEdges = currentEdges.Count(e => e.isInterior);
            int exteriorEdges = currentEdges.Count - interiorEdges;
            int verticesWithPartners = currentVertexPoints.Count(v => v.partnerVertices.Count > 0);
            int successfulInsetPoints = OrderedInsetPoints.Count(p => p.HasValue);
        }
        
        #endregion
        
        #region Scene Visualization
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            // Check for selection changes using our generic method
            CheckForSelectionChange();
            
            // Only show visualizer if debug visualization is enabled and we have ProBuilder faces selected
            if (!ShowDebugVisualization || MeshSelection.selectedFaceCount == 0)
                return;
                
            var selection = MeshSelection.top.ToArray();
            foreach (var mesh in selection)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                
                if (selectedFaces == null || selectedFaces.Length == 0)
                    continue;
                    
                DrawCornerDots(mesh, selectedFaces);
            }
        }
        
        private static void DrawCornerDots(ProBuilderMesh mesh, Face[] faces)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            
            // Draw only inset lines in cyan
            foreach (var insetLine in currentInsetLines)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(insetLine.startPosition, insetLine.endPosition, 2.0f);
            }
        }
        
        #endregion
    }
}

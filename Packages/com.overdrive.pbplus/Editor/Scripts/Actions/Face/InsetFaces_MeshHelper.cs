using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Mesh utility helper for inset face operations.
    /// Handles all mesh-related operations including applying insets, geometry cleanup, and mesh validation.
    /// </summary>
    public static class InsetFaces_MeshHelper
    {
        private const float TOLERANCE_RATIO = 0.25f; // Tolerance as a ratio of inset distance
        private const float POSITION_TOLERANCE = 0.001f; // Tolerance for position comparisons
        private const float PARALLEL_LINE_THRESHOLD = 0.001f; // Threshold for determining parallel lines
        private const float ZERO_AREA_THRESHOLD = 0.0001f; // Threshold for zero-area faces
        
        /// <summary>
        /// Normalize an edge by ensuring the smaller index comes first
        /// </summary>
        public static Edge NormalizeEdge(Edge edge)
        {
            return new Edge(Mathf.Min(edge.a, edge.b), Mathf.Max(edge.a, edge.b));
        }

        /// <summary>
        /// Apply inset using pre-calculated vertex positions
        /// </summary>
        public static bool ApplyInset(ProBuilderMesh mesh, Face[] faces, Dictionary<int, Vector3> preCalculatedPositions)
        {
            try
            {
                if (preCalculatedPositions == null || preCalculatedPositions.Count == 0) 
                    return false;

                // Create position-based movement mapping
                var positionMovements = new Dictionary<Vector3, Vector3>();
                foreach (var kvp in preCalculatedPositions)
                {
                    Vector3 originalWorldPos = mesh.transform.TransformPoint(mesh.GetVertices()[kvp.Key].position);
                    Vector3 targetWorldPos = kvp.Value;
                    positionMovements[originalWorldPos] = targetWorldPos;
                }

                // Cache original face positions for finding them after extrusion
                var originalFaceData = new List<(Face face, Vector3[] worldPositions)>();
                foreach (var face in faces)
                {
                    var originalVertices = mesh.GetVertices(face.indexes);
                    var cachedWorldPositions = originalVertices.Select(v => mesh.transform.TransformPoint(v.position)).ToArray();
                    originalFaceData.Add((face, cachedWorldPositions));
                }

                // Extrude all faces with 0 distance to create surrounding geometry
                var extrudedFaces = mesh.Extrude(faces, ExtrudeMethod.FaceNormal, 0f);
                if (extrudedFaces == null || extrudedFaces.Length == 0) return false;

                // Find the inner faces that match our cached positions
                var innerFaces = new List<Face>();
                foreach (var (originalFace, cachedPositions) in originalFaceData)
                {
                    var innerFace = FindMatchingFace(mesh, cachedPositions);
                    if (innerFace != null)
                        innerFaces.Add(innerFace);
                }

                if (innerFaces.Count == 0) return false;

                // Apply the pre-calculated movements using position-based mapping
                foreach (var innerFace in innerFaces)
                {
                    foreach (var vertexIndex in innerFace.distinctIndexes)
                    {
                        Vector3 currentWorldPos = mesh.transform.TransformPoint(mesh.GetVertices()[vertexIndex].position);
                        
                        // Find the target position for this vertex based on its current position
                        Vector3 targetWorldPos = Vector3.zero;
                        bool foundTarget = false;
                        
                        foreach (var kvp in positionMovements)
                        {
                            if (Vector3.Distance(currentWorldPos, kvp.Key) < POSITION_TOLERANCE)
                            {
                                targetWorldPos = kvp.Value;
                                foundTarget = true;
                                break;
                            }
                        }
                        
                        if (foundTarget)
                        {
                            Vector3 newLocalPos = mesh.transform.InverseTransformPoint(targetWorldPos);
                            Vector3 currentLocalPos = mesh.GetVertices()[vertexIndex].position;
                            Vector3 offset = newLocalPos - currentLocalPos;
                            mesh.TranslateVertices(new int[] { vertexIndex }, offset);
                        }
                    }
                }

                // Clean up and select result
                CleanupGeometry(mesh);
                mesh.SetSelectedFaces(innerFaces.ToArray());

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to inset faces: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculate inset positions for preview - returns vertex positions without modifying mesh
        /// </summary>
        public static Dictionary<int, Vector3> CalculateInsetPositionsForPreview(ProBuilderMesh mesh, Face[] faces, float insetDistance)
        {
            // Clamp distance to reasonable bounds
            insetDistance = Mathf.Clamp(insetDistance, 0.01f, 100f);
            
            // Get perimeter edges
            var truePerimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faces).ToList();
            
            return CalculateInsetIntersectionsWithDistance(mesh, faces, truePerimeterEdges, insetDistance);
        }

        /// <summary>
        /// Calculate inset positions with specified inset distance
        /// </summary>
        private static Dictionary<int, Vector3> CalculateInsetIntersectionsWithDistance(ProBuilderMesh mesh, Face[] faces, List<Edge> truePerimeterEdges, float insetDistance)
        {
            var allVertices = mesh.GetVertices();
            float tolerance = insetDistance * TOLERANCE_RATIO; // Tolerance as fraction of inset distance
            
            // First pass: store inset data for each edge (same as visualizer)
            var edgeInsetData = new Dictionary<Edge, (Vector3 center, Vector3 direction)>();
            
            foreach (var edge in truePerimeterEdges)
            {
                // Find which face this edge belongs to
                Face parentFace = null;
                foreach (var face in faces)
                {
                    if (face.edges.Contains(edge))
                    {
                        parentFace = face;
                        break;
                    }
                }
                
                if (parentFace == null) continue;

                // Get edge positions in world space
                Vector3 edgeStart = mesh.transform.TransformPoint(allVertices[edge.a].position);
                Vector3 edgeEnd = mesh.transform.TransformPoint(allVertices[edge.b].position);
                Vector3 edgeDirection = (edgeEnd - edgeStart).normalized;
                Vector3 edgeCenter = (edgeStart + edgeEnd) * 0.5f;
                
                // Find the third vertex in the triangle containing this edge
                Vector3 thirdVertexPos = Vector3.zero;
                bool foundThirdVertex = false;
                
                var triangles = parentFace.indexes;
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    int v1 = triangles[t];
                    int v2 = triangles[t + 1];
                    int v3 = triangles[t + 2];
                    
                    // Check if this triangle contains our edge
                    bool hasEdgeA = (v1 == edge.a || v2 == edge.a || v3 == edge.a);
                    bool hasEdgeB = (v1 == edge.b || v2 == edge.b || v3 == edge.b);
                    
                    if (hasEdgeA && hasEdgeB)
                    {
                        // Find the third vertex (not part of the edge)
                        int thirdVertex = (v1 != edge.a && v1 != edge.b) ? v1 :
                                         (v2 != edge.a && v2 != edge.b) ? v2 : v3;
                        thirdVertexPos = mesh.transform.TransformPoint(allVertices[thirdVertex].position);
                        foundThirdVertex = true;
                        break;
                    }
                }
                
                if (!foundThirdVertex) continue;
                
                // Calculate inset direction: toward third vertex, perpendicular to edge
                Vector3 toThirdVertex = (thirdVertexPos - edgeCenter).normalized;
                Vector3 insetDirection = Vector3.ProjectOnPlane(toThirdVertex, edgeDirection).normalized;

                // Store edge inset data
                edgeInsetData[edge] = (edgeCenter, insetDirection);
            }

            // Second pass: calculate intersection points for vertices (same as visualizer)
            var perimeterVertices = new HashSet<int>();
            foreach (var edge in truePerimeterEdges)
            {
                perimeterVertices.Add(edge.a);
                perimeterVertices.Add(edge.b);
            }
            
            var allNewVertexPositions = new Dictionary<int, Vector3>();

            foreach (var vertexIndex in perimeterVertices)
            {
                // Skip if we already calculated a position for this vertex
                if (allNewVertexPositions.ContainsKey(vertexIndex))
                    continue;
                    
                // Find the edges that share this vertex
                var vertexEdges = new List<Edge>();
                foreach (var edge in truePerimeterEdges)
                {
                    if (edge.a == vertexIndex || edge.b == vertexIndex)
                    {
                        vertexEdges.Add(edge);
                    }
                }

                if (vertexEdges.Count == 2 && edgeInsetData.ContainsKey(vertexEdges[0]) && edgeInsetData.ContainsKey(vertexEdges[1]))
                {
                    // Calculate intersection of the two inset edges
                    var edge1Data = edgeInsetData[vertexEdges[0]];
                    var edge2Data = edgeInsetData[vertexEdges[1]];
                    
                    // Get the original edge endpoints
                    var edge1 = vertexEdges[0];
                    var edge2 = vertexEdges[1];
                    
                    Vector3 edge1Start = mesh.transform.TransformPoint(allVertices[edge1.a].position);
                    Vector3 edge1End = mesh.transform.TransformPoint(allVertices[edge1.b].position);
                    
                    Vector3 edge2Start = mesh.transform.TransformPoint(allVertices[edge2.a].position);
                    Vector3 edge2End = mesh.transform.TransformPoint(allVertices[edge2.b].position);
                    
                    // Create inset lines parallel to original edges, moved inward by inset distance
                    Vector3 insetEdge1Start = edge1Start + edge1Data.direction * insetDistance;
                    Vector3 insetEdge1End = edge1End + edge1Data.direction * insetDistance;
                    
                    Vector3 insetEdge2Start = edge2Start + edge2Data.direction * insetDistance;
                    Vector3 insetEdge2End = edge2End + edge2Data.direction * insetDistance;
                    
                    // Find intersection of the two inset edge lines
                    Vector3 intersectionPoint = CalculateLineIntersection(
                        insetEdge1Start, insetEdge1End,
                        insetEdge2Start, insetEdge2End, tolerance);
                    
                    if (intersectionPoint != Vector3.zero)
                    {
                        allNewVertexPositions[vertexIndex] = intersectionPoint;
                    }
                }
                else if (vertexEdges.Count > 2)
                {
                    // For vertices with more than 2 perimeter edges, calculate all pairwise intersections and average them
                    var validIntersections = new List<Vector3>();
                    
                    for (int i = 0; i < vertexEdges.Count; i++)
                    {
                        for (int j = i + 1; j < vertexEdges.Count; j++)
                        {
                            var edge1 = vertexEdges[i];
                            var edge2 = vertexEdges[j];
                            
                            if (!edgeInsetData.ContainsKey(edge1) || !edgeInsetData.ContainsKey(edge2))
                                continue;
                                
                            var edge1Data = edgeInsetData[edge1];
                            var edge2Data = edgeInsetData[edge2];
                            
                            Vector3 edge1Start = mesh.transform.TransformPoint(allVertices[edge1.a].position);
                            Vector3 edge1End = mesh.transform.TransformPoint(allVertices[edge1.b].position);
                            
                            Vector3 edge2Start = mesh.transform.TransformPoint(allVertices[edge2.a].position);
                            Vector3 edge2End = mesh.transform.TransformPoint(allVertices[edge2.b].position);
                            
                            Vector3 insetEdge1Start = edge1Start + edge1Data.direction * insetDistance;
                            Vector3 insetEdge1End = edge1End + edge1Data.direction * insetDistance;
                            
                            Vector3 insetEdge2Start = edge2Start + edge2Data.direction * insetDistance;
                            Vector3 insetEdge2End = edge2End + edge2Data.direction * insetDistance;
                            
                            Vector3 intersectionPoint = CalculateLineIntersection(
                                insetEdge1Start, insetEdge1End,
                                insetEdge2Start, insetEdge2End, tolerance);
                            
                            if (intersectionPoint != Vector3.zero)
                            {
                                validIntersections.Add(intersectionPoint);
                            }
                        }
                    }
                    
                    if (validIntersections.Count > 0)
                    {
                        // Average all valid intersection points
                        Vector3 averagePosition = Vector3.zero;
                        foreach (var intersection in validIntersections)
                        {
                            averagePosition += intersection;
                        }
                        averagePosition /= validIntersections.Count;
                        allNewVertexPositions[vertexIndex] = averagePosition;
                    }
                }
                else if (vertexEdges.Count == 1 && edgeInsetData.ContainsKey(vertexEdges[0]))
                {
                    // For vertices with only one perimeter edge (end vertices), 
                    // move along the inset direction of that edge
                    var edgeData = edgeInsetData[vertexEdges[0]];
                    Vector3 originalPos = mesh.transform.TransformPoint(allVertices[vertexIndex].position);
                    allNewVertexPositions[vertexIndex] = originalPos + edgeData.direction * insetDistance;
                }
            }

            // Handle vertices on shared edges (same as visualizer)
            var finalVertexPositions = new Dictionary<int, Vector3>(allNewVertexPositions);
            var sharedEdges = FindSharedEdges(faces.ToList(), mesh);
            
            // Find intersections between inset edges and shared edges
            foreach (var face in faces)
            {
                foreach (var sharedEdgeRaw in face.edges)
                {
                    var normalizedSharedEdge = NormalizeEdge(sharedEdgeRaw);
                    if (!sharedEdges.Contains(normalizedSharedEdge)) 
                        continue;
                    
                    // Get the shared edge line in world space
                    Vector3 sharedStart = mesh.transform.TransformPoint(allVertices[sharedEdgeRaw.a].position);
                    Vector3 sharedEnd = mesh.transform.TransformPoint(allVertices[sharedEdgeRaw.b].position);
                    
                    // Check intersection with each inset edge
                    foreach (var edgeData in edgeInsetData)
                    {
                        var edge = edgeData.Key;
                        var insetData = edgeData.Value;
                        
                        // Get original edge endpoints
                        Vector3 edgeStart = mesh.transform.TransformPoint(allVertices[edge.a].position);
                        Vector3 edgeEnd = mesh.transform.TransformPoint(allVertices[edge.b].position);
                        
                        // Create inset line parallel to original edge
                        Vector3 insetStart = edgeStart + insetData.direction * insetDistance;
                        Vector3 insetEnd = edgeEnd + insetData.direction * insetDistance;
                        
                        // Find intersection between inset line and shared edge
                        Vector3 intersection = CalculateLineIntersection(insetStart, insetEnd, sharedStart, sharedEnd, tolerance);
                        
                        if (intersection != Vector3.zero)
                        {
                            bool onInsetSegment = IsPointOnLineSegment(intersection, insetStart, insetEnd, tolerance);
                            bool onSharedSegment = IsPointOnLineSegment(intersection, sharedStart, sharedEnd, tolerance);
                            
                            if (onInsetSegment && onSharedSegment)
                            {
                                // Find which vertex of the shared edge this intersection is closest to
                                float distToStart = Vector3.Distance(intersection, sharedStart);
                                float distToEnd = Vector3.Distance(intersection, sharedEnd);
                                
                                int closestVertexIndex = (distToStart < distToEnd) ? sharedEdgeRaw.a : sharedEdgeRaw.b;
                                
                                // Only override if this vertex was involved in the inset calculation
                                if (allNewVertexPositions.ContainsKey(closestVertexIndex))
                                {
                                    finalVertexPositions[closestVertexIndex] = intersection;
                                }
                            }
                        }
                    }
                }
            }
            
            return finalVertexPositions;
        }

        /// <summary>
        /// Calculate the intersection point of two 3D lines (same as visualizer)
        /// </summary>
        private static Vector3 CalculateLineIntersection(Vector3 line1Start, Vector3 line1End, Vector3 line2Start, Vector3 line2End, float tolerance = 0.1f)
        {
            Vector3 line1Dir = (line1End - line1Start).normalized;
            Vector3 line2Dir = (line2End - line2Start).normalized;
            
            Vector3 startDiff = line2Start - line1Start;
            Vector3 cross = Vector3.Cross(line1Dir, line2Dir);
            
            // Check if lines are parallel
            if (cross.magnitude < PARALLEL_LINE_THRESHOLD)
            {
                // Lines are parallel - check if they're colinear by projecting one point onto the other line
                Vector3 projection = Vector3.Project(startDiff, line1Dir);
                Vector3 closestPoint = line1Start + projection;
                float distanceToLine = Vector3.Distance(line2Start, closestPoint);
                
                if (distanceToLine < tolerance * 0.1f) // Lines are essentially colinear
                {
                    // Return the midpoint of the overlapping region
                    return (line1Start + line2Start) * 0.5f;
                }
                return Vector3.zero; // Parallel but not intersecting
            }
            
            // For skew lines in 3D, find the closest points on each line
            float denominator = cross.sqrMagnitude;
            
            Vector3 cross1 = Vector3.Cross(startDiff, line2Dir);
            Vector3 cross2 = Vector3.Cross(startDiff, line1Dir);
            
            float t1 = Vector3.Dot(cross1, cross) / denominator;
            float t2 = Vector3.Dot(cross2, cross) / denominator;
            
            Vector3 point1 = line1Start + t1 * line1Dir;
            Vector3 point2 = line2Start + t2 * line2Dir;
            
            // If the lines are close enough, return the midpoint
            if (Vector3.Distance(point1, point2) < tolerance)
            {
                return (point1 + point2) * 0.5f;
            }
            
            return Vector3.zero; // Lines don't intersect within tolerance
        }

        /// <summary>
        /// Check if a point lies on a line segment within a given tolerance
        /// </summary>
        private static bool IsPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd, float tolerance)
        {
            Vector3 lineDir = lineEnd - lineStart;
            Vector3 pointDir = point - lineStart;
            
            // Project point onto line
            float lineLength = lineDir.magnitude;
            if (lineLength < POSITION_TOLERANCE) return false;
            
            float projection = Vector3.Dot(pointDir, lineDir.normalized);
            
            // Check if projection is within line segment bounds
            if (projection < -tolerance || projection > lineLength + tolerance)
                return false;
            
            // Check if point is close enough to the line
            Vector3 projectedPoint = lineStart + lineDir.normalized * projection;
            float distance = Vector3.Distance(point, projectedPoint);
            
            return distance <= tolerance;
        }

        /// <summary>
        /// Find the face that matches the cached world positions
        /// </summary>
        private static Face FindMatchingFace(ProBuilderMesh mesh, Vector3[] cachedWorldPositions)
        {
            var allFaces = mesh.faces;
            var sortedCachedPositions = cachedWorldPositions.OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToArray();
            
            for (int i = 0; i < allFaces.Count; i++)
            {
                var face = allFaces[i];
                var faceVertices = mesh.GetVertices(face.indexes);
                var worldPositions = faceVertices.Select(v => mesh.transform.TransformPoint(v.position)).ToArray();
                
                // Quick length check first
                if (worldPositions.Length != cachedWorldPositions.Length) continue;
                
                // Sort face positions for comparison
                var sortedFacePositions = worldPositions.OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToArray();
                
                // Compare sorted arrays
                bool matches = true;
                for (int j = 0; j < sortedCachedPositions.Length; j++)
                {
                    if (Vector3.Distance(sortedCachedPositions[j], sortedFacePositions[j]) > POSITION_TOLERANCE)
                    {
                        matches = false;
                        break;
                    }
                }
                
                if (matches)
                {
                    return face;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Find edges that are shared between multiple faces using ProBuilder's ElementSelection
        /// </summary>
        public static HashSet<Edge> FindSharedEdges(List<Face> faces, ProBuilderMesh mesh)
        {
            var sharedEdges = new HashSet<Edge>();

            // For each face, check each edge to see if it's shared with another face
            foreach (var face in faces)
            {
                foreach (var edge in face.edges)
                {
                    var neighborFaces = new List<Face>();
                    ElementSelection.GetNeighborFaces(mesh, edge, neighborFaces);

                    // Count how many of the neighbor faces are in our selected faces list
                    int selectedNeighborCount = 0;
                    foreach (var neighborFace in neighborFaces)
                    {
                        if (faces.Contains(neighborFace))
                            selectedNeighborCount++;
                    }

                    // If this edge has more than one selected face as neighbors, it's shared
                    if (selectedNeighborCount > 1)
                    {
                        var normalizedEdge = NormalizeEdge(edge);
                        sharedEdges.Add(normalizedEdge);
                    }
                }
            }

            return sharedEdges;
        }

        /// <summary>
        /// Clean up geometry using ProBuilder's mesh validation tools
        /// </summary>
        private static void CleanupGeometry(ProBuilderMesh mesh)
        {
            try
            {
                // Use ProBuilder's cleanup tools to remove degenerate triangles
                var removedVertices = new List<int>();
                MeshValidation.RemoveDegenerateTriangles(mesh, removedVertices);
                
                // Remove unused vertices
                MeshValidation.RemoveUnusedVertices(mesh);

                // Weld all vertices with very small distance to clean up any overlapping vertices
                var allVertexIndices = new List<int>();
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    allVertexIndices.Add(i);
                }
                
                var weldedVertices = VertexEditing.WeldVertices(mesh, allVertexIndices, ZERO_AREA_THRESHOLD);

                // Final mesh validation and refresh
                mesh.ToMesh();
                mesh.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Cleanup geometry failed: {e.Message}");
            }
        }

        /// <summary>
        /// Finalize mesh after operations - handles ToMesh, Refresh, and Optimize
        /// </summary>
        public static void FinalizeMesh(ProBuilderMesh mesh)
        {
            mesh.ToMesh();
            mesh.Refresh();
            mesh.Optimize();
        }
    }
}

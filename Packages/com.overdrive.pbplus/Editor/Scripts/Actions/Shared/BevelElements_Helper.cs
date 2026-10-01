using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Helper class for vertex beveling operations.
    /// Handles the complex logic of splitting vertices and creating center faces.
    /// </summary>
    public static class BevelElements_Helper
    {
        /// <summary>
        /// Bevels vertices by splitting them and creating center faces.
        /// Returns the number of vertices successfully beveled.
        /// </summary>
        /// <param name="mesh">The mesh to modify</param>
        /// <param name="vertexIndices">The vertices to bevel</param>
        /// <param name="distance">The distance to expand along each connected edge</param>
        /// <returns>Number of vertices successfully beveled</returns>
        public static int BevelVertices(ProBuilderMesh mesh, int[] vertexIndices, float distance)
        {
            // DISABLED: Vertex beveling is temporarily disabled - complex implementation needs more work
            Debug.LogWarning("Vertex beveling is currently disabled. Use Edge or Face beveling instead.");
            return 0;
            
            // if (mesh == null || vertexIndices == null || vertexIndices.Length == 0 || distance <= 0)
            //     return 0;

            // int successCount = 0;

            // foreach (int vertexIndex in vertexIndices)
            // {
            //     if (BevelSingleVertex(mesh, vertexIndex, distance))
            //     {
            //         successCount++;
            //     }
            // }

            // return successCount;
        }

        /// <summary>
        /// Bevels a single vertex by splitting it and creating a center face.
        /// </summary>
        /// <param name="mesh">The mesh to modify</param>
        /// <param name="vertexIndex">The vertex to bevel</param>
        /// <param name="distance">The distance to expand along each connected edge</param>
        /// <returns>True if successful, false otherwise</returns>
        private static bool BevelSingleVertex(ProBuilderMesh mesh, int vertexIndex, float distance)
        {
            try
            {
                // Step 1: Get all connected faces and edges
                var connectedFaces = GetConnectedFaces(mesh, vertexIndex);
                var connectedEdges = GetConnectedEdges(mesh, vertexIndex);
                
                if (connectedFaces.Count < 2 || connectedEdges.Count < 2)
                {
                    Debug.LogWarning($"Vertex {vertexIndex} has insufficient connections for beveling (faces: {connectedFaces.Count}, edges: {connectedEdges.Count})");
                    return false;
                }
                
                // Step 2: Calculate the average normal for the center face orientation
                var centerNormal = CalculateWeightedAverageNormal(mesh, connectedFaces);
                
                // Step 3: Find all unique vertices connected to this vertex through edges
                var connectedVertexIndices = GetConnectedVertexIndices(mesh, vertexIndex, connectedEdges);
                
                if (connectedVertexIndices.Count < 2)
                {
                    Debug.LogWarning($"Vertex {vertexIndex} has insufficient connected vertices for beveling");
                    return false;
                }
                
                // Step 4: Create new vertices by moving along each connected edge
                var newVertexPositions = CreateBevelVertexPositions(mesh, vertexIndex, connectedVertexIndices, distance);
                
                if (newVertexPositions.Count != connectedVertexIndices.Count)
                {
                    Debug.LogError($"Failed to create bevel vertex positions for vertex {vertexIndex}");
                    return false;
                }
                
                // Step 5: Apply the vertex bevel by modifying the mesh
                return ApplyVertexBevel(mesh, vertexIndex, connectedFaces, newVertexPositions, centerNormal);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error beveling vertex {vertexIndex}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets all faces connected to a vertex, taking into account shared vertices.
        /// </summary>
        /// <param name="mesh">The mesh to search</param>
        /// <param name="vertexIndex">The vertex index</param>
        /// <returns>List of faces connected to the vertex</returns>
        public static List<Face> GetConnectedFaces(ProBuilderMesh mesh, int vertexIndex)
        {
            var connectedFaces = new List<Face>();
            
            // Get all shared vertex indices for this vertex
            var sharedVertices = mesh.sharedVertices;
            var sharedIndices = new HashSet<int>();
            
            foreach (var sharedGroup in sharedVertices)
            {
                if (sharedGroup.Contains(vertexIndex))
                {
                    foreach (var index in sharedGroup)
                    {
                        sharedIndices.Add(index);
                    }
                    break;
                }
            }
            
            // If no shared vertices found, just use the vertex itself
            if (sharedIndices.Count == 0)
            {
                sharedIndices.Add(vertexIndex);
            }
            
            // Find all faces that contain any of the shared vertex indices
            foreach (var face in mesh.faces)
            {
                foreach (var index in face.distinctIndexes)
                {
                    if (sharedIndices.Contains(index))
                    {
                        connectedFaces.Add(face);
                        break;
                    }
                }
            }
            
            return connectedFaces;
        }

        /// <summary>
        /// Gets all edges connected to a vertex.
        /// </summary>
        /// <param name="mesh">The mesh to search</param>
        /// <param name="vertexIndex">The vertex index</param>
        /// <returns>List of edges connected to the vertex</returns>
        public static List<Edge> GetConnectedEdges(ProBuilderMesh mesh, int vertexIndex)
        {
            var connectedEdges = new List<Edge>();
            
            // Get all shared vertex indices for this vertex
            var sharedVertices = mesh.sharedVertices;
            var sharedIndices = new HashSet<int>();
            
            foreach (var sharedGroup in sharedVertices)
            {
                if (sharedGroup.Contains(vertexIndex))
                {
                    foreach (var index in sharedGroup)
                    {
                        sharedIndices.Add(index);
                    }
                    break;
                }
            }
            
            // If no shared vertices found, just use the vertex itself
            if (sharedIndices.Count == 0)
            {
                sharedIndices.Add(vertexIndex);
            }
            
            // Find all edges that contain any of the shared vertex indices
            foreach (var face in mesh.faces)
            {
                foreach (var edge in face.edges)
                {
                    if ((sharedIndices.Contains(edge.a) || sharedIndices.Contains(edge.b)) &&
                        !connectedEdges.Contains(edge))
                    {
                        connectedEdges.Add(edge);
                    }
                }
            }
            
            return connectedEdges;
        }

        /// <summary>
        /// Calculates the average normal of connected faces, weighted by face area.
        /// </summary>
        /// <param name="mesh">The mesh</param>
        /// <param name="faces">The faces to average</param>
        /// <returns>The weighted average normal</returns>
        public static Vector3 CalculateWeightedAverageNormal(ProBuilderMesh mesh, List<Face> faces)
        {
            if (faces == null || faces.Count == 0)
                return Vector3.up;

            Vector3 weightedNormal = Vector3.zero;
            float totalWeight = 0f;
            var vertices = mesh.GetVertices();

            foreach (var face in faces)
            {
                var normal = UnityEngine.ProBuilder.Math.Normal(mesh, face);
                
                // Calculate face area using first triangle of the face
                float area = 1.0f; // Default weight if area calculation fails
                if (face.indexes.Count >= 3)
                {
                    var v1 = mesh.transform.TransformPoint(vertices[face.indexes[0]].position);
                    var v2 = mesh.transform.TransformPoint(vertices[face.indexes[1]].position);
                    var v3 = mesh.transform.TransformPoint(vertices[face.indexes[2]].position);
                    area = UnityEngine.ProBuilder.Math.TriangleArea(v1, v2, v3);
                }
                
                weightedNormal += normal * area;
                totalWeight += area;
            }

            if (totalWeight > 0f)
            {
                weightedNormal /= totalWeight;
            }

            return weightedNormal.normalized;
        }

        /// <summary>
        /// Gets all vertex indices connected to the given vertex through edges.
        /// </summary>
        /// <param name="mesh">The mesh</param>
        /// <param name="vertexIndex">The central vertex</param>
        /// <param name="connectedEdges">The edges connected to the vertex</param>
        /// <returns>List of connected vertex indices</returns>
        private static List<int> GetConnectedVertexIndices(ProBuilderMesh mesh, int vertexIndex, List<Edge> connectedEdges)
        {
            var connectedVertices = new HashSet<int>();
            
            // Get all shared vertex indices for the central vertex
            var sharedVertices = mesh.sharedVertices;
            var centralSharedIndices = new HashSet<int>();
            
            foreach (var sharedGroup in sharedVertices)
            {
                if (sharedGroup.Contains(vertexIndex))
                {
                    foreach (var index in sharedGroup)
                    {
                        centralSharedIndices.Add(index);
                    }
                    break;
                }
            }
            
            if (centralSharedIndices.Count == 0)
            {
                centralSharedIndices.Add(vertexIndex);
            }
            
            // Find all vertices connected through edges
            foreach (var edge in connectedEdges)
            {
                if (centralSharedIndices.Contains(edge.a))
                {
                    connectedVertices.Add(edge.b);
                }
                else if (centralSharedIndices.Contains(edge.b))
                {
                    connectedVertices.Add(edge.a);
                }
            }
            
            return connectedVertices.ToList();
        }

        /// <summary>
        /// Creates new vertex positions for beveling by moving along connected edges.
        /// </summary>
        /// <param name="mesh">The mesh</param>
        /// <param name="centralVertexIndex">The vertex being beveled</param>
        /// <param name="connectedVertexIndices">The vertices connected to the central vertex</param>
        /// <param name="distance">The distance to move along each edge</param>
        /// <returns>Dictionary mapping connected vertex indices to new positions</returns>
        private static Dictionary<int, Vector3> CreateBevelVertexPositions(ProBuilderMesh mesh, int centralVertexIndex, List<int> connectedVertexIndices, float distance)
        {
            var newPositions = new Dictionary<int, Vector3>();
            var vertices = mesh.GetVertices();
            var centralPos = vertices[centralVertexIndex].position;
            
            foreach (var connectedIndex in connectedVertexIndices)
            {
                var connectedPos = vertices[connectedIndex].position;
                var edgeDirection = (connectedPos - centralPos).normalized;
                
                // Move from central vertex toward connected vertex by the specified distance
                var newPos = centralPos + (edgeDirection * distance);
                newPositions[connectedIndex] = newPos;
            }
            
            return newPositions;
        }

        /// <summary>
        /// Applies the vertex bevel by modifying the mesh geometry.
        /// This creates new vertices and updates face indices to create the bevel effect.
        /// </summary>
        /// <param name="mesh">The mesh to modify</param>
        /// <param name="centralVertexIndex">The vertex being beveled</param>
        /// <param name="connectedFaces">The faces connected to the vertex</param>
        /// <param name="newVertexPositions">The new vertex positions</param>
        /// <param name="centerNormal">The normal for the center face</param>
        /// <returns>True if successful</returns>
        private static bool ApplyVertexBevel(ProBuilderMesh mesh, int centralVertexIndex, List<Face> connectedFaces, Dictionary<int, Vector3> newVertexPositions, Vector3 centerNormal)
        {
            try
            {
                var vertices = mesh.GetVertices().ToList();
                var faces = mesh.faces.ToList();
                
                // Step 1: Create new vertices for each edge direction
                var newVertexIndices = new Dictionary<int, int>();
                foreach (var kvp in newVertexPositions)
                {
                    var originalVertex = vertices[centralVertexIndex];
                    var newVertex = new Vertex();
                    newVertex.position = kvp.Value;
                    newVertex.normal = originalVertex.normal;
                    newVertex.tangent = originalVertex.tangent;
                    newVertex.uv0 = originalVertex.uv0;
                    newVertex.uv2 = originalVertex.uv2;
                    newVertex.uv3 = originalVertex.uv3;
                    newVertex.uv4 = originalVertex.uv4;
                    newVertex.color = originalVertex.color;
                    
                    vertices.Add(newVertex);
                    newVertexIndices[kvp.Key] = vertices.Count - 1;
                }
                
                // Step 2: Create center vertex
                var centerVertex = new Vertex();
                centerVertex.position = CalculateCenterPosition(mesh, centralVertexIndex, newVertexPositions);
                centerVertex.normal = centerNormal;
                centerVertex.tangent = vertices[centralVertexIndex].tangent;
                centerVertex.uv0 = vertices[centralVertexIndex].uv0;
                centerVertex.uv2 = vertices[centralVertexIndex].uv2;
                centerVertex.uv3 = vertices[centralVertexIndex].uv3;
                centerVertex.uv4 = vertices[centralVertexIndex].uv4;
                centerVertex.color = vertices[centralVertexIndex].color;
                
                vertices.Add(centerVertex);
                int centerVertexIndex = vertices.Count - 1;
                
                // Step 3: Update existing faces to use new vertices instead of central vertex
                var updatedFaces = UpdateFacesForVertexBevel(mesh, faces, centralVertexIndex, newVertexIndices, connectedFaces);
                
                // Step 4: Create center face connecting all the new vertices
                var centerFace = CreateCenterFace(newVertexIndices, centerVertexIndex, centerNormal);
                if (centerFace != null)
                {
                    updatedFaces.Add(centerFace);
                }
                
                // Step 5: Apply changes to mesh
                mesh.SetVertices(vertices);
                mesh.faces = updatedFaces.ToArray();
                
                // Step 6: Rebuild shared vertices to maintain topology
                var allVertices = mesh.GetVertices();
                Vector3[] allPositions = new Vector3[allVertices.Length];
                for (int i = 0; i < allVertices.Length; i++)
                {
                    allPositions[i] = allVertices[i].position;
                }
                mesh.sharedVertices = SharedVertex.GetSharedVerticesWithPositions(allPositions);
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error applying vertex bevel: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculates the center position for the new center face.
        /// </summary>
        private static Vector3 CalculateCenterPosition(ProBuilderMesh mesh, int centralVertexIndex, Dictionary<int, Vector3> newVertexPositions)
        {
            var vertices = mesh.GetVertices();
            var originalPos = vertices[centralVertexIndex].position;
            
            // Use the average of the new vertex positions, slightly inset toward the original position
            Vector3 averageNewPos = Vector3.zero;
            foreach (var pos in newVertexPositions.Values)
            {
                averageNewPos += pos;
            }
            averageNewPos /= newVertexPositions.Count;
            
            // Blend between original position and average new position for a good center point
            return Vector3.Lerp(originalPos, averageNewPos, 0.5f);
        }

        /// <summary>
        /// Updates faces to use new vertices instead of the central vertex.
        /// </summary>
        private static List<Face> UpdateFacesForVertexBevel(ProBuilderMesh mesh, List<Face> faces, int centralVertexIndex, Dictionary<int, int> newVertexIndices, List<Face> connectedFaces)
        {
            var updatedFaces = new List<Face>();
            var vertices = mesh.GetVertices();
            
            // Get shared vertex indices for the central vertex
            var sharedVertices = mesh.sharedVertices;
            var centralSharedIndices = new HashSet<int>();
            
            foreach (var sharedGroup in sharedVertices)
            {
                if (sharedGroup.Contains(centralVertexIndex))
                {
                    foreach (var index in sharedGroup)
                    {
                        centralSharedIndices.Add(index);
                    }
                    break;
                }
            }
            
            if (centralSharedIndices.Count == 0)
            {
                centralSharedIndices.Add(centralVertexIndex);
            }
            
            foreach (var face in faces)
            {
                if (connectedFaces.Contains(face))
                {
                    // This face is connected to our vertex - update it
                    var newIndices = new List<int>();
                    
                    foreach (var index in face.indexes)
                    {
                        if (centralSharedIndices.Contains(index))
                        {
                            // Replace central vertex with appropriate new vertex
                            // Find which new vertex to use based on adjacent vertices in this face
                            int replacementIndex = FindBestReplacementVertex(face, index, newVertexIndices, vertices);
                            newIndices.Add(replacementIndex);
                        }
                        else
                        {
                            newIndices.Add(index);
                        }
                    }
                    
                    var newFace = new Face(newIndices.ToArray());
                    newFace.submeshIndex = face.submeshIndex;
                    newFace.uv = face.uv;
                    newFace.textureGroup = face.textureGroup;
                    newFace.manualUV = face.manualUV;
                    
                    updatedFaces.Add(newFace);
                }
                else
                {
                    // This face is not connected - keep it unchanged
                    updatedFaces.Add(face);
                }
            }
            
            return updatedFaces;
        }

        /// <summary>
        /// Finds the best replacement vertex for a central vertex in a specific face.
        /// </summary>
        private static int FindBestReplacementVertex(Face face, int centralIndex, Dictionary<int, int> newVertexIndices, Vertex[] vertices)
        {
            // Simple approach: use the first new vertex we have
            // TODO: More sophisticated logic could examine adjacent vertices in the face
            // to determine which new vertex is most appropriate
            
            if (newVertexIndices.Count > 0)
            {
                return newVertexIndices.Values.First();
            }
            
            // Fallback: return the original index (shouldn't happen)
            return centralIndex;
        }

        /// <summary>
        /// Creates the center face that connects all the new vertices.
        /// </summary>
        private static Face CreateCenterFace(Dictionary<int, int> newVertexIndices, int centerVertexIndex, Vector3 centerNormal)
        {
            if (newVertexIndices.Count < 3)
            {
                Debug.LogWarning("Cannot create center face with less than 3 vertices");
                return null;
            }
            
            var indices = new List<int>();
            
            // Add the center vertex
            indices.Add(centerVertexIndex);
            
            // Add the new vertices around the perimeter
            // TODO: Sort them in proper winding order based on their positions
            foreach (var newVertexIndex in newVertexIndices.Values)
            {
                indices.Add(newVertexIndex);
            }
            
            // For now, create a simple triangle fan from center vertex
            // TODO: More sophisticated face creation for complex cases
            var triangles = new List<int>();
            for (int i = 1; i < indices.Count - 1; i++)
            {
                triangles.Add(indices[0]); // Center vertex
                triangles.Add(indices[i]);
                triangles.Add(indices[i + 1]);
            }
            
            // Close the fan
            if (indices.Count > 3)
            {
                triangles.Add(indices[0]); // Center vertex
                triangles.Add(indices[indices.Count - 1]);
                triangles.Add(indices[1]);
            }
            
            var centerFace = new Face(triangles.ToArray());
            centerFace.submeshIndex = 0;
            centerFace.uv = AutoUnwrapSettings.tile;
            
            return centerFace;
        }
    }
}

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
    /// Helper class for working with ProBuilder vertices in face selections.
    /// Provides utilities for identifying and filtering vertices based on their position
    /// relative to face boundaries (perimeter vs interior).
    /// </summary>
    public static class MeshHelpers_General
    {
        // Tolerance for comparing vertex positions to account for floating point precision
        private const float POSITION_TOLERANCE = 0.0001f;
        // ------------------------------

        /// <summary>
        /// Gets all interior edges from the given ProBuilder faces.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh containing the faces</param>
        /// <param name="faces">Array of faces to analyze</param>
        /// <returns>List of Edge objects representing interior edges</returns>
        public static List<Edge> GetInteriorEdgesFromFaces(ProBuilderMesh mesh, Face[] faces)
        {
            // Starter list
            var interiorEdges = new List<Edge>();

            // Safeguard against null or empty inputs
            if (mesh == null || faces == null || faces.Length == 0)
            {
                return interiorEdges;
            }

            // Get all edges from the selected faces
            var allEdges = new List<Edge>();
            foreach (var face in faces)
            {
                allEdges.AddRange(face.edges);
            }

            // Find perimeter edges (edges that are on the boundary of the selection)
            var perimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faces).ToHashSet();

            // Interior edges are those that are NOT perimeter edges
            foreach (var edge in allEdges)
            {
                // Compare both directions since Edge may not be ordered
                bool isPerimeter = perimeterEdges.Any(pe =>
                    (pe.a == edge.a && pe.b == edge.b) ||
                    (pe.a == edge.b && pe.b == edge.a));
                if (!isPerimeter)
                {
                    interiorEdges.Add(edge);
                }
            }

            // Log the number of interior edges found
            if (interiorEdges.Count > 0)
            {
                return interiorEdges;
            }
            else
            {
                return interiorEdges;
            }
        }

        /// <summary>
        ///  Checks if the given vertex is on an open edge (part of a hole).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh containing the vertex</param>
        /// <param name="vertexIndex">The index of the vertex to check</param>
        /// <returns>True if the vertex is on an open edge, false otherwise</returns>
        public static bool IsVertexOnOpenEdge(ProBuilderMesh mesh, int vertexIndex)
        {
            if (mesh == null)
                return false;

            // Get all open edges in the mesh
            var openEdges = GetOpenEdges(mesh, mesh.faces.ToArray());

            // Check if the vertex is part of any open edge
            foreach (var edge in openEdges)
            {
                if (edge.a == vertexIndex || edge.b == vertexIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the given edge is an open edge (part of a hole).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh containing the edge</param>
        /// <param name="edge">The edge to check</param>
        /// <returns>True if the edge is open, false otherwise</returns>
        public static bool IsEdgeOpen(ProBuilderMesh mesh, Edge edge)
        {
            if (mesh == null)
                return false;

            // Get all open edges in the mesh
            var openEdges = GetOpenEdges(mesh, mesh.faces.ToArray());

            // Check if the edge is in the list of open edges
            return openEdges.Any(e => (e.a == edge.a && e.b == edge.b) || (e.a == edge.b && e.b == edge.a));
        }        

        /// <summary>
        /// Gets all perimeter vertices from the given ProBuilder faces.
        /// Perimeter vertices are those that are connected to at least one exterior edge
        /// (an edge that is not shared between multiple faces in the selection).
        /// Always filters by unique position to avoid duplicates.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh containing the faces</param>
        /// <param name="faces">Array of faces to analyze</param>
        /// <returns>List of Vertex objects representing perimeter vertices</returns>
        public static List<Vertex> GetPerimeterVerts(ProBuilderMesh mesh, Face[] faces)
        {
            if (mesh == null || faces == null || faces.Length == 0)
                return new List<Vertex>();

            var allVertices = mesh.GetVertices();
            var perimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faces);

            if (!perimeterEdges.Any())
                return new List<Vertex>();

            // Find vertices that are part of perimeter edges
            var perimeterVertexIndices = new HashSet<int>();

            foreach (var edge in perimeterEdges)
            {
                perimeterVertexIndices.Add(edge.a);
                perimeterVertexIndices.Add(edge.b);
            }

            // Build the result list of perimeter vertices, filtering by unique position
            var result = new List<Vertex>();
            var positionGroups = new Dictionary<Vector3, int>();

            foreach (var vertexIndex in perimeterVertexIndices.OrderBy(i => i))
            {
                var worldPos = mesh.transform.TransformPoint(allVertices[vertexIndex].position);

                // Round to tolerance to handle floating point precision
                var roundedPos = new Vector3(
                    Mathf.Round(worldPos.x / POSITION_TOLERANCE) * POSITION_TOLERANCE,
                    Mathf.Round(worldPos.y / POSITION_TOLERANCE) * POSITION_TOLERANCE,
                    Mathf.Round(worldPos.z / POSITION_TOLERANCE) * POSITION_TOLERANCE
                );

                // Only add if we haven't seen this position before
                if (!positionGroups.ContainsKey(roundedPos))
                {
                    positionGroups[roundedPos] = vertexIndex;
                    result.Add(allVertices[vertexIndex]);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all interior vertices from the given ProBuilder faces.
        /// Interior vertices are those that are only connected to interior edges
        /// (edges that are shared between multiple faces in the selection or have duplicate positions).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh containing the faces</param>
        /// <param name="faces">Array of faces to analyze</param>
        /// <returns>List of Vertex objects representing interior vertices</returns>
        public static List<Vertex> GetInteriorVerts(ProBuilderMesh mesh, Face[] faces)
        {
            if (mesh == null || faces == null || faces.Length == 0)
                return new List<Vertex>();

            var allVertices = mesh.GetVertices();
            var perimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faces);

            // Get perimeter vertex indices
            var perimeterVertexIndices = new HashSet<int>();
            foreach (var edge in perimeterEdges)
            {
                perimeterVertexIndices.Add(edge.a);
                perimeterVertexIndices.Add(edge.b);
            }

            // Collect all unique vertex indices from the selected faces
            var uniqueVertices = new HashSet<int>();
            foreach (var face in faces)
            {
                foreach (var index in face.distinctIndexes)
                {
                    uniqueVertices.Add(index);
                }
            }

            // Find vertices that are NOT part of perimeter edges (interior vertices)
            var interiorVertexIndices = new HashSet<int>();

            foreach (var vertexIndex in uniqueVertices)
            {
                // If vertex is not on perimeter, it's interior
                if (!perimeterVertexIndices.Contains(vertexIndex))
                {
                    interiorVertexIndices.Add(vertexIndex);
                }
            }

            // Build the result list of interior vertices
            var result = new List<Vertex>();
            foreach (var vertexIndex in interiorVertexIndices.OrderBy(i => i))
            {
                result.Add(allVertices[vertexIndex]);
            }

            return result;
        }
        
        /// <summary>
        /// Get all open (non-manifold) edges from the selected faces using ProBuilder's WingedEdge system
        /// These are edges that only belong to one face (true boundary/open edges)
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="faces">Array of faces to analyze</param>
        /// <returns>List of Edge objects representing open edges</returns>
        public static List<Edge> GetOpenEdges(ProBuilderMesh mesh, Face[] faces)
        {
            if (mesh == null || faces == null || faces.Length == 0)
                return new List<Edge>();

            // Get all winged edges for the mesh
            var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
            
            // Filter to only get winged edges from our selected faces
            var selectedFaceWings = allWingedEdges.Where(x => faces.Contains(x.face));
            
            // Find edges that have no opposite (non-manifold/open edges)
            var openEdges = selectedFaceWings.Where(x => x.opposite == null).Select(y => y.edge.local).ToList();
            
            return openEdges;
        }

        /// <summary>
        /// Gets all vertex indices that share the same position as the specified vertex (coincident vertices).
        /// This includes the input vertex itself and any other vertices at the same world position.
        /// Uses ProBuilder's built-in GetCoincidentVertices method for robustness.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="vertexIndex">The vertex index to find coincident vertices for</param>
        /// <returns>List of vertex indices that are at the same position</returns>
        public static List<int> GetCoincidentVertices(ProBuilderMesh mesh, int vertexIndex)
        {
            if (mesh == null)
                return new List<int> { vertexIndex };

            // Use ProBuilder's built-in method for finding coincident vertices
            var coincidentVertices = mesh.GetCoincidentVertices(new int[] { vertexIndex });
            return coincidentVertices.ToList();
        }

        /// <summary>
        /// Checks if the specified edge is a boundary edge (part of a hole).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="edge">The edge to check</param>
        /// <returns>True if the edge is a boundary edge, false otherwise</returns>
        public static bool IsBoundaryEdge(ProBuilderMesh mesh, Edge edge)
        {
            if (mesh == null)
                return false;

            mesh.ToMesh();
            List<WingedEdge> wings = WingedEdge.GetWingedEdges(mesh);
            
            // Find if this edge exists as a boundary edge (no opposite)
            return wings.Any(w => w.opposite == null && 
                            ((w.edge.local.a == edge.a && w.edge.local.b == edge.b) ||
                             (w.edge.local.a == edge.b && w.edge.local.b == edge.a)));
        }

        /// <summary>
        /// Checks if the specified vertex (or any coincident vertices at the same position) is connected to any boundary edges.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="vertexIndex">The vertex index to check</param>
        /// <returns>True if the vertex or any coincident vertex is on a boundary, false otherwise</returns>
        public static bool IsVertexOnBoundary(ProBuilderMesh mesh, int vertexIndex)
        {
            if (mesh == null)
                return false;

            // Get all vertices that share the same position as the input vertex
            var coincidentVertices = GetCoincidentVertices(mesh, vertexIndex);
            
            mesh.ToMesh();
            List<WingedEdge> wings = WingedEdge.GetWingedEdges(mesh);
            
            // Check if any boundary edge contains any of the coincident vertices
            return wings.Any(w => w.opposite == null && 
                            coincidentVertices.Any(v => w.edge.local.a == v || w.edge.local.b == v));
        }

        /// <summary>
        /// Finds a boundary edge connected to the specified vertex (or any coincident vertices at the same position).
        /// This is used to convert a vertex selection to an edge for hole filling.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="vertexIndex">The vertex index to find a boundary edge for</param>
        /// <returns>A boundary edge containing the vertex, or Edge.Empty if none found</returns>
        public static Edge FindBoundaryEdgeFromVertex(ProBuilderMesh mesh, int vertexIndex)
        {
            if (mesh == null)
                return Edge.Empty;

            // Get all vertices that share the same position as the input vertex
            var coincidentVertices = GetCoincidentVertices(mesh, vertexIndex);

            mesh.ToMesh();
            List<WingedEdge> wings = WingedEdge.GetWingedEdges(mesh);
            
            // Find boundary edges that contain any of the coincident vertices
            var boundaryEdges = wings.Where(w => w.opposite == null && 
                                           coincidentVertices.Any(v => w.edge.local.a == v || w.edge.local.b == v));
            
            return boundaryEdges.FirstOrDefault()?.edge.local ?? Edge.Empty;
        }

        /// <summary>
        /// Fills a hole in the mesh starting from the specified edge.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh to modify</param>
        /// <param name="holeEdge">The edge that is part of the hole to fill</param>
        /// <returns>True if the hole was successfully filled, false otherwise</returns>
        /// <example>
        /// // Fill hole using an edge
        /// var mesh = MeshSelection.top.FirstOrDefault();
        /// var selectedEdge = mesh.selectedEdges.FirstOrDefault();
        /// bool success = MeshHelpers_General.FillHole(mesh, selectedEdge);
        /// </example>
        public static bool FillHole(ProBuilderMesh mesh, Edge holeEdge)
        {
            if (mesh == null)
            {
                Debug.LogWarning("FillHole: Mesh is null");
                return false;
            }

            if (holeEdge == Edge.Empty)
            {
                Debug.LogWarning("FillHole: Invalid edge provided");
                return false;
            }

            // Validate that the edge is actually a boundary edge
            if (!IsBoundaryEdge(mesh, holeEdge))
            {
                Debug.LogWarning($"FillHole: Edge {holeEdge.a}-{holeEdge.b} is not a boundary edge (not part of a hole). Only boundary/open edges can be filled.");
                return false;
            }

            return FillHoleInternal(mesh, holeEdge);
        }

        /// <summary>
        /// Fills a hole in the mesh that contains the specified vertex.
        /// Converts the vertex to a boundary edge first, then fills the hole.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh to modify</param>
        /// <param name="vertexIndex">The vertex index that is part of the hole to fill</param>
        /// <returns>True if the hole was successfully filled, false otherwise</returns>
        /// <example>
        /// // Fill hole using a vertex
        /// var mesh = MeshSelection.top.FirstOrDefault();
        /// var selectedVertex = mesh.selectedVertices.FirstOrDefault();
        /// bool success = MeshHelpers_General.FillHole(mesh, selectedVertex);
        /// </example>
        public static bool FillHole(ProBuilderMesh mesh, int vertexIndex)
        {
            if (mesh == null)
            {
                Debug.LogWarning("FillHole: Mesh is null");
                return false;
            }

            // First check if the vertex (or any coincident vertex) is on a boundary at all
            if (!IsVertexOnBoundary(mesh, vertexIndex))
            {
                Debug.LogWarning($"FillHole: Vertex {vertexIndex} is not on any boundary/open edge. Only vertices on hole boundaries can be used for filling.");
                return false;
            }

            // Convert vertex to boundary edge
            Edge boundaryEdge = FindBoundaryEdgeFromVertex(mesh, vertexIndex);
            
            if (boundaryEdge == Edge.Empty)
            {
                Debug.LogWarning($"FillHole: No boundary edge found for vertex {vertexIndex}");
                return false;
            }

            return FillHoleInternal(mesh, boundaryEdge);
        }

        /// <summary>
        /// Internal method that performs the actual hole filling logic.
        /// </summary>
        private static bool FillHoleInternal(ProBuilderMesh mesh, Edge holeEdge)
        {
            Undo.RecordObject(mesh, "Fill Hole");
            
            mesh.ToMesh();
            List<WingedEdge> wings = WingedEdge.GetWingedEdges(mesh);
            HashSet<int> common = GetSharedVertexHandles(mesh, mesh.faces.SelectMany(x => x.indexes));
            var holes = FindHolesProper(wings, common);
            
            foreach (var hole in holes)
            {
                if (hole.Any(e => (e.a == holeEdge.a && e.b == holeEdge.b) ||
                                  (e.a == holeEdge.b && e.b == holeEdge.a)))
                {
                    var holeVertices = hole.Select(e => e.a).ToList();
                    holeVertices.Reverse(); // Reverse to fix winding order
                    Face newFace = AppendElements.CreatePolygon(mesh, holeVertices, false);

                    if (newFace != null)
                    {
                        // Fix face orientation by matching adjacent face properties
                        List<WingedEdge> newWings = WingedEdge.GetWingedEdges(mesh);
                        var wing = newWings.FirstOrDefault(x => x.face == newFace);
                        
                        if (wing != null)
                        {
                            using (var it = new WingedEdgeEnumerator(wing))
                            {
                                while (it.MoveNext())
                                {
                                    if (it.Current?.opposite?.face != null && it.Current.opposite.face != newFace)
                                    {
                                        var oppositeFace = it.Current.opposite.face;
                                        newFace.submeshIndex = oppositeFace.submeshIndex;
                                        newFace.uv = new AutoUnwrapSettings(oppositeFace.uv);
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // Conform the new face normal to match adjacent faces
                        mesh.ConformNormals(new Face[] { newFace });
                        
                        mesh.ToMesh();
                        mesh.Refresh();
                        ProBuilderEditor.Refresh();
                        return true;
                    }
                    break;
                }
            }

            Debug.LogWarning("FillHole: No matching hole found for the specified edge");
            return false;
        }

        /// <summary>
        /// Gets shared vertex handles for the given vertices in the mesh.
        /// </summary>
        public static HashSet<int> GetSharedVertexHandles(ProBuilderMesh mesh, IEnumerable<int> vertices)
        {
            HashSet<int> common = new HashSet<int>();
            var sharedVertices = mesh.sharedVertices;
            
            foreach (var vertex in vertices)
            {
                // Find which shared vertex group this vertex belongs to
                for (int i = 0; i < sharedVertices.Count; i++)
                {
                    if (sharedVertices[i].Contains(vertex))
                    {
                        common.Add(i);
                        break;
                    }
                }
            }
            
            return common;
        }

        /// <summary>
        /// Finds all holes in the mesh using the proper algorithm.
        /// </summary>
        public static List<List<Edge>> FindHolesProper(List<WingedEdge> wings, HashSet<int> common)
        {
            const int k_MaxHoleIterations = 2048;
            HashSet<WingedEdge> used = new HashSet<WingedEdge>();
            List<List<Edge>> holes = new List<List<Edge>>();

            for (int i = 0; i < wings.Count; i++)
            {
                WingedEdge c = wings[i];

                // if this edge has been added to a hole already, or the edge isn't in the approved list of indexes,
                // or if there's an opposite face, this edge doesn't belong to a hole.  move along.
                if (c.opposite != null || used.Contains(c) || !(common.Contains(c.edge.common.a) || common.Contains(c.edge.common.b)))
                    continue;

                List<Edge> hole = new List<Edge>();
                WingedEdge it = c;
                int ind = it.edge.common.a;

                int counter = 0;

                while (it != null && counter++ < k_MaxHoleIterations)
                {
                    used.Add(it);
                    hole.Add(it.edge.local);

                    ind = it.edge.common.a == ind ? it.edge.common.b : it.edge.common.a;
                    it = FindNextEdgeInHole(it, ind);

                    if (it == c)
                        break;
                }

                if (hole.Count >= 3) // Need at least 3 edges to form a valid hole
                {
                    holes.Add(hole);
                }
            }

            return holes;
        }

        /// <summary>
        /// Finds the next edge in a hole traversal.
        /// </summary>
        public static WingedEdge FindNextEdgeInHole(WingedEdge wing, int common)
        {
            const int k_MaxHoleIterations = 2048;
            WingedEdge next = wing.GetAdjacentEdgeWithCommonIndex(common);
            int counter = 0;
            while (next != null && next != wing && counter++ < k_MaxHoleIterations)
            {
                if (next.opposite == null)
                    return next;

                next = next.opposite.GetAdjacentEdgeWithCommonIndex(common);
            }

            return null;
        }
    }
}

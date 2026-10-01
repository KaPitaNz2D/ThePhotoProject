using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace Overdrive.ProBuilderPlus
{
    public static class EdgeOperationHelper
    {
        /// <summary>
        /// Connects edges at calculated positions using ProBuilder's Connect method.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh to operate on</param>
        /// <param name="edges">The edges to connect</param>
        /// <param name="calculatedPositions">The calculated positions where connections should be made</param>
        /// <returns>List of new edges created by the Connect operation</returns>
        public static List<Edge> ConnectEdgesAtCalculatedPositions(
            ProBuilderMesh mesh,
            Edge[] edges,
            List<Vector3> calculatedPositions)
        {
            if (calculatedPositions == null || calculatedPositions.Count == 0 || edges == null || edges.Length == 0)
                return null;

            // Step 1: Calculate ProBuilder's default connection positions (world space)
            var pbDefaultPositions = new List<Vector3>();
            var vertices = mesh.GetVertices();

            foreach (var edge in edges)
            {
                if (edge.a < vertices.Length && edge.b < vertices.Length)
                {
                    var pointA = vertices[edge.a].position;
                    var pointB = vertices[edge.b].position;
                    // ProBuilder connects at center (0.5) by default
                    var pbPosition = Vector3.Lerp(pointA, pointB, 0.5f);
                    pbDefaultPositions.Add(mesh.transform.TransformPoint(pbPosition));
                }
            }

            // Step 2: Convert our calculated positions to world space
            var ourWorldPositions = new List<Vector3>();
            foreach (var localPos in calculatedPositions)
            {
                ourWorldPositions.Add(mesh.transform.TransformPoint(localPos));
            }

            // Step 3: Create position mapping (PB world position -> our world position)
            var positionMapping = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < Mathf.Min(pbDefaultPositions.Count, ourWorldPositions.Count); i++)
            {
                positionMapping[pbDefaultPositions[i]] = ourWorldPositions[i];
            }

            // Step 4: Execute ProBuilder's Connect method
            var result = mesh.Connect(edges);
            if (result.item2 == null || result.item2.Length == 0)
                return null;

            // Step 5: Find and move vertices at each PB position to our target positions
            var postConnectVertices = mesh.GetVertices();
            var updatedVertices = postConnectVertices.ToList();
            int movedVertexCount = 0;

            foreach (var mapping in positionMapping)
            {
                var pbWorldPos = mapping.Key;
                var ourWorldPos = mapping.Value;
                var ourLocalPos = mesh.transform.InverseTransformPoint(ourWorldPos);

                // Find all vertices at the PB position (within tolerance for floating point precision)
                const float tolerance = 0.001f;
                for (int i = 0; i < updatedVertices.Count; i++)
                {
                    var vertexWorldPos = mesh.transform.TransformPoint(updatedVertices[i].position);
                    if (Vector3.Distance(vertexWorldPos, pbWorldPos) < tolerance)
                    {
                        var vertex = updatedVertices[i];
                        vertex.position = ourLocalPos;
                        updatedVertices[i] = vertex;
                        movedVertexCount++;
                    }
                }
            }

            // Apply the updated positions back to the mesh
            mesh.SetVertices(updatedVertices.ToArray());

            // Return the new edges created by Connect
            return result.item2.ToList();
        }

        /// <summary>
        /// Gets the edge ring from a set of selected edges.
        /// This is adapted from ProBuilder's internal ElementSelection.GetEdgeRing method.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="edges">The selected edges to expand into a ring</param>
        /// <returns>Array of edges forming the complete ring</returns>
        public static Edge[] GetEdgeRing(ProBuilderMesh mesh, Edge[] edges)
        {
            List<WingedEdge> wings = WingedEdge.GetWingedEdges(mesh);

            // Create shared vertex lookup dictionary
            var sharedVertexLookup = new Dictionary<int, int>();
            SharedVertex.GetSharedVertexLookup(mesh.sharedVertices, sharedVertexLookup);

            List<EdgeLookup> edgeLookup = EdgeLookup.GetEdgeLookup(edges, sharedVertexLookup).ToList();
            edgeLookup = edgeLookup.Distinct().ToList();

            Dictionary<Edge, WingedEdge> wings_dic = new Dictionary<Edge, WingedEdge>();

            for (int i = 0; i < wings.Count; i++)
                if (!wings_dic.ContainsKey(wings[i].edge.common))
                    wings_dic.Add(wings[i].edge.common, wings[i]);

            HashSet<EdgeLookup> used = new HashSet<EdgeLookup>();

            for (int i = 0, c = edgeLookup.Count; i < c; i++)
            {
                if (!wings_dic.TryGetValue(edgeLookup[i].common, out var we) || used.Contains(we.edge))
                    continue;

                WingedEdge cur = we;

                while (cur != null)
                {
                    if (!used.Add(cur.edge)) break;
                    cur = EdgeRingNext(cur);
                    if (cur != null && cur.opposite != null) cur = cur.opposite;
                }

                cur = EdgeRingNext(we.opposite);
                if (cur != null && cur.opposite != null) cur = cur.opposite;

                // run in both directions
                while (cur != null)
                {
                    if (!used.Add(cur.edge)) break;
                    cur = EdgeRingNext(cur);
                    if (cur != null && cur.opposite != null) cur = cur.opposite;
                }
            }

            return used.Select(x => x.local).ToArray();
        }

        /// <summary>
        /// Helper method for edge ring traversal.
        /// Adapted from ProBuilder's internal ElementSelection.EdgeRingNext method.
        /// </summary>
        private static WingedEdge EdgeRingNext(WingedEdge edge)
        {
            if (edge == null)
                return null;

            WingedEdge next = edge.next, prev = edge.previous;
            int i = 0;

            while (next != prev && next != edge)
            {
                next = next.next;

                if (next == prev)
                    return null;

                prev = prev.previous;

                i++;
            }

            if (i % 2 == 0 || next == edge)
                next = null;

            return next;
        }
    }
}
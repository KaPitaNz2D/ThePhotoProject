using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Shared helper class for ConformNormals operations and visualization.
    /// Used by both Object and Face mode ConformNormals preview actions.
    /// </summary>
    public static class ConformNormalsHelper
    {
        // Visual settings
        private static readonly Color FLIP_NORMAL_COLOR = Color.cyan; // Cyan for new normal direction
        private static readonly Color EDGE_COLOR = new Color(1f, 0.2f, 0.1f, 0.8f); // Red for face edges
        private const float NORMAL_LINE_LENGTH = 0.5f;
        private const float NORMAL_LINE_THICKNESS = 3f;
        private const float EDGE_THICKNESS = 2f;

        /// <summary>
        /// Calculate which faces will be flipped when conforming normals.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToProcess">Faces to consider (all faces for object mode, selected faces for face mode)</param>
        /// <param name="useOtherDirection">If true, choose minority direction instead of majority</param>
        /// <returns>List of faces that will be flipped</returns>
        public static List<Face> CalculateFacesToFlip(ProBuilderMesh mesh, Face[] facesToProcess, bool useOtherDirection)
        {
            var facesToFlip = new List<Face>();
            
            // Use the same logic as ConformNormals to predict what will be flipped
            var wings = WingedEdge.GetWingedEdges(mesh, facesToProcess);
            var used = new HashSet<Face>();

            foreach (var wing in wings)
            {
                if (used.Contains(wing.face)) continue;

                var flags = new Dictionary<Face, bool>();
                GetWindingFlags(wing, true, flags);

                // For face mode, only consider flags for faces that are in our process list
                var faceSet = new HashSet<Face>(facesToProcess);
                var relevantFlags = flags.Where(kvp => faceSet.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                if (relevantFlags.Count == 0) continue;

                int flip = 0;
                foreach (var kvp in relevantFlags)
                    flip += kvp.Value ? 1 : -1;

                bool majorityDirection = flip > 0;
                
                // Determine which faces will be flipped
                bool targetDirection = useOtherDirection ? !majorityDirection : majorityDirection;
                
                foreach (var kvp in relevantFlags)
                {
                    if (targetDirection != kvp.Value)
                    {
                        facesToFlip.Add(kvp.Key);
                    }
                }

                used.UnionWith(relevantFlags.Keys);
            }

            return facesToFlip;
        }

        /// <summary>
        /// Draw normal direction lines and edge highlights for faces that will be flipped.
        /// Shows red face edges and cyan lines indicating the direction they will flip to.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToFlip">Faces that will be flipped</param>
        public static void DrawNormalFlipArrows(ProBuilderMesh mesh, List<Face> facesToFlip)
        {
            if (mesh == null || facesToFlip == null || facesToFlip.Count == 0) return;

            var vertices = mesh.GetVertices();
            
            // Set z-test to show visualization properly
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            foreach (var face in facesToFlip)
            {
                // Draw face edges in red
                DrawFaceEdges(mesh, face, vertices, EDGE_COLOR);
                
                // Calculate face center and flipped normal direction
                var faceCenter = CalculateFaceCenter(mesh, face, vertices);
                var currentNormal = CalculateFaceNormal(face, vertices);
                var flippedNormal = -currentNormal;

                // Transform to world space
                var worldCenter = mesh.transform.TransformPoint(faceCenter);
                var worldFlippedNormal = mesh.transform.TransformDirection(flippedNormal).normalized;

                // Draw simple cyan line showing new normal direction
                Handles.color = FLIP_NORMAL_COLOR;
                var endPoint = worldCenter + worldFlippedNormal * NORMAL_LINE_LENGTH;
                Handles.DrawLine(worldCenter, endPoint, NORMAL_LINE_THICKNESS);
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        /// <summary>
        /// Apply "other direction" conform logic manually.
        /// This flips faces to the minority direction instead of majority.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToProcess">Faces to process</param>
        /// <returns>ActionResult with flip count</returns>
        public static ActionResult ApplyOtherDirectionConform(ProBuilderMesh mesh, Face[] facesToProcess)
        {
            var wings = WingedEdge.GetWingedEdges(mesh, facesToProcess);
            var used = new HashSet<Face>();
            int totalFlipped = 0;

            foreach (var wing in wings)
            {
                if (used.Contains(wing.face)) continue;

                var flags = new Dictionary<Face, bool>();
                GetWindingFlags(wing, true, flags);

                // For face mode, only consider flags for faces that are in our process list
                var faceSet = new HashSet<Face>(facesToProcess);
                var relevantFlags = flags.Where(kvp => faceSet.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                if (relevantFlags.Count == 0) continue;

                int flip = 0;
                foreach (var kvp in relevantFlags)
                    flip += kvp.Value ? 1 : -1;

                bool majorityDirection = flip > 0;
                bool targetDirection = !majorityDirection; // Choose minority direction

                foreach (var kvp in relevantFlags)
                {
                    if (targetDirection != kvp.Value)
                    {
                        kvp.Key.Reverse();
                        totalFlipped++;
                    }
                }

                used.UnionWith(relevantFlags.Keys);
            }

            return totalFlipped > 0 ? 
                new ActionResult(ActionResult.Status.Success, $"Flipped {totalFlipped} face{(totalFlipped == 1 ? string.Empty : "s")}") :
                new ActionResult(ActionResult.Status.NoChange, "All normals already uniform");
        }

        /// <summary>
        /// Calculate the center point of a face.
        /// </summary>
        private static Vector3 CalculateFaceCenter(ProBuilderMesh mesh, Face face, UnityEngine.ProBuilder.Vertex[] vertices)
        {
            var center = Vector3.zero;
            foreach (var vertexIndex in face.distinctIndexes)
            {
                center += vertices[vertexIndex].position;
            }
            return center / face.distinctIndexes.Count;
        }

        /// <summary>
        /// Calculate the normal vector of a face.
        /// </summary>
        private static Vector3 CalculateFaceNormal(Face face, UnityEngine.ProBuilder.Vertex[] vertices)
        {
            if (face.distinctIndexes.Count < 3) return Vector3.up;

            var indices = face.distinctIndexes;
            var v0 = vertices[indices[0]].position;
            var v1 = vertices[indices[1]].position;
            var v2 = vertices[indices[2]].position;

            return Vector3.Cross(v1 - v0, v2 - v0).normalized;
        }

        /// <summary>
        /// Draw the edges of a face in the specified color.
        /// </summary>
        private static void DrawFaceEdges(ProBuilderMesh mesh, Face face, UnityEngine.ProBuilder.Vertex[] vertices, Color color)
        {
            Handles.color = color;
            
            var indices = face.indexes.ToArray();
            
            // Draw edges by connecting consecutive vertices in the face
            for (int i = 0; i < indices.Length; i++)
            {
                var currentVertex = vertices[indices[i]].position;
                var nextVertex = vertices[indices[(i + 1) % indices.Length]].position;
                
                // Transform to world space
                var worldCurrent = mesh.transform.TransformPoint(currentVertex);
                var worldNext = mesh.transform.TransformPoint(nextVertex);
                
                // Draw edge line
                Handles.DrawLine(worldCurrent, worldNext, EDGE_THICKNESS);
            }
        }

        // Copy the GetWindingFlags method from SurfaceTopology for preview prediction
        private static void GetWindingFlags(WingedEdge edge, bool flag, Dictionary<Face, bool> flags)
        {
            flags.Add(edge.face, flag);

            WingedEdge next = edge;

            do
            {
                WingedEdge opp = next.opposite;

                if (opp != null && !flags.ContainsKey(opp.face))
                {
                    Edge cea = GetCommonEdgeInWindingOrder(next);
                    Edge ceb = GetCommonEdgeInWindingOrder(opp);

                    GetWindingFlags(opp, cea.a == ceb.a ? !flag : flag, flags);
                }

                next = next.next;
            }
            while (next != edge);
        }

        // Copy the GetCommonEdgeInWindingOrder method from SurfaceTopology for preview prediction
        private static Edge GetCommonEdgeInWindingOrder(WingedEdge wing)
        {
            var face = wing.face;
            var edge = wing.edge.common; // Get the common edge from EdgeLookup
            var indices = face.indexes.ToArray();

            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] == edge.a)
                {
                    int next = (i + 1) % indices.Length;
                    if (indices[next] == edge.b)
                        return new Edge(edge.a, edge.b);
                }
                else if (indices[i] == edge.b)
                {
                    int next = (i + 1) % indices.Length;
                    if (indices[next] == edge.a)
                        return new Edge(edge.b, edge.a);
                }
            }

            return new Edge(edge.a, edge.b); // Return the edge as-is if no winding order found
        }
    }
}

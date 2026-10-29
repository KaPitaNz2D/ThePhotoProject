using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Shared helper class for FlipEdge operations and visualization.
    /// Used by FlipEdge preview action to show which diagonal edge will be flipped.
    /// </summary>
    public static class FlipEdgeHelper
    {
        // Visual settings
        private static readonly Color CURRENT_DIAGONAL_COLOR = Color.red;     // Current diagonal edge color
        private static readonly Color NEW_DIAGONAL_COLOR = Color.cyan;        // New diagonal edge after flip
        private static readonly Color FACE_EDGE_COLOR = Color.white;          // Face perimeter edges
        private const float EDGE_THICKNESS = 3.0f;
        private const float DIAGONAL_THICKNESS = 4.0f;

        /// <summary>
        /// Data structure to hold flip edge preview information for a face.
        /// </summary>
        public sealed class FlipEdgePreview
        {
            public Vector3[] vertices;           // All vertices in world space
            public Edge[] faceEdges;            // Perimeter edges of the face (always visible)
            public Edge currentDiagonal;        // Current diagonal edge (red)
            public Edge newDiagonal;            // New diagonal edge after flip (cyan)
            public bool canFlip;                // Whether this face can be flipped (is a valid quad)
        }

        /// <summary>
        /// Calculate flip edge preview for specific faces on a mesh.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToCheck">Specific faces to check for flipping</param>
        /// <returns>Array of flip edge preview data for each face</returns>
        public static FlipEdgePreview[] CalculateFlipEdgePreview(ProBuilderMesh mesh, Face[] facesToCheck)
        {
            var previews = new FlipEdgePreview[facesToCheck.Length];
            var vertices = mesh.GetVertices();
            
            // Convert vertex positions to world space
            var worldVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = mesh.transform.TransformPoint(vertices[i].position);
            }

            for (int faceIndex = 0; faceIndex < facesToCheck.Length; faceIndex++)
            {
                var face = facesToCheck[faceIndex];
                var preview = new FlipEdgePreview();
                preview.vertices = worldVertices;

                // Get face perimeter edges (always shown in white)
                preview.faceEdges = face.edges.ToArray();

                // Check if face can be flipped (must be a quad - 6 indices representing 2 triangles)
                var indexes = face.indexes;
                preview.canFlip = CanFlipFace(face);

                if (preview.canFlip)
                {
                    // Find the current and new diagonal edges
                    CalculateDiagonalEdges(face, out preview.currentDiagonal, out preview.newDiagonal);
                }
                else
                {
                    // Invalid face - no diagonals to show
                    preview.currentDiagonal = Edge.Empty;
                    preview.newDiagonal = Edge.Empty;
                }

                previews[faceIndex] = preview;
            }

            return previews;
        }

        /// <summary>
        /// Draw wireframe preview for flip edge operation.
        /// Shows face edges in white, current diagonal in red, new diagonal in cyan.
        /// </summary>
        /// <param name="previews">Array of flip edge previews to draw</param>
        public static void DrawFlipEdgePreviews(FlipEdgePreview[] previews)
        {
            if (previews == null) return;

            // Set z-test to respect depth so lines are properly occluded by geometry
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            foreach (var preview in previews)
            {
                if (preview?.vertices == null) continue;

                // Draw face perimeter edges in white
                Handles.color = FACE_EDGE_COLOR;
                DrawWireframeForVertices(preview.vertices, preview.faceEdges, EDGE_THICKNESS);

                if (preview.canFlip)
                {
                    // Draw new diagonal in cyan
                    if (!IsEdgeEmpty(preview.newDiagonal))
                    {
                        Handles.color = NEW_DIAGONAL_COLOR;
                        DrawSingleEdge(preview.vertices, preview.newDiagonal, DIAGONAL_THICKNESS);
                    }
                }
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        /// <summary>
        /// Apply flip edge operation to specific faces on a mesh.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToFlip">Specific faces to flip</param>
        /// <returns>Number of faces that were successfully flipped</returns>
        public static int ApplyFlipEdge(ProBuilderMesh mesh, Face[] facesToFlip)
        {
            int flippedCount = 0;

            foreach (var face in facesToFlip)
            {
                if (mesh.FlipEdge(face))
                {
                    flippedCount++;
                }
            }

            if (flippedCount > 0)
            {
                mesh.ToMesh();
                mesh.Refresh();
            }

            return flippedCount;
        }

        /// <summary>
        /// Create UI elements for flip edge settings and instructions.
        /// </summary>
        /// <returns>UI container with instructions and legend</returns>
        public static VisualElement CreateFlipEdgeUI()
        {
            var root = new VisualElement();
            return root;
        }

        /// <summary>
        /// Check if a face can be flipped (must be a quad).
        /// </summary>
        /// <param name="face">Face to check</param>
        /// <returns>True if face can be flipped</returns>
        private static bool CanFlipFace(Face face)
        {
            // FlipEdge only works on quads (faces with 6 indices representing 2 triangles)
            return face.indexes.Count == 6;
        }

        /// <summary>
        /// Calculate the current and new diagonal edges for a face that can be flipped.
        /// </summary>
        /// <param name="face">The face to analyze</param>
        /// <param name="currentDiagonal">Output: current diagonal edge</param>
        /// <param name="newDiagonal">Output: new diagonal edge after flip</param>
        private static void CalculateDiagonalEdges(Face face, out Edge currentDiagonal, out Edge newDiagonal)
        {
            currentDiagonal = Edge.Empty;
            newDiagonal = Edge.Empty;

            var indexes = face.indexes;
            if (indexes.Count != 6) return;

            // In a quad represented as 2 triangles (6 indices), we need to find:
            // 1. Which vertices appear twice (shared diagonal)
            // 2. Which vertices appear once (corners)

            int[] vertexCount = new int[indexes.Count];
            for (int i = 0; i < indexes.Count; i++)
            {
                for (int j = 0; j < indexes.Count; j++)
                {
                    if (indexes[i] == indexes[j])
                        vertexCount[i]++;
                }
            }

            // Find vertices that appear exactly twice (diagonal endpoints)
            var diagonalVertices = new List<int>();
            var cornerVertices = new List<int>();

            for (int i = 0; i < indexes.Count; i++)
            {
                if (vertexCount[i] == 2)
                {
                    if (!diagonalVertices.Contains(indexes[i]))
                        diagonalVertices.Add(indexes[i]);
                }
                else if (vertexCount[i] == 1)
                {
                    cornerVertices.Add(indexes[i]);
                }
            }

            // Current diagonal connects the two vertices that appear twice
            if (diagonalVertices.Count == 2)
            {
                currentDiagonal = new Edge(diagonalVertices[0], diagonalVertices[1]);
            }

            // New diagonal would connect the two vertices that appear once (opposite corners)
            if (cornerVertices.Count == 2)
            {
                newDiagonal = new Edge(cornerVertices[0], cornerVertices[1]);
            }
        }

        /// <summary>
        /// Check if an edge is empty.
        /// </summary>
        private static bool IsEdgeEmpty(Edge edge)
        {
            return edge.a == -1 || edge.b == -1 || edge.Equals(Edge.Empty);
        }

        /// <summary>
        /// Draw wireframe for a set of vertices and edges.
        /// </summary>
        private static void DrawWireframeForVertices(Vector3[] vertices, Edge[] edges, float thickness)
        {
            if (vertices == null || edges == null) return;

            foreach (var edge in edges)
            {
                DrawSingleEdge(vertices, edge, thickness);
            }
        }

        /// <summary>
        /// Draw a single edge with specified thickness.
        /// </summary>
        private static void DrawSingleEdge(Vector3[] vertices, Edge edge, float thickness)
        {
            if (vertices == null || edge.a < 0 || edge.b < 0 || 
                edge.a >= vertices.Length || edge.b >= vertices.Length) return;

            Handles.DrawLine(vertices[edge.a], vertices[edge.b], thickness);
        }
    }
}

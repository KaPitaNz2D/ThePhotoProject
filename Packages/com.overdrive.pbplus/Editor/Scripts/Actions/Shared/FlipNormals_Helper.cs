using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Shared helper class for FlipNormals operations and visualization.
    /// Used by both Object and Face mode FlipNormals preview actions.
    /// </summary>
    public static class FlipNormalsHelper
    {
        // Visual settings
        private static readonly Color FLIPPED_NORMAL_COLOR = Color.cyan; // Cyan for new normal direction
        private static readonly Color EDGE_COLOR = new Color(1f, 0.2f, 0.1f, 0.8f); // Red for face edges
        private const float NORMAL_LINE_LENGTH = 0.5f;
        private const float NORMAL_LINE_THICKNESS = 3f;
        private const float EDGE_THICKNESS = 2f;

        /// <summary>
        /// Draw normal direction lines and edge highlights for faces that will be flipped.
        /// Shows red face edges and cyan lines indicating the direction they will flip to.
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToFlip">Faces that will be flipped</param>
        public static void DrawFlipVisualization(ProBuilderMesh mesh, Face[] facesToFlip)
        {
            if (mesh == null || facesToFlip == null || facesToFlip.Length == 0) return;

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
                Handles.color = FLIPPED_NORMAL_COLOR;
                var endPoint = worldCenter + worldFlippedNormal * NORMAL_LINE_LENGTH;
                Handles.DrawLine(worldCenter, endPoint, NORMAL_LINE_THICKNESS);
            }

            // Reset handles color
            Handles.color = Color.white;
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
    }
}
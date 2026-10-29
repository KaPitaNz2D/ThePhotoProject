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
    /// Shared helper class for Triangulate operations and visualization.
    /// Used by both Object and Face mode Triangulate preview actions.
    /// </summary>
    public static class TriangulateHelper
    {
        // Visual settings
        private static readonly Color TRIANGLE_COLOR = Color.cyan;    // New triangle wireframe color
        private static readonly Color EXISTING_COLOR = Color.white;   // Existing edges color
        private const float WIREFRAME_THICKNESS = 2.0f;

        /// <summary>
        /// Data structure to hold triangulation preview information for a mesh.
        /// </summary>
        public sealed class TriangulationPreview
        {
            public Vector3[] triangleVertices;  // All vertices in world space
            public Edge[] newTriangleEdges;     // NEW edges created by triangulation
            public Edge[] existingEdges;       // Existing ProBuilder edges
        }

        /// <summary>
        /// Calculate triangulation preview for all faces on a mesh (Object mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <returns>Triangulation preview data</returns>
        public static TriangulationPreview CalculateTriangulationPreview(ProBuilderMesh mesh)
        {
            return CalculateTriangulationPreview(mesh, mesh.faces.ToArray());
        }

        /// <summary>
        /// Calculate triangulation preview for specific faces on a mesh (Face mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToTriangulate">Specific faces to triangulate</param>
        /// <returns>Triangulation preview data</returns>
        public static TriangulationPreview CalculateTriangulationPreview(ProBuilderMesh mesh, Face[] facesToTriangulate)
        {
            var preview = new TriangulationPreview();
            var vertices = mesh.GetVertices();
            
            // Convert vertex positions to world space
            preview.triangleVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                preview.triangleVertices[i] = mesh.transform.TransformPoint(vertices[i].position);
            }

            // Get existing ProBuilder edges from the faces we're considering
            var existingProBuilderEdges = new HashSet<Edge>();
            foreach (var face in facesToTriangulate)
            {
                // Get the face edges (perimeter edges of faces)
                foreach (var edge in face.edges)
                {
                    existingProBuilderEdges.Add(edge);
                }
            }

            // Store existing edges
            preview.existingEdges = existingProBuilderEdges.ToArray();

            // Calculate ALL triangle edges that would exist after triangulation
            var allTriangleEdges = new HashSet<Edge>();
            foreach (var face in facesToTriangulate)
            {
                var indices = face.indexes;
                
                // Each face's indices are already triangulated (every 3 indices = 1 triangle)
                for (int i = 0; i < indices.Count; i += 3)
                {
                    if (i + 2 < indices.Count)
                    {
                        int a = indices[i];
                        int b = indices[i + 1];
                        int c = indices[i + 2];
                        
                        // Add the three edges of this triangle
                        allTriangleEdges.Add(new Edge(a, b));
                        allTriangleEdges.Add(new Edge(b, c));
                        allTriangleEdges.Add(new Edge(c, a));
                    }
                }
            }

            // Filter out existing ProBuilder edges to get only NEW edges created by triangulation
            var newTriangulationEdges = new HashSet<Edge>();
            foreach (var triangleEdge in allTriangleEdges)
            {
                // Check if this edge already exists as a ProBuilder edge
                bool isExistingEdge = false;
                foreach (var existingEdge in existingProBuilderEdges)
                {
                    if ((triangleEdge.a == existingEdge.a && triangleEdge.b == existingEdge.b) ||
                        (triangleEdge.a == existingEdge.b && triangleEdge.b == existingEdge.a))
                    {
                        isExistingEdge = true;
                        break;
                    }
                }
                
                // Only add if it's a NEW edge created by triangulation
                if (!isExistingEdge)
                {
                    newTriangulationEdges.Add(triangleEdge);
                }
            }

            preview.newTriangleEdges = newTriangulationEdges.ToArray();
            return preview;
        }

        /// <summary>
        /// Draw wireframe preview for triangulation.
        /// Shows existing edges in white and new triangle edges in cyan.
        /// </summary>
        /// <param name="previews">Array of triangulation previews to draw</param>
        public static void DrawTriangulationPreviews(TriangulationPreview[] previews)
        {
            if (previews == null) return;

            // Set z-test to respect depth so lines are properly occluded by geometry
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            foreach (var preview in previews)
            {
                if (preview?.triangleVertices == null) continue;

                // Draw existing ProBuilder edges in white
                Handles.color = EXISTING_COLOR;
                DrawWireframeForVertices(preview.triangleVertices, preview.existingEdges);

                // Draw new triangulation edges in cyan
                Handles.color = TRIANGLE_COLOR;
                DrawWireframeForVertices(preview.triangleVertices, preview.newTriangleEdges);
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        /// <summary>
        /// Apply triangulation to all faces on a mesh (Object mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <returns>Number of faces that were triangulated</returns>
        public static int ApplyTriangulation(ProBuilderMesh mesh)
        {
            return ApplyTriangulation(mesh, mesh.faces.ToArray());
        }

        /// <summary>
        /// Apply triangulation to specific faces on a mesh (Face mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToTriangulate">Specific faces to triangulate</param>
        /// <returns>Number of faces that were triangulated</returns>
        public static int ApplyTriangulation(ProBuilderMesh mesh, Face[] facesToTriangulate)
        {
            // Count faces that actually need triangulation (not already triangles)
            int facesNeedingTriangulation = 0;
            foreach (var face in facesToTriangulate)
            {
                if (face.indexes.Count > 3) // More than 3 vertices = needs triangulation
                {
                    facesNeedingTriangulation++;
                }
            }

            if (facesNeedingTriangulation > 0)
            {
                // Apply triangulation to faces
                mesh.ToMesh();
                mesh.ToTriangles(facesToTriangulate);
                mesh.Refresh();
            }

            return facesNeedingTriangulation;
        }

        /// <summary>
        /// Create UI elements for triangulation settings.
        /// </summary>
        /// <param name="isFaceMode">True for face mode, false for object mode</param>
        /// <returns>UI container with instructions and legend</returns>
        public static VisualElement CreateTriangulateUI(bool isFaceMode)
        {
            var root = new VisualElement();
            return root;
        }

        /// <summary>
        /// Draw wireframe for a set of vertices and edges.
        /// </summary>
        private static void DrawWireframeForVertices(Vector3[] vertices, Edge[] edges)
        {
            if (vertices == null || edges == null) return;

            foreach (var edge in edges)
            {
                if (edge.a < vertices.Length && edge.b < vertices.Length)
                {
                    Handles.DrawLine(vertices[edge.a], vertices[edge.b], WIREFRAME_THICKNESS);
                }
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Shared helper class for Subdivide operations and visualization.
    /// Used by both Object and Face mode Subdivide preview actions.
    /// </summary>
    public static class SubdivideHelper
    {
        // Visual settings
        private static readonly Color SUBDIVISION_COLOR = Color.cyan;  // New subdivision wireframe color
        private static readonly Color EXISTING_COLOR = Color.white;    // Existing edges color
        private const float WIREFRAME_THICKNESS = 2.0f;

        /// <summary>
        /// Data structure to hold subdivision preview information for a mesh.
        /// </summary>
        public sealed class SubdivisionPreview
        {
            public Vector3[] allVertices;        // All vertices including new ones (world space)
            public Edge[] newSubdivisionEdges;   // NEW edges created by subdivision
            public Edge[] existingEdges;        // Existing ProBuilder edges
        }

        /// <summary>
        /// Calculate subdivision preview for all faces on a mesh (Object mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <returns>Subdivision preview data</returns>
        public static SubdivisionPreview CalculateSubdivisionPreview(ProBuilderMesh mesh)
        {
            return CalculateSubdivisionPreview(mesh, mesh.faces.ToArray());
        }

        /// <summary>
        /// Calculate subdivision preview for specific faces on a mesh (Face mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToSubdivide">Specific faces to subdivide</param>
        /// <returns>Subdivision preview data</returns>
        public static SubdivisionPreview CalculateSubdivisionPreview(ProBuilderMesh mesh, Face[] facesToSubdivide)
        {
            var preview = new SubdivisionPreview();
            var originalVertices = mesh.GetVertices();
            
            // Get existing ProBuilder edges from the faces we're considering
            var existingProBuilderEdges = new HashSet<Edge>();
            foreach (var face in facesToSubdivide)
            {
                // Get the face edges (perimeter edges of faces)
                foreach (var edge in face.edges)
                {
                    existingProBuilderEdges.Add(edge);
                }
            }

            // Store existing edges
            preview.existingEdges = existingProBuilderEdges.ToArray();

            // Calculate subdivision preview using ProBuilder's subdivision logic
            // For preview, we'll simulate what subdivision would create - showing only the hard edges
            var newVerticesList = new List<Vector3>(originalVertices.Select(v => v.position));
            var newSubdivisionEdges = new HashSet<Edge>();
            
            // Process each face for subdivision
            foreach (var face in facesToSubdivide)
            {
                var faceIndices = face.indexes;
                
                // For each face, subdivision creates new vertices at edge midpoints and face center
                if (faceIndices.Count >= 3)
                {
                    // Calculate face center
                    Vector3 faceCenter = Vector3.zero;
                    foreach (var index in faceIndices)
                    {
                        faceCenter += originalVertices[index].position;
                    }
                    faceCenter /= faceIndices.Count;
                    
                    int faceCenterIndex = newVerticesList.Count;
                    newVerticesList.Add(faceCenter);
                    
                    // Create edge midpoints and connect them to face center (these are the main subdivision edges)
                    for (int i = 0; i < faceIndices.Count; i++)
                    {
                        int current = faceIndices[i];
                        int next = faceIndices[(i + 1) % faceIndices.Count];
                        
                        // Create edge midpoint
                        Vector3 midpoint = (originalVertices[current].position + originalVertices[next].position) * 0.5f;
                        int midpointIndex = newVerticesList.Count;
                        newVerticesList.Add(midpoint);
                        
                        // ONLY connect midpoint to face center (this shows the subdivision structure without inner triangulation)
                        newSubdivisionEdges.Add(new Edge(midpointIndex, faceCenterIndex));
                    }
                    
                    // NOTE: We intentionally DON'T add edges from original vertices to face center
                    // or between adjacent midpoints as these create inner triangulation which the user doesn't want to see
                }
            }

            // Convert all vertices to world space
            preview.allVertices = new Vector3[newVerticesList.Count];
            for (int i = 0; i < newVerticesList.Count; i++)
            {
                preview.allVertices[i] = mesh.transform.TransformPoint(newVerticesList[i]);
            }

            // Store new subdivision edges
            preview.newSubdivisionEdges = newSubdivisionEdges.ToArray();
            
            return preview;
        }

        /// <summary>
        /// Draw wireframe preview for subdivision.
        /// Shows existing edges in white and new subdivision edges in cyan.
        /// </summary>
        /// <param name="previews">Array of subdivision previews to draw</param>
        public static void DrawSubdivisionPreviews(SubdivisionPreview[] previews)
        {
            if (previews == null) return;

            // Set z-test to respect depth so lines are properly occluded by geometry
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            foreach (var preview in previews)
            {
                if (preview?.allVertices == null) continue;

                // Draw existing ProBuilder edges in white
                Handles.color = EXISTING_COLOR;
                DrawWireframeForVertices(preview.allVertices, preview.existingEdges);

                // Draw new subdivision edges in cyan
                Handles.color = SUBDIVISION_COLOR;
                DrawWireframeForVertices(preview.allVertices, preview.newSubdivisionEdges);
            }

            // Reset handles
            Handles.color = Color.white;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        }

        /// <summary>
        /// Apply subdivision to all faces on a mesh (Object mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <returns>Array of resulting subdivided faces, or null if subdivision failed</returns>
        public static Face[] ApplySubdivision(ProBuilderMesh mesh)
        {
            return ApplySubdivision(mesh, mesh.faces.ToArray());
        }

        /// <summary>
        /// Apply subdivision to specific faces on a mesh (Face mode).
        /// </summary>
        /// <param name="mesh">The ProBuilder mesh</param>
        /// <param name="facesToSubdivide">Specific faces to subdivide</param>
        /// <returns>Array of resulting subdivided faces, or null if subdivision failed</returns>
        public static Face[] ApplySubdivision(ProBuilderMesh mesh, Face[] facesToSubdivide)
        {
            if (facesToSubdivide == null || facesToSubdivide.Length == 0)
                return null;

            // Apply subdivision using ProBuilder's subdivision operations
            mesh.ToMesh();

            // Use ProBuilder's built-in subdivision via ConnectElements.Connect (this is what Subdivide calls internally)
            var subdividedFaces = ConnectElements.Connect(mesh, facesToSubdivide);

            mesh.Refresh();

            return subdividedFaces;
        }

        /// <summary>
        /// Create UI elements for subdivision settings.
        /// </summary>
        /// <param name="isFaceMode">True for face mode, false for object mode</param>
        /// <returns>UI container with instructions and legend</returns>
        public static VisualElement CreateSubdivideUI(bool isFaceMode)
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

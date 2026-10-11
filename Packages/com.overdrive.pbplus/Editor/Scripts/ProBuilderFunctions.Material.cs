using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    // File contains static methods that get or apply materials or vertex colors to ProBuilder selections.
    public static partial class ProBuilderFunctions
    {
        public static void ApplyMaterial(Material material)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes.Select(static m => m.GetComponent<Renderer>()).Where(static r => r != null).ToArray(), "Change Face Material");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null) continue;

                var renderer = mesh.GetComponent<Renderer>();
                if (renderer == null) continue;

                foreach (var face in selectedFaces)
                {
                    // Set the material index for the face
                    if (material != null)
                    {
                        // Find or add material to renderer
                        var materials = renderer.sharedMaterials.ToList();
                        int materialIndex = materials.IndexOf(material);

                        if (materialIndex == -1)
                        {
                            materials.Add(material);
                            materialIndex = materials.Count - 1;
                            renderer.sharedMaterials = materials.ToArray();
                        }

                        face.submeshIndex = materialIndex;
                    }
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ClearVertexColor()
        {
            if (ProBuilderPlusCore.CurrentToolMode.IsEditMode())
            {
                ApplyVertexColor(Color.white);
            }
            else
            {
                var selectedMeshes = MeshSelection.top.ToArray();
                if (selectedMeshes.Length == 0) return;

                Undo.RecordObjects(selectedMeshes, "Clear Vertex Color");
                foreach (var mesh in selectedMeshes)
                {
                    mesh.colors = null;
                    mesh.ToMesh();
                    mesh.Refresh();
                }
            }
        }

        public static void ApplyVertexColor(Color color)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change Vertex Color");

            Color linearColor = PlayerSettings.colorSpace == ColorSpace.Linear ? color.linear : color;

            foreach (var mesh in selectedMeshes)
            {
                // Ensure the mesh has color arrays initialized
                Color[] colors = mesh.GetColors();
                if (colors == null || colors.Length != mesh.vertexCount)
                {
                    colors = new Color[mesh.vertexCount];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        colors[i] = Color.white;
                    }
                }

                var currentToolMode = ProBuilderPlusCore.CurrentToolMode;
                switch (currentToolMode)
                {
                    case ToolMode.Face:
                    case ToolMode.UvFace:
                        var selectedFaces = mesh.GetSelectedFaces();
                        if (selectedFaces != null)
                        {
                            foreach (var face in selectedFaces)
                            {
                                foreach (int vertexIndex in face.distinctIndexes)
                                {
                                    if (vertexIndex < colors.Length)
                                        colors[vertexIndex] = linearColor;
                                }
                            }
                        }
                        break;

                    case ToolMode.Edge:
                        var selectedEdges = mesh.selectedEdges;
                        if (selectedEdges != null)
                        {
                            foreach (var edge in selectedEdges)
                            {
                                // Color both vertices of the edge and their coincident vertices
                                int[] edgeVertices = { edge.a, edge.b };

                                foreach (int vertexIndex in edgeVertices)
                                {
                                    if (vertexIndex < colors.Length)
                                        colors[vertexIndex] = linearColor;

                                    // Find and color all coincident vertices for this edge vertex
                                    var sharedVertices = mesh.sharedVertices;
                                    foreach (var sharedVertexGroup in sharedVertices)
                                    {
                                        if (sharedVertexGroup.Contains(vertexIndex))
                                        {
                                            // Color all vertices in this shared vertex group
                                            foreach (int sharedVertexIndex in sharedVertexGroup)
                                            {
                                                if (sharedVertexIndex < colors.Length)
                                                    colors[sharedVertexIndex] = linearColor;
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case ToolMode.Vertex:
                        var selectedVertices = mesh.selectedVertices;
                        if (selectedVertices != null)
                        {
                            // For each selected vertex, find all coincident vertices and color them too
                            foreach (int vertexIndex in selectedVertices)
                            {
                                if (vertexIndex < colors.Length)
                                    colors[vertexIndex] = linearColor;

                                // Find and color all coincident vertices (vertices at the same position)
                                var sharedVertices = mesh.sharedVertices;
                                foreach (var sharedVertexGroup in sharedVertices)
                                {
                                    if (sharedVertexGroup.Contains(vertexIndex))
                                    {
                                        // Color all vertices in this shared vertex group
                                        foreach (int sharedVertexIndex in sharedVertexGroup)
                                        {
                                            if (sharedVertexIndex < colors.Length)
                                                colors[sharedVertexIndex] = linearColor;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                }

                // Set the colors back to the mesh
                mesh.colors = colors;
                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static Material GetCurrentFaceMaterial()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return null;

            var mesh = selectedMeshes[0];
            var selectedFaces = mesh.GetSelectedFaces();
            if (selectedFaces == null || selectedFaces.Length == 0) return null;

            // Get the material from the first selected face
            var face = selectedFaces[0];
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterials == null) return null;

            // ProBuilder faces have a material index
            int materialIndex = face.submeshIndex;
            if (materialIndex >= 0 && materialIndex < renderer.sharedMaterials.Length)
            {
                return renderer.sharedMaterials[materialIndex];
            }

            return null;
        }

        public static Color GetCurrentSelectionVertexColor()
        {
            if (MeshSelection.selectedFaceCount == 0) return Color.white;

            // Get color from actual ProBuilder selection using the correct APIs
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return Color.white;

            var mesh = selectedMeshes[0];

            // If the mesh has no color array, just return neutral white.
            if (!mesh.HasArrays(MeshArrays.Color))
            {
                return Color.white;
            }

            var vertices = mesh.GetVertices();
            if (vertices == null || vertices.Length == 0) return Color.white;

            Color vertexColor = Color.white;

            var currentToolMode = ProBuilderPlusCore.CurrentToolMode;
            switch (currentToolMode)
            {
                case ToolMode.Face:
                case ToolMode.UvFace:
                    var selectedFaces = mesh.GetSelectedFaces();
                    if (selectedFaces != null && selectedFaces.Length > 0)
                    {
                        var face = selectedFaces[0];
                        if (face.indexes.Count > 0 && face.indexes[0] < vertices.Length)
                            vertexColor = vertices[face.indexes[0]].color;
                    }
                    break;

                case ToolMode.Edge:
                    var selectedEdges = mesh.selectedEdges;
                    if (selectedEdges != null && selectedEdges.Count > 0)
                    {
                        var edge = selectedEdges[0];
                        if (edge.a < vertices.Length)
                            vertexColor = vertices[edge.a].color;
                    }
                    break;

                case ToolMode.Vertex:
                    var selectedVertices = mesh.selectedVertices;
                    if (selectedVertices != null && selectedVertices.Count > 0)
                    {
                        int vertexIndex = selectedVertices[0];
                        if (vertexIndex < vertices.Length)
                            vertexColor = vertices[vertexIndex].color;
                    }
                    break;
            }

            // Convert from linear to gamma space for display
            return vertexColor.gamma;
        }
    }
}

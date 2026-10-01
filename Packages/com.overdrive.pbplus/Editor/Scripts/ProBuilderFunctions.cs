using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Static methods that access the Unity and UnityProBuilder API through it's
    /// static <see cref="MeshSelection"/> and <see cref="Selection"/> types.
    /// </summary>
    public static partial class ProBuilderFunctions
    {
        public static HashSet<int> CreateSelectedEdgesHashSet()
        {
            var indices = new HashSet<int>();
            var selectedMeshes = MeshSelection.top.ToArray();

            foreach (var mesh in selectedMeshes)
            {
                var selectedEdges = mesh.selectedEdges;
                if (selectedEdges != null)
                {
                    foreach (var edge in selectedEdges)
                    {
                        // Create a unique identifier for this edge using its vertex indices
                        var edgeId = edge.a * 1000000 + edge.b; // Simple hash for edge
                        indices.Add(edgeId);
                    }
                }
            }

            return indices;
        }

        public static HashSet<int> CreateSelectedFacesHashSet()
        {
            var indices = new HashSet<int>();
            var selectedMeshes = MeshSelection.top.ToArray();

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces != null)
                {
                    var allFaces = mesh.faces.ToArray();
                    for (var i = 0; i < allFaces.Length; i++)
                    {
                        foreach (var selectedFace in selectedFaces)
                        {
                            if (allFaces[i] == selectedFace)
                            {
                                indices.Add(i);
                                break;
                            }
                        }
                    }
                }
            }

            return indices;
        }

        public static HashSet<int> CreateSelectedVerticesHashSet()
        {
            var indices = new HashSet<int>();
            var selectedMeshes = MeshSelection.top.ToArray();

            foreach (var mesh in selectedMeshes)
            {
                var selectedVertices = mesh.selectedVertices;
                if (selectedVertices != null)
                {
                    foreach (var vertexIndex in selectedVertices)
                    {
                        indices.Add(vertexIndex);
                    }
                }
            }

            return indices;
        }

        public static int GetProBuilderSelectionCount(ToolMode mode)
        {
            return mode switch
            {
                ToolMode.Face => MeshSelection.selectedFaceCount,
                ToolMode.Edge => MeshSelection.selectedEdgeCount,
                ToolMode.Vertex => MeshSelection.selectedVertexCount,
                ToolMode.UvFace => MeshSelection.selectedFaceCount,
                _ => 0,
            };
        }

        public static bool IsBasicMeshSelected()
        {
            if (Selection.gameObjects == null || Selection.transforms.Length == 0)
            {
                return false;
            }

            foreach (var obj in Selection.transforms)
            {
                if (obj.TryGetComponent(out MeshFilter meshFilter)
                    && obj.TryGetComponent(out MeshRenderer meshRenderer))
                {
                    if (!obj.TryGetComponent(out ProBuilderMesh _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsProBuilderMeshesSelected()
        {
            return MeshSelection.selectedObjectCount > 0;
        }
    }
}

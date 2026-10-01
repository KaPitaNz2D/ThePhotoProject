using System.Linq;
using UnityEngine;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    // File contains static methods that get mixed values from selected faces/edges/vertices.
    // The term 'mixed' means that multiple selected items have a different value.
    public static partial class ProBuilderFunctions
    {
        public static (Material material, bool hasMixed) GetCurrentFaceMaterialWithMixed()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return (null, false);

            Material firstMaterial = null;
            var hasMixed = false;

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null) continue;

                var renderer = mesh.GetComponent<Renderer>();
                if (renderer == null || renderer.sharedMaterials == null) continue;

                foreach (var face in selectedFaces)
                {
                    var materialIndex = face.submeshIndex;
                    Material material = null;
                    if (materialIndex >= 0 && materialIndex < renderer.sharedMaterials.Length)
                    {
                        material = renderer.sharedMaterials[materialIndex];
                    }

                    if (firstMaterial == null) firstMaterial = material;
                    else if (firstMaterial != material) hasMixed = true;
                }
                if (hasMixed) break;
            }

            return (firstMaterial, hasMixed);
        }

        public static (int smoothingGroup, bool hasMixed) GetCurrentFaceSmoothingGroupWithMixed()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return (0, false);

            int? firstSmoothingGroup = null;
            var hasMixed = false;

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null) continue;

                foreach (var face in selectedFaces)
                {
                    if (firstSmoothingGroup == null) firstSmoothingGroup = face.smoothingGroup;
                    else if (firstSmoothingGroup != face.smoothingGroup) hasMixed = true;
                }
                if (hasMixed) break;
            }

            return (firstSmoothingGroup ?? 0, hasMixed);
        }

        public static (Color color, bool hasMixed) GetCurrentSelectionColorWithMixed()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return (Color.white, false);

            Color? firstColor = null;
            var hasMixed = false;
            var currentToolMode = ProBuilderPlusCore.CurrentToolMode;

            foreach (var mesh in selectedMeshes)
            {
                if (!mesh.HasArrays(MeshArrays.Color))
                {
                    continue;
                }

                var vertices = mesh.GetVertices();
                if (vertices == null || vertices.Length == 0) continue;


                switch (currentToolMode)
                {
                    case ToolMode.Face:
                    case ToolMode.UvFace:
                        var selectedFaces = mesh.GetSelectedFaces();
                        if (selectedFaces != null)
                        {
                            foreach (var face in selectedFaces)
                            {
                                if (face.indexes.Count > 0 && face.indexes[0] < vertices.Length)
                                {
                                    var color = vertices[face.indexes[0]].color.gamma;
                                    if (firstColor == null) firstColor = color;
                                    else if (!Mathf.Approximately(Vector4.Distance(firstColor.Value, color), 0))
                                    {
                                        hasMixed = true;
                                        break;
                                    }
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
                                if (edge.a < vertices.Length)
                                {
                                    var color = vertices[edge.a].color.gamma;
                                    if (firstColor == null) firstColor = color;
                                    else if (!Mathf.Approximately(Vector4.Distance(firstColor.Value, color), 0))
                                    {
                                        hasMixed = true;
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    case ToolMode.Vertex:
                        var selectedVertices = mesh.selectedVertices;
                        if (selectedVertices != null)
                        {
                            foreach (var vertexIndex in selectedVertices)
                            {
                                if (vertexIndex < vertices.Length)
                                {
                                    var color = vertices[vertexIndex].color.gamma;
                                    if (firstColor == null) firstColor = color;
                                    else if (!Mathf.Approximately(Vector4.Distance(firstColor.Value, color), 0))
                                    {
                                        hasMixed = true;
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                }

                if (hasMixed)
                {
                    break;
                }
            }

            return (firstColor ?? Color.white, hasMixed);
        }

        public static (UVMode uvMode, bool hasMixed) GetCurrentUVModeWithMixed()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return (UVMode.Auto, false);

            UVMode? firstMode = null;
            var hasMixed = false;

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null || selectedFaces.Length == 0) continue;

                foreach (var selectedFace in selectedFaces)
                {
                    var selectedFaceMode = selectedFace.manualUV ? UVMode.Manual : UVMode.Auto;

                    if (firstMode == null)
                    {
                        firstMode = selectedFaceMode;
                    }
                    else if (firstMode != selectedFaceMode)
                    {
                        hasMixed = true;
                        break;
                    }
                }

                if (hasMixed)
                {
                    break;
                }
            }

            return (firstMode ?? UVMode.Auto, hasMixed);
        }

        internal static UVValues GetUVValuesWithMixedDetection()
        {
            var result = new UVValues();
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0)
            {
                // Default values
                result.anchor = AutoUnwrapSettings.Anchor.MiddleCenter;
                result.fill = AutoUnwrapSettings.Fill.Fit;
                result.offset = Vector2.zero;
                result.rotation = 0f;
                result.scale = Vector2.one;
                result.useWorldSpace = false;
                result.textureGroup = 0;
                return result;
            }

            AutoUnwrapSettings.Anchor? firstAnchor = null;
            AutoUnwrapSettings.Fill? firstFill = null;
            Vector2? firstOffset = null;
            float? firstRotation = null;
            Vector2? firstScale = null;
            bool? firstUseWorldSpace = null;
            int? firstTextureGroup = null; // currently not fully implemented

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null) continue;

                foreach (var face in selectedFaces)
                {
                    var uvSettings = face.uv;

                    // Check anchor
                    if (firstAnchor == null) firstAnchor = uvSettings.anchor;
                    else if (firstAnchor != uvSettings.anchor) result.hasMixedAnchor = true;

                    // Check fill
                    if (firstFill == null) firstFill = uvSettings.fill;
                    else if (firstFill != uvSettings.fill) result.hasMixedFill = true;

                    // Check offset
                    if (firstOffset == null) firstOffset = uvSettings.offset;
                    else if (Vector2.Distance(firstOffset.Value, uvSettings.offset) > 0.001f) result.hasMixedOffset = true;

                    // Check rotation
                    if (firstRotation == null) firstRotation = uvSettings.rotation;
                    else if (Mathf.Abs(firstRotation.Value - uvSettings.rotation) > 0.001f) result.hasMixedRotation = true;

                    // Check scale
                    if (firstScale == null) firstScale = uvSettings.scale;
                    else if (Vector2.Distance(firstScale.Value, uvSettings.scale) > 0.001f) result.hasMixedScale = true;

                    // UseWorldSpace
                    if (firstUseWorldSpace == null) firstUseWorldSpace = uvSettings.useWorldSpace;
                    else if (firstUseWorldSpace != uvSettings.useWorldSpace) result.hasMixedUseWorldSpace = true;

                    // Texture group (placeholder for now) // Todo: Curretly does not use face.textureGroup why?
                    if (firstTextureGroup == null) firstTextureGroup = 0; // Default group
                    else if (firstTextureGroup != 0) result.hasMixedTextureGroup = true;
                }
            }

            // Set the first values as defaults
            result.anchor = firstAnchor ?? AutoUnwrapSettings.Anchor.MiddleCenter;
            result.fill = firstFill ?? AutoUnwrapSettings.Fill.Fit;
            result.offset = firstOffset ?? Vector2.zero;
            result.rotation = firstRotation ?? 0f;
            result.scale = firstScale ?? Vector2.one;
            result.useWorldSpace = firstUseWorldSpace ?? false;
            result.textureGroup = firstTextureGroup ?? 0;

            return result;
        }

        /// <summary>
        /// Mixed value representation of UV values of <see cref="AutoUnwrapSettings"/>.
        /// </summary>
        internal struct UVValues
        {
            #region MixedFlags
            public bool hasMixedAnchor;
            public bool hasMixedFill;
            public bool hasMixedOffset;
            public bool hasMixedRotation;
            public bool hasMixedScale;
            public bool hasMixedUseWorldSpace;
            public bool hasMixedTextureGroup;
            #endregion MixedFlags

            #region Values
            public AutoUnwrapSettings.Anchor anchor;
            public AutoUnwrapSettings.Fill fill;
            public Vector2 offset;
            public float rotation;
            public Vector2 scale;
            public bool useWorldSpace;
            public int textureGroup;
            #endregion Values

            public readonly BooleanValues GetUvUseWorld()
            {
                return this.useWorldSpace ? BooleanValues.True : BooleanValues.False;
            }
        }
    }
}

using System.Linq;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    // File contains static methods that change the UV settings of selected ProBuilder faces.
    public static partial class ProBuilderFunctions
    {
        public static void ProjectUV(UVProjectionMode projectionMode)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Project UV");

            foreach (var mesh in selectedMeshes)
            {
                if (mesh.selectedFaceCount > 0)
                {
                    mesh.ToMesh();

                    // Uses Unity internal projection methods through reflection.
                    if (projectionMode == UVProjectionMode.Planar)
                    {
                        var projectAuto = ProBuilderInternals.ProjectFacesAutoMethod;
                        var channel = 0;
                        projectAuto.Invoke(obj: null, new object[] { mesh, mesh.GetSelectedFaces(), channel });
                    }
                    else
                    {
                        var projectAuto = ProBuilderInternals.ProjectFacesBoxMethod;
                        var channel = 0;
                        var lowerLeftAnchor = Vector2.zero;
                        projectAuto.Invoke(obj: null, new object[] { mesh, mesh.GetSelectedFaces(), lowerLeftAnchor, channel });
                    }

                    mesh.Refresh();
                    mesh.Optimize();
                }
            }
        }

        public static void FlipUV(UVAxis axis)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Flip UV");

            // Flipping is done by reflecting points on a line
            var mirrorLineDirection = axis switch
            {
                UVAxis.U => Vector2.up, // vertical line to flip horizontally
                _ => Vector2.right, // the other way round
            };

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                // Copy of texture UVs is only required for manualUV faces,
                // so get it once per mesh only when required.
                List<Vector2> textureUV0Values = null;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    if (face.manualUV) // UV Manual writes the TextureUV0 coordinates directly 
                    {
                        if (textureUV0Values == null)
                        {
                            textureUV0Values = new List<Vector2>(mesh.textures);
                        }

                        // Face may be triangle or quad. Triangles have 3 vertices, quads have 3X2 vertices, but two are shared.
                        var vertexIndices = face.distinctIndexes;

                        var center = textureUV0Values[vertexIndices[0]];
                        foreach (var vertexIndex in vertexIndices)
                        {
                            textureUV0Values[vertexIndex] = Math.ReflectPoint(
                                point: textureUV0Values[vertexIndex],
                                lineStart: center,
                                lineEnd: center + mirrorLineDirection);
                        }
                    }
                    else // UV Auto writes into the AutoUnwrapSettings
                    {
                        var uvSettings = face.uv;

                        if (axis == UVAxis.U)
                        {
                            uvSettings.flipU = !uvSettings.flipU;
                        }
                        else if (axis == UVAxis.V)
                        {
                            uvSettings.flipV = !uvSettings.flipV;
                        }

                        face.uv = uvSettings;
                        SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                    }
                }

                if (textureUV0Values != null)
                {
                    // Write back new UVs if changed.
                    mesh.textures = textureUV0Values;
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVAnchor(AutoUnwrapSettings.Anchor anchor)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Anchor");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];
                    var uvSettings = face.uv;
                    uvSettings.anchor = anchor;
                    face.uv = uvSettings;
                    SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVUseWorldSpace(bool useWorldSpace)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Set UV Use World Space");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    var uvSettings = face.uv;
                    uvSettings.useWorldSpace = useWorldSpace;
                    face.uv = uvSettings;
                    SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVFillMode(AutoUnwrapSettings.Fill fillMode)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Fill Mode");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    var uvSettings = face.uv;
                    uvSettings.fill = fillMode;
                    face.uv = uvSettings;
                    SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVGroup(int uvGroup)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Group");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null) continue;

                foreach (var face in selectedFaces)
                {
                    // Note: UV Group might need to be stored as a custom property
                    // since ProBuilder's AutoUnwrapSettings doesn't have a direct equivalent
                    // This would need to be implemented based on how UV groups are used
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVMode(UVMode uvMode)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Mode");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                var allFaces = mesh.faces;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];
                    face.manualUV = uvMode == UVMode.Manual;
                }
            }
        }

        public static void ApplyUVOffset(Vector2 offset)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Offset");
            int operationsCount = 0;

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    if (!face.manualUV) // Settings the offset to UV Manual faces would do nothing.
                    {
                        var uvSettings = face.uv;
                        uvSettings.offset = offset;
                        face.uv = uvSettings;
                        SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                        operationsCount++;
                    }
                }

                mesh.ToMesh();
                mesh.Refresh();
            }

            if (operationsCount == 0)
            {
                Debug.LogWarning("Settings UV Offsets does not work on faces with UV mode set to manual.");
            }
        }

        public static void ApplyUVOffsetPanTexels(Vector2 offsetTexels)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Offset");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                if (!mesh.TryGetComponent<Renderer>(out var renderer)) continue;

                if (renderer.sharedMaterials == null) continue;

                // Copy of texture UVs is only required for manualUV faces,
                // so get it once per mesh only when required.
                List<Vector2> textureUV0Values = null;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    int materialIndex = face.submeshIndex;
                    if (materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
                    {
                        continue;
                    }

                    var material = renderer.sharedMaterials[materialIndex];
                    if (material.mainTexture == null)
                    {
                        continue;
                    }

                    // From Texel Size to relative UV Values.
                    var w = (float)material.mainTexture.width;
                    var h = (float)material.mainTexture.width;
                    var uvOffset = new Vector2(
                        offsetTexels.x / w,
                        offsetTexels.y / h);

                    if (face.manualUV) // UV Manual writes the TextureUV0 coordinates directly 
                    {
                        if (textureUV0Values == null)
                        {
                            textureUV0Values = new List<Vector2>(mesh.textures);
                        }

                        // Face may be triangle or quad. Triangles have 3 vertices, quads have 3X2 vertices, but two are shared.
                        var vertexIndices = face.distinctIndexes;
                        foreach (var vertexIndex in vertexIndices)
                        {
                            textureUV0Values[vertexIndex] += uvOffset;
                        }
                    }
                    else // UV Auto writes into the AutoUnwrapSettings
                    {
                        var uvSettings = face.uv;
                        uvSettings.offset += uvOffset;
                        face.uv = uvSettings;
                        SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                    }
                }

                if (textureUV0Values != null)
                {
                    // Write back new UVs if changed.
                    mesh.textures = textureUV0Values;
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVRotation(float rotation)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Rotation");

            int totalFacesUpdated = 0;
            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;


                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    var uvSettings = face.uv;
                    uvSettings.rotation = rotation;
                    face.uv = uvSettings;
                    SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                    totalFacesUpdated++;
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void ApplyUVScale(Vector2 scale)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Scale");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    var uvSettings = face.uv;
                    uvSettings.scale = scale;
                    face.uv = uvSettings;
                    SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static void AppplyUVOffsetPan(Vector2 offset)
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return;

            Undo.RecordObjects(selectedMeshes, "Change UV Offset");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaceIndices = mesh.selectedFaceIndexes;
                if (selectedFaceIndices == null) continue;

                IList<Face> allFaces = mesh.faces;
                if (allFaces == null) continue;

                // Copy of texture UVs is only required for manualUV faces,
                // so get it once per mesh only when required.
                List<Vector2> textureUV0Values = null;

                for (int i = 0; i < selectedFaceIndices.Count; i++)
                {
                    var selectedFaceIndex = selectedFaceIndices[i];
                    var face = allFaces[selectedFaceIndex];

                    if (face.manualUV) // UV Manual writes the TextureUV0 coordinates directly 
                    {
                        if (textureUV0Values == null)
                        {
                            textureUV0Values = new List<Vector2>(mesh.textures);
                        }

                        // Face may be triangle or quad. Triangles have 3 vertices, quads have 3X2 vertices, but two are shared.
                        var vertexIndices = face.distinctIndexes;
                        foreach (var vertexIndex in vertexIndices)
                        {
                            textureUV0Values[vertexIndex] += offset;
                        }
                    }
                    else // UV Auto writes into the AutoUnwrapSettings
                    {
                        var uvSettings = face.uv;
                        uvSettings.offset += offset;
                        face.uv = uvSettings;
                        SetUVSettingsToGroup(uvSettings, face.textureGroup, allFaces);
                    }
                }

                if (textureUV0Values != null)
                {
                    // Write back new UVs if changed.
                    mesh.textures = textureUV0Values;
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static AutoUnwrapSettings.Fill GetCurrentUVFillMode()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return AutoUnwrapSettings.Fill.Fit;

            var mesh = selectedMeshes[0];
            var selectedFaceIndices = mesh.selectedFaceIndexes;
            if (selectedFaceIndices == null || selectedFaceIndices.Count == 0) return AutoUnwrapSettings.Fill.Fit;

            // Return the fill mode from the first selected face
            return mesh.faces[selectedFaceIndices[0]].uv.fill;
        }

        public static AutoUnwrapSettings? GetCurrentUVSettings()
        {
            var selectedMeshes = MeshSelection.top.ToArray();
            if (selectedMeshes.Length == 0) return null;

            var mesh = selectedMeshes[0];
            var selectedFaceIndices = mesh.selectedFaceIndexes;
            if (selectedFaceIndices == null || selectedFaceIndices.Count == 0) return null;

            // Return the UV settings from the first selected face
            return mesh.faces[selectedFaceIndices[0]].uv;
        }

        /// <summary>
        /// Applies the given uvSettings to all faces that match the given textureGroup if it is valid ( bigger than 0 ).
        /// </summary>
        /// <param name="uvSettings">Settings to write.</param>
        /// <param name="textureGroup">ID of the textureGroup. Values above 0 indicate a textureGroup.</param>
        /// <param name="allFaces">Collection of all faces in the mesh.</param>
        public static void SetUVSettingsToGroup(AutoUnwrapSettings uvSettings, int textureGroup, IList<Face> allFaces)
        {
            // Acts like 'UnityEngine.ProBuilder.ProBuilderMesh.SetGroupUV'
            if (textureGroup <= 0)
                return;

            foreach (var face in allFaces)
            {
                if (face.textureGroup != textureGroup)
                    continue;

                face.uv = uvSettings;
            }
        }
    }
}

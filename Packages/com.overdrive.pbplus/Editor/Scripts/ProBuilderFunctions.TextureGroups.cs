using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    // File contains static methods that get or apply TextureGroups to selected ProBuilder faces.
    public static partial class ProBuilderFunctions
    {
        public static void SetSelectedFacesToUnusedTextureGroup()
        {
            if (MeshSelection.selectedFaceCount == 0) return;
            var selectedMeshes = MeshSelection.top.ToArray();

            Undo.RecordObjects(selectedMeshes, "Group Selected Faces");

            foreach (var mesh in selectedMeshes)
            {
                Face[] faces = mesh.GetSelectedFaces();
                AutoUnwrapSettings cont_uv = faces[0].uv;
                int texGroup = GetUnusedTextureGroup(mesh);

                foreach (Face f in faces)
                {
                    f.uv = new AutoUnwrapSettings(cont_uv);
                    f.textureGroup = texGroup;
                }

                mesh.ToMesh();
                mesh.Refresh();
            }

            ProBuilderEditor.Refresh();
        }

        public static void UngroupSelectedFaces()
        {
            if (MeshSelection.selectedFaceCount == 0) return;
            var selectedMeshes = MeshSelection.top.ToArray();

            Undo.RecordObjects(selectedMeshes, "Ungroup Selected Faces");

            foreach (var mesh in selectedMeshes)
            {
                Face[] faces = mesh.GetSelectedFaces();
                AutoUnwrapSettings cuv = faces[0].uv;

                foreach (Face f in faces)
                {
                    f.textureGroup = -1;
                    f.uv = new AutoUnwrapSettings(cuv);
                }

                mesh.ToMesh();
                mesh.Refresh();
                mesh.Optimize();
            }

            ProBuilderEditor.Refresh();
        }

        private static int GetUnusedTextureGroup(ProBuilderMesh pb, int startIndex = 1)
        {
            var allFaces = pb.faces;
            var usedGroups = new HashSet<int>(allFaces.Select(static f => f.textureGroup));

            int textureGroup = startIndex;
            while (usedGroups.Contains(textureGroup))
            {
                textureGroup++;
            }

            return textureGroup;
        }
    }
}

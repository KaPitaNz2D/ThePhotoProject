using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;

namespace Overdrive.ProBuilderPlus
{
    // File contains static methods that get or apply smoothing groups to ProBuilder selections.
    public static partial class ProBuilderFunctions
    {
        public static void ApplySmoothingGroup(int smoothingGroup)
        {
            if (MeshSelection.selectedFaceCount == 0) return;

            var selectedMeshes = MeshSelection.top.ToArray();
            
            Undo.RecordObjects(selectedMeshes, "Change Smoothing Group");

            foreach (var mesh in selectedMeshes)
            {
                var selectedFaces = mesh.GetSelectedFaces();
                if (selectedFaces == null) continue;

                foreach (var face in selectedFaces)
                {
                    face.smoothingGroup = smoothingGroup;
                }

                mesh.ToMesh();
                mesh.Refresh();
            }
        }

        public static int GetCurrentFaceSmoothingGroup()
        {
            if (MeshSelection.selectedFaceCount == 0) return 0;

            var selectedMeshes = MeshSelection.top.ToArray();

            var mesh = selectedMeshes[0];
            var selectedFaces = mesh.GetSelectedFaces();
            if (selectedFaces == null || selectedFaces.Length == 0) return 0;

            // Get the smoothing group from the first selected face
            return selectedFaces[0].smoothingGroup;
        }
    }
}

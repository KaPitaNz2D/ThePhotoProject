using UnityEditor;

namespace Overdrive.ProBuilderPlus
{
    [FilePath("Library/ProBuilderPlusData/ProBuilderPlusSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ProjectSettings : ScriptableSingleton<ProjectSettings>
    {
        public static void Save()
        {
            string filePath = GetFilePath();
            string directory = System.IO.Path.GetDirectoryName(filePath);

            instance.Save(true);
        }
    }
}
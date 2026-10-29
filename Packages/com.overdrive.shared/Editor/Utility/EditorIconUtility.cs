using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Overdrive.Framework
{
    /// <summary>
    /// Utility for loading Unity's built-in editor icons.
    /// Provides consistent icon loading across different packages.
    /// </summary>
    public static class EditorIconUtility
    {
        /// <summary>
        /// Loads a built-in Unity editor icon by name.
        /// Tries multiple loading methods to ensure maximum compatibility.
        /// </summary>
        /// <param name="iconName">The name of the icon to load (e.g., "Project", "GameObject Icon")</param>
        /// <returns>The loaded Texture2D, or null if not found</returns>
        public static Texture2D LoadEditorIcon(string iconName)
        {
            // Try FindTexture first (works for some icons like "Project")
            var texture = EditorGUIUtility.FindTexture(iconName);
            if (texture != null)
                return texture;

            // Try IconContent (works for component icons)
            var content = EditorGUIUtility.IconContent(iconName);
            if (content?.image != null)
                return content.image as Texture2D;

            // Try loading from EditorAssetBundle (works for most other icons)
            try
            {
                var editorAssetBundle = GetEditorAssetBundle();
                if (editorAssetBundle != null)
                {
                    var icon = editorAssetBundle.LoadAsset<Texture2D>(iconName);
                    if (icon != null)
                        return icon;
                }
            }
            catch
            {
                // Fallback if bundle loading fails
            }

            return null;
        }

        /// <summary>
        /// Gets Unity's internal editor asset bundle using reflection.
        /// </summary>
        private static AssetBundle GetEditorAssetBundle()
        {
            var method = typeof(EditorGUIUtility).GetMethod("GetEditorAssetBundle", BindingFlags.NonPublic | BindingFlags.Static);
            return (AssetBundle)method?.Invoke(null, new object[] { });
        }
    }
}

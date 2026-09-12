/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#if YS_USE_TEXT_ANIMATOR_2 || YS_USE_TEXT_ANIMATOR_3
#define USE_TEXT_ANIMATOR
#endif

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;

    public class ExampleVersionChecker : MonoBehaviour
    {
        [Range(2, 3)]
        [SerializeField]
        internal int version = 0;
        void Start()
        {
            ValidateVersion(version);
        }
        public void ValidateVersion(int supportedVersion)
        {
#if YS_USE_TEXT_ANIMATOR_2
            var expectedVersion = 2;
#elif YS_USE_TEXT_ANIMATOR_3
            var expectedVersion = 3;
#endif

#if USE_TEXT_ANIMATOR
            if (supportedVersion != expectedVersion)
            {
                if (EditorUtility.DisplayDialog("Unable to continue", $"This example was created for Text Animator v{supportedVersion}, it will not work.", "Exit Play mode"))
                {
                    EditorApplication.ExitPlaymode();
                }
            }
#else
            if (EditorUtility.DisplayDialog("Unable to continue", "This example requires Text Animator installed and configured, it will not work.", "Exit Play mode"))
            {
                EditorApplication.ExitPlaymode();
            }
#endif
        }

        void OnValidate()
        {
            // have this set like this so after the example scene has been made it is annoying to change.
            // Is only designed to stop casual changes.
            // to change this set the flags to be HideFlags.None

            this.hideFlags = HideFlags.NotEditable;
        }
    }

    [CustomEditor(typeof(ExampleVersionChecker))]
    public class ExampleVersionCheckerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var value = target as ExampleVersionChecker;
            if (value == null)
            {
                return;
            }
#if YS_USE_TEXT_ANIMATOR_2
            if (value.version != 2)
            {
                EditorGUILayout.HelpBox($"This example was created for Text Animator version 2. It WILL not work correctly.", MessageType.Error);
            }
#elif YS_USE_TEXT_ANIMATOR_3
            if (value.version != 3)
            {
                EditorGUILayout.HelpBox($"This example was created for Text Animator version 3. It WILL not work correctly.", MessageType.Error);
            }
#else
            EditorGUILayout.HelpBox($"This example requires Text Animator installed and configured. It WILL not work correctly.", MessageType.Error);
#endif
        }
    }
#else // UNITY_EDITOR
    public class ExampleVersionChecker : MonoBehaviour
    {
        // This class is only used in the editor, so we'll replace it with an
        // empty class (to prevent Unity going 'hey there's no class with this
        // guid!')
    }
#endif
}
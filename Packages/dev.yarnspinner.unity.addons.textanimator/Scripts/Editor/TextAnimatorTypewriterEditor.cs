using UnityEngine;
using UnityEditor;
using Yarn.Unity.Addons.TextAnimatorIntegration;
using Yarn.Unity.Editor;
using Yarn.Unity.Addons.TextAnimatorIntegration.Editor;

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{

#if YS_USE_TEXT_ANIMATOR_2
    [CustomEditor(typeof(TextAnimatorYarnTypewriter))]
    public class TextAnimatorTypewriterEditor : YarnEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var value = target as TextAnimatorYarnTypewriter;
            if (value == null)
            {
                return;
            }

            if (value.insertionAction == null)
            {
                if (value.tagMode != TagMode.TextAnimatorOnlyTags)
                {
                    EditorGUILayout.HelpBox($"The tag mode is set to be {value.tagMode}, but {nameof(value.insertionAction)} is null. You must have an action configured to use this tag mode.", MessageType.Warning);
                }
                return;
            }

            var tagID = value.insertionAction.TagID ?? "NULL";
            var registrationStatus = TextAnimationActionState.IsActionRegistered(value.insertionAction);

            switch (registrationStatus)
            {
                case TextAnimationActionState.RegistrationResult.ExistingRegistration:
                    if (value.tagMode != TagMode.TextAnimatorOnlyTags)
                    {
                        EditorGUILayout.HelpBox($"There already exists a Text Animator action registered with tag id: \"{tagID}\". You must only have a single action registered for this tag.", MessageType.Warning);
                    }
                    break;

                case TextAnimationActionState.RegistrationResult.NotRegistered:
                    if (value.tagMode == TagMode.TextAnimatorOnlyTags)
                    {
                        break;
                    }
                    EditorGUILayout.HelpBox($"The tag mode is set to be {value.tagMode}, but {nameof(value.insertionAction)} is not registered with Text Animator. You must register the action to use this mode.", MessageType.Info);
                    if (GUILayout.Button("Register action"))
                    {
                        TextAnimationActionState.RegisterAction(value.insertionAction);
                    }
                    break;

                case TextAnimationActionState.RegistrationResult.Registered:
                    if (value.tagMode == TagMode.TextAnimatorOnlyTags)
                    {
                        EditorGUILayout.HelpBox($"You have registered a Yarn Spinner action tag with Text Animator. This won't cause issues but is indicative of a configuration issue.", MessageType.Info);
                    }
                    break;
            }
        }
    }
#elif !YS_USE_TEXT_ANIMATOR_3
    [CustomEditor(typeof(TextAnimatorYarnTypewriter))]
    public class TextAnimatorTypewriterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox($"This package requires configuration before it can be used.", MessageType.Error);
            if (GUILayout.Button("Open Configuration Window"))
            {
                TextAnimatorConfigurationWindow.OpenWindow();
            }
        }
    }
#endif
}
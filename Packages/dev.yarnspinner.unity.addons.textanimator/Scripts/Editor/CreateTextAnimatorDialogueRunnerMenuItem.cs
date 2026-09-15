/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#if YS_USE_TEXT_ANIMATOR_2 || YS_USE_TEXT_ANIMATOR_3
#define USE_TEXT_ANIMATOR
#endif

namespace Yarn.Unity.Addons.TextAnimatorIntegration.Editor
{
#nullable enable
#if USE_TEXT_ANIMATOR
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.EventSystems;

    // Literally copy-pasted from YS, just with a different menuitem and guid
    public static class CreateTextAnimatorDialogueRunnerMenuItem
    {
        // gonna have to change this so that it uses a different guid for each one
        // which sucks but oh well
#if YS_USE_TEXT_ANIMATOR_3
        const string DialogueRunnerPrefabGUID = "cd09801223b5f4504a921d33ef1ff98c";
#elif YS_USE_TEXT_ANIMATOR_2
        const string DialogueRunnerPrefabGUID = "eeb327a65c4bc4376ae432ce641f04af";
#endif

        /// <summary>
        /// Instantiates the Dialogue System prefab in the currently active scene,
        /// and returns the created <see cref="DialogueRunner"/>.
        /// </summary>
        /// <returns>A newly created <see cref="DialogueRunner"/>.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when the
        /// Dialogue System prefab cannot be found in the Yarn Spinner
        /// package.</exception>
        [MenuItem("GameObject/Yarn Spinner/Dialogue System with Text Animator")]
        public static DialogueRunner CreateDialogueRunner()
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(DialogueRunnerPrefabGUID);
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefabAsset == null)
            {
                throw new System.InvalidOperationException(
                    $"Can't create a new Dialogue System: Can't find the prefab to create a Dialogue System from."
                );
            }

            var instantiatedPrefab = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);

#if UNITY_2023_1_OR_NEWER
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
#else
            var eventSystems = Object.FindObjectsOfType<EventSystem>();
#endif

            if (eventSystems.Length > 1)
            {
                // At least one other event system is present in the scene. Turn off
                // the one that came with the prefab - it's not needed.
                var instantiatedEventSystem = instantiatedPrefab.GetComponentInChildren<EventSystem>();

                instantiatedEventSystem.gameObject.SetActive(false);
            }

            return instantiatedPrefab.GetComponent<DialogueRunner>();
        }
    }
#endif
}
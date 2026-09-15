/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#if YS_USE_TEXT_ANIMATOR_2 || YS_USE_TEXT_ANIMATOR_3
#define USE_TEXT_ANIMATOR
#endif

#nullable enable

namespace Yarn.Unity.Addons.TextAnimatorIntegration.Editor
{
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor.Build;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Linq;

#if YS_USE_TEXT_ANIMATOR_2
    using Febucci.UI;
    using ActionScriptableBase = Febucci.UI.Actions.ActionScriptableBase;
#elif YS_USE_TEXT_ANIMATOR_3
    using Febucci.TextAnimatorForUnity;
    using ActionScriptableBase = Febucci.TextAnimatorForUnity.Actions.Core.ActionScriptableBase;
#else
    using ActionScriptableBase = UnityEngine.ScriptableObject;
#endif

    public class TextAnimatorConfigurationWindow : EditorWindow
    {
        private const string YSTA2Define = "YS_USE_TEXT_ANIMATOR_2";
        private const string YSTA3Define = "YS_USE_TEXT_ANIMATOR_3";

        private const string TextAnimatorV2SettingsType = "Febucci.UI.TextAnimatorSettings";
        private const string TextAnimatorV3SettingsType = "Febucci.TextAnimatorForUnity.TextAnimatorSettings";

        private const string TextAnimatorSettingsName = "TextAnimatorSettings";
        private const string TextAnimatorPageURL = "https://www.febucci.com/tools/text-animator-unity";
        private const string installedAndNotDefinedUXMLGuid = "b2fe556531a6644c0b405d6482d9de4f";
        private const string notInstalledUXMLGuid = "c39c92b7ce46b46329bda933bb30f5fc";
        private const string InstalledAndDefinedUXMLGuid = "02d739c3d6bb545fdb6d4bf14bf07822";

        private const string TextAnimatorRunnerPrefabGuidV2 = "eeb327a65c4bc4376ae432ce641f04af";
        private const string TextAnimatorRunnerPrefabGuidV3 = "cd09801223b5f4504a921d33ef1ff98c";

        private const string TextAnimatorRunnerPackageGuidV2 = "5a3687d8c4744405692bd44867d6ac58";
        private const string TextAnimatorRunnerPackageGuidV3 = "014e0ab0fc9ca4cd69ce3203bfe646f9";

        // this is a terrible hack but seems to work
        // it will get all the possible build targets for whatever version of unity the package is installed in
        // this works because unity has kindly defined a whole bunch of public static build targets already
        // would be nicer to be able to go like `NamedBuildTarget.All` and have it return a list of them, but oh well
        private static List<NamedBuildTarget> AllBuildTargets
        {
            get
            {
                var targets = new List<NamedBuildTarget>();

                // we have this because there are a few targets (like CloudRendering) that are redirected to a different target
                // and these change on a unity version basis so we can't just manually ignore them like we do with Unknown
                // so instead if we have run into a build target with a resolved name that we've already seen we ignore it
                var names = new HashSet<string>();

                var fields = typeof(NamedBuildTarget).GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (var field in fields)
                {
                    // so turns out weird things can happen when trying to set or get defines on unknown
                    // plus it doesn't really mean anything so why bother, let's just skip it
                    if (field.Name == "Unknown")
                    {
                        continue;
                    }

                    // if it isn't a NamedBuildTarget we ignore it
                    if (field.FieldType == typeof(NamedBuildTarget))
                    {
                        var t = (NamedBuildTarget)field.GetValue(null);

                        // Stadia is deprecated and will throw an error if we
                        // attempt to use it; ignore it
                        if (t.TargetName == "Stadia")
                        {
                            continue;
                        }

                        if (names.Add(t.TargetName))
                        {
                            targets.Add(t);
                        }
                    }
                }

                return targets;
            }
        }

        // Because we don't know if TextAnimator is installed we can't really safely add the scripting define
        // So this property does a check looking for the TextAnimator global settings in the resources folder
        // if we find something with it's name we then check if the name of the type is what we'd expect
        // this isn't perfect but seems to work pretty well and is mostly robust to change
        private bool IsTextAnimatorInstalled
        {
            get
            {
                var resources = Resources.LoadAll(TextAnimatorSettingsName);
                foreach (var resource in resources)
                {
                    if (resource.GetType().ToString().EndsWith(TextAnimatorV2SettingsType))
                    {
                        return true;
                    }
                    else if (resource.GetType().ToString().EndsWith(TextAnimatorV3SettingsType))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private bool IsTextAnimatorV2Installed
        {
            get
            {
                var resources = Resources.LoadAll(TextAnimatorSettingsName);
                foreach (var resource in resources)
                {
                    if (resource.GetType().ToString().EndsWith(TextAnimatorV2SettingsType))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private bool IsTextAnimatorV3Installed
        {
            get
            {
                var resources = Resources.LoadAll(TextAnimatorSettingsName);
                foreach (var resource in resources)
                {
                    if (resource.GetType().ToString().EndsWith(TextAnimatorV3SettingsType))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private string? ScriptingDefine
        {
            get
            {
                if (IsTextAnimatorV2Installed)
                {
                    return YSTA2Define;
                }
                else if (IsTextAnimatorV3Installed)
                {
                    return YSTA3Define;
                }
                else
                {
                    return null;
                }
            }
        }

        private void AddDefines(Dictionary<string, bool> filters)
        {
            var definitionSymbol = ScriptingDefine;
            if (string.IsNullOrEmpty(definitionSymbol))
            {
                Debug.LogWarning("Was unable to determine the valid scripting define for this version of Text Animator. Cannot add the necessary defines.");
                return;
            }
            foreach (var target in AllBuildTargets)
            {
                try
                {
                    if (filters.TryGetValue(target.TargetName, out var value))
                    {
                        if (value == false)
                        {
                            Debug.Log($"skipping {target.TargetName}");
                            continue;
                        }
                    }

                    PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                    if (defines.Contains(definitionSymbol))
                    {
                        continue;
                    }
                    else
                    {
                        var allSymbols = defines.ToList();
                        allSymbols.Add(definitionSymbol);
                        PlayerSettings.SetScriptingDefineSymbols(target, allSymbols.ToArray());
                    }
                }
                catch (System.ArgumentException)
                {
                    Debug.LogWarning($"{target.TargetName} is not supported");
                    continue;
                }
            }
            this.Close();
        }

        private enum InstallationState
        {
            InstalledAndDefined, InstalledAndNotDefined, NotInstalledAndNotDefined, NotInstalledAndDefined,
        }

        private InstallationState installationState
        {
            get
            {
#if USE_TEXT_ANIMATOR
                if (IsTextAnimatorInstalled)
                {
                    return InstallationState.InstalledAndDefined;
                }
                return InstallationState.NotInstalledAndDefined;
#else
                if (IsTextAnimatorInstalled)
                {
                    return InstallationState.InstalledAndNotDefined;
                }
                return InstallationState.NotInstalledAndNotDefined;
#endif
            }
        }

        [SerializeField]
        private VisualTreeAsset? m_VisualTreeAsset = default;

        [MenuItem("Window/Yarn Spinner/Text Animator Integration Configuration")]
        public static void OpenWindow()
        {
            TextAnimatorConfigurationWindow window = GetWindow<TextAnimatorConfigurationWindow>(true, "Yarn Spinner Text Animator Configuration", true);

            var height = 438;

            switch (window.installationState)
            {
                case InstallationState.NotInstalledAndDefined:
                    goto case InstallationState.NotInstalledAndNotDefined;
                case InstallationState.NotInstalledAndNotDefined:
                    height += 54;
                    break;
                case InstallationState.InstalledAndDefined:
                    if (TextAnimationActionState.DefaultActionIsRegistered() == TextAnimationActionState.RegistrationResult.NotRegistered)
                    {
                        height += 99;
                    }
                    else
                    {
                        height += 19;
                    }
                    break;
                case InstallationState.InstalledAndNotDefined:
                    height += 63 + AllBuildTargets.Count * 19;
                    break;
            }

            window.minSize = new Vector2(512, height);
            window.maxSize = window.minSize;
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            if (m_VisualTreeAsset == null)
            {
                return;
            }

            var installation = installationState;
            string activeContainerGUID = installation switch
            {
                InstallationState.InstalledAndDefined => InstalledAndDefinedUXMLGuid,
                InstallationState.InstalledAndNotDefined => installedAndNotDefinedUXMLGuid,
                _ => notInstalledUXMLGuid,
            };

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            root.Q<TemplateContainer>().style.flexGrow = 1;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath(activeContainerGUID));
            if (tree == null)
            {
                return;
            }
            var container = tree.CloneTree();
            root.Add(container);

            switch (installation)
            {
                case InstallationState.InstalledAndDefined:
                    CreateInstalledAndDefinedGUI(container);
                    break;

                case InstallationState.InstalledAndNotDefined:
                    CreateInstalledAndNotDefinedGUI(container);
                    break;

                case InstallationState.NotInstalledAndDefined:
                    goto case InstallationState.NotInstalledAndNotDefined;
                case InstallationState.NotInstalledAndNotDefined:
                    CreateNotInstalledGUI(container);
                    break;
            }
        }

        public void CreateInstalledAndNotDefinedGUI(VisualElement root)
        {
            var container = root.Q<VisualElement>("scriptingToggleContainer");
            if (container == null)
            {
                return;
            }

            var button = root.Q<Button>("scriptingButton");
            if (button == null)
            {
                return;
            }

            Dictionary<string, bool> targetValues = new();
            foreach (var target in AllBuildTargets)
            {
                targetValues.Add(target.TargetName, true);

                var targetName = target.TargetName;
                Toggle toogle = new Toggle(targetName);

                toogle.name = targetName;
                toogle.style.marginLeft = 20;

                toogle.SetValueWithoutNotify(true);
                container.Add(toogle);

                toogle.RegisterValueChangedCallback((evt) =>
                {
                    if (evt.currentTarget is not Toggle t)
                    {
                        return;
                    }
                    targetValues[t.name] = evt.newValue;
                });
            }

            button.clicked += () =>
            {
                ExtractTextAnimatorPackage();
                AddDefines(targetValues);
            };
        }

        private bool GetAssetExists(string guid)
        {
            return string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)) == false;
        }

#pragma warning disable IDE0051 // Private member is unused
        private bool ExtractPackageIfAssetNotPresent(string assetGUID, string packageGUID)
        {
            AssetDatabase.Refresh();
            if (GetAssetExists(assetGUID))
            {
                // The runner prefab already exists. Nothing left to do.
                Debug.Log("Not unpacking Text Animator for Yarn Spinner assets, because it's already in the project.");
                return false;
            }

            var pathToPackage = AssetDatabase.GUIDToAssetPath(packageGUID);

            if (pathToPackage == string.Empty)
            {
                Debug.LogError($"Failed to find the Text Animator for Yarn Spinner package file to extract!");
                return false;
            }

            Debug.Log("Importing package.");
            AssetDatabase.ImportPackage(pathToPackage, false);

            return true;
        }
#pragma warning disable

        private void ExtractTextAnimatorPackage()
        {
            if (IsTextAnimatorV2Installed)
            {
                ExtractPackageIfAssetNotPresent(TextAnimatorRunnerPrefabGuidV2, TextAnimatorRunnerPackageGuidV2);
            }
            else if (IsTextAnimatorV3Installed)
            {
                ExtractPackageIfAssetNotPresent(TextAnimatorRunnerPrefabGuidV3, TextAnimatorRunnerPackageGuidV3);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Unknown Version of TextAnimator",
                    "We aren't able to detect which version of Text Animator you have installed. " +
                    "This means we can't complete the installation. Please contact Yarn Spinner support for help.",
                    "Close");
            }
        }

        public void CreateInstalledAndDefinedGUI(VisualElement root)
        {
            // at this point we are fine to continue but might as well offer to see if the default action can be registered and do it
            var message = root.Q<Label>("definedLabel");
            var button = root.Q<Button>("definedButton");
            if (message == null || button == null)
            {
                return;
            }

            if (TextAnimationActionState.DefaultActionIsRegistered() == TextAnimationActionState.RegistrationResult.NotRegistered)
            {
                message.style.display = DisplayStyle.Flex;
                button.style.display = DisplayStyle.Flex;
                button.clicked += () =>
                {
                    this.Close();
                    TextAnimationActionState.RegisterDefaultAction();
                };
            }
            else
            {
                message.style.display = DisplayStyle.None;
                button.style.display = DisplayStyle.None;
            }
        }
        public void CreateNotInstalledGUI(VisualElement root)
        {
            // basically get the button
            // hook up the call
            var button = root.Q<Button>("notInstalledButton");
            if (button == null)
            {
                return;
            }
            button.clicked += GetTextAnimator;
        }

        private void GetTextAnimator()
        {
            this.Close();
            Application.OpenURL(TextAnimatorConfigurationWindow.TextAnimatorPageURL);
        }
    }

    class TextAnimationActionState
    {
        internal enum RegistrationResult
        {
            NotRegistered, Registered, ExistingRegistration, UnableToResolve,
        }

#if YS_USE_TEXT_ANIMATOR_2
        const string defaultActionGUID = "797c9181a760b4946ab39cc7d835909f";

        internal static RegistrationResult IsActionRegistered(ActionScriptableBase action)
        {
            foreach (var registeredAction in TextAnimatorSettings.Instance.actions.defaultDatabase.Data)
            {
                if (registeredAction == null)
                {
                    continue;
                }
                if (action.TagID == registeredAction.TagID)
                {
                    if (action.GetType() == registeredAction.GetType())
                    {
                        return RegistrationResult.Registered;
                    }
                    return RegistrationResult.ExistingRegistration;
                }
            }
            return RegistrationResult.NotRegistered;
        }
        internal static RegistrationResult DefaultActionIsRegistered()
        {
            ActionMarkupAction? value;

            var path = AssetDatabase.GUIDToAssetPath(defaultActionGUID);
            if (string.IsNullOrEmpty(path))
            {
                return RegistrationResult.UnableToResolve;
            }

            value = AssetDatabase.LoadAssetAtPath<ActionMarkupAction>(path);
            if (value == null)
            {
                return RegistrationResult.UnableToResolve;
            }
            return IsActionRegistered(value);
        }
        internal static void RegisterDefaultAction()
        {
            ActionMarkupAction? value;

            var path = AssetDatabase.GUIDToAssetPath(defaultActionGUID);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            value = AssetDatabase.LoadAssetAtPath<ActionMarkupAction>(path);
            if (value == null)
            {
                return;
            }
            TextAnimatorSettings.Instance.actions.defaultDatabase.Add(value);
        }
        internal static void RegisterAction(ActionScriptableBase action)
        {
            TextAnimatorSettings.Instance.actions.defaultDatabase.Add(action);
        }
#elif YS_USE_TEXT_ANIMATOR_3
        // in TA3 we have a custom typewriter so don't need to worry about the registration
        // so we can just say it's registered and otherwise move on with our life
        internal static RegistrationResult DefaultActionIsRegistered()
        {
            return RegistrationResult.Registered;
        }
        internal static void RegisterDefaultAction()
        {
            return;
        }
#else
        internal static RegistrationResult DefaultActionIsRegistered() { return RegistrationResult.UnableToResolve; }
        internal static void RegisterDefaultAction() { return; }
#endif
    }
}
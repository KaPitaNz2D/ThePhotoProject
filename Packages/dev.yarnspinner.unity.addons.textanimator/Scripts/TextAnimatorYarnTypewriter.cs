/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#if YS_USE_TEXT_ANIMATOR_2 || YS_USE_TEXT_ANIMATOR_3
#define USE_TEXT_ANIMATOR
#endif

#nullable enable

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
    using System.Threading;
    using System.Collections.Generic;
    using UnityEngine;
    using Yarn.Unity;
    using Yarn.Markup;
#if UNITY_EDITOR
    using UnityEditor;
    using Yarn.Unity.Editor;
#endif
#if !USE_TEXT_ANIMATOR
    using TextAnimator_TMP = UnityEngine.Object;
    using TypewriterCore = UnityEngine.Object;
#endif

    public enum TagMode
    {
        YarnOnlyTags, TextAnimatorOnlyTags, DefaultYarnMarkup,
    }

#if !USE_TEXT_ANIMATOR
    // This only exists if Text Animator isn't installed
    // in which case it doesn't really do anything except make sure the serialised references aren't lost
    // this means it also has the references for both versions of Text Animator
    public class TextAnimatorYarnTypewriter : MonoBehaviour, IAsyncTypewriter, IAttributeMarkerProcessor
    {

#pragma warning disable CS8618
        [HideInInspector] public List<IActionMarkupHandler> ActionMarkupHandlers { get; set; }
#pragma warning restore CS8618

        public TextAnimator_TMP? Animator;

        public TypewriterCore? Typewriter;

        [SerializeField] internal TagMode tagMode = TagMode.DefaultYarnMarkup;

        public ActionMarkupAction? insertionAction;

#pragma warning disable CS8618
        [SerializeField] private List<string> actionTags;
#pragma warning restore CS8618
        public bool ActionMarkupFinishedProcessing => false;

        public YarnTask RunTypewriter(MarkupParseResult line, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public void PrepareForContent(MarkupParseResult line)
        {
            throw new System.NotImplementedException();
        }

        public void ContentWillDismiss()
        {
            throw new System.NotImplementedException();
        }

        public ReplacementMarkerResult ProcessReplacementMarker(MarkupAttribute marker, System.Text.StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
        {
            throw new System.NotImplementedException();
        }
        public void CancelMarkupProcessing()
        {
            throw new System.NotImplementedException();
        }
        public void StartMarkup(int index)
        {
            throw new System.NotImplementedException();
        }

        public void ContentDidDismiss()
        {
            throw new System.NotImplementedException();
        }

        public TMPro.TMP_Text? TextElement { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    }
#endif


}
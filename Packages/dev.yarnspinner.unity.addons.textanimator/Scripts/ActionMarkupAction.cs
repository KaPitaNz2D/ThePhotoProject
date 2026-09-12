/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#nullable enable
namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
    using System.Collections;
    using UnityEngine;
#if YS_USE_TEXT_ANIMATOR_2
    using Febucci.UI.Core;
    using Febucci.UI.Core.Parsing;
    using Febucci.UI.Actions;

    [System.Serializable]
    public class ActionMarkupAction : ActionScriptableBase
    {
        private TextAnimatorYarnTypewriter? ytw;
        public override IEnumerator DoAction(ActionMarker action, TypewriterCore typewriter, TypingInfo typingInfo)
        {
            if (ytw == null)
            {
                ytw = typewriter.GetComponentInParent<TextAnimatorYarnTypewriter>();
            }

            if (ytw != null)
            {
                ytw.StartMarkup(action.index);

                while (!ytw.ActionMarkupFinishedProcessing)
                {
                    yield return null;
                }
            }
        }
    }
#else
    using ActionScriptableBase = UnityEngine.ScriptableObject;
    public class ActionMarkupAction : ActionScriptableBase
    {
#pragma warning disable CS8618
        public string TagID => tagID;
        public string tagID;
#pragma warning restore CS8618
    }
#endif
}
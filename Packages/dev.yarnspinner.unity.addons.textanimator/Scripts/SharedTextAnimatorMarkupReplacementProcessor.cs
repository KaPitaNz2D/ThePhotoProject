/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
#nullable enable
    using System.Text;
    using System.Collections.Generic;
    using UnityEngine;
    using Yarn.Unity;
    using Yarn.Markup;

    /// <summary>
    /// This object exists to allow multiple Text Animator contexts to exist simultaneously but still have Yarn markup be converted into Text Animator form.
    /// Without this object because each Typewriter does it themselves you get a collision of multiple objects attempting to provide replacements for the same markup.
    /// </summary>
    public class SharedTextAnimatorMarkupReplacementProcessor : MonoBehaviour, IAttributeMarkerProcessor
    {
#if YS_USE_TEXT_ANIMATOR_3
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            var runner = DialogueRunner.FindRunner(this);

            if (runner == null)
            {
                Debug.LogWarning("Was unable to find a dialogue runner to register the Text Animator replacement markup upon.");
                return;
            }
            foreach (var tag in TextAnimatorMarkupManager.AllTags())
            {
                runner.LineProvider.RegisterMarkerProcessor(tag, this);
            }
        }

        public ReplacementMarkerResult ProcessReplacementMarker(MarkupAttribute marker, StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
        {
            int invisibleCharacters = 0;

            if (TextAnimatorMarkupManager.ConvertedForm(marker, out var front, out var back))
            {
                childBuilder.Insert(0, front);
                invisibleCharacters = front.Length;
                if (marker.Length > 0)
                {
                    childBuilder.Append(back);
                    invisibleCharacters += back.Length;
                }
            }
            return new ReplacementMarkerResult(invisibleCharacters);
        }
#else
        public ReplacementMarkerResult ProcessReplacementMarker(MarkupAttribute marker, StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
        {
            throw new System.InvalidOperationException("This object cannot exist without Text Animator 3 being installed and correctly configured!");
        }
#endif
    }
}
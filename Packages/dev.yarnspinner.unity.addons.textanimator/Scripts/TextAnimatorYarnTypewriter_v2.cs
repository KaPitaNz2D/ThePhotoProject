/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#if YS_USE_TEXT_ANIMATOR_2 || YS_USE_TEXT_ANIMATOR_3
#define USE_TEXT_ANIMATOR
#endif

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using Yarn.Unity;
    using Yarn.Markup;
    using Yarn.Unity.Attributes;

#if YS_USE_TEXT_ANIMATOR_2
    using Febucci.UI;
    using Febucci.UI.Core;
#else
    using TextAnimator_TMP = UnityEngine.Object;
    using TypewriterCore = UnityEngine.Object;
#endif

#nullable enable

#if YS_USE_TEXT_ANIMATOR_2
    public class TextAnimatorYarnTypewriter : MonoBehaviour, IAsyncTypewriter, IAttributeMarkerProcessor
    {
        [HideInInspector] public List<IActionMarkupHandler> ActionMarkupHandlers { get; set; } = new();

        [Group("Text Animator")]
        public TextAnimator_TMP? Animator;

        [Group("Text Animator")]
        public TypewriterCore? Typewriter;

        [Group("Tags")]
        [SerializeField] public TagMode tagMode = TagMode.DefaultYarnMarkup;

        [Group("Tags")]
        [HideIf(nameof(tagMode), TagMode.TextAnimatorOnlyTags)]
        public ActionMarkupAction? insertionAction;

        [Group("Tags")]
        [ShowIf(nameof(tagMode), TagMode.DefaultYarnMarkup)]
        [SerializeField] private List<string> actionTags = new();
        private YarnTaskCompletionSource? typewriterComplete;
        private CancellationToken? currentContentCancellationToken;
        private MarkupParseResult? currentContent;
        private YarnTaskCompletionSource? markupHandlersProcessingCompletionSource;

        /// <summary>
        /// Indicates to the typewriter that it should prepare for displaying a new line.
        /// </summary>
        /// <param name="lineText">The line that will be shown.</param>
        public void PrepareForContent(MarkupParseResult lineText)
        {
            if (ActionMarkupHandlers == null)
            {
                return;
            }
            if (Animator == null)
            {
                return;
            }

            foreach (var handler in ActionMarkupHandlers)
            {
                handler.OnPrepareForLine(lineText, Animator.TMProComponent);
            }

            switch (tagMode)
            {
                case TagMode.TextAnimatorOnlyTags:
                    this.Animator.SetText(lineText.Text, hideText: true);
                    break;
                case TagMode.YarnOnlyTags:
                    {
                        var text = ProcessTagModeYarn(lineText);
                        if (text == null)
                        {
                            this.Animator.SetText(lineText.Text, hideText: true);
                        }
                        else
                        {
                            this.Animator.SetText(text, hideText: true);
                        }
                        break;
                    }
                case TagMode.DefaultYarnMarkup:
                    {
                        this.Animator.SetText(lineText.Text, hideText: true);
                        break;
                    }
            }
        }

        void Start()
        {
            var runner = DialogueRunner.FindRunner(this);
            if (runner != null)
            {
                if (tagMode == TagMode.TextAnimatorOnlyTags)
                {
                    return;
                }
                foreach (var tag in TextAnimatorMarkupManager.AllTags())
                {
                    runner.LineProvider.RegisterMarkerProcessor(tag, this);
                }

                if (tagMode == TagMode.DefaultYarnMarkup)
                {
                    HashSet<string> tags = new HashSet<string>(actionTags);
                    foreach (var tag in tags)
                    {
                        runner.LineProvider.RegisterMarkerProcessor(tag, this);
                    }
                }
            }
        }

        // This method can be thought of as a supersized replacement markup system
        // it adds a Text Animator action (defaults to <y>) at every character in the line
        // as it walks down the line inserting this potential pauses if it encounters markup that is a Text Animator tag it will instead convert this from Yarn Spinner markup to Text Animator tag form
        // and then continue
        private string? ProcessTagModeYarn(MarkupParseResult lineText)
        {
            if (insertionAction == null)
            {
                return null;
            }

            var insert = $"{TextAnimatorMarkupManager.OpeningActionsBracket}{insertionAction.TagID}{TextAnimatorMarkupManager.ClosingActionsBracket}";
            System.Text.StringBuilder stringBuilder = new();
            for (int i = 0; i < lineText.Text.Length; i++)
            {
                stringBuilder.Append(insert);
                foreach (var attribute in lineText.Attributes)
                {
                    if (TextAnimatorMarkupManager.IsTextAnimatorTag(attribute.Name))
                    {
                        if (attribute.Position == i || (attribute.Position + attribute.Length == i))
                        {
                            if (TextAnimatorMarkupManager.ConvertedForm(attribute, out var front, out var back))
                            {
                                // we are at the start
                                if (attribute.Position == i)
                                {
                                    stringBuilder.Append(front);
                                }
                                else
                                {
                                    stringBuilder.Append(back);
                                }
                            }
                        }
                    }
                }
                var character = lineText.Text[i];
                stringBuilder.Append(character);
            }
            return stringBuilder.ToString();
        }


        /// <summary>
        /// Indicates to the typewriter that the user interface elements necessary for
        /// showing the the line screen have finished appearing.
        /// </summary>
        /// <param name="text"></param>
        public void OnLineDisplayBegin(MarkupParseResult text)
        {
            if (ActionMarkupHandlers == null)
            {
                return;
            }
            if (Animator == null)
            {
                return;
            }

            foreach (var handler in ActionMarkupHandlers)
            {
                handler.OnLineDisplayBegin(text, Animator.TMProComponent);
            }
        }

        /// <summary>
        /// Indicates to the typewriter that the user interface elements
        /// necessary for showing the line are about to disappear.
        /// </summary>
        public void ContentWillDismiss()
        {
            if (ActionMarkupHandlers == null)
            {
                return;
            }
            foreach (var handler in ActionMarkupHandlers)
            {
                handler.OnLineWillDismiss();
            }
        }

        public void ContentDidDismiss()
        {
            Typewriter.SkipTypewriter();
        }
        public TMPro.TMP_Text? TextElement { get; set; }

        public async YarnTask RunTypewriter(MarkupParseResult line, CancellationToken cancellationToken)
        {
            if (ActionMarkupHandlers == null)
            {
                return;
            }
            if (Animator == null)
            {
                return;
            }
            if (Typewriter == null)
            {
                return;
            }

            currentContent = line;
            currentContentCancellationToken = cancellationToken;

            // Let every markup handler know that display is about to begin
            foreach (var markupHandler in ActionMarkupHandlers)
            {
                markupHandler.OnLineDisplayBegin(line, Animator.TMProComponent);
            }

            // Create a completion source that we'll complete when TextAnimator tells us
            // that the typewriter has finished
            typewriterComplete = new YarnTaskCompletionSource();

            var registration = cancellationToken.Register(() =>
            {
                // If our cancellation token is tripped, the user wants us to hurry
                // up our delivery. Tell TextAnimator to skip the rest of the
                // presentation.
                Typewriter.SkipTypewriter();
            });

            // We want to be notified when each character appears.
            Typewriter.onTextShowed.AddListener(OnTypewriterComplete);

            // Start the typewriter and wait for it to finish
            Typewriter.StartShowingText();
            await typewriterComplete.Task;

            // Clean up
            Typewriter.onTextShowed.RemoveListener(OnTypewriterComplete);
            registration.Dispose();
            typewriterComplete = null;
            currentContent = null;
            currentContentCancellationToken = null;

            // Notify our action markup handlers that the line is done
            foreach (var markupHandler in ActionMarkupHandlers)
            {
                markupHandler.OnLineDisplayComplete();
            }
        }

        private async YarnTask ProcessActionMarkup(int index, MarkupParseResult content, IReadOnlyList<IActionMarkupHandler> actionMarkupHandlers, CancellationToken token)
        {
            // should also link the token to markupHandlersProcessingCompletionSource
            foreach (var processor in actionMarkupHandlers)
            {
                await processor.OnCharacterWillAppear(index, content, token);
            }
            markupHandlersProcessingCompletionSource?.TrySetResult();
        }

        public void CancelMarkupProcessing()
        {
            markupHandlersProcessingCompletionSource?.TrySetCanceled();
        }

        private void OnTypewriterComplete()
        {
            // The TextAnimator typewriter has finished showing all of its text.
            // Notify that we're done.
            typewriterComplete?.TrySetResult();
        }

        public bool ActionMarkupFinishedProcessing
        {
            get
            {
                return markupHandlersProcessingCompletionSource == null || markupHandlersProcessingCompletionSource.Task.IsCompleted();
            }
        }

        public void StartMarkup(int index)
        {
            if (ActionMarkupHandlers == null || ActionMarkupHandlers.Count == 0)
            {
                return;
            }
            if (!currentContent.HasValue)
            {
                return;
            }

            markupHandlersProcessingCompletionSource = new YarnTaskCompletionSource();

            var token = this.currentContentCancellationToken ?? this.destroyCancellationToken;
            ProcessActionMarkup(index, currentContent.Value, ActionMarkupHandlers, token).Forget();
        }

        public ReplacementMarkerResult ProcessReplacementMarker(MarkupAttribute marker, System.Text.StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
        {
            int invisibleCharacters = 0;
            if (tagMode == TagMode.DefaultYarnMarkup)
            {
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
                else
                {
                    // here we are doing a bit of a hack, we are adding an action tag into the string at the point of the markup (<y> by default)
                    // and then adding the attribute back in, that way it can still be processed just fine at runtime using the <y> marker when hit by Text Animator
                    // in combination with the above which converts Yarn Spinner markup into Text Animator tags a line such as 
                    // Hello here is a line that [rainb]mixes and matches Yarn[pause=1000 /] and Text Animator[/rain] markup systems!
                    // would become:
                    // Hello here is a line that <rainb>mixes and matches Yarn<y> and Text Animator</rainb> markup systems!
                    // and when the <y> is hit by the Text Animator typewriter it will call out to our system which will forward onto pause

                    if (insertionAction != null)
                    {
                        childBuilder.Insert(0, $"{TextAnimatorMarkupManager.OpeningActionsBracket}{insertionAction.TagID}{TextAnimatorMarkupManager.ClosingActionsBracket}");
                    }
                    childAttributes.Add(marker);
                }
            }
            else
            {
                // we aren't in the minimal mode
                // so we don't want to swap out the content but have them just be normal markers
                childAttributes.Add(marker);
            }
            return new ReplacementMarkerResult(invisibleCharacters);
        }
    }
#elif YS_USE_TEXT_ANIMATOR_3
    public class NullTextAnimatorYarnTypewriter : MonoBehaviour { }
#endif
}
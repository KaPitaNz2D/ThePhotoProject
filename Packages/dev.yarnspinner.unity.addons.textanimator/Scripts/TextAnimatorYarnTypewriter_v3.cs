/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
#nullable enable
#if YS_USE_TEXT_ANIMATOR_3
    using UnityEngine;
    using Yarn.Unity;
    using Yarn.Markup;
    using Febucci.TextAnimatorCore.Typing;
    using Febucci.TextAnimatorCore.Text;
    using Febucci.TextAnimatorForUnity;
    using System.Threading;
    using System.Collections.Generic;
    using System.Text;

    public class TextAnimatorYarnTypewriter : TypewriterComponent, IAsyncTypewriter, IAttributeMarkerProcessor
    {
        public Febucci.TextAnimatorForUnity.TextMeshPro.TextAnimator_TMP? Animator;
        
        // IAttributeMarkerProcessor required elements 
        [HideInInspector] public List<IActionMarkupHandler> ActionMarkupHandlers { get; set; } = new();

        // holds the current line being presented, or null otherwise
        private MarkupParseResult? currentContent;
        // the current cancellation token given to us by the dialogue presenter
        // this will be cancelled if the line is declared as needing to hurry up or be skipped
        private CancellationToken currentContentPresentationCancellationToken = CancellationToken.None;
        // the completion source for the overall typewriting of the line
        private YarnTaskCompletionSource? currentContentPresentation;
        // the completion source for the overall typewriting of the current character being typewritten
        private YarnTaskCompletionSource? currentCharacterProgressSource;
        // the completion source for any markup processors (if necessary) for the current character
        private YarnTaskCompletionSource? currentCharacterMarkupProgress;
        // keeps track of the index of the last character we correctly finished processing
        private int lastProcessedCharacterIndex = -1;

        // this is a lot of different pieces the idea being that when a line comes in we will need to have a fresh currentContentPresentation created for that line
        // then for each character as they typewrite along down the line we will create a new currentCharacterProgressSource for that specific character
        // then for that character when they hit any markup we make a new currentCharacterMarkupProgress to hold that being done
        // once markup finishes currentCharacterMarkupProgress and currentCharacterProgressSource will get completed
        // which will make the typewriter advance to the next character
        // eventually then currentContentPresentation finishes up and that frees up the await call in RunTypewriter

        void Start()
        {
            // if we can find a SharedTextAnimatorMarkupReplacementProcessor we can safely assume that it is handling the processing of replacing the TA markup
            // otherwise it's on us to do it
            if (!GameObject.FindAnyObjectByType<SharedTextAnimatorMarkupReplacementProcessor>())
            {    
                var runner = DialogueRunner.FindRunner(this);
                if (runner != null)
                {
                    foreach (var tag in TextAnimatorMarkupManager.AllTags())
                    {
                        runner.LineProvider.RegisterMarkerProcessor(tag, this);
                    }
                }
                else
                {
                    Debug.LogWarning($"{name} is expecting to have to register itself as the object handling Text Animator markup but was unable to find a Dialogue Runner!");
                }
            }
        }

        public void PrepareForContent(MarkupParseResult line)
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
                handler.OnPrepareForLine(line, Animator.TMProComponent);
            }

            this.ShowText(line.Text);
        }
        
        public async YarnTask RunTypewriter(MarkupParseResult line, CancellationToken cancellationToken)
        {
            if (currentContentPresentation != null)
            {
                currentContentPresentation.TrySetCanceled();
            }
            if (Animator == null)
            {
                return;
            }

            // Let every markup handler know that display is about to begin
            foreach (var markupHandler in ActionMarkupHandlers)
            {
                markupHandler.OnLineDisplayBegin(line, Animator.TMProComponent);
            }

            currentContentPresentation = new YarnTaskCompletionSource();
            cancellationToken.Register(() =>
            {
                currentContentPresentation?.TrySetCanceled();
            });

            lastProcessedCharacterIndex = -1;
            currentCharacterProgressSource?.TrySetResult();
            currentCharacterProgressSource = null;
            currentContent = line;
            currentContentPresentationCancellationToken = cancellationToken;
            currentCharacterProgressSource = new();
            cancellationToken.Register(() =>
            {
                currentCharacterProgressSource?.TrySetCanceled();
            });
            this.StartShowingText();
            
            try
            {
                await currentContentPresentation.Task;
            }
            catch (System.OperationCanceledException)
            {
                this.SkipTypewriter();

                // run every markup from the last index we processed
                // this ensure that if there is any markup that wasn't hit due to the hurrying up it still gets a chance to do anything it NEEDS to do
                // because they are given a pre-cancelled token they will ideally finish instantly
                // we do this character by character instead of all at once to avoid a situation where one piece of markup changes something that impacts a later event
                // especially if it is changing poses or positions of game objects
                if (lastProcessedCharacterIndex != -1)
                {
                    for (int i = lastProcessedCharacterIndex; i < Animator.textWithoutAnyTag.Length; i++)
                    {
                        List<YarnTask> cancelledActions = new();
                        foreach (var processor in ActionMarkupHandlers)
                        {
                            cancelledActions.Add(processor.OnCharacterWillAppear(i, currentContent.Value, cancellationToken));
                        }
                        await YarnTask.WhenAll(cancelledActions);
                    }
                }
            }

            // Notify our action markup handlers that the line is done
            foreach (var markupHandler in ActionMarkupHandlers)
            {
                markupHandler.OnLineDisplayComplete();
            }

            // do final cleanup
            currentCharacterProgressSource?.TrySetResult();
            currentCharacterProgressSource = null;
            currentContentPresentationCancellationToken = CancellationToken.None;
            currentContentPresentation?.TrySetResult();
            currentContentPresentation = null;
            currentContent = null;
        }

        public void ContentWillDismiss()
        {
            if (ActionMarkupHandlers != null)
            {
                foreach (var handler in ActionMarkupHandlers)
                {
                    handler.OnLineWillDismiss();
                }
            }
            this.ShowText(string.Empty);
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
        
        protected override ActionStatus OnBeforeShowingCharacter(CharacterData character)
        {
            // I am not sure if I am triggering this correctly, it seems to want to typewrite on first frame
            // so for now I am just gonna tell it to chill out and wait until there is something to show
            if (currentContentPresentation == null)
            {
                return ActionStatus.Running;
            }

            if (currentCharacterProgressSource == null)
            {
                Debug.LogWarning($"Attempting to calculate delay for character {character.index} but {nameof(currentCharacterProgressSource)} is null, we cannot determine if we have paused for the right duration.");
                
                return base.OnBeforeShowingCharacter(character);
            }

            if (currentCharacterProgressSource.Task.IsCompleted())
            {
                return base.OnBeforeShowingCharacter(character);
            }

            // fire off the task
            RunActionMarkup(character.index).Forget();

            return ActionStatus.Running;
        }
        protected override ActionStatus OnAfterWaitingCharacter(CharacterData character)
        {
            // the character has been presented which means it is time to advance
            // so we flag this as done (if not already done) and refresh it
            currentCharacterProgressSource?.TrySetResult();
            currentCharacterProgressSource = new();
            currentContentPresentationCancellationToken.Register(() =>
            {
                currentCharacterProgressSource?.TrySetCanceled();
            });

            // need to check if this is the last character
            // if it is we also want to finish off the entire line presentation
            if (currentContentPresentation != null && Animator != null)
            {
                if (character.index == Animator.textWithoutAnyTag.Length -1)
                {
                    currentContentPresentation?.TrySetResult();
                }
            }
            lastProcessedCharacterIndex = character.index;

            return ActionStatus.Finished;
        }

        private async YarnTask RunActionMarkup(int index)
        {
            if (!currentContent.HasValue)
            {
                return;
            }

            if (currentCharacterProgressSource == null)
            {
                return;
            }

            // if we aren't null that means we have a task already in progress for presenting the markup
            // because this method might get called every frame we want to ensure that doesn't happen
            // so we capture and move over it
            if (currentCharacterMarkupProgress != null)
            {
                return;
            }
            currentCharacterMarkupProgress = new();
            currentContentPresentationCancellationToken.Register(() =>
            {
                currentCharacterMarkupProgress?.TrySetCanceled();
            });

            foreach (var processor in ActionMarkupHandlers)
            {
                await processor.OnCharacterWillAppear(index, currentContent.Value, currentContentPresentationCancellationToken);
            }
            currentCharacterProgressSource.TrySetResult();
            currentCharacterMarkupProgress.TrySetResult();
            currentCharacterMarkupProgress = null;
        }

        public void ContentDidDismiss()
        {
            this.ShowText(string.Empty);
        }

        public TMPro.TMP_Text? TextElement { get; set; }
    }
#elif YS_USE_TEXT_ANIMATOR_2
    public class NullTextAnimatorYarnTypewriter : UnityEngine.MonoBehaviour {}
#endif
}
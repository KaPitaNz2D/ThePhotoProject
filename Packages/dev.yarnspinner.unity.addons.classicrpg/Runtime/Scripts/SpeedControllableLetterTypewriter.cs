#nullable enable

namespace Yarn.Unity.Addons.ClassicRPG
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using TMPro;
    using Yarn.Markup;

    /// <summary>
    /// An implementation of <see cref="IAsyncTypewriter"/> that delivers
    /// characters one at a time, invokes any <see
    /// cref="IActionMarkupHandler"/>s along the way as needed, and dynamically
    /// adjusts its characters-per-second based on the presence of
    /// <c>[speed]</c> tags in the line.
    /// </summary>
    public class SpeedControllableLetterTypewriter : IAsyncTypewriter
    {
        /// <summary>
        /// The <see cref="TMP_Text"/> to display the text in.
        /// </summary>
        public TMP_Text? TextElement { get; set; }

        /// <summary>
        /// A collection of <see cref="IActionMarkupHandler"/> objects that
        /// should be invoked as needed during the typewriter's delivery in <see
        /// cref="RunTypewriter"/>, depending upon the contents of a line.
        /// </summary>
        public List<IActionMarkupHandler> ActionMarkupHandlers { get; set; } = new();

        /// <summary>
        /// The default number of characters per second to deliver.
        /// </summary>
        /// <remarks><para>If this value is zero, all characters are delivered
        /// at once, subject to any delays added by the markup handlers in <see
        /// cref="ActionMarkupHandlers"/>.</para><para>This value may be
        /// modified in the middle of a line by the <c>[speed]</c>
        /// tag.</para></remarks>
        public float CharactersPerSecond { get; set; } = 0f;

        public float SkipCharactersPerSecond { get; set; } = 0f;

        const string SpeedTag = "speed";

        private List<SpeedRange> speedRanges = new();

        private bool isSkippingContent = false;

        private struct SpeedRange
        {
            public float CharactersPerSecond;
            public int Start;

            public int Length;

            public SpeedRange(int start, int length, float charactersPerSecond)
            {
                Start = start;
                Length = length;
                CharactersPerSecond = charactersPerSecond;
            }
        }

        private double GetCurrentSecondsPerCharacter()
        {
            double baselineSecondsPerCharacter = 0;
            double targetCharactersPerSecond = isSkippingContent ? SkipCharactersPerSecond : CharactersPerSecond;

            if (targetCharactersPerSecond > 0)
            {
                baselineSecondsPerCharacter = 1.0 / targetCharactersPerSecond;
            }
            return baselineSecondsPerCharacter;
        }

        /// <inheritdoc/>
        public async YarnTask RunTypewriter(Markup.MarkupParseResult line, CancellationToken cancellationToken)
        {
            if (TextElement == null)
            {
                Debug.LogWarning($"Can't show text as typewriter, because {nameof(TextElement)} was not provided");
            }
            else
            {
                TextElement.maxVisibleCharacters = 0;
                TextElement.text = line.Text;

                // Let every markup handler know that display is about to begin
                foreach (var markupHandler in ActionMarkupHandlers)
                {
                    markupHandler.OnLineDisplayBegin(line, TextElement);
                }

                // Get the count of visible characters from TextMesh to exclude markup characters
                var visibleCharacterCount = TextElement.GetTextInfo(line.Text).characterCount;

                // Start with a full time budget so that we immediately show the first character
                double accumulatedDelay = GetCurrentSecondsPerCharacter();

                // Go through each character of the line and let the
                // processors know about it
                for (int i = 0; i < visibleCharacterCount; i++)
                {
                    var thisCharacterDuration = GetCurrentSecondsPerCharacter();

                    // Are we in a speed range? Update our secondsPerCharacter if so, but only if we're not skipping.
                    if (!isSkippingContent)
                    {

                        foreach (var speedRange in this.speedRanges)
                        {
                            if (i >= speedRange.Start && i <= speedRange.Start + speedRange.Length)
                            {
                                // We're in a speed range.
                                if (speedRange.CharactersPerSecond > 0)
                                {
                                    thisCharacterDuration = 1.0f / speedRange.CharactersPerSecond;
                                }
                                else
                                {
                                    thisCharacterDuration = 0;
                                }
                                break;
                            }
                        }
                    }

                    // If we don't already have enough accumulated time budget
                    // for a character, wait until we do (or until we're
                    // cancelled)
                    while (!cancellationToken.IsCancellationRequested
                        && (accumulatedDelay < thisCharacterDuration))
                    {
                        var timeBeforeYield = Time.timeAsDouble;
                        await YarnTask.Yield();
                        var timeAfterYield = Time.timeAsDouble;
                        accumulatedDelay += timeAfterYield - timeBeforeYield;
                    }

                    // Tell every markup handler that it is time to process the
                    // current character. If we're skipping, pass them an
                    // already cancelled token, so that they complete quickly.
                    var actionMarkupCancellation = isSkippingContent ? new CancellationToken(true) : cancellationToken;
                    foreach (var processor in ActionMarkupHandlers)
                    {
                        await processor
                            .OnCharacterWillAppear(i, line, actionMarkupCancellation)
                            .SuppressCancellationThrow();
                    }

                    TextElement.maxVisibleCharacters += 1;

                    accumulatedDelay -= thisCharacterDuration;
                }

                // We've finished showing every character (or we were
                // cancelled); ensure that everything is now visible.
                TextElement.maxVisibleCharacters = visibleCharacterCount;
            }

            // Let each markup handler know the line has finished displaying
            foreach (var markupHandler in ActionMarkupHandlers)
            {
                markupHandler.OnLineDisplayComplete();
            }
        }

        public void PrepareForContent(Markup.MarkupParseResult line)
        {
            if (TextElement == null)
            {
                return;
            }

            TextElement.maxVisibleCharacters = 0;
            TextElement.text = line.Text;
            isSkippingContent = false;

            foreach (var processor in ActionMarkupHandlers)
            {
                processor.OnPrepareForLine(line, TextElement);
            }

            speedRanges.Clear();

            // grabbing out any pauses and speed ranges inside the line
            foreach (var attribute in line.Attributes)
            {
                if (attribute.Name == SpeedTag)
                {
                    float charactersPerSecond;
                    if (attribute.Properties.TryGetValue(SpeedTag, out MarkupValue value))
                    {
                        // depending on the property value we need to take a different path this is because they have made it an integer or a float which are roughly the same.
                        // But they also might have done something weird and we need to handle that
                        switch (value.Type)
                        {
                            case MarkupValueType.Integer:
                                charactersPerSecond = value.IntegerValue;
                                break;
                            case MarkupValueType.Float:
                                charactersPerSecond = value.FloatValue;
                                break;
                            default:
                                Debug.LogWarning($"{SpeedTag} property in line \"{line.Text}\" is of type {value.Type}, which is not allowed. Defaulting to all-at-once.");
                                charactersPerSecond = 0;

                                break;
                        }
                    }
                    else
                    {
                        // No characters per second provided; default to 'all at once'.
                        charactersPerSecond = 0;
                    }

                    speedRanges.Add(new SpeedRange(attribute.Position, attribute.Length, charactersPerSecond));

                }
            }
        }

        public void ContentWillDismiss()
        {
            // we tell all action processors that the line is finished and is about to go away
            foreach (var processor in ActionMarkupHandlers)
            {
                processor.OnLineWillDismiss();
            }
        }

        /// <summary>
        /// Sets whether the typewriter is in 'skipping' mode, in which it
        /// displays its content quickly, makes action markup complete more
        /// quickly, and ignores speed tags.
        /// </summary>
        /// <param name="typewriterIsSkipping">Whether the typewriter is in
        /// skipping mode.</param>
        /// <remarks>This flag is reset to <see langword="false"/> when <see
        /// cref="PrepareForContent(MarkupParseResult)"/> is called.</remarks>
        internal void SetSkippingContent(bool typewriterIsSkipping)
        {
            isSkippingContent = typewriterIsSkipping;
        }

        public void ContentDidDismiss()
        {
            if (TextElement != null)
            {
                TextElement.maxVisibleCharacters = 0;
            }
        }
    }
}

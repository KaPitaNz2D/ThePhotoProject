using UnityEngine;
using Yarn.Unity.Attributes;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;


#nullable enable
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Yarn.Unity.Addons.ClassicRPG
{
    /// <summary>
    /// A dialogue presenter that shows text in a classic console style.
    /// </summary>
    public class RPGDialoguePresenter : DialoguePresenterBase
    {
        #region Tags
        /// <summary>
        /// A hashtag that marks the line as the final one (and makes the 'next'
        /// button appear as a square.)
        /// </summary>
        const string EndOfDialogueTag = "end";

        /// <summary>
        /// A hashtag prefix that marks the line has having an icon. The text
        /// after the colon is used to find a sprite in the Resources folder.
        /// </summary>
        const string IconTag = "icon:";

        /// <summary>
        /// A hashtag that indicates that the next piece of content will be
        /// options, and that the dialogue presenter should show the content in
        /// the options view.
        /// </summary>
        const string NextLineIsOptionsTag = "lastline";

        /// <summary>
        /// A hashtag that indicates that the line should be centered in the
        /// dialogue box.
        /// </summary>
        const string LineIsCenteredTag = "center";

        /// <summary>
        /// A hashtag that indicates that the line should not allow skipping,
        /// and should interrupt any skipping that is already taking place.
        /// </summary>
        const string DontAllowSkippingContentTag = "noskip";

        #endregion


        #region Structure
        /// <summary>
        /// The canvas group containing the dialogue box.
        /// </summary>
        /// <remarks>This is used for fading the box in.</remarks>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useAppearanceAnimation))] CanvasGroup canvasGroup;

        /// <summary>
        /// The object that contains the dialogue box.
        /// </summary>
        /// <remarks>This is used for controlling the box's visibility, and for
        /// animating its size.</remarks>
        [SerializeField, MustNotBeNull] Transform container;

        /// <summary>
        /// The <see cref="Letterbox"/>, which is activated when dialogue begins
        /// and deactivated when dialogue ends.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useLetterbox))] Letterbox letterbox;
        #endregion

        #region  Input
        [SerializeField, ShowIf(nameof(useSkipping))] UnityEngine.InputSystem.InputActionReference? skipAction;
        #endregion

        #region Style
        [Header("Style")]
        [SerializeField, MustNotBeNull, ShowIf(nameof(useBackgroundStyles))] Image backgroundImage;
        [SerializeField, Indent, ShowIf(nameof(useBackgroundStyles))] SerializableDictionary<string, Sprite> backgroundStyles = new();
        [SerializeField, Indent, ShowIf(nameof(useBackgroundStyles))] string defaultStyle = "normal";
        #endregion

        #region Timing
        /// <summary>
        /// The amount of time it takes for the dialogue box to appear.
        /// </summary>
        [Header("Timing")]
        [SerializeField, ShowIf(nameof(useAppearanceAnimation))] float appearanceTime = 0.75f;

        /// <summary>
        /// The default rate of characters per second to display.
        /// </summary>
        /// <remarks>This may be modified by any <c>[speed]</c> tags in the
        /// line.</remarks>
        [SerializeField] float defaultCharactersPerSecond = 30f;

        /// <summary>
        /// The rate of characters to display when skipping.
        /// </summary>
        [SerializeField, ShowIf(nameof(useSkipping))] float skipCharactersPerSecond = 200f;
        #endregion

        #region Lines

        /// <summary>
        /// The object containing the UI components for showing lines.
        /// </summary>
        [Header("Lines")]
        [SerializeField, MustNotBeNull] Transform lineComponents;
        /// <summary>
        /// The TextMeshPro object that displays the text of the line.
        /// </summary>
        [SerializeField, MustNotBeNull] TMPro.TMP_Text lineText;

        /// <summary>
        /// The <see cref="Image"/> that presents the line continuation sprite.
        /// </summary>
        [SerializeField, MustNotBeNull] Image lineCompleteImage;

        /// <summary>
        /// The sprite to show in <see cref="lineCompleteImage"/> when advancing
        /// the dialogue will show more dialogue.
        /// </summary>
        [SerializeField, MustNotBeNull] Sprite continueSprite;

        /// <summary>
        /// The sprite to show in <see cref="lineCompleteImage"/> when advancing
        /// the dialogue will close the dialogue.
        /// </summary>
        [SerializeField, MustNotBeNull] Sprite endDialogueSprite;
        #endregion

        #region Icons
        /// <summary>
        /// The object that contains the <see cref="iconImage"/>.
        /// </summary>
        [Header("Icon")]
        [SerializeField, MustNotBeNull, ShowIf(nameof(useIcons))] Transform iconContainer;

        /// <summary>
        /// The <see cref="Image"/> that will be used to show any icon
        /// associated with the line.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useIcons))] Image iconImage;
        #endregion 

        #region Options
        /// <summary>
        /// The object containing the UI components for showing options, along
        /// with lines that immediately precede options.
        /// </summary>
        [Header("Options")]
        [SerializeField, MustNotBeNull, ShowIf(nameof(useOptions))] Transform optionComponents;

        /// <summary>
        /// The TextMeshPro component that shows lines immediately preceding
        /// options.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useOptions))] TMPro.TMP_Text optionLineText;

        /// <summary>
        /// The object containing the list of available options.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useOptions))] Transform optionItemsContainer;

        /// <summary>
        /// The prefab to use for each option.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useOptions))] OptionItem itemPrefab;
        #endregion Options

        #region Action Button
        [Header("Action Button")]

        /// <summary>
        /// The Action button, which indicates what will happen when the
        /// 'interact' input action is performed.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useActionButton))] ActionButton actionButton;

        /// <summary>
        /// The text that should appear in the actions button when advancing the
        /// dialogue will show more dialogue.
        /// </summary>
        [SerializeField, ShowIf(nameof(useActionButton))] string NextLineLabel = "Next";

        /// <summary>
        /// The text that should appear in the actions button when advancing the
        /// dialogue will leave the dialogue.
        /// </summary>
        [SerializeField, ShowIf(nameof(useActionButton))] string LeaveConversationLabel = "Return";

        /// <summary>
        /// The text that should appear in the actions button when choosing
        /// between options.
        /// </summary>
        [SerializeField, ShowIf(nameof(useActionButton))] string SelectOptionLabel = "Decide";
        #endregion Action Button

        #region Audio
        /// <summary>
        /// The <see cref="AudioSource"/> to use to play UI sounds with.
        /// </summary>
        [Header("Audio")]
        [SerializeField, MustNotBeNull, ShowIf(nameof(useAudio))] AudioSource audioSource;

        /// <summary>
        /// The <see cref="AudioClip"/> to play when the player advances to the
        /// next line.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useAudio))] AudioClip nextLineSound;

        /// <summary>
        /// The <see cref="AudioClip"/> to play when the player reaches the end
        /// of the last line of the dialogue.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useAudio))] AudioClip endDialogueSound;

        /// <summary>
        /// The <see cref="AudioClip"/> to play when the player leaves the
        /// dialogue.
        /// </summary>
        [SerializeField, MustNotBeNull, ShowIf(nameof(useAudio))] AudioClip leaveDialogueSound;
        [SerializeField, MustNotBeNull, ShowIf(nameof(useAudio)), ShowIf(nameof(useOptions))] AudioClip changeOptionSelection;

        #endregion Audio

        #region Feature Flags

        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useAudio;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useOptions;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useIcons;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useAppearanceAnimation;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useActionButton;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useLetterbox;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useSkipping;
        [Group("Feature Flags"), SerializeField, ShowIf(nameof(ShowFeatureFlags))] bool useBackgroundStyles;
        #endregion

        public bool ShowFeatureFlags
        {
            get
            {
#if UNITY_EDITOR
                return Editor.DialogueViewEditorMenu.ShowFeatureFlags;
#else
                return false;
#endif
            }
        }

        #region Typewriter
        private SpeedControllableLetterTypewriter typewriter = new();
        public override IAsyncTypewriter? Typewriter => typewriter;

        #endregion

        private bool currentContentAllowsSkipping = true;
        private bool typewriterIsSkipping = false;

        private bool boxIsVisible = false;

        protected void Awake()
        {
            // When we start up, do some initial setup
            if (useAppearanceAnimation)
            {
                canvasGroup.alpha = 0f;
            }

            container.gameObject.SetActive(false);
            boxIsVisible = false;

            // Add a pause event processor to the typewriter so it knows how to
            // handle the [pause] tag
            typewriter.ActionMarkupHandlers.Add(new PauseEventProcessor());

            // Set the initial characters per second from what we were
            // configured with
            typewriter.CharactersPerSecond = defaultCharactersPerSecond;
            typewriter.SkipCharactersPerSecond = skipCharactersPerSecond;
        }

        #region Box Presentation

        [YarnCommand("set_dialogue_style")]
        public static void SetDialogueBoxStyle(string style)
        {
            foreach (var presenter in FindObjectsByType<RPGDialoguePresenter>(FindObjectsSortMode.None))
            {
                if (!presenter.useBackgroundStyles) { continue; }

                if (presenter.backgroundStyles.TryGetValue(style, out var sprite))
                {
                    presenter.backgroundImage.sprite = sprite;
                }
                else
                {
                    Debug.LogError($"Can't set dialogue box style to '{style}': not a valid style name (must be {string.Join(" or ", presenter.backgroundStyles.Keys)})");
                }
            }
        }

        [YarnCommand("hide_dialogue")]
        public static void HideDialogueBoxes()
        {
            foreach (var presenter in FindObjectsByType<RPGDialoguePresenter>(FindObjectsSortMode.None))
            {
                presenter.HideDialogueBox();
            }
        }

        private async YarnTask ShowDialogueBox()
        {
            // Start showing our container, but hide the line and
            // options inside it. We'll wait until we actually receive content
            // to show before those appear.
            container.gameObject.SetActive(true);
            lineComponents.gameObject.SetActive(false);
            if (useOptions)
            {
                optionComponents.gameObject.SetActive(false);
            }

            lineCompleteImage.gameObject.SetActive(false);

            if (boxIsVisible)
            {
                // Early out if the box is already visible.
                return;
            }
            boxIsVisible = true;

            if (useAppearanceAnimation)
            {
                // Dialogue appears with a scaling 'pop' animation while fading in
                var pop = Tween.Run(
                    Vector3.zero, // Start at zero
                    Vector3.one, // End at normal scale
                    appearanceTime, // Use our appearance time
                    Tween.EasingFunctions.OutBack, // Use an 'overshoot and return' easing function
                    (from, to, t) => container.localScale = Vector3.LerpUnclamped(from, to, t), // Affect scale
                    this.destroyCancellationToken // Cancel animation if this object is destroyed
                );

                var fade = Tween.Run(
                    0, // Start at zero
                    1, // End at full alpha
                    appearanceTime, // Use the same appearance time
                    Tween.EasingFunctions.Linear, // Use a linear easing funciton
                    (from, to, t) => canvasGroup.alpha = Mathf.Lerp(from, to, t), // Affect opacity
                    this.destroyCancellationToken // Cancel atnimation if this object is destroyed
                );

                // The animations are now started; wait for them to both finish
                await pop;
                await fade;
            }
        }

        private void HideDialogueBox()
        {
            // Dialogue disppears in a single frame, so hide it immediately
            container.gameObject.SetActive(false);

            boxIsVisible = false;

            // Stop any dialogue skipping in progress when we close the box.
            typewriterIsSkipping = false;
        }
        #endregion

        #region Dialogue Start and End

        public override YarnTask OnDialogueStartedAsync()
        {
            if (useLetterbox)
            {
                // Start showing the letterbox. Don't show the dialogue box yet -
                // we'll wait until we get dialogue content for that.
                letterbox.SetVisible(true).Forget();
            }

            if (useBackgroundStyles)
            {
                // Always start dialogue in the normal style
                if (backgroundStyles.TryGetValue(defaultStyle, out var sprite))
                {
                    backgroundImage.sprite = sprite;
                }
                else
                {
                    Debug.LogError($"Can't set background image to {defaultStyle}: no such style is defined");
                }
            }

            // Stop any skipping that may have been happening earlier.
            typewriterIsSkipping = false;

            // Enable and configure the skip action, if it's present.
            if (skipAction != null)
            {
                skipAction.action.Enable();
                skipAction.action.performed += OnSkipActionPerformed;
            }

            // We're all done - return synchronously.
            return YarnTask.CompletedTask;
        }


        public override YarnTask OnDialogueCompleteAsync()
        {
            HideDialogueBox();

            if (useLetterbox)
            {
                // Dismiss the letterbox (it'll animate away, but we don't need to
                // wait for that.)
                letterbox.SetVisible(false).Forget();
            }

            // Disable the skip action, if it's present.
            if (skipAction != null)
            {
                skipAction.action.Disable();
                skipAction.action.performed -= OnSkipActionPerformed;
            }

            if (useActionButton && actionButton != null)
            {
                actionButton.SetText("");
            }

            // We're all done - return synchronously.
            return YarnTask.CompletedTask;
        }
        #endregion

        #region Line Presentation
        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            // Ensure that the dialogue box is visible
            await ShowDialogueBox();

            bool isOptions = false;
            bool lineIsEnd = false;
            Sprite? icon = null;
            TMPro.HorizontalAlignmentOptions horizontalAlignment = TMPro.HorizontalAlignmentOptions.Left;
            currentContentAllowsSkipping = useSkipping;

            // Check for tags: is this the end of dialogue, a line before
            // options, or a line with an icon?
            foreach (var metadata in line.Metadata)
            {
                if (metadata == EndOfDialogueTag)
                {
                    // This is a dialogue-ending line.
                    lineIsEnd = true;
                }
                else if (metadata == NextLineIsOptionsTag && useOptions)
                {
                    // This is a line that appears right before options.
                    isOptions = true;

                    // Options interrupt text skipping, so stop it if that's
                    // happening.
                    currentContentAllowsSkipping = false;
                }
                else if (metadata == LineIsCenteredTag)
                {
                    // This line is horizontally centered in the dialogue box.
                    horizontalAlignment = TMPro.HorizontalAlignmentOptions.Center;
                }
                else if (metadata == DontAllowSkippingContentTag)
                {
                    currentContentAllowsSkipping = false;
                }
                else if (metadata.StartsWith(IconTag) && useIcons)
                {
                    // The line has an icon. Try to find a sprite with a
                    // matching name.
                    var spriteName = metadata[IconTag.Length..];
                    icon = Resources.Load<Sprite>(spriteName);
                    if (icon == null)
                    {
                        Debug.LogWarning($"Failed to find icon " + spriteName);
                    }
                }
            }

            // Cancel skipping if the line doesn't allow it.
            typewriterIsSkipping &= currentContentAllowsSkipping && useSkipping;

            // Give a warning if we've got redundant tags
            if (lineIsEnd && isOptions)
            {
                Debug.LogWarning($"Lines should not have both #{EndOfDialogueTag} and #{NextLineIsOptionsTag} tags");
            }

            // If this is a set of options, clean up the last set
            if (isOptions)
            {
                foreach (Transform item in optionItemsContainer)
                {
                    Destroy(item.gameObject);
                }
            }

            // Show either the line components or the option components
            lineComponents.gameObject.SetActive(!isOptions);

            if (useOptions)
            {
                optionComponents.gameObject.SetActive(isOptions);
            }

            // Hide the line complete image - we're just at the start of the
            // line (or if it's options, we don't need to show it at all)
            lineCompleteImage.gameObject.SetActive(false);

            // Get the text from our line
            var text = line.TextWithoutCharacterName.Text;

            // Select which text view we're using and give it the text
            var targetLineText = isOptions ? optionLineText : lineText;
            targetLineText.text = text;
            targetLineText.horizontalAlignment = horizontalAlignment;

            if (useIcons)
            {
                // Update our icon view as necessary
                iconContainer.gameObject.SetActive(icon != null);
                iconImage.sprite = icon;
            }

            // Select which sprite to use when showing the line
            lineCompleteImage.sprite = lineIsEnd ? endDialogueSprite : continueSprite;

            // Get ready to show our content
            typewriter.TextElement = targetLineText;
            typewriter.PrepareForContent(line.TextWithoutCharacterName);

            if (useActionButton)
            {
                // Show 'Next' in the action button while the line plays out
                actionButton.SetText(NextLineLabel);
            }

            // Tell the typewriter to skip, if we're already skipping.
            typewriter.SetSkippingContent(typewriterIsSkipping);

            // Run the line!
            await typewriter.RunTypewriter(line.TextWithoutCharacterName, token.HurryUpToken);

            if (!isOptions)
            {
                if (!typewriterIsSkipping || lineIsEnd)
                {
                    // We're not showing options. Show the continuation icon if
                    // we're not skipping, or this is an end line.
                    lineCompleteImage.gameObject.SetActive(true);
                }

                if (lineIsEnd)
                {
                    // We're at the end of the dialogue, so show 'Return' in the
                    // action button and play the 'end of dialogue' sound.
                    if (useActionButton)
                    {
                        actionButton.SetText(LeaveConversationLabel);
                    }

                    if (useAudio)
                    {
                        audioSource.PlayOneShot(endDialogueSound);
                    }

                    // If we reach the end of the dialogue, we're also done skipping.
                    typewriterIsSkipping = false;
                }

                if (!typewriterIsSkipping)
                {
                    // If we're not skipping, wait until the line has been
                    // dismissed.
                    await YarnTask.WaitUntilCanceled(token.NextContentToken);
                }

                // Signal to our typewriter that we're about to get rid of the
                // line.
                typewriter.ContentWillDismiss();

                if (useAudio)
                {
                    // Play an appropriate sound for advancing the dialogue.
                    if (lineIsEnd)
                    {
                        // We're leaving dialogue, so play the 'leaving dialogue' sound.
                        audioSource.PlayOneShot(leaveDialogueSound);
                    }
                    else if (!typewriterIsSkipping)
                    {
                        // We're advancing to the next line (and not skipping), so
                        // play the 'next line' sound.
                        audioSource.PlayOneShot(nextLineSound);
                    }
                }

                // Tidy up by dismissing our line components.
                lineCompleteImage.gameObject.SetActive(false);
                lineComponents.gameObject.SetActive(false);
            }
            else
            {
                // We're done showing the line that precedes options; leave it
                // on screen. Wait one frame to prevent any hurry-up input
                // actions from accidentally selecting content, and then report
                // that we're done so that options appear next. Don't notify the
                // typewriter that we're going to dismiss its content -
                // RunOptionsAsync will do that when an option is selected.
                await YarnTask.Yield();
            }
        }

        #endregion

        #region  Option Presentation
        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            if (useOptions == false)
            {
                return null;
            }

            // We're showing options. These will appear alongside the previous
            // line that ran before the options.

            // Get rid of the line complete icon - it doesn't appear when we're
            // showing options
            lineCompleteImage.gameObject.SetActive(false);

            // Show the option components
            optionComponents.gameObject.SetActive(true);

            // Get rid of any option items from the last time we did this
            foreach (Transform item in optionItemsContainer)
            {
                Destroy(item.gameObject);
            }

            // Create a completion source we can use to be notified that a
            // choice was made
            YarnTaskCompletionSource<DialogueOption> completion = new();

            // Start making our list of option items that will appear on screen
            int optionsAdded = 0;
            var optionsAndLines = new List<(Markup.MarkupParseResult lineText, OptionItem optionItem)>();
            for (int i = 0; i < dialogueOptions.Length; i++)
            {
                // Get the option
                DialogueOption? option = dialogueOptions[i];

                if (option.IsAvailable == false)
                {
                    // Don't show this option - it's unavailable
                    continue;
                }

                // Create the option item
                var item = Instantiate(itemPrefab, optionItemsContainer);
                item.Text = option.Line.TextWithoutCharacterName.Text;
                item.Selected = false;

                // If this is the first visible option, select it
                if (optionsAdded == 0)
                {
                    EventSystem.current.SetSelectedGameObject(item.gameObject);
                }

                // When the item is selected, set the result of our completion
                // source. This will signal that the choice has been made, and
                // we can continue.
                item.onSubmit = () => completion.TrySetResult(option);

                if (useAudio)
                {
                    // When the item is selected, play the 'selection changed'
                    // sound
                    item.onSelected = () => audioSource.PlayOneShot(changeOptionSelection);
                }

                optionsAndLines.Add((option.Line.TextWithoutCharacterName, item));

                item.Text = option.Line.TextWithoutCharacterName.Text;

                if (!typewriterIsSkipping)
                {
                    // If we're not skipping, set up each of the text views with
                    // the option item content, but hide it. (We do this so that
                    // the text is laid out correctly.)
                    item.TextView.maxVisibleCharacters = 0;
                }
                else
                {
                    // If we are skipping, then ensure that all of the text is
                    // immediately visible.
                    item.TextView.maxVisibleCharacters = int.MaxValue;
                }

                optionsAdded += 1;
            }

            // If we're  not skipping, reveal each of the options, one at a
            // time. (If we are skipping, the text is already visible.)
            if (!typewriterIsSkipping)
            {
                foreach (var (line, item) in optionsAndLines)
                {
                    typewriter.TextElement = item.TextView;
                    typewriter.SetSkippingContent(this.typewriterIsSkipping);
                    typewriter.PrepareForContent(line);
                    await typewriter.RunTypewriter(line, cancellationToken.HurryUpToken);
                    typewriter.ContentWillDismiss();
                }
            }

            if (useActionButton)
            {
                // Show the 'Decide' label in the action button
                actionButton.SetText(SelectOptionLabel);
            }

            // Cancel any skipping that might be running, because we need user
            // input to continue.
            typewriterIsSkipping = false;
            currentContentAllowsSkipping = false;

            // Wait until a choice has been made. We'll get back the option that
            // was chosen.
            var selection = await completion.Task;

            // The typewriter for the previous line will have displayed all of
            // the content, and left it in a 'all done, ready to be dismissed'
            // state. Notify that we're dismissing its contents now.
            typewriter.ContentWillDismiss();

            // We're no longer selecting anything
            EventSystem.current.SetSelectedGameObject(null);

            // Tidy up after ourselves by deleting the option items
            foreach (Transform item in optionItemsContainer)
            {
                Destroy(item.gameObject);
            }

            if (useAudio)
            {
                // We're moving on to the next line, so play the next line sound
                audioSource.PlayOneShot(nextLineSound);
            }

            // Hide the option components
            optionComponents.gameObject.SetActive(false);

            // Finally, return our selection back to the dialogue system
            return selection;
        }
        #endregion

        #region Line Skipping
        private void OnSkipActionPerformed(InputAction.CallbackContext context)
        {
            // If our current content allows us to start skipping, set the flag
            // and notify the typewriter.
            if (currentContentAllowsSkipping)
            {
                typewriterIsSkipping = true;
                typewriter.SetSkippingContent(true);
            }
        }

        #endregion
    }

#if UNITY_EDITOR
    namespace Editor
    {
        using UnityEditor;

        [CustomEditor(typeof(RPGDialoguePresenter))]
        public class DialogueViewEditor : Yarn.Unity.Editor.YarnEditor { }

        public readonly struct SessionBool
        {
            private readonly string Key;
            public readonly bool DefaultValue;
            public SessionBool(string key, bool defaultValue = false)
            {
                Key = key;
                DefaultValue = defaultValue;
            }

            public static implicit operator bool(SessionBool sessionBool)
            {
                return sessionBool.Value;
            }

            public readonly bool Value
            {
                get => SessionState.GetBool(Key, DefaultValue);
                set => SessionState.SetBool(Key, value);
            }
        }

        public static class DialogueViewEditorMenu
        {
            const string MenuFeatureFlags = "Window/Yarn Spinner/Classic RPG/Show Feature Flags";
            const string EditorStateFeatureFlags = "showFeatureFlags";

            internal static SessionBool ShowFeatureFlags = new(EditorStateFeatureFlags, true);

            // Add a menu item that you can click to add a check to the "MenuTest" menu item in MyMenu in the main menu. 
            [MenuItem(MenuFeatureFlags)]
            static void ToggleFeatureFlags()
            {
                ShowFeatureFlags.Value = !ShowFeatureFlags.Value;
                Menu.SetChecked(MenuFeatureFlags, ShowFeatureFlags);
            }
        }
    }

#endif
}
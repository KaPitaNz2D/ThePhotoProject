using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Yarn.Unity;

#nullable enable

namespace Yarn.Unity.Addons.Snaaake
{
    public class TalkingHeadDialoguePresenter : Yarn.Unity.DialoguePresenterBase
    {
        [Header("Audio")]
        /// <summary>
        /// The audio source to monitor.
        /// </summary>
        public AudioSource? audioSource;


        /// <summary>
        /// The channel in <see cref="audioSource"/> to monitor.
        /// </summary>
        public int sampleChannel = 0;



        /// <summary>
        /// A dictionary for mapping names to animators that present the characters,
        /// </summary>
        public Yarn.Unity.SerializableDictionary<string, TalkingHeadCharacterView> characterSlots = new();

        public Yarn.Unity.SerializableDictionary<string, TalkingHeadCharacter> characters = new();

        [Range(0f, 1f)]
        public float threshold = 0.5f;

        /// <summary>
        /// The current animator whose parameters we are changing.
        /// </summary>
        private TalkingHeadCharacterView? activeView = null;



        /// <summary>
        /// A buffer containing audio samples collected from <see
        /// cref="audioSource"/>. 
        /// </summary>
        /// <remarks>
        /// (This is kept in the class because we want to reuse the same buffer,
        /// rather than allocate every time we want to sample it.)
        /// </remarks>
        private float[] sampleBuffer = new float[512];

        /// <summary>
        /// The amount of time, in milliseconds, between samples of the audio source.
        /// </summary>
        /// <remarks>
        /// We don't need to be super precise with monitoring the audio source, so
        /// there's no need to measure every frame. Instead, we'll measure at a fix
        /// number of milliseconds between samples.
        /// </remarks>
        private readonly int timeBetweenSamplesMilliseconds = 50;

        /// <summary>
        /// The amount of time remaining until we next sample.
        /// </summary>
        private float timeUntilNextSample = 0;

        protected void Start()
        {
            if (audioSource)
            {
                // Unity creates the internal buffers and starts recording the first
                // time this is called, so call it immediately on start
                audioSource.GetOutputData(sampleBuffer, sampleChannel);
            }
        }

        protected void Update()
        {

            // Ensure that we have the components that we need
            if (audioSource == null || activeView == null)
            {
                return;
            }

            // Are we due for another audio source sample? If not, wait.
            if (timeUntilNextSample > 0)
            {
                timeUntilNextSample -= Time.deltaTime;
                return;
            }

            // Read sample data out of the audio source.

            audioSource.GetOutputData(sampleBuffer, sampleChannel);

            // Determine the highest peak level that we hit.
            var maxPower = 0f;

            for (int i = 0; i < sampleBuffer.Length; i++)
            {
                maxPower = Mathf.Max(Mathf.Abs(maxPower), sampleBuffer[i]);
            }

            // Set the parameter to whether or not we have a loud enough signal!
            activeView.Talking = maxPower > threshold;


            // Reset our clock.
            timeUntilNextSample = timeBetweenSamplesMilliseconds / 1000f;
        }

        /// <summary>
        /// Associates a character name with a slot.
        /// </summary>
        /// <param name="characterName">The character name to associate.</param>
        /// <param name="slotName">The name of the slot to associate the character
        /// with.</param>
        [YarnCommand("set_slot")]
        public static void SetCharacterToSlot(string characterName, string slotName)
        {
            var presenter = FindAnyObjectByType<TalkingHeadDialoguePresenter?>();
            if (presenter == null)
            {
                Debug.LogWarning($"Can't associate {characterName} with slot {slotName}: no {nameof(TalkingHeadDialoguePresenter)}");
                return;
            }

            if (presenter.characters.TryGetValue(characterName, out var characterData) == false)
            {
                Debug.LogWarning($"Can't associate {characterName} with slot {slotName}: no known character {characterName}");
                return;
            }

            if (presenter.characterSlots.TryGetValue(slotName, out var slot) == false)
            {
                Debug.LogWarning($"Can't associate {characterName} with slot {slotName}: no slot named {slotName}");
                return;
            }

            slot.CurrentCharacter = characterData;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            // Ensure that we have a character, a slot name for that character, and
            // an animator for that slot name.
            var characterName = line.CharacterName;
            if (characterName == null)
            {
                Debug.LogWarning($"Can't run talking animation for line {line.TextID} '{line.Text}': line has no character name");
                return;
            }

            if (characters.TryGetValue(characterName, out var character) == false)
            {
                Debug.LogWarning($"Can't run talking animation for line {line.TextID} '{line.Text}': {characterName} is not a known character");
                return;
            }

            TalkingHeadCharacterView? slot = null;

            // Find the slot that contains this character
            foreach (var s in characterSlots.Values)
            {
                if (s.CurrentCharacter == character)
                {
                    slot = s;
                    break;
                }
            }

            if (slot == null)
            {
                // We don't know which slot is showing this character
                Debug.LogWarning($"Can't run talking animation for line {line.TextID} '{line.Text}': {characterName} is not associated with a slot");
                return;
            }

            // The animator we're working with is this one we've arrived at
            activeView = slot;

            // Wait until the line is finished
            await YarnTask.WaitUntilCanceled(token.NextContentToken);

            if (slot != null)
            {
                // Turn off the animation just in case the audio level is currently
                // over the threshold
                slot.Talking = false;
            }

            // Tidy up
            activeView = null;
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            // Clear all characters from their slots
            foreach (var kv in characterSlots)
            {
                kv.Value.CurrentCharacter = null;
            }

            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            return YarnTask.CompletedTask;
        }
    }
}
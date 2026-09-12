using UnityEngine;

using TMPro;
using Yarn.Unity.Attributes;
using System.Threading;
using UnityEngine.UI;

#nullable enable
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Yarn.Unity.Addons.ClassicRPG
{

    /// <summary>
    /// Manages the presentation and animation of a UI element on screen that
    /// indicates to the player what will happen when the press the Interact
    /// button.
    /// </summary>
    public class ActionButton : MonoBehaviour
    {
        /// <summary>
        /// A TextMeshPro component that shows the text of the action button.
        /// </summary>
        [SerializeField, MustNotBeNull] TMP_Text label;

        /// <summary>
        /// The object to animate when the label changes.
        /// </summary>
        [SerializeField, MustNotBeNull] Transform pivot;

        /// <summary>
        /// The amount of time to take when animating the button.
        /// </summary>
        [SerializeField] float flipTime = 0.1f;

        /// <summary>
        /// A token source that we can use to cancel ongoing animations.
        /// </summary>
        CancellationTokenSource cancellationTokenSource = new();

        public void Awake()
        {
            label.text = string.Empty;
        }

        public void SetText(string? newText)
        {
            // Cancel any ongoing animation and set up for a new one
            cancellationTokenSource.Cancel();
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            // Perform the animation
            SetText(newText, cancellationTokenSource.Token);
        }

        private async void SetText(string? newText, CancellationToken cancellationToken)
        {
            newText ??= string.Empty;

            if (label.text == newText)
            {
                // No text change needed; we don't need to animate
                return;
            }

            try
            {
                // Flip away from the player
                await Tween.Run(
                    0, 90, // Flip to 90 degrees
                    flipTime / 2, // Taking half of our animation time
                    Tween.EasingFunctions.Linear, // Move linearly
                    (a, b, t) =>
                        // Adjust rotation around X over time
                        pivot.eulerAngles = new Vector3(Mathf.Lerp(a, b, t), 0, 0),
                    cancellationToken
                );

                // Update the label
                label.text = newText ?? string.Empty;

                // Flip back towards the player
                await Tween.Run(
                    -90, 0, // Now finish the flip, rotating back to the player
                    flipTime / 2,
                    Tween.EasingFunctions.Linear,
                    (a, b, t) =>
                          pivot.eulerAngles = new Vector3(Mathf.Lerp(a, b, t), 0, 0),
                    cancellationToken
                );
            }
            catch (System.OperationCanceledException)
            {
                // no-op
            }
        }
    }


#if UNITY_EDITOR
    namespace Editor
    {
        using UnityEditor;

        [CustomEditor(typeof(ActionButton))]
        public class ActionButtonEditor : Yarn.Unity.Editor.YarnEditor { }
    }

#endif
}
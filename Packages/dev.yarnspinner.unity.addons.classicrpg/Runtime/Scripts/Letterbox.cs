using System;
using System.Threading;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Attributes;
using Yarn.Unity.Samples;

#nullable enable
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Yarn.Unity.Addons.ClassicRPG
{

    [ExecuteAlways]
    public class Letterbox : MonoBehaviour
    {

        /// <summary>
        /// The top letterbox image.
        /// </summary>
        [SerializeField, MustNotBeNull] RectTransform top;

        /// <summary>
        /// The bottom letterbox image.
        /// </summary>
        [SerializeField, MustNotBeNull] RectTransform bottom;

        /// <summary>
        /// The height of each of the letter box images when the letterbox
        /// effect is active.
        /// </summary>
        [SerializeField] float size = 100f;

        /// <summary>
        /// The current percentage of the letterbox effect. 0 means fully
        /// hidden, 1 means fully visible.
        /// </summary>
        [Range(0, 1f)]
        [SerializeField] float factor;

        /// <summary>
        /// The time that the letterbox effect will take to appear.
        /// </summary>
        [Min(0)]
        [SerializeField] float time = 0.25f;

        CancellationTokenSource cancellationTokenSource = new();

        public void Awake()
        {
            if (Application.isPlaying)
            {
                // Reset any factor that may have been set in the editor
                UpdateAppearance(0);
            }
        }

        void OnValidate()
        {
            if (Application.isEditor && Application.isPlaying == false)
            {
                // If we've changed our value in the editor and we aren't
                // playing, immediately update the presentation style for
                // preview purposes.
                UpdateAppearance(factor);
            }
        }

        public async YarnTask SetVisible(bool visible)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            float target = visible ? 1 : 0;

            try
            {
                await Tween.Run(factor, target, time, Tween.EasingFunctions.Linear, (a, b, t) => UpdateAppearance(Mathf.Lerp(a, b, t)), cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation - we'll just stop the animation immediately.
            }
        }

        private void UpdateAppearance(float newFactor)
        {
            if (top == null || bottom == null) { return; }

            top.sizeDelta = new Vector2(0, Mathf.Lerp(0, size, newFactor));
            bottom.sizeDelta = new Vector2(0, Mathf.Lerp(0, size, newFactor));

            factor = newFactor;
        }
    }

#if UNITY_EDITOR
    namespace Editor
    {
        using UnityEditor;

        [CustomEditor(typeof(Letterbox))]
        public class LetterboxEditor : Yarn.Unity.Editor.YarnEditor { }
    }
#endif
}
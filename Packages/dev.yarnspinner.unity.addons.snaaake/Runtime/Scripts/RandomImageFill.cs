using UnityEngine;
using UnityEngine.UI;

#nullable enable
namespace Yarn.Unity.Addons.Snaaake
{
    /// <summary>
    /// Randomly varies the <see cref="Image.fillAmount"/> property of an <see
    /// cref="Image"/> over time, at a fixed frame rate.
    /// </summary>
    public class RandomImageFill : MonoBehaviour
    {
        /// <summary>
        /// The minimum amount to fill.
        /// </summary>
        public float MinFill = 0.5f;

        /// <summary>
        /// The maximum amount to fill.
        /// </summary>
        public float MaxFill = 1.0f;

        /// <summary>
        /// The degree to which the fill amount will change each frame.
        /// </summary>
        public float Speed = 1f;

        /// <summary>
        /// The rate at which the fill amount will change.
        /// </summary>
        public float FrameRate = 8f;

        /// <summary>
        /// The image to change the fill amount of.
        /// </summary>
        public Image? Image;

        void Update()
        {
            if (Image == null)
            {
                // No image, so nothing to update.
                return;
            }

            var t = Time.time;

            // Snap time to framerate
            t *= FrameRate;
            t = Mathf.FloorToInt(t);
            t /= FrameRate;

            // Increase speed
            t *= Speed;

            // Evaluate the noise at our calculated point and update the image's
            // fill amount
            var noise = Mathf.PerlinNoise1D(t);
            var fill = Mathf.Lerp(MinFill, MaxFill, noise);
            Image.fillAmount = fill;
        }
    }
}
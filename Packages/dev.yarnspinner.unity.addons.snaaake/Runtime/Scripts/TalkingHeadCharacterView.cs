using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

#nullable enable

namespace Yarn.Unity.Addons.Snaaake
{
    /// <summary>
    /// A view that presents an animated talking head in an <see cref="Image"/>,
    /// driven by an <see cref="Animator"/>.
    /// </summary>
    [ExecuteInEditMode]
    public class TalkingHeadCharacterView : MonoBehaviour
    {
        /// <summary>
        /// The <see cref="Image"/> to show the sprite in.
        /// </summary>
        [SerializeField] Image? image;

        /// <summary>
        /// The character that this view is presenting.
        /// </summary>
        private TalkingHeadCharacter? currentCharacter;

        [Min(0.001f)]
        public float FramesPerSecond = 4;

        public bool Talking { get; set; }

        void OnValidate()
        {
            if (this.image == null && TryGetComponent<Image>(out var image))
            {
                this.image = image;
            }
        }

        public TalkingHeadCharacter? CurrentCharacter
        {
            get => currentCharacter;
            set
            {
                currentCharacter = value;

                if (image == null)
                {
                    return;
                }

                if (currentCharacter != null && currentCharacter.sprites.Count > 0)
                {
                    image.sprite = currentCharacter.sprites[0];
                }
                else
                {
                    image.sprite = null;
                }
            }
        }

        private int _currentFrame = 0;
        private int CurrentFrame
        {
            get => _currentFrame;
            set
            {
                if (this.currentCharacter == null || this.currentCharacter.sprites.Count == 0)
                {
                    return;
                }

                _currentFrame = value;

                if (this.image == null)
                {
                    return;
                }

                // Ping-pong the actual index of the sprite to use based on our
                // frame count
                var i = PingPong(CurrentFrame, currentCharacter.sprites.Count - 1);
                var sprite = this.currentCharacter.sprites[i];

                this.image.sprite = sprite;
            }
        }

        private static int PingPong(int input, int max)
        {

            return Mathf.Abs(((input + max) % (max * 2)) - max);
        }

        void Awake()
        {
            RunAnimation().Forget();
        }

        public async YarnTask RunAnimation()
        {
            var destroyToken = this.destroyCancellationToken;
            while (destroyToken.IsCancellationRequested == false)
            {
                if (!Talking)
                {
                    await YarnTask.Yield();
                    continue;
                }

                // Start the talking animation. Stop when we're no longer
                // talking AND we've reached the end of a cycle.
                try
                {
                    do
                    {
                        if (currentCharacter == null || currentCharacter.sprites.Count == 0)
                        {
                            // No character to show; it must have changed.
                            // Stop looping.
                            break;
                        }

                        // Show the next frame, and wait until it's time to
                        // show the next.
                        CurrentFrame += 1;

                        await YarnTask.Delay(System.TimeSpan.FromSeconds(1.0f / FramesPerSecond), destroyToken);

                    } while (Talking || CurrentFrame % (2 * (currentCharacter.sprites.Count - 1)) != 0);
                }
                catch (System.OperationCanceledException)
                {
                    // We must have been destroyed, so stop immediately
                    return;
                }

            }
        }
    }
}
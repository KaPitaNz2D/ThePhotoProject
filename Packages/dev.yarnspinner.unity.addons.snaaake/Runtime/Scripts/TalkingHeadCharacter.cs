using System.Collections.Generic;
using UnityEngine;

namespace Yarn.Unity.Addons.Snaaake
{
    /// <summary>
    /// Stores a list of sprites associated with a character in the Snaaake
    /// dialogue presenter.
    /// </summary>
    [CreateAssetMenu(menuName = "Yarn Spinner/Snaaake/Talking Head Character")]
    public class TalkingHeadCharacter : ScriptableObject
    {
        /// <summary>
        /// The list of sprites for the character. The first sprite will be used
        /// as the 'idle' pose, when the character is not talking.
        /// </summary>
        public List<Sprite> sprites = new();
    }
}
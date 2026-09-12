#nullable enable

using System.Threading;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Samples;
using Unity.Cinemachine;
namespace Yarn.Unity.Addons.ClassicRPG
{
    /// <summary>
    /// Manages the state of the camera.
    /// </summary>
    [ExecuteAlways]
    public class DialogueCameraManager : MonoBehaviour
    {
        /// <summary>
        /// The camera to activate when the player enters conversation.
        /// </summary>
        [SerializeField] GameObject? conversationCamera;

        /// <summary>
        /// The player character's object.
        /// </summary>
        [SerializeField] Transform? player;

        /// <summary>
        /// A target group containing the player character and any character they're
        /// talking to.
        /// </summary>
        [SerializeField] CinemachineTargetGroup? targetGroup;

        public void Awake()
        {
            // When we start, we won't be in dialogue immediately, so ensure
            // that the conversation camera is off.
            if (conversationCamera != null)
            {
                conversationCamera.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Called when the player's current interactable changes (i.e. they
        /// walk near a person, or walk away from a person)
        /// </summary>
        /// <param name="interactable"></param>
        public void OnCurrentInteractableChanged(Interactable? interactable)
        {
            // Early out if we don't have a target group to work with
            if (targetGroup == null)
            {
                return;
            }

            if (interactable == null)
            {
                // The player has either walked away from the interactable, or
                // has started interacting with it. If they've walked away, the
                // conversation target group isn't being used (so changing it
                // doesn't matter), and if they're interacting, we shouldn't
                // remove the previous target from the list. Either way, we
                // shouldn't change anything.
                return;
            }

            // Clear the target group, and add in the character (if we have it)
            // and the current interactable (if we have it)
            targetGroup.Targets.Clear();

            if (player != null)
            {
                targetGroup.AddMember(player, 1f, 1f);
            }

            if (interactable != null)
            {
                targetGroup.AddMember(interactable.transform, 2f, 1f);
            }
        }

        public void OnDialogueStarted()
        {
            if (conversationCamera != null)
            {
                conversationCamera.gameObject.SetActive(true);

            }
        }

        public void OnDialogueComplete()
        {
            if (conversationCamera != null)
            {
                conversationCamera.gameObject.SetActive(false);
            }
        }

    }
}
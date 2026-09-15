using UnityEngine;
using Yarn.Unity.Samples;

#nullable enable

namespace Yarn.Unity.Addons.ClassicRPG
{
    public class UpdateActionButtonWhenInteractableChanges : MonoBehaviour
    {
        [SerializeField] ActionButton? actionButton;
        public void OnInteractableChanged(Interactable? interactable)
        {
            if (actionButton != null)
            {
                actionButton.SetText(interactable != null ? "Speak" : null);
            }
        }
    }
}
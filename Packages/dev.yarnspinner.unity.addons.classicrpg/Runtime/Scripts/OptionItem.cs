using UnityEngine;
using TMPro;
using Yarn.Unity.Attributes;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

#pragma warning disable CS8618

#nullable enable

namespace Yarn.Unity.Addons.ClassicRPG
{

    public class OptionItem : Selectable, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField, MustNotBeNull] GameObject selectionIcon;
        [SerializeField, MustNotBeNull] TMP_Text text;

        public Action? onSubmit = null;
        public Action? onSelected = null;

        public bool Selected { get => selectionIcon.activeInHierarchy; set => selectionIcon.SetActive(value); }
        public string Text { get => text.text; set => text.text = value; }

        public TMP_Text TextView => text;

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            this.Selected = false;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            this.Selected = true;
            onSelected?.Invoke();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            onSubmit?.Invoke();
        }
    }
}
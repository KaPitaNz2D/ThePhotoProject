using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Yarn;
using Yarn.Unity;

#nullable enable

namespace Yarn.Unity.Addons.Snaaake
{
    /// <summary>
    /// A basic dialogue presenter that shows the line text in a TextMeshPro
    /// text view.
    /// </summary>
    /// <remarks>
    /// This presenter waits until the line has been externally cancelled before
    /// completing. It expects to be used alongside other presenters that signal
    /// that the line has been cancelled, like <see cref="VoiceOverPresenter"/>.
    /// </remarks>
    public class SubtitlePresenter : DialoguePresenterBase
    {

        public TMPro.TMP_Text? text;

        void Awake()
        {
            if (text != null)
            {
                text.enabled = false;
            }
        }

        public override async YarnTask OnDialogueStartedAsync() { }

        public override async YarnTask OnDialogueCompleteAsync() { }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (text == null)
            {
                return;
            }
            text.enabled = true;

            text.text = line.TextWithoutCharacterName.Text;

            // Wait until the line has been ended by someone else
            await YarnTask.WaitUntilCanceled(token.NextContentToken);

            text.enabled = false;
        }
    }
}
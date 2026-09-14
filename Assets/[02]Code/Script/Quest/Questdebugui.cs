using System.Text;
using TMPro;
using UnityEngine;

public class QuestDebugUI : MonoBehaviour
{
    public JournalManager journalManager;
    public TMP_Text questListText;

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStatusChanged += HandleQuestStatusChanged;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStatusChanged -= HandleQuestStatusChanged;
        }
    }

    private void HandleQuestStatusChanged(string creatureId, QuestManager.QuestStatus status)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (questListText == null || journalManager == null) return;

        StringBuilder sb = new StringBuilder();
        foreach (JournalEntry entry in journalManager.allEntries)
        {
            if (entry == null) continue;
            if (QuestManager.Instance != null &&
                QuestManager.Instance.GetStatus(entry.creatureId) == QuestManager.QuestStatus.Active)
            {
                sb.AppendLine($"Capture \"{entry.displayName}\"");
            }
        }

        questListText.text = sb.ToString();
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถว Quest 1 แถวในหน้า Tasks ของ Journal — แถบสีแทน: ช่องติ๊ก (ติ๊กเมื่อ Complete) + ชื่อ Quest + ลูกศร
/// กดแถบเพื่อกาง/หุบรายละเอียด (ลูกศรชี้ขวา = หุบ, ชี้ลง = กาง)
/// JournalUI Clone แถวนี้จาก Template ที่ซ่อนไว้ในหน้า Tasks แล้วเรียก Setup() ทุกครั้งที่รีเฟรชหน้า
/// </summary>
public class JournalTaskRowUI : MonoBehaviour
{
    [Header("References")]
    public Button headerButton;
    [Tooltip("รูปติ๊กในช่อง Checkbox — เปิดเมื่อ Quest Complete แล้ว")]
    public GameObject checkMark;
    public TMP_Text titleText;
    [Tooltip("ลูกศรท้ายแถบ — หมุน 0 องศา = ชี้ลง (กาง), 90 องศา = ชี้ขวา (หุบ)")]
    public RectTransform chevron;
    [Tooltip("ส่วนรายละเอียดใต้แถบ — ซ่อนตอนหุบ")]
    public GameObject detailsRoot;
    public TMP_Text detailsText;

    private Action onToggle;

    private void Awake()
    {
        if (headerButton != null) headerButton.onClick.AddListener(() => onToggle?.Invoke());
    }

    public void Setup(string title, string details, bool completed, bool expanded, Action toggle)
    {
        onToggle = toggle;
        if (titleText != null) titleText.text = title;
        if (detailsText != null) detailsText.text = details;
        if (checkMark != null) checkMark.SetActive(completed);
        if (detailsRoot != null) detailsRoot.SetActive(expanded);
        if (chevron != null) chevron.localEulerAngles = new Vector3(0f, 0f, expanded ? 0f : 90f);
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// แปะไว้ที่ Prefab 1 ช่องในกริด 9 ช่องของ Journal
/// แสดงแค่ Silhouette (ภาพเงาดำ) เสมอ ไม่สนใจว่าปลดล็อกหรือยัง — แค่แจ้ง JournalUI ว่าช่องไหนถูกคลิก
/// การตัดสินใจว่าจะโชว์ข้อมูลจริงหรือ "???" เป็นหน้าที่ของหน้า Detail ใน JournalUI ทั้งหมด
/// </summary>
public class JournalGridSlotUI : MonoBehaviour, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("References")]
    public Image silhouetteImage;
    [Tooltip("กรอบไฮไลท์ตอนถูกเลือก (เมาส์/จอย) — Optional")]
    public GameObject highlightFrame;

    // Index ในลิสต์ที่กรองตามหมวดหมู่แล้ว (ไม่ใช่ index ของ allEntries ทั้งหมด) — JournalUI เป็นคนตั้งค่าให้
    private int entryIndex;
    private JournalUI owner;

    private void Start()
    {
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void Setup(int index, Sprite silhouette, JournalUI ui)
    {
        entryIndex = index;
        owner = ui;
        if (silhouetteImage != null)
        {
            silhouetteImage.sprite = silhouette;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.OnGridSlotClicked(entryIndex);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        owner?.OnGridSlotClicked(entryIndex);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (highlightFrame != null) highlightFrame.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }
}
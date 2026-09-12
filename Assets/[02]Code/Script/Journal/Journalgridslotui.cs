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
    [Tooltip("จุดแดงบอกว่า Entry นี้มี Quest ที่ยังไม่ Complete อยู่")]
    public GameObject questIndicator;

    // Index ในลิสต์ที่กรองตามหมวดหมู่แล้ว (ไม่ใช่ index ของ allEntries ทั้งหมด) — JournalUI เป็นคนตั้งค่าให้
    private int entryIndex;
    private JournalUI owner;

    private void Awake()
    {
        // ใช้ Awake() แทน Start() เพราะ Awake() ทำงานทันทีตอน Instantiate()
        // (ก่อนที่ JournalUI จะเรียก Setup()/SetQuestActive()) ต่างจาก Start() ที่หน่วงไปทำงานทีหลัง
        // ถ้า Reset ค่าใน Start() จะไปเขียนทับค่าที่ JournalUI เพิ่งตั้งไว้ทันทีหลัง Instantiate
        if (highlightFrame != null) highlightFrame.SetActive(false);
        if (questIndicator != null) questIndicator.SetActive(false);
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

    /// <summary>เปิด/ปิดจุดแดง — JournalUI เรียกหลัง Setup() ทุกครั้งที่สร้าง/รีเฟรชกริด</summary>
    public void SetQuestActive(bool active)
    {
        if (questIndicator != null) questIndicator.SetActive(active);
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
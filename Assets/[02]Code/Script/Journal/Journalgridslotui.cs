using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// แปะไว้ที่ช่อง 1 ช่องในกริด 3x3 ของหน้า Plants/Animals (ช่องสร้างไว้ตายตัว 9 ช่องในสมุด ไม่ Instantiate ใหม่)
/// แสดงแค่ Silhouette (ภาพเงาดำ) + ชื่อใต้รูปเสมอ ไม่สนใจว่าปลดล็อกหรือยัง — แค่แจ้ง JournalUI ว่าช่องไหนถูกคลิก
/// ช่องที่ไม่มี Entry (เกินจำนวนในหน้าสุดท้าย) เรียก SetEmpty() — ไม่มีรูป ไม่มีชื่อ กดไม่ได้
/// การตัดสินใจว่าจะโชว์ข้อมูลจริง (รูป/คำอธิบาย) หรือ "???" เป็นหน้าที่ของหน้า Detail ใน JournalUI ทั้งหมด
/// </summary>
public class JournalGridSlotUI : MonoBehaviour, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("References")]
    public Image silhouetteImage;
    [Tooltip("ชื่อสิ่งมีชีวิต/พืช โชว์ใต้รูปเสมอ ไม่ว่าจะปลดล็อกหรือยัง")]
    public TMP_Text nameText;
    [Tooltip("กรอบ/ป้ายชื่อใต้รูป — ซ่อนทั้งอันตอนช่องว่าง")]
    public GameObject namePill;
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

    public void Setup(int index, Sprite silhouette, string displayName, JournalUI ui)
    {
        entryIndex = index;
        owner = ui;
        if (silhouetteImage != null)
        {
            silhouetteImage.sprite = silhouette;
            silhouetteImage.enabled = silhouette != null; // ยังไม่มีรูปเงาก็โชว์แค่วงกลมพื้นหลัง ไม่โชว์สี่เหลี่ยมขาว
        }
        if (nameText != null)
        {
            nameText.text = displayName;
        }
        if (namePill != null) namePill.SetActive(true);
        SetHoverEnabled(true);
    }

    /// <summary>ช่องว่าง — ไม่มีรูป ไม่มีชื่อ และกดไม่ได้ (owner = null)</summary>
    public void SetEmpty()
    {
        owner = null;
        if (silhouetteImage != null) silhouetteImage.enabled = false;
        if (nameText != null) nameText.text = "";
        if (namePill != null) namePill.SetActive(false);
        SetQuestActive(false);
        SetHoverEnabled(false);
    }

    // ช่องว่างกดไม่ได้ -> ไม่นูนตอนเอาเมาส์วาง
    private void SetHoverEnabled(bool enabled)
    {
        UIHoverPop hover = GetComponent<UIHoverPop>();
        if (hover != null) hover.enabled = enabled;
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
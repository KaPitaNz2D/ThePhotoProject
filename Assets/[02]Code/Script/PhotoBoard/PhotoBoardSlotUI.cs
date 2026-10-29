using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ช่อง 1 ช่องในกริดคลิกของบอร์ด (World Space ทับ Quad ของบอร์ดพอดี)
/// ไม่วาดรูปเอง — รูปจริงโชว์บน Quad 3D ของ PhotoBoard ข้างหลังอยู่แล้ว (ความละเอียดเต็ม) ช่องนี้เป็นแค่จุดคลิก + กรอบไฮไลต์ + พื้นช่องว่าง
/// ไม่ตัดสินใจอะไรเอง แจ้ง PhotoBoardUI เมื่อถูกคลิก (เหมือน StorageThumbnailUI)
/// </summary>
public class PhotoBoardSlotUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
{
    [Header("References")]
    [Tooltip("Image พื้นของช่องนี้ (ตัวรับคลิก) — ช่องว่างโชว์เป็นสี่เหลี่ยมจางๆ ช่องที่มีรูปจะใสหมด ให้เห็นรูปบนบอร์ดข้างหลัง")]
    public Image background;
    public Color emptyColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("UI Elements")]
    [Tooltip("กรอบไฮไลต์ตอนเอาเมาส์ไปวาง/โฟกัสช่องนี้")]
    public GameObject highlightFrame;

    private int slotIndex;
    private PhotoBoardUI mainUI;

    private void Start()
    {
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void Setup(int index, bool hasPhoto, PhotoBoardUI ui)
    {
        slotIndex = index;
        mainUI = ui;
        // alpha 0 ยังรับคลิกได้ (raycastTarget ไม่ขึ้นกับสี)
        if (background != null) background.color = hasPhoto ? Color.clear : emptyColor;
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (mainUI != null) mainUI.OnSlotClicked(slotIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && mainUI != null)
        {
            mainUI.OnSlotClicked(slotIndex);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (highlightFrame != null) highlightFrame.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    // เมาส์ออก = เลิกเลือกด้วย ไม่งั้น Selection ค้างที่ช่องนี้ กรอบเลยโชว์ค้างทั้งที่ไม่ได้ชี้อยู่
    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    private void OnDisable()
    {
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }
}

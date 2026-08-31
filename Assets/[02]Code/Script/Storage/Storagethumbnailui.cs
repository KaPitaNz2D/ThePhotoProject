using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// แปะไว้ที่ Prefab รูปย่อย 1 ช่องใน Storage Grid
/// รับผิดชอบแค่ "แสดงภาพ" กับ "แจ้ง StorageUI เมื่อถูกคลิก" ไม่ตัดสินใจอะไรเอง
/// ตรรกะจริง (จะเปิดขยาย/จะลบ) ให้ StorageUI เป็นคนจัดการทั้งหมด
/// </summary>
public class StorageThumbnailUI : MonoBehaviour//, IPointerClickHandler 
    , ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [Header("References")]
    public Image thumbnailImage;

    [Header("UI Elements")]
    public GameObject highlightFrame;

    // Index ของภาพนี้ใน PhotoStorage.Photos — StorageUI เป็นคนตั้งค่าให้ตอนสร้าง
    private int photoIndex;
    //private StorageUI owner;
    private StorageUI mainUI;

    private void Start()
    {
        // 1. แก้บั๊กกรอบค้าง: บังคับปิดกรอบเสมอเมื่อถูกสร้างขึ้นมา
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void Setup(int index, Sprite sprite, /*StorageUI storageUI*/ StorageUI ui)
    {
        photoIndex = index;
        //owner = storageUI;
        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = sprite;
        }
        mainUI = ui; // เก็บค่าอ้างอิงไว้ใช้สั่งเลื่อนจอ
    }

    //public void OnPointerClick(PointerEventData eventData)
    //{
    //    if (owner == null) return;

    //    if (eventData.button == PointerEventData.InputButton.Left)
    //    {
    //        owner.OnThumbnailClicked(photoIndex);
    //    }
    //    else if (eventData.button == PointerEventData.InputButton.Right)
    //    {
    //        owner.OnThumbnailRightClicked(photoIndex);
    //    }
    //}

    // เมื่อใช้จอย/คีย์บอร์ด เลื่อนมาตกที่ปุ่มนี้
    public void OnSelect(BaseEventData eventData)
    {
        if (highlightFrame != null) highlightFrame.SetActive(true);

        // 3. แจ้งให้ Scroll View เลื่อนจอตามลงมา
        if (mainUI != null)
        {
            mainUI.SnapToTarget(GetComponent<RectTransform>());
        }
    }
    public void OnDeselect(BaseEventData eventData)
    {
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 2. รวบระบบเมาส์และจอยเข้าด้วยกัน: เมื่อเมาส์ชี้ ให้บอก EventSystem ว่า "เลือกรูปนี้"
        // EventSystem จะไปสั่ง OnSelect ตัวนี้ และไปสั่ง OnDeselect รูปเก่าให้อัตโนมัติ
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    // เมื่อใช้เมาส์ เลื่อนออกไป
    public void OnPointerExit(PointerEventData eventData)
    {
        // เช็คว่าถึงเมาส์จะออกไป แต่จอยยังเลือกปุ่มนี้อยู่ไหม ถ้าใช่ก็อย่าเพิ่งปิดกรอบ
        if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            if (highlightFrame != null) highlightFrame.SetActive(false);
        }
    }
}
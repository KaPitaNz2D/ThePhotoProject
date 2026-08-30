using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// แปะไว้ที่ Prefab รูปย่อย 1 ช่องใน Storage Grid
/// รับผิดชอบแค่ "แสดงภาพ" กับ "แจ้ง StorageUI เมื่อถูกคลิก" ไม่ตัดสินใจอะไรเอง
/// ตรรกะจริง (จะเปิดขยาย/จะลบ) ให้ StorageUI เป็นคนจัดการทั้งหมด
/// </summary>
public class StorageThumbnailUI : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public Image thumbnailImage;

    // Index ของภาพนี้ใน PhotoStorage.Photos — StorageUI เป็นคนตั้งค่าให้ตอนสร้าง
    private int photoIndex;
    private StorageUI owner;

    public void Setup(int index, Sprite sprite, StorageUI storageUI)
    {
        photoIndex = index;
        owner = storageUI;
        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = sprite;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner.OnThumbnailClicked(photoIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.OnThumbnailRightClicked(photoIndex);
        }
    }
}
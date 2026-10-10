using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ตัวจัดการหลักของ UI คลังภาพ (Storage) — แยกออกจาก Journal โดยสิ้นเชิงตามที่ต้องการ
///
/// หน้าที่:
///   1) รับปุ่ม Toggle (I) เปิด/ปิด Panel พร้อมสลับ SystemState.Storage (บล็อกการเดินไปด้วยในตัว)
///   2) สร้าง Thumbnail Grid จาก PhotoStorage.Photos ทุกครั้งที่เปิด Panel หรือ Storage เปลี่ยน
///   3) จัดการคลิกซ้าย (ขยายเต็มจอ/ย่อกลับ) และคลิกขวา (เปิด Popup ยืนยันลบ)
///
/// Grid โชว์ "รูปย่อ" จาก PhotoStorage (สร้างไว้แล้วตอนถ่าย ไม่ต้องอ่านไฟล์) เปิดหน้านี้เลยไม่กระตุก
/// ส่วนภาพเต็มโหลดจากไฟล์บนดิสก์เฉพาะตอนกดขยายดูทีละภาพ และ Destroy ทิ้งทันทีที่ปิดภาพขยาย
/// เพื่อไม่ให้ค้าง RAM สอดคล้องกับหลักการออกแบบของ PhotoStorage
/// </summary>
public class StorageUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action แบบ Button สำหรับเปิด/ปิด Storage (ปุ่ม I)")]
    public InputActionReference toggleStorageInput;

    [Tooltip("Action สำหรับกดยกเลิกหรือออก (เช่น ปุ่ม Esc)")]
    public InputActionReference cancelInput; // 1. เพิ่มตัวแปรรับค่า Input

    [Header("Grid References")]
    public GameObject storagePanel;
    [Tooltip("Transform ของ Content ใน Scroll View ที่จะสร้าง Thumbnail ลงไป")]
    public Transform gridContent;
    [Tooltip("Prefab รูปย่อย 1 ช่อง ต้องมี StorageThumbnailUI ติดอยู่")]
    public GameObject thumbnailPrefab;
    public ScrollRect storageScrollRect;

    [Header("Fullscreen Viewer")]
    public GameObject fullscreenPanel;
    public Image fullscreenImage;

    [Header("Delete Confirm Popup")]
    public GameObject deleteConfirmPanel;
    public Button deleteConfirmYesButton;
    public Button deleteConfirmNoButton;

    // ภาพเต็มที่โหลดมาตอนกดขยาย (ทีละภาพ) — เป็นของ UI นี้ ต้อง Destroy เองตอนปิดภาพขยาย
    private Texture2D fullscreenTexture;
    private Sprite fullscreenSprite;
    private bool isFullscreenOpen;
    private int pendingDeleteIndex = -1;

    private void Start()
    {
        if (toggleStorageInput != null)
        {
            toggleStorageInput.action.Enable();
            toggleStorageInput.action.performed += OnToggleStorage;
        }

        if (cancelInput != null)
        {
            cancelInput.action.Enable();
            cancelInput.action.performed += OnCancelPressed;
        }

        if (deleteConfirmYesButton != null) deleteConfirmYesButton.onClick.AddListener(ConfirmDelete);
        if (deleteConfirmNoButton != null) deleteConfirmNoButton.onClick.AddListener(CancelDelete);

        if (storagePanel != null) storagePanel.SetActive(false);
        if (fullscreenPanel != null) fullscreenPanel.SetActive(false);
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (toggleStorageInput != null)
        {
            toggleStorageInput.action.performed -= OnToggleStorage;
        }

        if (cancelInput != null)
        {
            cancelInput.action.performed -= OnCancelPressed;
        }
    }

    private void OnCancelPressed(InputAction.CallbackContext ctx)
    {
        // ถ้าระบบไม่ได้อยู่ในสถานะ Storage ให้ข้ามไปเลย จะได้ไม่ไปทับซ้อนกับระบบอื่น
        if (StateManager.Instance != null && !StateManager.Instance.IsSystemState(StateManager.SystemState.Storage))
            return;

        // ลำดับการปิด (จากในสุด ออกนอกสุด)
        if (deleteConfirmPanel != null && deleteConfirmPanel.activeSelf)
        {
            CancelDelete();
        }
        else if (isFullscreenOpen)
        {
            CloseFullscreen();
        }
        else if (storagePanel != null && storagePanel.activeSelf)
        {
            CloseStorage();
        }
    }

    // ==================== เปิด/ปิด Panel หลัก ====================
    private void OnToggleStorage(InputAction.CallbackContext ctx) => ToggleStorageUI();

    /// <summary>เรียกจากปุ่ม UI (Button.onClick) หรือ Input Action ก็ได้ — สลับเปิด/ปิด Storage</summary>
    public void ToggleStorageUI()
    {
        if (StateManager.Instance == null) return;

        StateManager.SystemState current = StateManager.Instance.CurrentSystemState;

        if (current == StateManager.SystemState.Storage)
        {
            CloseStorage();
        }
        else if (current == StateManager.SystemState.Normal)
        {
            // เปิด Storage ได้เฉพาะตอนอยู่ Normal เท่านั้น กันเปิดซ้อนตอน Talking/Pause/Journal/Photograph
            OpenStorage();
        }
    }

    private void OpenStorage()
    {
        StateManager.Instance.SetSystemState(StateManager.SystemState.Storage);
        if (storagePanel != null) storagePanel.SetActive(true);

        // ปลดล็อกและแสดงเมาส์
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshGrid();
    }

    private void CloseStorage()
    {
        // ปิด Fullscreen/Popup ที่อาจค้างอยู่ไปด้วย กันเปิด Storage รอบหน้าแล้วเจอ UI ค้าง
        CloseFullscreen();
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        pendingDeleteIndex = -1;

        ClearGrid();
        if (storagePanel != null) storagePanel.SetActive(false);
        StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);

        // ล็อกและซ่อนเมาส์กลับไปเหมือนเดิมเมื่อเข้าเกม
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ==================== สร้าง/ล้าง Grid ====================
    private void RefreshGrid()
    {
        ClearGrid();

        if (PhotoStorage.Instance == null || gridContent == null || thumbnailPrefab == null) return;

        GameObject firstThumbnail = null; // 1. สร้างตัวแปรมาเก็บรูปแรกสุด

        IReadOnlyList<PhotoStorage.StoredPhoto> photos = PhotoStorage.Instance.Photos;
        for (int i = 0; i < photos.Count; i++)
        {
            // รูปย่อที่ PhotoStorage เป็นเจ้าของ (ห้าม Destroy ที่นี่) — ปกติสร้างไว้แล้วตอนถ่าย ไม่มีการอ่านไฟล์
            Sprite sprite = PhotoStorage.Instance.GetThumbnailSprite(photos[i]);
            if (sprite == null) continue;

            GameObject thumbObj = Instantiate(thumbnailPrefab, gridContent);

            // 2. ถ้าเป็นรอบแรก (i == 0) ให้เก็บ Object นี้ไว้
            if (i == 0) firstThumbnail = thumbObj;

            StorageThumbnailUI thumb = thumbObj.GetComponent<StorageThumbnailUI>();
            if (thumb != null)
            {
                thumb.Setup(i, sprite, this);
            }
        }

        // 3. บังคับให้ EventSystem ของ Unity โฟกัสไปที่รูปแรกสุด เพื่อให้จอยสติ๊กเริ่มทำงานได้
        if (firstThumbnail != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(firstThumbnail);
        }
    }

    private void ClearGrid()
    {
        if (gridContent != null)
        {
            foreach (Transform child in gridContent)
            {
                Destroy(child.gameObject);
            }
        }

    }

    // Destroy ภาพเต็มที่โหลดไว้ตอนขยายดู ป้องกัน RAM ค้าง (ตามหลักการเดียวกับ PhotoStorage)
    private void ReleaseFullscreenImage()
    {
        if (fullscreenImage != null) fullscreenImage.sprite = null;
        if (fullscreenSprite != null) Destroy(fullscreenSprite);
        if (fullscreenTexture != null) Destroy(fullscreenTexture);
        fullscreenSprite = null;
        fullscreenTexture = null;
    }

    // ==================== คลิกซ้าย: ขยายเต็มจอ ====================
    public void OnThumbnailClicked(int index)
    {
        if (isFullscreenOpen)
        {
            // กดซ้ำตอนเปิดเต็มจอค้างอยู่ -> ปิดกลับ
            CloseFullscreen();
            return;
        }

        if (PhotoStorage.Instance == null) return;
        var photos = PhotoStorage.Instance.Photos;
        if (index < 0 || index >= photos.Count) return;

        // ภาพเต็มจริง โหลดจากไฟล์ทีละภาพเฉพาะตอนกดดู
        Texture2D texture = PhotoStorage.Instance.LoadPhotoTexture(photos[index]);
        if (texture == null) return;

        ReleaseFullscreenImage();
        fullscreenTexture = texture;
        fullscreenSprite = PhotoThumbnail.ToSprite(texture);

        if (fullscreenImage != null) fullscreenImage.sprite = fullscreenSprite;
        if (fullscreenPanel != null) fullscreenPanel.SetActive(true);
        isFullscreenOpen = true;
    }

    public void CloseFullscreen()
    {
        if (fullscreenPanel != null) fullscreenPanel.SetActive(false);
        isFullscreenOpen = false;
        ReleaseFullscreenImage();
    }

    // ==================== คลิกขวา: เปิด Popup ยืนยันลบ ====================
    public void OnThumbnailRightClicked(int index)
    {
        pendingDeleteIndex = index;
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(true);
    }

    private void ConfirmDelete()
    {
        if (pendingDeleteIndex >= 0 && PhotoStorage.Instance != null)
        {
            PhotoStorage.Instance.DeletePhoto(pendingDeleteIndex);
            RefreshGrid(); // Index ของภาพหลังจากนี้ขยับหมด ต้องสร้าง Grid ใหม่ทั้งชุด ไม่ลบทีละช่องเฉยๆ
        }

        pendingDeleteIndex = -1;
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    private void CancelDelete()
    {
        pendingDeleteIndex = -1;
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    // ==================== เลื่อน Scroll View อัตโนมัติ (แบบ Smart Scroll) ====================
    public void SnapToTarget(RectTransform targetRect)
    {
        if (storageScrollRect == null || storageScrollRect.viewport == null) return;

        Canvas.ForceUpdateCanvases();

        float contentHeight = storageScrollRect.content.rect.height;
        float viewportHeight = storageScrollRect.viewport.rect.height;

        // ถ้าของยังไม่ล้นจอ ก็ไม่ต้องเลื่อน
        if (contentHeight <= viewportHeight) return;

        // 1. ดึงตำแหน่ง Y ของรูป (หาก Pivot ของรูปเป็น 0.5 ค่านี้คือจุดศูนย์กลางของรูป)
        float posY = Mathf.Abs(targetRect.anchoredPosition.y);

        // 2. คำนวณหา "ขอบบนสุด" และ "ขอบล่างสุด" ที่แท้จริงของรูป โดยการหักลบค่า Pivot
        float itemTop = posY - (targetRect.rect.height * targetRect.pivot.y);
        float itemBottom = posY + (targetRect.rect.height * (1f - targetRect.pivot.y));

        // 3. เผื่อระยะขอบ (Margin) บน-ล่างเล็กน้อย เพื่อไม่ให้จอดันภาพไปชิดขอบจนอึดอัด
        float margin = 30f;
        itemTop -= margin;
        itemBottom += margin;

        // 4. หาตำแหน่งขอบบนและล่างของ "กรอบหน้าจอที่ผู้เล่นกำลังมองเห็นอยู่ (Viewport)"
        float maxPosition = contentHeight - viewportHeight;
        float currentScrollY = (1f - storageScrollRect.verticalNormalizedPosition) * maxPosition;
        float viewTop = currentScrollY;
        float viewBottom = currentScrollY + viewportHeight;

        float newScrollY = currentScrollY;

        // 5. เช็คว่าควรเลื่อนจอไหม?
        if (itemTop < viewTop)
        {
            // ถ้ารูปทะลุขอบบน -> ดึงจอกลับขึ้นไป
            newScrollY = itemTop;
        }
        else if (itemBottom > viewBottom)
        {
            // ถ้ารูปทะลุขอบล่าง -> ดึงจอลงมา
            newScrollY = itemBottom - viewportHeight;
        }
        else
        {
            // ถ้ารูปอยู่ในระยะที่มองเห็นครบถ้วนแล้ว ไม่ต้องขยับจอ
            return;
        }

        // ป้องกันค่าหลุดขอบ (ต่ำกว่า 0 หรือมากกว่าสุดจอ)
        newScrollY = Mathf.Clamp(newScrollY, 0, maxPosition);

        // แปลงค่ากลับเป็น 0 ถึง 1 ให้ Scroll View
        float normalizedY = 1f - (newScrollY / maxPosition);
        storageScrollRect.verticalNormalizedPosition = normalizedY;
    }
}
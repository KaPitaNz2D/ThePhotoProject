using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ตัวจัดการหลักของ UI คลังภาพ (Storage) — แยกออกจาก Journal โดยสิ้นเชิงตามที่ต้องการ
///
/// หน้าที่:
///   1) รับปุ่ม Toggle (I) เปิด/ปิด Panel พร้อมสลับ SystemState.Storage (บล็อกการเดินไปด้วยในตัว)
///   2) สร้าง Thumbnail Grid จาก PhotoStorage.Photos ทุกครั้งที่เปิด Panel หรือ Storage เปลี่ยน
///   3) จัดการคลิกซ้าย (ขยายเต็มจอ/ย่อกลับ) และคลิกขวา (เปิด Popup ยืนยันลบ)
///
/// โหลด Texture2D จาก Disk แค่ตอน Panel เปิดอยู่เท่านั้น และ Destroy ทิ้งทันทีตอนปิด Panel
/// เพื่อไม่ให้ค้าง RAM สอดคล้องกับหลักการออกแบบของ PhotoStorage
/// </summary>
public class StorageUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action แบบ Button สำหรับเปิด/ปิด Storage (ปุ่ม I)")]
    public InputActionReference toggleStorageInput;

    [Header("Grid References")]
    public GameObject storagePanel;
    [Tooltip("Transform ของ Content ใน Scroll View ที่จะสร้าง Thumbnail ลงไป")]
    public Transform gridContent;
    [Tooltip("Prefab รูปย่อย 1 ช่อง ต้องมี StorageThumbnailUI ติดอยู่")]
    public GameObject thumbnailPrefab;

    [Header("Fullscreen Viewer")]
    public GameObject fullscreenPanel;
    public Image fullscreenImage;

    [Header("Delete Confirm Popup")]
    public GameObject deleteConfirmPanel;
    public Button deleteConfirmYesButton;
    public Button deleteConfirmNoButton;

    // เก็บ Texture2D ที่โหลดมาทั้งหมดตอน Panel เปิดอยู่ ไว้ Destroy ทีเดียวตอนปิด
    private List<Texture2D> loadedTextures = new List<Texture2D>();
    private bool isFullscreenOpen;
    private int pendingDeleteIndex = -1;

    private void Start()
    {
        if (toggleStorageInput != null)
        {
            toggleStorageInput.action.Enable();
            toggleStorageInput.action.performed += OnToggleStorage;
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
    }

    // ==================== เปิด/ปิด Panel หลัก ====================
    private void OnToggleStorage(InputAction.CallbackContext ctx)
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
        RefreshGrid();
    }

    private void CloseStorage()
    {
        // ปิด Fullscreen/Popup ที่อาจค้างอยู่ไปด้วย กันเปิด Storage รอบหน้าแล้วเจอ UI ค้าง
        if (fullscreenPanel != null) fullscreenPanel.SetActive(false);
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        isFullscreenOpen = false;
        pendingDeleteIndex = -1;

        ClearGrid();
        if (storagePanel != null) storagePanel.SetActive(false);
        StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
    }

    // ==================== สร้าง/ล้าง Grid ====================
    private void RefreshGrid()
    {
        ClearGrid();

        if (PhotoStorage.Instance == null || gridContent == null || thumbnailPrefab == null) return;

        IReadOnlyList<PhotoStorage.StoredPhoto> photos = PhotoStorage.Instance.Photos;
        for (int i = 0; i < photos.Count; i++)
        {
            Texture2D texture = PhotoStorage.Instance.LoadPhotoTexture(photos[i]);
            if (texture == null) continue;

            loadedTextures.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            GameObject thumbObj = Instantiate(thumbnailPrefab, gridContent);
            StorageThumbnailUI thumb = thumbObj.GetComponent<StorageThumbnailUI>();
            if (thumb != null)
            {
                thumb.Setup(i, sprite, this);
            }
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

        // Destroy Texture2D ที่โหลดไว้ทั้งหมด ป้องกัน RAM ค้าง (ตามหลักการเดียวกับ PhotoStorage)
        foreach (Texture2D texture in loadedTextures)
        {
            if (texture != null) Destroy(texture);
        }
        loadedTextures.Clear();
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

        Texture2D texture = PhotoStorage.Instance.LoadPhotoTexture(photos[index]);
        if (texture == null) return;

        loadedTextures.Add(texture); // เก็บไว้ Destroy รวมตอนปิด Storage ทีเดียว
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

        if (fullscreenImage != null) fullscreenImage.sprite = sprite;
        if (fullscreenPanel != null) fullscreenPanel.SetActive(true);
        isFullscreenOpen = true;
    }

    private void CloseFullscreen()
    {
        if (fullscreenPanel != null) fullscreenPanel.SetActive(false);
        isFullscreenOpen = false;
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
}
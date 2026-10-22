using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้า UI เต็มจอของบอร์ดแขวนรูป (ตัวเดียวในฉาก ใช้ร่วมกันทุกบอร์ด — PhotoBoardInteractable เป็นคนสั่ง Open() พร้อมบอกว่าบอร์ดไหน)
///
/// 2 หน้าจอย่อย:
///   1) boardPanel  — กริดช่องแขวนรูป 5 ช่อง (PhotoBoardSlotUI) คลิกช่องว่าง = ไปหยิบรูปจาก Storage, คลิกช่องที่มีรูป = ดูขยาย
///   2) enlargedPanel — ดูรูปขยาย + ปุ่มลบรูป/เปลี่ยนรูป คลิกนอกปุ่มทั้งสอง (หรือ ESC) = กลับไป boardPanel
///
/// ESC/Cancel ที่ boardPanel (หน้านอกสุด) = ปิดบอร์ดทั้งหมด กลับ SystemState.Normal
/// </summary>
public class PhotoBoardUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action สำหรับกดยกเลิกหรือออก (เช่น ปุ่ม Esc) — ใช้ตัวเดียวกับ Storage/Journal ก็ได้")]
    public InputActionReference cancelInput;

    [Header("References")]
    [Tooltip("ถ้าไม่ใส่ไว้จะหาใน Scene ให้เอง — ใช้เปิด Storage ในโหมดเลือกภาพตอนกดช่องว่าง/ปุ่มเปลี่ยนรูป")]
    public StorageUI storageUI;

    [Header("Board Panel (กริด 5 ช่อง — World Space Canvas ย้ายไปแปะหน้าบอร์ดที่เปิดอยู่ตอน Open())")]
    [Tooltip("GameObject ที่มี Canvas แบบ World Space (หน่วย 1 = 1 มม. ที่ scale 0.001) ตำแหน่งช่องต้องตรงกับ Quad ของ PhotoBoard")]
    public GameObject boardPanel;
    [Tooltip("ตำแหน่ง local เทียบกับ PhotoBoard — z ติดลบเล็กน้อยให้อยู่หน้า Backing/Quad (ด้านที่มองเห็นคือ -Z)")]
    public Vector3 boardPanelLocalOffset = new Vector3(0f, 0f, -0.032f);
    public PhotoBoardSlotUI[] slotUIs;

    [Header("Enlarged Panel (ดูรูปขยาย)")]
    public GameObject enlargedPanel;
    public Image enlargedImage;
    public Button deleteButton;
    public Button changeButton;
    [Tooltip("ปุ่มโปร่งใสเต็มจอ อยู่หลังรูป/ปุ่มอื่น คลิกที่ไหนก็ตามที่ไม่ใช่ปุ่มลบ/เปลี่ยนรูป จะเรียกปุ่มนี้ (ปิดกลับไป boardPanel)")]
    public Button backgroundCloseButton;

    private PhotoBoard currentBoard;
    private int selectedSlotIndex = -1;
    private bool isEnlargedOpen;

    // ภาพเต็มที่โหลดมาตอนกดดูขยาย (ทีละภาพ) — ของ UI นี้ ต้อง Destroy เองตอนปิด (เหมือน StorageUI)
    private Texture2D enlargedTexture;
    private Sprite enlargedSprite;

    private void Start()
    {
        if (storageUI == null) storageUI = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);

        if (cancelInput != null)
        {
            cancelInput.action.Enable();
            cancelInput.action.performed += OnCancelPressed;
        }

        if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
        if (changeButton != null) changeButton.onClick.AddListener(OnChangeClicked);
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(CloseEnlarged);

        if (boardPanel != null) boardPanel.SetActive(false);
        if (enlargedPanel != null) enlargedPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (cancelInput != null) cancelInput.action.performed -= OnCancelPressed;
    }

    private void OnCancelPressed(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null || !StateManager.Instance.IsSystemState(StateManager.SystemState.Board))
            return;

        if (isEnlargedOpen) CloseEnlarged();
        else if (boardPanel != null && boardPanel.activeSelf) Close();
    }

    // ==================== เปิด/ปิดทั้งบอร์ด ====================
    /// <summary>บอร์ดตัวนี้เป็นตัวที่เปิดค้างอยู่ตอนนี้หรือเปล่า — ใช้ตัดสินว่ากด Interact ซ้ำควรเปิดหรือปิด</summary>
    public bool IsShowing(PhotoBoard board) => currentBoard == board;

    public void Open(PhotoBoard board)
    {
        if (StateManager.Instance == null || board == null) return;

        currentBoard = board;
        StateManager.Instance.SetSystemState(StateManager.SystemState.Board);
        if (CameraController.Instance != null) CameraController.Instance.FocusCamera(board.boardCam);

        if (boardPanel != null)
        {
            // กริดเป็นชุดเดียวใช้ร่วมกันทุกบอร์ด ย้ายไปแปะหน้าบอร์ดที่เพิ่งเปิด (ทุกบอร์ดมาจาก Prefab เดียวกัน เลย์เอาต์ช่องตรงกัน)
            Transform panel = boardPanel.transform;
            panel.SetParent(board.transform, false);
            panel.localPosition = boardPanelLocalOffset;
            panel.localRotation = Quaternion.identity;

            Canvas canvas = boardPanel.GetComponent<Canvas>();
            if (canvas != null) canvas.worldCamera = Camera.main;

            boardPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshSlots();
    }

    public void Close()
    {
        CloseEnlarged();
        if (boardPanel != null) boardPanel.SetActive(false);

        if (CameraController.Instance != null && currentBoard != null) CameraController.Instance.UnfocusCamera(currentBoard.boardCam);
        currentBoard = null;

        if (StateManager.Instance != null) StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ==================== กริด 5 ช่อง ====================
    private void RefreshSlots()
    {
        if (slotUIs == null || currentBoard == null) return;

        SyncSlotLayoutToBoard();

        // ล้าง Selection เก่า (เช่นช่องที่คลิกก่อนไปเปิด Storage) ไม่งั้นช่องนั้นค้างสถานะ "ถูกเลือก" ชี้เมาส์กลับมาก็ไม่ขึ้นกรอบ
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] == null) continue;

            slotUIs[i].Setup(i, currentBoard.GetSlotPhoto(i) != null, this);
        }
    }

    // ช่องในกริดคัดลอกตำแหน่ง/ขนาดจาก Quad บนบอร์ดจริงทุกครั้ง (1 หน่วย Canvas = 1 มม.) — ปรับ Quad ในบอร์ดอย่างเดียวพอ กริดตามเอง
    private void SyncSlotLayoutToBoard()
    {
        int count = Mathf.Min(slotUIs.Length, currentBoard.slots.Length);
        for (int i = 0; i < count; i++)
        {
            Renderer quad = currentBoard.slots[i].displayRenderer;
            if (slotUIs[i] == null || quad == null) continue;

            Transform q = quad.transform;
            RectTransform rt = (RectTransform)slotUIs[i].transform;
            rt.anchoredPosition = new Vector2(q.localPosition.x, q.localPosition.y) * 1000f;
            rt.sizeDelta = new Vector2(q.localScale.x, q.localScale.y) * 1000f;
            rt.localRotation = q.localRotation;
        }
    }

    /// <summary>เรียกจาก PhotoBoardSlotUI ตอนคลิกช่อง — มีรูปแล้ว = ดูขยาย, ยังว่าง = ไปหยิบรูปจาก Storage</summary>
    public void OnSlotClicked(int slotIndex)
    {
        if (currentBoard == null) return;

        PhotoStorage.StoredPhoto photo = currentBoard.GetSlotPhoto(slotIndex);
        if (photo != null) OpenEnlarged(slotIndex);
        else BeginPick(slotIndex);
    }

    // ==================== ดูรูปขยาย ====================
    private void OpenEnlarged(int slotIndex)
    {
        if (currentBoard == null || PhotoStorage.Instance == null) return;

        PhotoStorage.StoredPhoto photo = currentBoard.GetSlotPhoto(slotIndex);
        if (photo == null) return;

        Texture2D texture = PhotoStorage.Instance.LoadPhotoTexture(photo);
        if (texture == null) return;

        ReleaseEnlargedImage();
        selectedSlotIndex = slotIndex;
        enlargedTexture = texture;
        enlargedSprite = PhotoThumbnail.ToSprite(texture);

        if (enlargedImage != null) enlargedImage.sprite = enlargedSprite;
        if (enlargedPanel != null) enlargedPanel.SetActive(true);
        isEnlargedOpen = true;
    }

    private void CloseEnlarged()
    {
        if (enlargedPanel != null) enlargedPanel.SetActive(false);
        isEnlargedOpen = false;
        selectedSlotIndex = -1;
        ReleaseEnlargedImage();
    }

    private void ReleaseEnlargedImage()
    {
        if (enlargedImage != null) enlargedImage.sprite = null;
        if (enlargedSprite != null) Destroy(enlargedSprite);
        if (enlargedTexture != null) Destroy(enlargedTexture);
        enlargedSprite = null;
        enlargedTexture = null;
    }

    private void OnDeleteClicked()
    {
        if (selectedSlotIndex < 0 || currentBoard == null) return;

        currentBoard.RemovePhoto(selectedSlotIndex);
        CloseEnlarged();
        RefreshSlots();
    }

    private void OnChangeClicked()
    {
        if (selectedSlotIndex < 0) return;

        int slotIndex = selectedSlotIndex;
        CloseEnlarged(); // ปิดรูปขยายก่อน (boardPanel/SystemState.Board ยังค้างอยู่)
        BeginPick(slotIndex);
    }

    // ==================== หยิบรูปจาก Storage ====================
    private void BeginPick(int slotIndex)
    {
        if (storageUI == null || currentBoard == null) return;

        if (boardPanel != null) boardPanel.SetActive(false); // ซ่อนเมนูบอร์ดไว้ชั่วคราว ไม่ปิด SystemState.Board
        storageUI.OpenForPicking(photo => OnPicked(slotIndex, photo), StateManager.SystemState.Board);
    }

    private void OnPicked(int slotIndex, PhotoStorage.StoredPhoto photo)
    {
        if (boardPanel != null) boardPanel.SetActive(true);
        if (photo != null && currentBoard != null) currentBoard.PlacePhoto(slotIndex, photo);
        RefreshSlots();
    }
}

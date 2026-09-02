using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ตัวควบคุม UI สมุดบันทึกทั้งหมด แบ่งเป็น 3 ชั้น:
///   Category (เลือกพืช/สัตว์) -> Grid (กริด 9 ช่อง โชว์เงาดำเสมอ) -> Detail (ข้อมูล+รูปจริงถ้าปลดล็อกแล้ว)
///
/// ปุ่ม Toggle เปิด/ปิดทั้งชุด, ปุ่ม Cancel (Esc) ถอยกลับทีละชั้น — Pattern เดียวกับ StorageUI
/// </summary>
public class JournalUI : MonoBehaviour
{
    private const int ITEMS_PER_PAGE = 9;

    [Header("Data")]
    public JournalManager journalManager;

    [Header("Input")]
    [Tooltip("Action แบบ Button สำหรับเปิด/ปิด Journal")]
    public InputActionReference toggleJournalInput;
    [Tooltip("Action สำหรับถอยกลับทีละชั้น (เช่น ปุ่ม Esc) — ใช้ Action เดียวกับ StorageUI ได้เลย")]
    public InputActionReference cancelInput;

    [Header("Panels")]
    public GameObject journalRootPanel;
    public GameObject categoryPanel;
    public GameObject gridPanel;
    public GameObject detailPanel;

    [Header("Category Panel")]
    public Button plantCategoryButton;
    public Button animalCategoryButton;

    [Header("Grid Panel")]
    public Transform gridContent;
    [Tooltip("Prefab ช่อง 1 ช่อง ต้องมี JournalGridSlotUI ติดอยู่")]
    public GameObject gridSlotPrefab;
    public Button nextPageButton;
    public Button prevPageButton;
    public TMP_Text pageLabelText;

    [Header("Detail Panel")]
    public Image detailPhotoImage;
    public Image detailSilhouetteImage;
    public TMP_Text detailNameText;
    public TMP_Text detailDescriptionText;

    private enum JournalView { Category, Grid, Detail }
    private JournalView currentView;

    private JournalEntry.JournalCategory currentCategory;
    private List<JournalEntry> currentCategoryEntries = new List<JournalEntry>();
    private int currentPage;

    private Texture2D loadedDetailTexture;

    private void Start()
    {
        if (toggleJournalInput != null)
        {
            toggleJournalInput.action.Enable();
            toggleJournalInput.action.performed += OnToggleJournal;
        }
        if (cancelInput != null)
        {
            cancelInput.action.Enable();
            cancelInput.action.performed += OnCancelPressed;
        }

        if (plantCategoryButton != null)
            plantCategoryButton.onClick.AddListener(() => SelectCategory(JournalEntry.JournalCategory.Plant));
        if (animalCategoryButton != null)
            animalCategoryButton.onClick.AddListener(() => SelectCategory(JournalEntry.JournalCategory.Animal));
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
        if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);

        if (journalRootPanel != null) journalRootPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (toggleJournalInput != null) toggleJournalInput.action.performed -= OnToggleJournal;
        if (cancelInput != null) cancelInput.action.performed -= OnCancelPressed;
    }

    // ==================== เปิด/ปิด ====================
    private void OnToggleJournal(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null) return;
        StateManager.SystemState current = StateManager.Instance.CurrentSystemState;

        if (current == StateManager.SystemState.Journal)
        {
            CloseJournal();
        }
        else if (current == StateManager.SystemState.Normal)
        {
            // เปิดได้เฉพาะตอน Normal เท่านั้น กันเปิดซ้อนตอน Photograph/Storage/Talking/Pause
            OpenJournal();
        }
    }

    private void OpenJournal()
    {
        StateManager.Instance.SetSystemState(StateManager.SystemState.Journal);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (journalRootPanel != null) journalRootPanel.SetActive(true);
        ShowCategoryView();
    }

    private void CloseJournal()
    {
        ClearDetailTexture();
        ClearGrid();

        if (journalRootPanel != null) journalRootPanel.SetActive(false);
        StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ==================== Escape ถอยกลับทีละชั้น ====================
    private void OnCancelPressed(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null || !StateManager.Instance.IsSystemState(StateManager.SystemState.Journal)) return;

        switch (currentView)
        {
            case JournalView.Detail:
                ShowGridView();
                break;
            case JournalView.Grid:
                ShowCategoryView();
                break;
            case JournalView.Category:
                CloseJournal();
                break;
        }
    }

    // ==================== ชั้นที่ 1: Category ====================
    private void ShowCategoryView()
    {
        currentView = JournalView.Category;
        ClearDetailTexture();
        ClearGrid();

        if (categoryPanel != null) categoryPanel.SetActive(true);
        if (gridPanel != null) gridPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    private void SelectCategory(JournalEntry.JournalCategory category)
    {
        currentCategory = category;
        currentPage = 0;
        ShowGridView();
    }

    // ==================== ชั้นที่ 2: Grid (9 ช่องต่อหน้า) ====================
    private void ShowGridView()
    {
        currentView = JournalView.Grid;
        ClearDetailTexture();

        if (categoryPanel != null) categoryPanel.SetActive(false);
        if (gridPanel != null) gridPanel.SetActive(true);
        if (detailPanel != null) detailPanel.SetActive(false);

        if (journalManager != null)
        {
            currentCategoryEntries = journalManager.GetEntriesByCategory(currentCategory);
        }

        PopulateGridPage();
    }

    private void PopulateGridPage()
    {
        ClearGrid();
        if (gridContent == null || gridSlotPrefab == null) return;

        int startIndex = currentPage * ITEMS_PER_PAGE;
        int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, currentCategoryEntries.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            JournalEntry entry = currentCategoryEntries[i];
            GameObject slotObj = Instantiate(gridSlotPrefab, gridContent, false);
            JournalGridSlotUI slot = slotObj.GetComponent<JournalGridSlotUI>();
            if (slot != null)
            {
                slot.Setup(i, entry.silhouette, this);
            }
        }

        int totalPages = Mathf.Max(1, Mathf.CeilToInt(currentCategoryEntries.Count / (float)ITEMS_PER_PAGE));
        if (pageLabelText != null)
        {
            pageLabelText.text = $"{currentPage + 1} / {totalPages}";
        }
        if (prevPageButton != null) prevPageButton.interactable = currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = currentPage < totalPages - 1;
    }

    private void ClearGrid()
    {
        if (gridContent == null) return;
        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }
    }

    private void NextPage()
    {
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(currentCategoryEntries.Count / (float)ITEMS_PER_PAGE));
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            PopulateGridPage();
        }
    }

    private void PrevPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            PopulateGridPage();
        }
    }

    // ==================== ชั้นที่ 3: Detail ====================
    public void OnGridSlotClicked(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= currentCategoryEntries.Count) return;
        ShowDetailView(currentCategoryEntries[entryIndex]);
    }

    private void ShowDetailView(JournalEntry entry)
    {
        currentView = JournalView.Detail;

        if (categoryPanel != null) categoryPanel.SetActive(false);
        if (gridPanel != null) gridPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(true);

        ClearDetailTexture();

        bool unlocked = journalManager != null && journalManager.IsUnlocked(entry);

        if (unlocked)
        {
            loadedDetailTexture = journalManager.LoadPhotoForEntry(entry);

            if (detailNameText != null) detailNameText.text = entry.displayName;
            if (detailDescriptionText != null) detailDescriptionText.text = entry.description;
            if (detailSilhouetteImage != null) detailSilhouetteImage.gameObject.SetActive(false);

            if (detailPhotoImage != null)
            {
                detailPhotoImage.gameObject.SetActive(true);
                if (loadedDetailTexture != null)
                {
                    Sprite sprite = Sprite.Create(
                        loadedDetailTexture,
                        new Rect(0, 0, loadedDetailTexture.width, loadedDetailTexture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    detailPhotoImage.sprite = sprite;
                }
            }
        }
        else
        {
            // ยังไม่เคยถ่ายติด -> โชว์ "???" กับเงาดำแทน
            if (detailNameText != null) detailNameText.text = "???";
            if (detailDescriptionText != null) detailDescriptionText.text = "ยังไม่เคยถ่ายรูปสิ่งมีชีวิตนี้";
            if (detailPhotoImage != null) detailPhotoImage.gameObject.SetActive(false);

            if (detailSilhouetteImage != null)
            {
                detailSilhouetteImage.gameObject.SetActive(true);
                detailSilhouetteImage.sprite = entry.silhouette;
            }
        }
    }

    private void ClearDetailTexture()
    {
        if (loadedDetailTexture != null)
        {
            Destroy(loadedDetailTexture);
            loadedDetailTexture = null;
        }
    }
}
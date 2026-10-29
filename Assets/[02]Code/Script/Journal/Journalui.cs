using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ตัวควบคุม UI สมุดบันทึก (สมุดสันห่วง + แท็บสีด้านขวา) มี 5 หน้า:
///   Tasks (รายการ Quest) / Contents (ปุ่ม Plants, Animals) / Grid (กริด 3x3 ของ Plants หรือ Animals)
///   / Detail (รูป + ข้อมูลของ 1 Entry) / Change Photos (เลือกรูปจาก Storage มาเป็นรูปประจำ Entry)
///
/// ◀ (และ Esc) ย้อนขึ้นไปหน้า "แม่" ตามลำดับชั้นเสมอ ไม่ใช่ History ที่เคยเปิด — Tasks เป็นหน้าบนสุด:
///   Change Photos -> Detail -> Plants/Animals -> Contents -> Tasks -> ปิดสมุด
///   (หน้าที่มีหลายหน้าย่อย ◀ ย้อนหน้าย่อยก่อนจนถึงหน้าย่อยแรก แล้วค่อยขึ้นไปหน้าแม่)
/// ▶ ใช้พลิกไปหน้าย่อยถัดไปของ Grid / Change Photos เท่านั้น — หน้าอื่นซ่อนไว้ / แท็บ = กระโดดไปหน้านั้นเลย
/// เปิดครั้งแรกเจอหน้า Tasks หลังจากนั้นเปิดมาเจอหน้าล่าสุดที่เปิดค้างไว้
///
/// โครงสร้าง UI ทั้งหมดสร้างจากเมนู Tools > Photo Project > Rebuild Journal UI (JournalUIBuilder)
/// </summary>
public class JournalUI : MonoBehaviour
{
    private const int GRID_SLOTS_PER_PAGE = 9;

    [Header("Data")]
    public JournalManager journalManager;

    [Header("Input")]
    [Tooltip("Action แบบ Button สำหรับเปิด/ปิด Journal")]
    public InputActionReference toggleJournalInput;
    [Tooltip("Action สำหรับย้อนกลับ (เช่น ปุ่ม Esc) — ทำงานเหมือนปุ่ม ◀ ย้อนขึ้นจนถึง Tasks แล้วปิดสมุด")]
    public InputActionReference cancelInput;

    [Header("Book")]
    public GameObject journalRootPanel;
    public TMP_Text pageTitleText;
    public Button closeButton;
    [Tooltip("◀ มุมล่างซ้าย — ย้อนขึ้นหน้าแม่ (หน้า Tasks = ปิดสมุด)")]
    public Button backButton;
    [Tooltip("▶ มุมล่างขวา — หน้าย่อยถัดไปของ Grid / Change Photos (หน้าอื่นซ่อน)")]
    public Button forwardButton;

    [Header("Tabs")]
    public Button tasksTab;
    public Button contentsTab;
    public Button plantsTab;
    public Button animalsTab;

    [Header("Pages")]
    public GameObject tasksPage;
    public GameObject contentsPage;
    public GameObject gridPage;
    public GameObject detailPage;
    public GameObject changePhotoPage;

    [Header("Tasks Page")]
    public Transform taskListContent;
    [Tooltip("แถว Quest ต้นแบบ (ซ่อนไว้) — Clone ใหม่ทุกครั้งที่รีเฟรชหน้า")]
    public JournalTaskRowUI taskRowTemplate;
    [Tooltip("ข้อความตอนยังไม่มี Quest เลย")]
    public GameObject tasksEmptyText;

    [Header("Contents Page")]
    public Button contentsPlantsButton;
    public Button contentsAnimalsButton;

    [Header("Grid Page (3x3)")]
    public JournalGridSlotUI[] gridSlots = new JournalGridSlotUI[0];

    [Header("Detail Page")]
    public Image detailPhotoImage;
    public Image detailSilhouetteImage;
    [Tooltip("เครื่องหมาย ? โชว์ตอนยังไม่มีทั้งรูปถ่ายและรูปเงา")]
    public GameObject detailUnknownMark;
    [Tooltip("ปุ่มกลมข้างรูป — ไปหน้า Change Photos (กดได้เมื่อมีรูปของ Entry นี้ใน Storage)")]
    public Button changePhotoButton;
    [Tooltip("แถบชื่อ กดเพื่อกาง/หุบรายละเอียด")]
    public Button detailNameBarButton;
    public TMP_Text detailNameText;
    public RectTransform detailChevron;
    public GameObject detailBody;
    [Tooltip("ย่อหน้าแรก: สถานที่ — โชว์ตลอดตั้งแต่ยังไม่ถ่ายรูป")]
    public TMP_Text detailHabitatText;
    [Tooltip("ย่อหน้าที่สอง: ความรู้จาก NPC — โชว์เมื่อถ่ายรูปแล้วและ NPC เล่าเรื่องนี้ให้ฟังแล้ว")]
    public TMP_Text detailDescriptionText;
    [Tooltip("ระยะเวลาเฟดจากรูปเงาไปเป็นรูปที่ถ่ายจริง เล่นแค่ครั้งแรกที่เปิดดู Entry นั้นหลังปลดล็อก")]
    public float photoRevealFadeDuration = 0.6f;

    [Header("Change Photos Page (2x3)")]
    public Button[] changePhotoSlots = new Button[0];
    [Tooltip("รูปในแต่ละช่อง เรียงตรงกับ changePhotoSlots")]
    public Image[] changePhotoImages = new Image[0];

    private enum PageType { Tasks, Contents, Grid, Detail, ChangePhoto }

    /// <summary>หน้าที่เปิดอยู่ — Grid ใช้ animals + subPage, Detail ใช้ entry, ChangePhoto ใช้ entry + subPage</summary>
    private struct PageState
    {
        public PageType type;
        public bool animals;
        public JournalEntry entry;
        public int subPage;
    }

    private PageState currentPage = new PageState { type = PageType.Tasks };

    private readonly List<JournalTaskRowUI> spawnedTaskRows = new List<JournalTaskRowUI>();
    private readonly HashSet<string> expandedTaskIds = new HashSet<string>();
    private bool detailExpanded = true;

    private List<JournalEntry> gridEntries = new List<JournalEntry>();
    private List<PhotoStorage.StoredPhoto> changePhotoCandidates = new List<PhotoStorage.StoredPhoto>();

    private Coroutine photoRevealRoutine;
    private Sprite detailPhotoSprite; // Sprite ที่ห่อรูปย่อใน Detail (เป็นของ JournalUI เอง)

    // เก็บ creatureId ที่เคยเล่นเฟดเผยรูปไปแล้ว — เปิดดูซ้ำครั้งต่อไปโชว์รูปจริงทันทีไม่ต้องเฟดอีก
    private HashSet<string> revealedCreatureIds = new HashSet<string>();

    private bool IsOpen => journalRootPanel != null && journalRootPanel.activeSelf;

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

        if (closeButton != null) closeButton.onClick.AddListener(CloseJournal);
        if (backButton != null) backButton.onClick.AddListener(GoBack);
        if (forwardButton != null) forwardButton.onClick.AddListener(GoForward);

        if (tasksTab != null) tasksTab.onClick.AddListener(() => NavigateTo(new PageState { type = PageType.Tasks }));
        if (contentsTab != null) contentsTab.onClick.AddListener(() => NavigateTo(new PageState { type = PageType.Contents }));
        if (plantsTab != null) plantsTab.onClick.AddListener(() => NavigateTo(new PageState { type = PageType.Grid, animals = false }));
        if (animalsTab != null) animalsTab.onClick.AddListener(() => NavigateTo(new PageState { type = PageType.Grid, animals = true }));
        if (contentsPlantsButton != null) contentsPlantsButton.onClick.AddListener(() => NavigateTo(new PageState { type = PageType.Grid, animals = false }));
        if (contentsAnimalsButton != null) contentsAnimalsButton.onClick.AddListener(() => NavigateTo(new PageState { type = PageType.Grid, animals = true }));

        if (changePhotoButton != null) changePhotoButton.onClick.AddListener(() =>
            NavigateTo(new PageState { type = PageType.ChangePhoto, animals = currentPage.animals, entry = currentPage.entry }));
        if (detailNameBarButton != null) detailNameBarButton.onClick.AddListener(() =>
        {
            detailExpanded = !detailExpanded;
            RefreshDetailExpanded();
        });

        for (int i = 0; i < changePhotoSlots.Length; i++)
        {
            int slotIndex = i;
            if (changePhotoSlots[i] != null) changePhotoSlots[i].onClick.AddListener(() => OnChangePhotoSlotClicked(slotIndex));
        }

        if (taskRowTemplate != null) taskRowTemplate.gameObject.SetActive(false);

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStatusChanged += HandleQuestStatusChanged;
        }
        if (PhotoStorage.Instance != null)
        {
            PhotoStorage.Instance.OnStorageChanged += HandleStorageChanged;
        }

        if (journalRootPanel != null) journalRootPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (toggleJournalInput != null) toggleJournalInput.action.performed -= OnToggleJournal;
        if (cancelInput != null) cancelInput.action.performed -= OnCancelPressed;
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestStatusChanged -= HandleQuestStatusChanged;
        if (PhotoStorage.Instance != null) PhotoStorage.Instance.OnStorageChanged -= HandleStorageChanged;
        if (detailPhotoSprite != null) Destroy(detailPhotoSprite);
    }

    // รีเฟรชหน้าที่เปิดค้างอยู่ทันที เผื่อ Quest เปลี่ยนสถานะ / Storage เปลี่ยนระหว่างเปิด Journal ค้างไว้
    private void HandleQuestStatusChanged(string creatureId, QuestManager.QuestStatus status)
    {
        if (IsOpen) RenderCurrentPage();
    }

    private void HandleStorageChanged()
    {
        if (IsOpen && currentPage.type == PageType.ChangePhoto) RenderCurrentPage();
    }

    // ==================== เปิด/ปิด ====================
    private void OnToggleJournal(InputAction.CallbackContext ctx) => ToggleJournalUI();

    /// <summary>เรียกจากปุ่ม UI (Button.onClick) หรือ Input Action ก็ได้ — สลับเปิด/ปิด Journal</summary>
    public void ToggleJournalUI()
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
        // ไม่รีเซ็ตหน้า — เปิดมาเจอหน้าล่าสุดที่เปิดค้างไว้ (ครั้งแรกสุด currentPage เริ่มที่ Tasks)
        RenderCurrentPage();
    }

    private void CloseJournal()
    {
        if (StateManager.Instance == null || !StateManager.Instance.IsSystemState(StateManager.SystemState.Journal)) return;

        StopPhotoRevealFade();

        if (journalRootPanel != null) journalRootPanel.SetActive(false);
        StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnCancelPressed(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null || !StateManager.Instance.IsSystemState(StateManager.SystemState.Journal)) return;

        GoBack();
    }

    // ==================== นำทาง (◀ / ▶ / แท็บ) ====================
    private void NavigateTo(PageState page)
    {
        if (SamePage(page, currentPage)) return;

        currentPage = page;
        RenderCurrentPage();
    }

    /// <summary>◀ / Esc — ย้อนหน้าย่อยก่อน แล้วขึ้นไปหน้าแม่ตามลำดับชั้น อยู่หน้า Tasks แล้วกดอีก = ปิดสมุด</summary>
    private void GoBack()
    {
        if (currentPage.subPage > 0)
        {
            PageState previous = currentPage;
            previous.subPage--;
            NavigateTo(previous);
            return;
        }

        switch (currentPage.type)
        {
            case PageType.ChangePhoto:
                NavigateTo(new PageState { type = PageType.Detail, animals = currentPage.animals, entry = currentPage.entry });
                break;
            case PageType.Detail:
                NavigateTo(GridPageContaining(currentPage.entry, currentPage.animals));
                break;
            case PageType.Grid:
                NavigateTo(new PageState { type = PageType.Contents });
                break;
            case PageType.Contents:
                NavigateTo(new PageState { type = PageType.Tasks });
                break;
            default:
                CloseJournal();
                break;
        }
    }

    // ย้อนจาก Detail กลับไปหน้าย่อยของกริดที่มี Entry นั้นอยู่ (ไม่เด้งกลับหน้าแรกทุกครั้ง)
    private PageState GridPageContaining(JournalEntry entry, bool animals)
    {
        int index = journalManager != null ? journalManager.GetEntriesForSection(animals).IndexOf(entry) : -1;
        return new PageState { type = PageType.Grid, animals = animals, subPage = Mathf.Max(0, index) / GRID_SLOTS_PER_PAGE };
    }

    /// <summary>▶ — พลิกไปหน้าย่อยถัดไปของ Grid / Change Photos (หน้าอื่นไม่มีอะไรให้ไปต่อ)</summary>
    private void GoForward()
    {
        if (!HasNextSubPage()) return;

        PageState next = currentPage;
        next.subPage++;
        NavigateTo(next);
    }

    private bool HasNextSubPage()
    {
        switch (currentPage.type)
        {
            case PageType.Grid:
                return (currentPage.subPage + 1) * GRID_SLOTS_PER_PAGE < gridEntries.Count;
            case PageType.ChangePhoto:
                return (currentPage.subPage + 1) * changePhotoSlots.Length < changePhotoCandidates.Count;
            default:
                return false;
        }
    }

    private static bool SamePage(PageState a, PageState b)
    {
        return a.type == b.type && a.animals == b.animals && a.entry == b.entry && a.subPage == b.subPage;
    }

    // ==================== วาดหน้าปัจจุบัน ====================
    private void RenderCurrentPage()
    {
        StopPhotoRevealFade();

        // Entry ที่เคยเปิดดูอาจหายไปจาก allEntries ระหว่างทาง (เช่นแก้ Inspector ตอน Play) -> กลับไปหน้า Grid
        if ((currentPage.type == PageType.Detail || currentPage.type == PageType.ChangePhoto) && currentPage.entry == null)
        {
            currentPage = new PageState { type = PageType.Grid, animals = currentPage.animals };
        }

        if (tasksPage != null) tasksPage.SetActive(currentPage.type == PageType.Tasks);
        if (contentsPage != null) contentsPage.SetActive(currentPage.type == PageType.Contents);
        if (gridPage != null) gridPage.SetActive(currentPage.type == PageType.Grid);
        if (detailPage != null) detailPage.SetActive(currentPage.type == PageType.Detail);
        if (changePhotoPage != null) changePhotoPage.SetActive(currentPage.type == PageType.ChangePhoto);

        switch (currentPage.type)
        {
            case PageType.Tasks: RenderTasksPage(); break;
            case PageType.Contents: break;
            case PageType.Grid: RenderGridPage(); break;
            case PageType.Detail: RenderDetailPage(); break;
            case PageType.ChangePhoto: RenderChangePhotoPage(); break;
        }

        if (pageTitleText != null) pageTitleText.text = GetPageTitle();
        // ◀ โชว์ทุกหน้า (หน้า Tasks = ปิดสมุด) ส่วน ▶ โชว์เฉพาะตอนมีหน้าย่อยถัดไป
        if (backButton != null) backButton.gameObject.SetActive(true);
        if (forwardButton != null) forwardButton.gameObject.SetActive(HasNextSubPage());
    }

    private string GetPageTitle()
    {
        switch (currentPage.type)
        {
            case PageType.Tasks: return "Tasks";
            case PageType.Contents: return "Contents";
            case PageType.ChangePhoto: return "Change Photos";
            default: return currentPage.animals ? "Animals" : "Plants";
        }
    }

    // ==================== หน้า Tasks ====================
    private void RenderTasksPage()
    {
        foreach (JournalTaskRowUI row in spawnedTaskRows)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false); // Destroy() รอถึงท้ายเฟรม — ซ่อนก่อนกัน Layout นับแถวเก่าซ้อนกับแถวใหม่
            Destroy(row.gameObject);
        }
        spawnedTaskRows.Clear();

        int questCount = 0;
        if (journalManager != null && QuestManager.Instance != null && taskRowTemplate != null && taskListContent != null)
        {
            // Quest โผล่ก็ต่อเมื่อคุยกับ NPC แล้วรับ Quest (start_quest) — Complete แล้วยังอยู่แต่มีติ๊กในช่อง
            foreach (JournalEntry entry in journalManager.allEntries)
            {
                if (entry == null) continue;
                QuestManager.QuestStatus status = QuestManager.Instance.GetStatus(entry.creatureId);
                if (status == QuestManager.QuestStatus.NotStarted) continue;

                JournalTaskRowUI row = Instantiate(taskRowTemplate, taskListContent);
                row.gameObject.SetActive(true);
                string creatureId = entry.creatureId;
                row.Setup(entry.QuestTitleOrDefault, entry.QuestDetailsOrDefault,
                    status == QuestManager.QuestStatus.Completed,
                    expandedTaskIds.Contains(creatureId),
                    () => ToggleTaskExpanded(creatureId));
                spawnedTaskRows.Add(row);
                questCount++;
            }
        }

        if (tasksEmptyText != null) tasksEmptyText.SetActive(questCount == 0);
    }

    private void ToggleTaskExpanded(string creatureId)
    {
        if (!expandedTaskIds.Remove(creatureId)) expandedTaskIds.Add(creatureId);
        RenderTasksPage();
    }

    // ==================== หน้า Grid (Plants / Animals 3x3) ====================
    private void RenderGridPage()
    {
        gridEntries = journalManager != null
            ? journalManager.GetEntriesForSection(currentPage.animals)
            : new List<JournalEntry>();

        int maxSubPage = Mathf.Max(0, Mathf.CeilToInt(gridEntries.Count / (float)GRID_SLOTS_PER_PAGE) - 1);
        currentPage.subPage = Mathf.Clamp(currentPage.subPage, 0, maxSubPage);

        int startIndex = currentPage.subPage * GRID_SLOTS_PER_PAGE;
        for (int i = 0; i < gridSlots.Length; i++)
        {
            JournalGridSlotUI slot = gridSlots[i];
            if (slot == null) continue;

            int entryIndex = startIndex + i;
            if (entryIndex >= gridEntries.Count)
            {
                slot.SetEmpty();
                continue;
            }

            JournalEntry entry = gridEntries[entryIndex];
            slot.Setup(entryIndex, entry.silhouette, entry.displayName, this);
            bool questActive = QuestManager.Instance != null &&
                QuestManager.Instance.GetStatus(entry.creatureId) == QuestManager.QuestStatus.Active;
            slot.SetQuestActive(questActive);
        }
    }

    public void OnGridSlotClicked(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= gridEntries.Count) return;
        NavigateTo(new PageState { type = PageType.Detail, animals = currentPage.animals, entry = gridEntries[entryIndex] });
    }

    // ==================== หน้า Detail ====================
    private void RenderDetailPage()
    {
        JournalEntry entry = currentPage.entry;
        bool unlocked = journalManager != null && journalManager.IsUnlocked(entry);

        if (detailNameText != null) detailNameText.text = entry.displayName;

        // ย่อหน้าแรก: สถานที่ — โชว์ตลอด ไม่ว่าจะปลดล็อกหรือยัง เพราะผู้เล่นต้องรู้ก่อนถึงจะไปถ่ายได้
        if (detailHabitatText != null)
        {
            detailHabitatText.text = !string.IsNullOrEmpty(entry.habitatInfo)
                ? entry.habitatInfo
                : "No location data yet";
        }

        // ย่อหน้าที่สอง: โชว์ต่อเมื่อ "ถ่ายรูปแล้ว + คุยกับ NPC จนได้ฟังเรื่องนี้แล้ว" เท่านั้น
        // เหมือนได้ความรู้จาก NPC มาจดลงสมุดบันทึกเอง (ดู QuestManager.MarkInformed / mark_informed ใน Yarn)
        bool informed = unlocked && QuestManager.Instance != null && QuestManager.Instance.IsInformed(entry.creatureId);
        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = informed ? entry.description : "";
            detailDescriptionText.gameObject.SetActive(informed && !string.IsNullOrEmpty(entry.description));
        }
        RefreshDetailExpanded();

        // ปุ่มเปลี่ยนรูปกดได้ก็ต่อเมื่อมีรูปของ Entry นี้ค้างอยู่ใน Storage ให้เลือก
        if (changePhotoButton != null)
        {
            changePhotoButton.interactable = journalManager != null && journalManager.GetStoragePhotosFor(entry).Count > 0;
        }

        Texture2D photoTexture = unlocked ? journalManager.LoadPhotoForEntry(entry) : null;
        if (photoTexture == null)
        {
            // ยังไม่มีรูปถ่าย -> โชว์รูปเงา (ภาพ Sketch ในอนาคต) ถ้าไม่มีก็โชว์กรอบมืด + "?"
            ShowDetailImages(photo: false, silhouette: entry.silhouette != null);
            if (detailSilhouetteImage != null) detailSilhouetteImage.sprite = entry.silhouette;
            if (detailUnknownMark != null) detailUnknownMark.SetActive(entry.silhouette == null);
            return;
        }

        if (detailUnknownMark != null) detailUnknownMark.SetActive(false);

        // ยืม Texture2D มาจาก JournalManager (สำเนาของมันเอง) — ห้าม Destroy() Texture ตัวนี้
        // แต่ Sprite ที่ห่อเป็นของเรา สร้างใหม่ทุกครั้งเลยต้อง Destroy ตัวเก่า
        if (detailPhotoSprite != null) Destroy(detailPhotoSprite);
        detailPhotoSprite = PhotoThumbnail.ToSprite(photoTexture);
        if (detailPhotoImage != null) detailPhotoImage.sprite = detailPhotoSprite;

        if (revealedCreatureIds.Contains(entry.creatureId) || entry.silhouette == null)
        {
            // เคยเฟดให้ดูไปแล้ว (หรือไม่มีรูปเงาให้เฟดจาก) -> โชว์รูปจริงทันที
            revealedCreatureIds.Add(entry.creatureId);
            ShowDetailImages(photo: true, silhouette: false);
        }
        else
        {
            // เปิดดู Entry นี้เป็นครั้งแรกหลังปลดล็อก -> เฟดจากรูปเงาไปเป็นรูปที่ถ่ายจริง
            revealedCreatureIds.Add(entry.creatureId);
            if (detailSilhouetteImage != null) detailSilhouetteImage.sprite = entry.silhouette;
            photoRevealRoutine = StartCoroutine(RevealPhotoFade());
        }
    }

    private void ShowDetailImages(bool photo, bool silhouette)
    {
        if (detailPhotoImage != null)
        {
            SetImageAlpha(detailPhotoImage, 1f);
            detailPhotoImage.gameObject.SetActive(photo);
        }
        if (detailSilhouetteImage != null)
        {
            SetImageAlpha(detailSilhouetteImage, 1f);
            detailSilhouetteImage.gameObject.SetActive(silhouette);
        }
    }

    private void RefreshDetailExpanded()
    {
        if (detailBody != null) detailBody.SetActive(detailExpanded);
        if (detailChevron != null) detailChevron.localEulerAngles = new Vector3(0f, 0f, detailExpanded ? 0f : 90f);
    }

    // ==================== หน้า Change Photos (2x3) ====================
    private void RenderChangePhotoPage()
    {
        // เฉพาะรูปใน Storage ที่ถ่ายติด Entry นี้ (เช่นหน้า Deer โชว์แค่รูปที่มีกวาง)
        changePhotoCandidates = journalManager != null
            ? journalManager.GetStoragePhotosFor(currentPage.entry)
            : new List<PhotoStorage.StoredPhoto>();

        int perPage = changePhotoSlots.Length;
        int maxSubPage = perPage == 0 ? 0 : Mathf.Max(0, Mathf.CeilToInt(changePhotoCandidates.Count / (float)perPage) - 1);
        currentPage.subPage = Mathf.Clamp(currentPage.subPage, 0, maxSubPage);

        int startIndex = currentPage.subPage * perPage;
        for (int i = 0; i < perPage; i++)
        {
            int photoIndex = startIndex + i;
            bool hasPhoto = photoIndex < changePhotoCandidates.Count;

            // Sprite เป็นของ PhotoStorage (ห้าม Destroy) — ช่องว่างโชว์แค่กรอบมืด กดไม่ได้
            Image image = i < changePhotoImages.Length ? changePhotoImages[i] : null;
            if (image != null)
            {
                image.sprite = hasPhoto ? PhotoStorage.Instance.GetThumbnailSprite(changePhotoCandidates[photoIndex]) : null;
                image.enabled = hasPhoto && image.sprite != null;
            }
            if (changePhotoSlots[i] != null) changePhotoSlots[i].interactable = hasPhoto;
        }
    }

    private void OnChangePhotoSlotClicked(int slotIndex)
    {
        int photoIndex = currentPage.subPage * changePhotoSlots.Length + slotIndex;
        if (journalManager == null || photoIndex < 0 || photoIndex >= changePhotoCandidates.Count) return;

        if (journalManager.SetPhotoForEntry(currentPage.entry, changePhotoCandidates[photoIndex]))
        {
            // เลือกเสร็จกลับไปหน้า Detail ของ Entry นี้ (ไม่ต้องเล่นเฟดเผยรูปซ้ำ)
            revealedCreatureIds.Add(currentPage.entry.creatureId);
            NavigateTo(new PageState { type = PageType.Detail, animals = currentPage.animals, entry = currentPage.entry });
        }
    }

    // ==================== เฟดรูปเงา -> รูปจริง (Detail) ====================
    private IEnumerator RevealPhotoFade()
    {
        if (detailSilhouetteImage == null || detailPhotoImage == null) yield break;

        detailSilhouetteImage.gameObject.SetActive(true);
        detailPhotoImage.gameObject.SetActive(true);
        SetImageAlpha(detailSilhouetteImage, 1f);
        SetImageAlpha(detailPhotoImage, 0f);

        float elapsed = 0f;
        while (elapsed < photoRevealFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / photoRevealFadeDuration);
            SetImageAlpha(detailSilhouetteImage, 1f - t);
            SetImageAlpha(detailPhotoImage, t);
            yield return null;
        }

        SetImageAlpha(detailPhotoImage, 1f);
        detailSilhouetteImage.gameObject.SetActive(false);
        photoRevealRoutine = null;
    }

    private void StopPhotoRevealFade()
    {
        if (photoRevealRoutine != null)
        {
            StopCoroutine(photoRevealRoutine);
            photoRevealRoutine = null;
        }
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// เมนู Pause (Esc) + หน้า Setting
///
/// กด Esc ตอนเดินปกติ / โหมดถ่ายรูป / คุย NPC = เปิด Pause — ถ้าเปิด Journal/Storage/บอร์ดอยู่ Esc จะไปปิดหน้านั้นทีละชั้นก่อนตามเดิม
/// (หน้าพวกนั้นฟัง Esc ของตัวเอง) กลับมาเดินปกติแล้วค่อย Esc เข้า Pause ได้
///
/// ระหว่าง Pause: Time.timeScale = 0 (สัตว์/เวลาในเกมหยุด), ปิดทุก Action Map ใน inputActions ยกเว้นของปุ่ม Pause เอง
/// (คลิก Resume ตอนอยู่โหมดถ่ายรูปจะไม่ไปถ่ายรูปซ้อน, E/F/J/I ไม่ทำงานข้างหลังเมนู) และเปิด Volume เบลอฉากหลัง (blurVolume.weight = 1)
/// ไม่ได้เปลี่ยน SystemState — ถ้าเปลี่ยน CameraController จะดึงกล้องออกจากโหมดถ่ายรูปทันทีที่กด Pause
///
/// ออก: Resume หรือ Esc (ถ้าอยู่หน้า Setting Esc = กลับหน้า Pause ก่อน)
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    /// <summary>true เฉพาะเฟรมที่เพิ่งปิด Pause — กันคลิก Resume ไปเดินบทสนทนา/ทำอย่างอื่นต่อในเฟรมเดียวกัน</summary>
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    private static int closedFrame = -1;
    /// <summary>เปิด/ปิด Pause (true = เพิ่งเปิด) — ให้ UI อื่นที่อยู่ใน State เดิมซ่อนตัวเองได้ เช่น World HUD</summary>
    public static event System.Action<bool> OnPauseChanged;

    [Header("Input")]
    [Tooltip("ไฟล์ PlayerControls.inputactions — ปุ่ม Pause หาจากชื่อ และ Action Map อื่นทั้งหมดในไฟล์นี้จะถูกปิดระหว่าง Pause")]
    public InputActionAsset inputActions;
    public string pauseActionPath = "System/Pause";

    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject settingsPanel;
    [Tooltip("ปุ่มที่ถูกเลือกไว้ตอนเปิดแต่ละหน้า (ให้ใช้คีย์บอร์ด/จอยได้) ปล่อยว่างได้")]
    public GameObject firstPauseButton;
    public GameObject firstSettingsSelectable;

    [Header("Background Blur")]
    [Tooltip("Volume ที่มี Depth of Field (Gaussian) — ปกติ weight 0, ระหว่าง Pause weight 1")]
    public Volume blurVolume;

    [Header("Back to Menu")]
    public string mainMenuSceneName = "MainMenu";

    private InputAction pauseAction;
    private readonly List<InputAction> disabledActions = new List<InputAction>();
    private float timeScaleBeforePause = 1f;
    private CursorLockMode cursorLockBeforePause;
    private bool cursorVisibleBeforePause;
    private int lastStateChangeFrame = -1;

    private void Awake()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (blurVolume != null) blurVolume.weight = 0f;
    }

    private void OnEnable()
    {
        pauseAction = inputActions != null ? inputActions.FindAction(pauseActionPath, false) : null;
        if (pauseAction == null)
        {
            Debug.LogWarning($"[PauseMenuUI] หา Action \"{pauseActionPath}\" ไม่เจอ — ลาก PlayerControls.inputactions ใส่ช่อง Input Actions");
            return;
        }
        pauseAction.Enable();
        pauseAction.performed += OnPausePressed;
    }

    private void Start()
    {
        if (StateManager.Instance != null) StateManager.Instance.OnSystemStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (pauseAction != null) pauseAction.performed -= OnPausePressed;
    }

    private void OnDestroy()
    {
        if (StateManager.Instance != null) StateManager.Instance.OnSystemStateChanged -= HandleStateChanged;
        // ถูกทำลายทั้งที่ยัง Pause อยู่ (เช่นโหลดซีนใหม่) ต้องคืนเวลา/ปุ่มให้ ไม่งั้นซีนถัดไปค้าง
        if (IsPaused) RestoreGameplay();
    }

    // Esc เดียวกันอาจถูก Journal/Storage ใช้ปิดหน้าตัวเองไปแล้วในเฟรมนี้ (ลำดับ Callback ไม่แน่นอน) — ถ้า State เพิ่งเปลี่ยนเฟรมนี้ไม่เปิด Pause
    private void HandleStateChanged(StateManager.SystemState oldState, StateManager.SystemState newState)
    {
        lastStateChangeFrame = Time.frameCount;
    }

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        if (IsPaused)
        {
            if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
            else Resume();
            return;
        }

        if (CanPauseNow()) Pause();
    }

    private bool CanPauseNow()
    {
        if (StateManager.Instance == null) return true;
        if (Time.frameCount == lastStateChangeFrame) return false;

        switch (StateManager.Instance.CurrentSystemState)
        {
            case StateManager.SystemState.Normal:
            case StateManager.SystemState.Photograph:
            case StateManager.SystemState.Talking:
                return true;
            default:
                return false; // Journal/Storage/Board = Esc ใช้ย้อนกลับของหน้านั้น, Pause = กำลังนอน
        }
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        cursorLockBeforePause = Cursor.lockState;
        cursorVisibleBeforePause = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetGameplayInput(false);
        if (blurVolume != null) blurVolume.weight = 1f;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
        Select(firstPauseButton);
        OnPauseChanged?.Invoke(true);
    }

    /// <summary>ปุ่ม Resume</summary>
    public void Resume()
    {
        if (!IsPaused) return;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        RestoreGameplay();

        Cursor.lockState = cursorLockBeforePause;
        Cursor.visible = cursorVisibleBeforePause;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>ปุ่ม Setting</summary>
    public void OpenSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        Select(firstSettingsSelectable);
    }

    /// <summary>ปุ่ม Back ในหน้า Setting</summary>
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
        Select(firstPauseButton);
    }

    /// <summary>ปุ่ม Back to Menu — ล้างเควส/รูปทั้งหมดเหมือนปุ่ม R แล้วโหลดเมนูหลัก</summary>
    public void BackToMenu()
    {
        RestoreGameplay();
        DebugGameReset.ResetToMainMenu(mainMenuSceneName);
    }

    private void RestoreGameplay()
    {
        IsPaused = false;
        closedFrame = Time.frameCount;
        Time.timeScale = timeScaleBeforePause;
        SetGameplayInput(true);
        if (blurVolume != null) blurVolume.weight = 0f;
        OnPauseChanged?.Invoke(false);
    }

    // ปิดเฉพาะ Action ที่เปิดอยู่จริง แล้วคืนเฉพาะตัวพวกนั้น (ไม่ไปเปิด Action ที่ระบบอื่นตั้งใจปิดไว้)
    private void SetGameplayInput(bool enabled)
    {
        if (inputActions == null) return;

        if (!enabled)
        {
            disabledActions.Clear();
            foreach (InputActionMap map in inputActions.actionMaps)
            {
                if (pauseAction != null && map == pauseAction.actionMap) continue;
                foreach (InputAction action in map.actions)
                {
                    if (!action.enabled) continue;
                    action.Disable();
                    disabledActions.Add(action);
                }
            }
        }
        else
        {
            foreach (InputAction action in disabledActions) action.Enable();
            disabledActions.Clear();
        }
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }
}

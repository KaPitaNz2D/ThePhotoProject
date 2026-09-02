using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// สลับ Priority ระหว่าง Vcam โหมดเดิน (Third Person) กับ Vcam โหมดถ่ายรูป (Photo Cam)
/// โดยอิงจาก StateManager.SystemState — ไม่ต้องมี Logic การเดิน/ถ่ายรูปในนี้เลย
/// แค่ทำหน้าที่ "ฟัง" การเปลี่ยน State แล้วสลับกล้องให้ตรงกัน
///
/// การเดินไม่ได้ตอนอยู่ในโหมดถ่ายรูปนั้น PlayerMovement จัดการอยู่แล้วผ่าน
/// StateManager.Instance.CanControlPlayer() ไม่เกี่ยวกับสคริปต์นี้โดยตรง
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Virtual Cameras")]
    public CinemachineCamera thirdPersonCam;
    public CinemachineCamera photoCam;

    [Header("Priority Settings")]
    [Tooltip("Priority ตอนกล้องนั้นเป็นกล้องหลัก (Active)")]
    public int activePriority = 20;
    [Tooltip("Priority ตอนกล้องนั้นถูกซ่อน (Inactive)")]
    public int inactivePriority = 0;

    [Header("Transition")]
    [Tooltip("ถ้าใส่ไว้ จะ Fade จอดำก่อนสลับกล้องทุกครั้ง (ไม่ใส่ก็ได้ จะสลับทันทีแบบเดิม)")]
    public PhotoTransitionUI transitionUI;

    private CinemachinePanTilt photoCamPanTilt;

    private void Awake()
    {
        if (photoCam != null)
        {
            photoCamPanTilt = photoCam.GetComponent<CinemachinePanTilt>();
        }
    }

    private void Start()
    {
        // Subscribe ใน Start() แทน OnEnable() เพราะ Unity รับประกันว่า Awake() ของทุก Object
        // ในซีนจะทำงานจบหมดก่อน Start() ของ Object ไหนจะเริ่ม -> StateManager.Instance
        // การันตีว่าถูกสร้างแล้วแน่นอน ต่างจาก OnEnable ที่ลำดับไม่แน่นอนระหว่าง Object
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnSystemStateChanged += HandleSystemStateChanged;

            // ตั้งค่ากล้องเริ่มต้นให้ตรงกับ SystemState ปัจจุบันตอนเริ่มเกม (ปกติคือ Normal -> Third Person)
            SetPhotoMode(StateManager.Instance.CurrentSystemState == StateManager.SystemState.Photograph);
        }
        else
        {
            Debug.LogError("CameraController หา StateManager.Instance ไม่เจอ! เช็คว่ามี StateManager วางอยู่ในซีนหรือยัง");
            SetPhotoMode(false);
        }
    }

    private void OnDestroy()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnSystemStateChanged -= HandleSystemStateChanged;
        }
    }

    private void HandleSystemStateChanged(StateManager.SystemState oldState, StateManager.SystemState newState)
    {
        bool isPhotoMode = newState == StateManager.SystemState.Photograph;

        if (transitionUI != null)
        {
            // Fade จอดำก่อน -> สลับกล้องตอนดำสนิท (มองไม่เห็นจังหวะกระโดดตำแหน่ง) -> Fade กลับ
            transitionUI.PlayTransition(() => SetPhotoMode(isPhotoMode));
        }
        else
        {
            SetPhotoMode(isPhotoMode);
        }
    }

    private void SetPhotoMode(bool isPhotoMode)
    {
        if (isPhotoMode && photoCamPanTilt != null)
        {
            // รีเซ็ต Pan/Tilt กลับเป็น 0 ก่อนสลับเข้ากล้องถ่ายรูปทุกครั้ง
            // ป้องกันค่าที่ค้างจากการใช้งานครั้งก่อน (เช่นก้มกล้องลงไว้ก่อนออกจากโหมด)
            // ทำให้กล้องกระชาก/ล็อคมุมเดิมก่อนแล้วค่อย Blend เข้ามาจริง
            // พอรีเซ็ตเป็น 0 กล้องจะเริ่มจากทิศทางที่ PhotoCameraPivot หันอยู่จริงเสมอ (ตรงกับทิศตัวละคร)
            photoCamPanTilt.PanAxis.Value = 0f;
            photoCamPanTilt.TiltAxis.Value = 0f;
        }

        if (thirdPersonCam != null)
        {
            thirdPersonCam.Priority = isPhotoMode ? inactivePriority : activePriority;
        }
        if (photoCam != null)
        {
            photoCam.Priority = isPhotoMode ? activePriority : inactivePriority;
        }
    }
}
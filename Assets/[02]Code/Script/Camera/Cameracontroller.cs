using System.Collections.Generic;
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

    [Header("Photo Mode - Hide Character")]
    [Tooltip("Root ของโมเดลตัวละคร (ตัวเดียวกับ PlayerObj ที่ ThirdPersonCam หมุนตามทิศเดิน) — forward ของมันคือทิศหน้าตัวละคร ใช้คำนวณมุมเริ่มต้นของกล้องถ่ายรูป และ Renderer ทั้งหมดใต้นี้จะถูกซ่อนตอนอยู่ในโหมดถ่ายรูป (กล้องอยู่ติดหัว หมุนได้ 360°) แล้วคืนค่าตอนออกจากโหมด")]
    public Transform characterModel;

    private CinemachinePanTilt photoCamPanTilt;
    private CinemachineInputAxisController thirdPersonInputAxis;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    private void Awake()
    {
        if (photoCam != null)
        {
            photoCamPanTilt = photoCam.GetComponent<CinemachinePanTilt>();
        }
        if (thirdPersonCam != null)
        {
            thirdPersonInputAxis = thirdPersonCam.GetComponent<CinemachineInputAxisController>();
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
            SetThirdPersonLookLocked(!StateManager.Instance.CanControlPlayer());
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

        // ล็อค Look input ของ Third Person Cam ทันทีที่ออกจาก Normal (เช่น เข้าโหมดถ่ายรูป/เปิดสมุดบันทึก/คุยกับ NPC)
        // ไม่ต้องรอ Fade เพราะ Vcam ตัวนี้ยังรับ Input หมุนกล้องอยู่แม้ Priority จะถูกลดจนไม่เห็นภาพแล้วก็ตาม
        // ถ้าไม่ล็อค พอกลับมา Normal กล้องจะหมุนไปมุมที่ไม่ได้ตั้งใจจากการขยับเมาส์ระหว่างอยู่ State อื่น
        SetThirdPersonLookLocked(!StateManager.Instance.CanControlPlayer());

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
        // ต้องทำก่อนสลับ Priority — ตอนนี้ Main Camera ยังเป็นภาพของ Third Person Cam อยู่ ทิศที่อ่านได้คือทิศที่ผู้เล่นกำลังมองจริงๆ
        if (isPhotoMode) AlignPhotoCamToThirdPersonView();

        SetCharacterHidden(isPhotoMode);

        if (thirdPersonCam != null)
        {
            thirdPersonCam.Priority = isPhotoMode ? inactivePriority : activePriority;
        }
        if (photoCam != null)
        {
            photoCam.Priority = isPhotoMode ? activePriority : inactivePriority;
        }
    }

    // กล้องถ่ายรูปเริ่มจากทิศที่ Third Person Cam มองอยู่ (แนวนอน) ไม่ใช่ทิศหน้าตัวละคร — ตัวละครไม่ต้องหันตาม
    // Pan อ้างอิง PhotoCameraPivot ซึ่งเป็นลูกของ characterModel (วัดจากทิศหน้าตัวละคร) เลยต้องแปลงทิศกล้องเป็นมุมเทียบกับหน้าตัวละคร
    // ส่วน Tilt เริ่มที่ 0 (มองระดับสายตา) เพราะ Third Person Cam มักก้มมองลงมาที่ตัวละคร
    private void AlignPhotoCamToThirdPersonView()
    {
        if (photoCamPanTilt == null) return;

        photoCamPanTilt.TiltAxis.Value = 0f;
        photoCamPanTilt.PanAxis.Value = 0f;

        Camera mainCam = Camera.main;
        if (characterModel == null || mainCam == null) return;

        Vector3 cameraDirection = Vector3.ProjectOnPlane(mainCam.transform.forward, Vector3.up);
        Vector3 facing = Vector3.ProjectOnPlane(characterModel.forward, Vector3.up);
        if (cameraDirection.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f) return;

        float angleToCamera = Vector3.SignedAngle(facing, cameraDirection, Vector3.up);
        photoCamPanTilt.PanAxis.Value = photoCamPanTilt.PanAxis.ClampValue(angleToCamera);
    }

    // ซ่อนเฉพาะ Renderer ที่เปิดอยู่ตอนนั้น แล้วคืนเฉพาะตัวที่เราปิดเอง — ไม่ไปเปิด Renderer ที่ตั้งใจปิดไว้ (เช่น PlayerObj ที่ปิดไว้ใน Prefab)
    private void SetCharacterHidden(bool hidden)
    {
        if (characterModel == null) return;

        if (hidden)
        {
            if (hiddenRenderers.Count > 0) return;

            foreach (Renderer r in characterModel.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }
        else
        {
            foreach (Renderer r in hiddenRenderers)
            {
                if (r != null) r.enabled = true;
            }
            hiddenRenderers.Clear();
        }
    }

    private void SetThirdPersonLookLocked(bool locked)
    {
        if (thirdPersonInputAxis != null)
        {
            thirdPersonInputAxis.enabled = !locked;
        }
    }
}
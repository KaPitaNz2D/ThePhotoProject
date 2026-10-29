using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// เอียงกล้องถ่ายรูป (Roll / Dutch angle) ด้วย Input Action TiltLeft (Q) / TiltRight (E) กดค้างเพื่อเอียงต่อเนื่อง ปล่อยแล้วค้างมุมไว้
/// จำกัดไม่เกิน ±maxRoll องศาในแต่ละด้าน ทำงานเฉพาะตอน SystemState.Photograph
///
/// เอียงผ่านค่า Dutch ใน Lens ของ photoVcam — กล้องที่ PhotoShooter ใช้ Capture ภาพคือ Main Camera ตัวเดียวกับที่ผู้เล่นเห็น
/// ภาพที่บันทึกจึงเอียงตรงกับที่เห็นบนจอโดยไม่ต้องทำอะไรเพิ่ม (ไม่กระทบทิศ SphereCast ที่ใช้ตรวจจับสัตว์ เพราะ Roll หมุนรอบแกน forward)
/// </summary>
public class PhotoRoll : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Cinemachine Camera ของ PhotoCam (ตัวเดียวกับที่ PhotoZoom ใช้)")]
    public CinemachineCamera photoVcam;

    [Header("Input (Action แบบ Button)")]
    [Tooltip("เอียงซ้าย (Photograph/TiltLeft) เว้นว่างไว้ = หาจากชื่อใน PlayerControls ให้เอง")]
    public InputActionReference tiltLeftInput;
    [Tooltip("เอียงขวา (Photograph/TiltRight) เว้นว่างไว้ = หาจากชื่อใน PlayerControls ให้เอง")]
    public InputActionReference tiltRightInput;
    [Tooltip("Action แกนเดิม (Photograph/Roll Q=-1, E=+1) ใช้เป็นตัวสำรองถ้าหา TiltLeft/TiltRight ไม่เจอ และเป็นทางไปหา InputActionAsset")]
    public InputActionReference rollInput;

    [Header("Roll Settings")]
    [Tooltip("มุมเอียงสูงสุดในแต่ละด้าน (องศา)")]
    public float maxRoll = 60f;
    [Tooltip("ความเร็วเอียงตอนกดค้าง (องศา/วินาที)")]
    public float rollSpeed = 30f;
    [Tooltip("ความนุ่มนวลตอนกล้องไล่ตามมุมเป้าหมาย ยิ่งมากยิ่งไว")]
    public float smoothing = 12f;

    /// <summary>มุมเอียงปัจจุบัน (องศา) บวก = เอียงขวา (E), ลบ = เอียงซ้าย (Q) — เอาไปทำ UI วัดระดับได้</summary>
    public float CurrentRoll => currentRoll;

    private float targetRoll;
    private float currentRoll;
    private bool wasPhotoMode;

    private InputAction tiltLeft;
    private InputAction tiltRight;

    private void Start()
    {
        tiltLeft = PhotoInputLookup.Resolve(tiltLeftInput, rollInput, "Photograph/TiltLeft");
        tiltRight = PhotoInputLookup.Resolve(tiltRightInput, rollInput, "Photograph/TiltRight");

        if (tiltLeft != null && tiltRight != null)
        {
            tiltLeft.Enable();
            tiltRight.Enable();
        }
        else if (rollInput != null)
        {
            tiltLeft = tiltRight = null; // ใช้แกน Roll เดิมแทน
            rollInput.action.Enable();
        }
        else
        {
            Debug.LogError("[PhotoRoll] หา Input Action TiltLeft / TiltRight ไม่เจอ — ลาก Action มาใส่ช่อง Tilt Left Input / Tilt Right Input ใน Inspector");
        }
    }

    private void Update()
    {
        bool isPhotoMode = StateManager.Instance != null
                           && StateManager.Instance.IsSystemState(StateManager.SystemState.Photograph);

        if (!isPhotoMode)
        {
            // ตอนออกจากโหมดไม่รีเซ็ตมุมทันที เพราะระหว่าง Fade ดำกล้องถ่ายรูปยังแสดงผลอยู่ จะเห็นภาพกระชากกลับระดับ
            // (กล้องนี้ไม่ใช่กล้องหลักแล้วหลังสลับ มุมที่ค้างอยู่เลยไม่มีผล และจะรีเซ็ตตอนเข้าโหมดครั้งหน้า)
            wasPhotoMode = false;
            return;
        }

        if (!wasPhotoMode)
        {
            wasPhotoMode = true;
            targetRoll = 0f;
            currentRoll = 0f;
            ApplyRoll(0f);
        }

        // E (TiltRight) = +1 เอียงขวา, Q (TiltLeft) = -1 เอียงซ้าย กดทั้งสองพร้อมกันหักล้างกัน
        float input = 0f;
        if (tiltLeft != null && tiltRight != null)
        {
            input = (tiltRight.IsPressed() ? 1f : 0f) - (tiltLeft.IsPressed() ? 1f : 0f);
        }
        else if (rollInput != null)
        {
            input = rollInput.action.ReadValue<float>();
        }
        targetRoll = Mathf.Clamp(targetRoll + input * rollSpeed * Time.deltaTime, -maxRoll, maxRoll);

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * smoothing);
        ApplyRoll(currentRoll);
    }

    // Cinemachine: Dutch เป็นบวก = กล้องหมุนทวนเข็มนาฬิกา (เอียงซ้าย) เลยกลับเครื่องหมายให้ E (บวก) เอียงขวา
    private void ApplyRoll(float roll)
    {
        if (photoVcam == null) return;

        LensSettings lens = photoVcam.Lens;
        lens.Dutch = -roll;
        photoVcam.Lens = lens;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ยก/ลดกล้องถ่ายรูปขึ้นลง เหมือนยกแขนขึ้นหรือย่อแขนลงเพื่อถ่ายมุมใหม่ๆ — กด PushUpCamera (Space) = ยกขึ้น, PullDownCamera (Left Ctrl) = ลดลง
/// กดค้างเพื่อเลื่อนต่อเนื่อง ปล่อยแล้วค้างระดับไว้ ทำงานเฉพาะตอน SystemState.Photograph และรีเซ็ตกลับระดับเดิมทุกครั้งที่ "เข้า" โหมดถ่ายรูป
///
/// ปุ่มมาจาก Input Action (Photograph/PushUpCamera, Photograph/PullDownCamera ใน PlayerControls.inputactions) เปลี่ยนปุ่มได้ที่ Input Actions
/// PhotoCam ล็อคตำแหน่งตาม PhotoCameraPivot (Tracking Target) อยู่แล้ว เลยขยับความสูงของ Pivot นี้แทนการขยับกล้องตรงๆ
/// (ไม่กระทบแกนหมุน Pan/Tilt เพราะ Pan/Tilt วัดเฉพาะการหมุนของ Pivot)
///
/// Pivot นี้ PlayerCrouch ก็คุมความสูงอยู่ (ย่อ/ลุก) — สคริปต์นี้เลยไม่เขียน localPosition ตรงๆ แต่ฝาก "ค่าชดเชยเพิ่ม" ไว้ที่ PlayerCrouch
/// ผ่าน SetExtraHeightOffset ย่อกับยก/ลดกล้องจึงบวกซ้อนกันได้ (ย่อได้เหมือนเดิมตอนถ่ายรูป) และไม่แย่งกันเขียนตำแหน่งเดียวกัน
/// ระยะยก/ลดสูงสุดปรับได้จาก maxRaise / maxLower และมีตัวกันไม่ให้กล้องทะลุเพดาน/จมพื้น (ปิดได้ที่ blockByObstacles)
/// </summary>
public class PhotoHeight : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PhotoCameraPivot — จุดที่ PhotoCam ล็อคตาม (Tracking Target) ยก/ลดกล้องด้วยการขยับความสูงของจุดนี้")]
    public Transform cameraPivot;
    [Tooltip("ตัวคุมท่าย่อ ถ้า cameraPivot อยู่ใน cameraHeightTargets ของมัน จะฝากค่ายก/ลดไว้ที่นั่น (เว้นว่างไว้ = หาให้เองจาก Player)")]
    public PlayerCrouch playerCrouch;

    [Header("Input (Action แบบ Button)")]
    [Tooltip("ยกกล้องขึ้น (Photograph/PushUpCamera) เว้นว่างไว้ = หาจากชื่อใน PlayerControls ให้เอง")]
    public InputActionReference pushUpInput;
    [Tooltip("ลดกล้องลง (Photograph/PullDownCamera) เว้นว่างไว้ = หาจากชื่อใน PlayerControls ให้เอง")]
    public InputActionReference pullDownInput;

    [Header("Height Settings")]
    [Tooltip("ยกกล้องขึ้นได้สูงสุดเหนือระดับเริ่มต้น (เมตร)")]
    public float maxRaise = 0.6f;
    [Tooltip("ลดกล้องลงได้ต่ำสุดใต้ระดับเริ่มต้น (เมตร)")]
    public float maxLower = 0.6f;
    [Tooltip("ความเร็วตอนกดค้าง (เมตร/วินาที)")]
    public float moveSpeed = 1f;
    [Tooltip("ความนุ่มนวลตอนกล้องไล่ตามระดับเป้าหมาย ยิ่งมากยิ่งไว")]
    public float smoothing = 12f;

    [Header("Obstacle Check")]
    [Tooltip("กันกล้องทะลุเพดาน/กิ่งไม้ที่มี Collider และจมพื้น (หยุดเลื่อนเมื่อใกล้ชน)")]
    public bool blockByObstacles = true;
    public LayerMask obstacleMask = ~0;
    [Tooltip("เว้นระยะจากสิ่งกีดขวาง (เมตร)")]
    public float clearance = 0.25f;

    /// <summary>ระดับที่ยก/ลดอยู่ตอนนี้ (เมตร) บวก = ยกขึ้น, ลบ = ลดลง (ไม่รวมการย่อ)</summary>
    public float CurrentOffset => appliedOffset;

    private InputAction pushUp;
    private InputAction pullDown;
    private float baseY;               // Local Y เริ่มต้นของ Pivot (ใช้เฉพาะตอนไม่มี PlayerCrouch คุม)
    private float targetOffset;
    private float appliedOffset;
    private bool wasPhotoMode;

    private void Awake()
    {
        if (cameraPivot != null) baseY = cameraPivot.localPosition.y;
        if (playerCrouch == null) playerCrouch = GetComponentInParent<PlayerCrouch>(true);
    }

    private void Start()
    {
        // PhotoZoom.zoomInput ผูกไว้ใน Prefab อยู่แล้ว ใช้เป็นทางไปหา InputActionAsset เดียวกัน
        PhotoZoom zoom = GetComponent<PhotoZoom>();
        InputActionReference anchor = zoom != null ? zoom.zoomInput : null;

        pushUp = PhotoInputLookup.Resolve(pushUpInput, anchor, "Photograph/PushUpCamera");
        pullDown = PhotoInputLookup.Resolve(pullDownInput, anchor, "Photograph/PullDownCamera");

        if (pushUp == null || pullDown == null)
        {
            Debug.LogError("[PhotoHeight] หา Input Action PushUpCamera / PullDownCamera ไม่เจอ — ลาก Action มาใส่ช่อง Push Up Input / Pull Down Input ใน Inspector " +
                           "(ต้องมีใน Photograph map ของ PlayerControls.inputactions)");
        }

        pushUp?.Enable();
        pullDown?.Enable();
    }

    private void Update()
    {
        if (cameraPivot == null) return;

        bool isPhotoMode = StateManager.Instance != null
                           && StateManager.Instance.IsSystemState(StateManager.SystemState.Photograph);

        if (!isPhotoMode)
        {
            // ไม่รีเซ็ตตอนออก (กล้องนี้ยังแสดงผลอยู่ระหว่าง Fade ดำ) ไปรีเซ็ตตอนเข้าครั้งหน้าแทน เหมือน PhotoRoll
            wasPhotoMode = false;
            return;
        }

        if (!wasPhotoMode)
        {
            wasPhotoMode = true;
            targetOffset = 0f;
            appliedOffset = 0f;
            Apply(0f);
        }

        // กดทั้งสองปุ่มพร้อมกันหักล้างกัน
        float input = (pushUp != null && pushUp.IsPressed() ? 1f : 0f) - (pullDown != null && pullDown.IsPressed() ? 1f : 0f);
        targetOffset = Mathf.Clamp(targetOffset + input * moveSpeed * Time.deltaTime, -maxLower, maxRaise);

        float limited = blockByObstacles ? LimitByObstacles(targetOffset) : targetOffset;

        // ชนสิ่งกีดขวางแล้วให้เป้าหมายหยุดอยู่ที่ระดับที่ไปได้จริง ไม่งั้นตอนปล่อยปุ่มแล้วกดกลับ ต้องรอให้ค่าเป้าหมายที่ล้นอยู่ลดลงมาก่อนกล้องถึงจะขยับ
        if (limited < targetOffset && targetOffset > appliedOffset) targetOffset = limited;
        else if (limited > targetOffset && targetOffset < appliedOffset) targetOffset = limited;

        appliedOffset = Mathf.Lerp(appliedOffset, limited, Time.deltaTime * smoothing);
        Apply(appliedOffset);
    }

    // ฝากค่ายก/ลดไว้ที่ PlayerCrouch (บวกซ้อนกับท่าย่อ) ถ้าไม่มีตัวคุมนี้ ขยับความสูงของ Pivot เอง (แก้เฉพาะแกน Y)
    private void Apply(float offset)
    {
        if (playerCrouch != null && playerCrouch.SetExtraHeightOffset(cameraPivot, offset)) return;

        Vector3 pos = cameraPivot.localPosition;
        pos.y = baseY + offset;
        cameraPivot.localPosition = pos;
    }

    // ยิง SphereCast จากตำแหน่งกล้องจริงตอนนี้ (รวมท่าย่อแล้ว) ไปทางที่กำลังจะขยับเพิ่ม แล้วลดระยะลงถ้าชนอะไรก่อน
    // คืนค่าเป็นระดับ offset ที่ไปได้ (อยู่ระหว่าง appliedOffset กับ desired)
    private float LimitByObstacles(float desired)
    {
        float delta = desired - appliedOffset;
        if (Mathf.Abs(delta) < 0.001f) return desired;

        Vector3 up = cameraPivot.parent != null ? cameraPivot.parent.up : Vector3.up;
        Vector3 direction = delta > 0f ? up : -up;
        float distance = Mathf.Abs(delta) + clearance;
        const float radius = 0.15f;

        if (Physics.SphereCast(cameraPivot.position, radius, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float allowed = Mathf.Max(0f, hit.distance - clearance);
            return appliedOffset + Mathf.Sign(delta) * Mathf.Min(allowed, Mathf.Abs(delta));
        }
        return desired;
    }
}

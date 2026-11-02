using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// กล้องบินอิสระสำหรับอัดวิดีโอ — กด F8 (ตอนเดินปกติ) เพื่อเปิด/ปิด
///   - เริ่มจากมุมที่ Main Camera มองอยู่ตอนนั้น ตอนปิด Cinemachine Brain เบลนด์กลับไปกล้องผู้เล่นเอง
///   - WASD เดินตามทิศที่มอง, E/Space ขึ้น, Q/Ctrl ลง, เมาส์หมุนมอง, ลูกกลิ้งปรับความเร็ว, Shift เร็วขึ้น, Alt ช้าลง
///   - Z/C ซูมเข้า/ออก (Field of View), R รีเซ็ต FOV
///   - ระหว่างบินตั้ง SystemState.FreeCam: ผู้เล่นหยุด, HUD ซ่อน, กล้องผู้เล่นไม่รับเมาส์ (สัตว์ยังรับรู้จากตัวผู้เล่น ไม่ใช่กล้องนี้)
/// วางบน GameObject ที่มี CinemachineCamera (ไม่มี Body/Aim) ใน PlayerRig — ใช้ได้เฉพาะ Editor/Development Build ถ้าไม่ติ๊ก allowInReleaseBuild
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class FreeCam : MonoBehaviour
{
    [Header("Toggle")]
    public Key toggleKey = Key.F8;
    [Tooltip("ใช้ได้ใน Build จริงด้วย (ปกติใช้ได้แค่ใน Editor และ Development Build)")]
    public bool allowInReleaseBuild = false;
    [Tooltip("Priority ตอนเปิด — ต้องสูงกว่ากล้องอื่นทั้งหมด (CameraController ใช้ 20-30)")]
    public int activePriority = 100;

    [Header("Move")]
    [Tooltip("ความเร็วเริ่มต้น (เมตร/วินาที) ปรับด้วยลูกกลิ้งระหว่างบิน")]
    public float moveSpeed = 5f;
    public Vector2 speedRange = new Vector2(0.5f, 60f);
    [Tooltip("ลูกกลิ้ง 1 คลิก คูณความเร็วเท่านี้")]
    public float scrollSpeedStep = 1.2f;
    public float fastMultiplier = 3f;
    public float slowMultiplier = 0.3f;
    [Tooltip("เวลา (วินาที) ที่ตำแหน่งไล่ตามเป้า ยิ่งมากยิ่งนุ่ม/ไหล")]
    public float moveSmoothTime = 0.25f;

    [Header("Look")]
    [Tooltip("องศาต่อพิกเซลที่เมาส์ขยับ")]
    public float lookSensitivity = 0.08f;
    [Tooltip("เวลา (วินาที) ที่การหมุนไล่ตามเมาส์ ยิ่งมากยิ่งนุ่ม")]
    public float lookSmoothTime = 0.12f;

    [Header("Zoom")]
    public float zoomSpeed = 30f;
    public Vector2 fovRange = new Vector2(15f, 90f);

    public bool IsActive { get; private set; }

    private CinemachineCamera vcam;
    private Vector3 targetPosition;
    private Vector3 moveVelocity;
    private float targetYaw, targetPitch, yaw, pitch;
    private float defaultFov;

    private bool Allowed => allowInReleaseBuild || Application.isEditor || Debug.isDebugBuild;

    private void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        vcam.Priority = 0;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !Allowed) return;

        if (keyboard[toggleKey].wasPressedThisFrame)
        {
            if (IsActive) Deactivate();
            else Activate();
        }

        if (IsActive) Fly(keyboard, Mouse.current);
    }

    private void Activate()
    {
        // เปิดได้เฉพาะตอนเดินปกติ ไม่ไปทับโหมดถ่ายรูป/คุย/Journal
        if (StateManager.Instance == null || !StateManager.Instance.IsSystemState(StateManager.SystemState.Normal)) return;

        Camera main = Camera.main;
        if (main != null)
        {
            transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            defaultFov = main.fieldOfView;
            LensSettings lens = vcam.Lens;
            lens.FieldOfView = defaultFov;
            vcam.Lens = lens;
        }

        targetPosition = transform.position;
        moveVelocity = Vector3.zero;
        Vector3 euler = transform.eulerAngles;
        yaw = targetYaw = euler.y;
        pitch = targetPitch = Mathf.DeltaAngle(0f, euler.x);

        vcam.Priority = activePriority;
        vcam.PreviousStateIsValid = false;
        StateManager.Instance.SetSystemState(StateManager.SystemState.FreeCam);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsActive = true;
    }

    private void Deactivate()
    {
        IsActive = false;
        vcam.Priority = 0;
        if (StateManager.Instance != null && StateManager.Instance.IsSystemState(StateManager.SystemState.FreeCam))
        {
            StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
        }
    }

    private void Fly(Keyboard keyboard, Mouse mouse)
    {
        // ใช้ unscaledDeltaTime — บินได้แม้ตั้ง Time.timeScale = 0 (หยุดโลกไว้ถ่ายช็อตนิ่ง)
        float dt = Time.unscaledDeltaTime;

        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            targetYaw += delta.x * lookSensitivity;
            targetPitch = Mathf.Clamp(targetPitch - delta.y * lookSensitivity, -89f, 89f);

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                moveSpeed *= scroll > 0f ? scrollSpeedStep : 1f / scrollSpeedStep;
                moveSpeed = Mathf.Clamp(moveSpeed, speedRange.x, speedRange.y);
            }
        }

        float lookT = lookSmoothTime > 0f ? 1f - Mathf.Exp(-dt / lookSmoothTime) : 1f;
        yaw = Mathf.LerpAngle(yaw, targetYaw, lookT);
        pitch = Mathf.Lerp(pitch, targetPitch, lookT);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 input = Vector3.zero;
        if (keyboard.wKey.isPressed) input += Vector3.forward;
        if (keyboard.sKey.isPressed) input += Vector3.back;
        if (keyboard.dKey.isPressed) input += Vector3.right;
        if (keyboard.aKey.isPressed) input += Vector3.left;
        Vector3 move = rotation * input;
        if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed) move += Vector3.up;
        if (keyboard.qKey.isPressed || keyboard.leftCtrlKey.isPressed) move += Vector3.down;

        float speed = moveSpeed;
        if (keyboard.leftShiftKey.isPressed) speed *= fastMultiplier;
        if (keyboard.leftAltKey.isPressed) speed *= slowMultiplier;
        if (move.sqrMagnitude > 1f) move.Normalize();
        targetPosition += move * speed * dt;

        Vector3 position = Vector3.SmoothDamp(transform.position, targetPosition, ref moveVelocity, moveSmoothTime, Mathf.Infinity, dt);
        transform.SetPositionAndRotation(position, rotation);

        float zoom = 0f;
        if (keyboard.zKey.isPressed) zoom -= 1f;
        if (keyboard.cKey.isPressed) zoom += 1f;
        LensSettings lens = vcam.Lens;
        if (keyboard.rKey.wasPressedThisFrame) lens.FieldOfView = defaultFov;
        else if (zoom != 0f) lens.FieldOfView = Mathf.Clamp(lens.FieldOfView + zoom * zoomSpeed * dt, fovRange.x, fovRange.y);
        vcam.Lens = lens;
    }
}

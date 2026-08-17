using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCam : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform player;
    public Transform playerObj;

    public float rotationSpeed = 7f;

    [Header("Input References")]
    [Tooltip("Action การเดิน (เช่น WASD/Joystick)")]
    public InputActionReference moveInput;
    [Tooltip("Action การหมุนกล้อง (เช่น Mouse Delta) เพื่อใช้เช็คว่ากำลังขยับกล้องไหม")]
    public InputActionReference lookInput;

    [Header("Settings")]
    [Tooltip("ค่าความไวขั้นต่ำของเมาส์/จอย ที่นับว่ากำลังขยับกล้องอยู่ (กันเมาส์สั่น)")]
    public float lookThreshold = 0.05f;
    [Tooltip("ระยะเวลาที่ยัง \"นับว่ากำลังหมุนกล้องอยู่\" ต่อ หลังจาก Look input ล่าสุดที่รับมา " +
             "ป้องกันอาการทิศทางตีกันตอน Mouse Delta กระพริบเป็น 0 สลับไปมาในบางเฟรม")]
    public float cameraTurnHoldTime = 0.15f;

    private float lastLookTime = -999f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (moveInput != null) moveInput.action.Enable();
        if (lookInput != null) lookInput.action.Enable();
    }

    private void Update()
    {
        if (player == null || orientation == null || playerObj == null || moveInput == null || lookInput == null) return;

        // 1. คำนวณ Orientation ของกล้องเพื่อใช้เป็นแกนอ้างอิงเสมอ (ห้ามเอียงตามแกน Y)
        Vector3 viewDir = player.position - new Vector3(transform.position.x, player.position.y, transform.position.z);
        viewDir.y = 0;
        orientation.forward = viewDir.normalized;

        // 2. อ่านค่าการเดิน
        Vector2 inputVector = moveInput.action.ReadValue<Vector2>();
        Vector3 moveDir = orientation.forward * inputVector.y + orientation.right * inputVector.x;

        // ถ้าอยู่ในโหมดถ่ายรูป (หรือโหมดอื่นที่ไม่ใช่ Normal) ตัวละครหยุดหมุนสนิท
        // ไม่งั้นตอนเล็งกล้องถ่ายรูปด้วยเมาส์ ตัวละครจะพยายามหมุนตามไปด้วยพร้อมกัน ดูแปลกๆ
        if (StateManager.Instance != null && !StateManager.Instance.IsSystemState(StateManager.SystemState.Normal))
        {
            return;
        }

        // 3. เช็คว่าผู้เล่นกำลังขยับมุมกล้องอยู่หรือไม่ — ใช้ Hold Timer แทนการเช็คค่าเฟรมต่อเฟรมตรงๆ
        // เพราะ Mouse Delta จะรีเซ็ตเป็น 0 ในเฟรมที่ไม่มี Event เมาส์ใหม่เข้ามา (โดยเฉพาะเมาส์ Polling Rate ต่ำ)
        // ถ้าไม่มี Hold Timer ค่านี้จะกระพริบ true/false สลับกันเร็วมาก ทำให้ตัวละครหันไปมาสองทางพร้อมกัน
        Vector2 lookVector = lookInput.action.ReadValue<Vector2>();
        if (lookVector.sqrMagnitude > (lookThreshold * lookThreshold))
        {
            lastLookTime = Time.time;
        }
        bool isTurningCamera = (Time.time - lastLookTime) < cameraTurnHoldTime;

        // 4. ตัดสินใจหมุนโมเดลตัวละคร (PlayerObj) — Priority: กล้อง > ปุ่มเดิน > ไม่หมุนเลย
        if (isTurningCamera)
        {
            // [กรณีที่ 1] ขยับกล้องอยู่ (หรือเพิ่งขยับไปเมื่อครู่ ยังอยู่ในช่วง Hold) -> หันตามกล้องเสมอ
            playerObj.forward = Vector3.Slerp(playerObj.forward, orientation.forward, Time.deltaTime * rotationSpeed);
        }
        else if (moveDir != Vector3.zero)
        {
            // [กรณีที่ 2] ไม่ได้ขยับกล้อง แต่กดเดิน -> หันหน้าไปตามทิศที่เดิน
            playerObj.forward = Vector3.Slerp(playerObj.forward, moveDir.normalized, Time.deltaTime * rotationSpeed);
        }
        // [กรณีที่ 3] ไม่ได้ขยับกล้อง และไม่ได้เดิน -> ไม่หมุนอะไร ค้างทิศเดิมไว้
    }
}
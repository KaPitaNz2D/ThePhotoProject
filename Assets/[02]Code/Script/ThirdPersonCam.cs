using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCam : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform player;
    public Transform playerObj;
    public Rigidbody rb;
    public float rotationSpeed = 7f;

    [Header("Input References")]
    [Tooltip("อย่าลืมลาก Action การเดิน (เช่น WASD/Joystick) มาใส่ช่องนี้")]
    public InputActionReference moveInput;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // เช็คว่ามีการใส่ Input มาหรือยังก่อน Enable เพื่อป้องกัน Error
        if (moveInput != null)
        {
            moveInput.action.Enable();
        }
        else
        {
            Debug.LogError("ยังไม่ได้ใส่ moveInput ใน Inspector!");
        }
    }

    private void Update()
    {
        // ป้องกัน Error หากลืมลาก Object มาใส่ใน Inspector
        if (player == null || orientation == null || playerObj == null || moveInput == null) return;

        // คำนวณ Orientation ของกล้อง
        Vector3 viewDir = player.position - new Vector3(transform.position.x, player.position.y, transform.position.z);
        orientation.forward = viewDir.normalized;

        // อ่านค่า Vector2 จาก New Input System
        Vector2 inputVector = moveInput.action.ReadValue<Vector2>();

        // ใช้ค่า X และ Y จาก Vector2 โดยอ้างอิงจากทิศทางของกล้อง (Orientation)
        Vector3 inputDir = orientation.forward * inputVector.y + orientation.right * inputVector.x;

        // หมุนตัวละคร (playerObj) ไปตามทิศทางที่กดเดิน
        if (inputDir != Vector3.zero)
        {
            playerObj.forward = Vector3.Slerp(playerObj.forward, inputDir.normalized, Time.deltaTime * rotationSpeed);
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerMovement : MonoBehaviour
{
    //Events
    public event Action<float> OnSpeedChanged;
    public event Action OnJumped;

    [Header("References")]
    public Transform orientation;
    public Rigidbody rb;

    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float groundDrag = 5f;
    public float airMultiplier = 0.4f;

    [Header("Jump Settings")]
    public float jumpForce = 8f;
    public float jumpCooldown = 0.25f;
    private bool readyToJump = true;

    [Header("Ground Check")]
    public float playerHeight = 2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Slope Handling")]
    [Tooltip("มุมทางลาดสูงสุดที่เดินขึ้น-ลงได้แบบปกติ (องศา)")]
    public float maxSlopeAngle = 45f;
    [Tooltip("แรงกดลงพื้นตอนอยู่บนทางลาด ป้องกันอาการลอย/กระเด้ง")]
    public float slopeStickForce = 10f;
    private RaycastHit slopeHit;

    [Header("Input References")]
    [Tooltip("อย่าลืมลาก Action การเดิน (เช่น WASD/Joystick) มาใส่ช่องนี้")]
    public InputActionReference moveInput;
    [Tooltip("อย่าลืมลาก Action การกระโดด (เช่น Space) มาใส่ช่องนี้")]
    public InputActionReference jumpInput;

    private Vector2 inputVector;
    private Vector3 moveDirection;

    private void Start()
    {
        // ป้องกัน Error หากไม่ได้ผูก Rigidbody
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        rb.freezeRotation = true;

        // เช็คว่ามีการใส่ Input มาหรือยัง ก่อน Enable
        if (moveInput != null)
        {
            moveInput.action.Enable();
        }
        else
        {
            Debug.LogError("ยังไม่ได้ใส่ moveInput ใน Inspector!");
        }

        if (jumpInput != null)
        {
            jumpInput.action.Enable();
            jumpInput.action.performed += ctx => TryJump();
        }
        else
        {
            Debug.LogError("ยังไม่ได้ใส่ jumpInput ใน Inspector!");
        }
    }

    private void Update()
    {
        // ป้องกัน Error หากลืมลาก Object มาใส่ใน Inspector
        if (orientation == null || rb == null || moveInput == null) return;

        // เช็คว่าติดพื้นอยู่หรือไม่ ด้วยการยิง Raycast ลงไปด้านล่าง
        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, groundLayer);

        // เดินได้เฉพาะตอน SystemState เป็น Normal เท่านั้น
        // (ถ้าอยู่ใน Photograph, Talking, Pause, Journal ให้ inputVector เป็นศูนย์ไปเลย)
        bool canControl = StateManager.Instance == null || StateManager.Instance.CanControlPlayer();
        inputVector = canControl ? moveInput.action.ReadValue<Vector2>() : Vector2.zero;

        OnSpeedChanged?.Invoke(inputVector.magnitude);

        // ควบคุม Drag ตามสถานะการติดพื้น
        rb.linearDamping = isGrounded ? groundDrag : 0f;

        UpdateMovementState();
    }

    private void UpdateMovementState()
    {
        if (StateManager.Instance == null) return;

        // ยังไม่มี Running/Crouch ตอนนี้ เลยเช็คแค่ขยับหรือไม่ขยับพอ
        if (inputVector.sqrMagnitude > 0.01f)
        {
            StateManager.Instance.SetMovementState(StateManager.MovementState.Walking);
        }
        else
        {
            StateManager.Instance.SetMovementState(StateManager.MovementState.Idle);
        }
    }

    private void FixedUpdate()
    {
        if (orientation == null || rb == null) return;
        MovePlayer();
    }

    private void MovePlayer()
    {
        // คำนวณทิศทางการเดินโดยอ้างอิงจาก Orientation (เหมือนกับกล้อง)
        moveDirection = orientation.forward * inputVector.y + orientation.right * inputVector.x;

        if (isGrounded && OnSlope())
        {
            // ปรับทิศทางเดินให้ขนานไปกับพื้นทางลาด แทนที่จะเดินตรงแนวราบ
            // ทำให้ตอนลงเนินไม่มีช่วงที่ตัวละคร "หลุดลอย" จากพื้น
            Vector3 slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
            rb.AddForce(slopeMoveDirection * moveSpeed * 10f, ForceMode.Force);

            // แรงกดลงตามแนว Normal ของพื้นลาด ช่วยให้ตัวละคร "แปะ" ติดพื้นตอนข้ามขอบเนิน
            rb.AddForce(-slopeHit.normal * slopeStickForce, ForceMode.Force);
        }
        else if (isGrounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            // เดินอากาศได้แต่แรงน้อยลง
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }
    }

    private bool OnSlope()
    {
        // ยิง Raycast ลงพื้นเพื่อหา Normal ของพื้นผิวที่ยืนอยู่
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            // ถือว่าเป็น "ทางลาด" เมื่อมุมมากกว่า 0 แต่ไม่ชันเกินที่กำหนด
            return angle < maxSlopeAngle && angle != 0f;
        }
        return false;
    }

    private void TryJump()
    {
        bool canControl = StateManager.Instance == null || StateManager.Instance.CanControlPlayer();
        if (!canControl || !isGrounded || !readyToJump) return;

        readyToJump = false;

        // รีเซ็ต Velocity แกน Y ก่อนกระโดด เพื่อให้แรงกระโดดสม่ำเสมอ
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        OnJumped?.Invoke();

        Invoke(nameof(ResetJump), jumpCooldown);
    }

    private void ResetJump()
    {
        readyToJump = true;
    }
}
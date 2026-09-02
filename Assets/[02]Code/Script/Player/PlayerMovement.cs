using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerMovement : MonoBehaviour
{
    // Events
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
    public bool IsGrounded => isGrounded;

    [Header("Slope Handling")]
    [Tooltip("Maximum walkable slope angle in degrees")]
    public float maxSlopeAngle = 45f;
    [Tooltip("Downward force to keep player attached to slope surface")]
    public float slopeStickForce = 10f;
    private RaycastHit slopeHit;

    [Header("Input References")]
    public InputActionReference moveInput;
    public InputActionReference jumpInput;

    [Header("Crouch")]
    [Tooltip("ถ้าใส่ไว้ จะลดความเร็วเดินตอนย่อ และรายงาน MovementState.Crouch ให้ StateManager")]
    public PlayerCrouch playerCrouch;
    [Tooltip("ตัวคูณความเร็วตอนย่อ (0.5 = เดินช้าลงครึ่งหนึ่ง)")]
    [Range(0.1f, 1f)]
    public float crouchSpeedMultiplier = 0.5f;

    private Vector2 inputVector;
    public Vector2 CurrentInput => inputVector;
    private Vector3 moveDirection;

    private void Start()
    {
        Debug.Log(Application.persistentDataPath);
        // ป้องกัน Error หากไม่ได้ผูก Rigidbody
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        rb.freezeRotation = true;

        // Enable inputs
        if (moveInput != null)
        {
            moveInput.action.Enable();
        }
        else
        {
            Debug.LogError("moveInput is not assigned!");
        }

        if (jumpInput != null)
        {
            jumpInput.action.Enable();
            jumpInput.action.performed += ctx => TryJump();
        }
        else
        {
            Debug.LogError("jumpInput is not assigned!");
        }
    }

    private void Update()
    {
        if (orientation == null || rb == null || moveInput == null) return;

        // Ground check via downward raycast
        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, groundLayer);

        // Read input if control is permitted
        bool canControl = StateManager.Instance == null || StateManager.Instance.CanControlPlayer();
        inputVector = canControl ? moveInput.action.ReadValue<Vector2>() : Vector2.zero;

        OnSpeedChanged?.Invoke(inputVector.magnitude);

        // Apply drag only when grounded on flat ground (Disable drag on slope to prevent speed loss)
        if (isGrounded && !OnSlope())
        {
            rb.linearDamping = groundDrag;
        }
        else
        {
            rb.linearDamping = 0f;
        }

        UpdateMovementState();
    }

    private void UpdateMovementState()
    {
        if (StateManager.Instance == null) return;

        // ย่ออยู่ -> รายงาน Crouch เสมอ ไม่ว่าจะขยับหรือไม่ก็ตาม (Priority สูงสุด)
        if (playerCrouch != null && playerCrouch.IsCrouching)
        {
            StateManager.Instance.SetMovementState(StateManager.MovementState.Crouch);
            return;
        }

        // ยังไม่มี Running ตอนนี้ เลยเช็คแค่ขยับหรือไม่ขยับพอ
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
        // Calculate move direction relative to orientation
        moveDirection = orientation.forward * inputVector.y + orientation.right * inputVector.x;

        // ลดความเร็วตอนย่อ — คูณเข้ากับ moveSpeed ก่อนใส่แรง ใช้ร่วมกับทุกโหมด (พื้นราบ/ทางลาด/กลางอากาศ)
        float effectiveSpeed = moveSpeed;
        if (playerCrouch != null && playerCrouch.IsCrouching)
        {
            effectiveSpeed *= crouchSpeedMultiplier;
        }

        if (isGrounded && OnSlope())
        {
            // Disable gravity on slopes
            rb.useGravity = false;

            // Calculate exact target velocity along slope surface
            Vector3 slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
            rb.AddForce(slopeMoveDirection * effectiveSpeed * 10f, ForceMode.Force);

            // Apply downward force against slope normal to stay attached when moving downhill
            rb.AddForce(-slopeHit.normal * slopeStickForce, ForceMode.Force);
        }
        else if (isGrounded)
        {
            rb.AddForce(moveDirection.normalized * effectiveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            // เดินอากาศได้แต่แรงน้อยลง
            rb.AddForce(moveDirection.normalized * effectiveSpeed * 10f * airMultiplier, ForceMode.Force);
        }
    }

    private bool OnSlope()
    {
        // Slightly increased ray length (+0.5f) to ensure ground contact when running downhill
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.5f, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0f;
        }
        return false;
    }

    private void TryJump()
    {
        bool canControl = StateManager.Instance == null || StateManager.Instance.CanControlPlayer();
        if (!canControl || !isGrounded || !readyToJump) return;

        readyToJump = false;

        // Re-enable gravity before jumping
        rb.useGravity = true;

        // Reset Y velocity for consistent jump height
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
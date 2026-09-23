using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerMovement : MonoBehaviour
{
    public event Action<float> OnSpeedChanged;
    public event Action OnJumped;

    [Header("References")]
    public Transform orientation;
    public Rigidbody rb;

    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float groundDrag = 5f;
    public float airMultiplier = 0.4f;

    [Header("Input Smoothing")]
    [Tooltip("ความเร็วในการเร่งตอนเริ่มเดิน (ยิ่งมากยิ่งออกตัวไว)")]
    public float inputAcceleration = 15f;

    [Tooltip("ความเร็วในการเบรกตอนปล่อยปุ่ม (ยิ่งมากยิ่งหยุดทันที ไม่ไถล)")]
    public float stopDeceleration = 30f;

    private Vector2 smoothedInputVector; // ตัวแปรเก็บค่า Input ที่ผ่านการเกลี่ยแล้ว

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
    public float maxSlopeAngle = 45f;
    public float slopeStickForce = 10f;
    private RaycastHit slopeHit;

    [Header("Input References")]
    public InputActionReference moveInput;
    public InputActionReference jumpInput;

    [Header("Sprint")]
    [Tooltip("drag PlayerSprint component into this box")]
    public PlayerSprint playerSprint;

    [Header("Crouch")]
    public PlayerCrouch playerCrouch;
    [Range(0.1f, 1f)]
    public float crouchSpeedMultiplier = 0.5f;

    private Vector2 inputVector;
    public Vector2 CurrentInput => inputVector;
    private Vector3 moveDirection;
    private float currentEffectiveSpeed;

    private void Start()
    {
        Debug.Log(Application.persistentDataPath);
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        rb.freezeRotation = true;

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

        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, groundLayer);
        bool canControl = StateManager.Instance == null || StateManager.Instance.CanControlPlayer();

        // 1. อ่านค่า Input ดิบ
        Vector2 targetInputVector = canControl ? moveInput.action.ReadValue<Vector2>() : Vector2.zero;

        // 2. เช็คว่ากำลังกดเดินอยู่ หรือกำลังปล่อยปุ่ม
        // ถ้ากำลังกด ให้ใช้ความเร็วเร่ง (inputAcceleration) แต่ถ้าปล่อยปุ่ม ให้ใช้ความเร็วเบรก (stopDeceleration)
        float currentSmoothSpeed = (targetInputVector.sqrMagnitude > 0.01f) ? inputAcceleration : stopDeceleration;

        // เกลี่ยค่า Input
        smoothedInputVector = Vector2.Lerp(smoothedInputVector, targetInputVector, Time.deltaTime * currentSmoothSpeed);
        inputVector = smoothedInputVector;

        bool crouchingNow = playerCrouch != null && playerCrouch.IsCrouching;
        bool isSprintingNow = playerSprint != null && playerSprint.IsSprinting;

        float speedRatio = 1f;
        if (crouchingNow)
        {
            speedRatio = crouchSpeedMultiplier;
        }
        else if (isSprintingNow && playerSprint != null && playerSprint.WalkSpeed > 0f)
        {
            speedRatio = moveSpeed / playerSprint.WalkSpeed;
        }

        float effectiveSpeedNormalized = inputVector.magnitude * speedRatio;
        OnSpeedChanged?.Invoke(effectiveSpeedNormalized);

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

        StateManager.Instance.SetCrouch(playerCrouch != null && playerCrouch.IsCrouching);

        bool isSprinting = playerSprint != null && playerSprint.IsSprinting;

        if (inputVector.sqrMagnitude > 0.01f)
        {
            bool canRun = isSprinting && !(playerCrouch != null && playerCrouch.IsCrouching);
            StateManager.Instance.SetMovementState(canRun ? StateManager.MovementState.Running : StateManager.MovementState.Walking);
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
        SpeedControl();
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * inputVector.y + orientation.right * inputVector.x;

        float effectiveSpeed = moveSpeed;
        if (playerCrouch != null && playerCrouch.IsCrouching)
        {
            effectiveSpeed *= crouchSpeedMultiplier;
        }
        currentEffectiveSpeed = effectiveSpeed;

        bool onSlope = isGrounded && OnSlope();
        rb.useGravity = !onSlope;

        if (onSlope)
        {
            Vector3 slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
            rb.AddForce(slopeMoveDirection * effectiveSpeed * 10f, ForceMode.Force);
            rb.AddForce(-slopeHit.normal * slopeStickForce, ForceMode.Force);
        }
        else if (isGrounded)
        {
            rb.AddForce(moveDirection.normalized * effectiveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            rb.AddForce(moveDirection.normalized * effectiveSpeed * 10f * airMultiplier, ForceMode.Force);
        }
    }

    private void SpeedControl()
    {
        if (isGrounded && OnSlope())
        {
            if (rb.linearVelocity.magnitude > currentEffectiveSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * currentEffectiveSpeed;
            }
        }
        else
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.magnitude > currentEffectiveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * currentEffectiveSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }
    }

    private bool OnSlope()
    {
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

        rb.useGravity = true;
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
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

    [Header("Photograph")]
    [Tooltip("ตัวคูณความเร็วเดินตอนอยู่โหมดถ่ายรูป (เทียบกับ Move Speed) ยิ่งน้อยยิ่งช้า — แก้ความเร็วเดินตอนถ่ายรูปที่ช่องนี้")]
    [Range(0.05f, 1f)]
    public float photoSpeedMultiplier = 0.3f;

    /// <summary>เดินช้าอยู่ไหม — ตอนย่อ หรืออยู่ในโหมดถ่ายรูป</summary>
    public bool IsSlowWalking => SpeedMultiplier < 1f;

    /// <summary>
    /// ตัวคูณความเร็วเดินจากท่าทาง: ย่อ = crouchSpeedMultiplier, โหมดถ่ายรูป = photoSpeedMultiplier
    /// ทั้งสองอย่างพร้อมกันใช้ค่าที่ช้ากว่า (ไม่คูณซ้อน)
    /// </summary>
    public float SpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            if (playerCrouch != null && playerCrouch.IsCrouching) multiplier = Mathf.Min(multiplier, crouchSpeedMultiplier);
            if (StateManager.Instance != null && StateManager.Instance.IsSystemState(StateManager.SystemState.Photograph))
                multiplier = Mathf.Min(multiplier, photoSpeedMultiplier);
            return multiplier;
        }
    }

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
        // เดินได้ทั้งโหมดปกติและโหมดถ่ายรูป (โหมดถ่ายรูปเดินได้แต่ช้า) ส่วนวิ่ง/กระโดดยังใช้ CanControlPlayer เหมือนเดิม
        bool canWalk = StateManager.Instance == null || StateManager.Instance.CanWalk();
        inputVector = canWalk ? moveInput.action.ReadValue<Vector2>() : Vector2.zero;

        bool isSprintingNow = playerSprint != null && playerSprint.IsSprinting;

        float speedRatio = 1f;
        if (IsSlowWalking)
        {
            speedRatio = SpeedMultiplier;
        }
        else if (isSprintingNow && playerSprint != null && playerSprint.WalkSpeed > 0f)
        {
            speedRatio = moveSpeed / playerSprint.WalkSpeed;
        }

        float effectiveSpeedNormalized = inputVector.magnitude * speedRatio;
        OnSpeedChanged?.Invoke(effectiveSpeedNormalized);

        // Collider ผู้เล่นไม่มีแรงเสียดทาน (ใช้ Physic Material แรงเสียดทาน 0) — drag เป็นตัวหยุดตัวเองทั้งบนพื้นราบและพื้นเอียง
        // เดิมบนพื้นเอียงไม่มี drag แต่พึ่งแรงเสียดทานกับ slopeStickForce ที่กดตัวลงพื้น ซึ่งแรงเสียดทาน (0.6 x 30N) มากกว่าแรงเดินตอนย่อ
        // ทำให้ย่อแล้วเดินขึ้นทางเอียงไม่ได้ และเดินปกติก็ขึ้นทางชัน ~35° ขึ้นไปไม่ได้ ทั้งที่ maxSlopeAngle ตั้งไว้ 45°
        rb.linearDamping = isGrounded ? groundDrag : 0f;

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
        if (IsSlowWalking)
        {
            effectiveSpeed *= SpeedMultiplier;
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
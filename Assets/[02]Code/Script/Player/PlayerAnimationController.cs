using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement playerMovement;
    public Transform orientation; // ลาก Transform ของ Orientation หรือ Camera มารองรับเพื่อเทียบมุมหัน

    [Header("Turn Settings")]
    [Tooltip("ความเร็วในการเกลี่ยค่า Turn ให้สมูท (ยิ่งเยอะยิ่งเปลี่ยนไว)")]
    public float turnSmoothSpeed = 10f;
    private float currentTurnValue = 0f;

    private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int TurnHash = Animator.StringToHash("Turn");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsCrouchHash = Animator.StringToHash("IsCrouch");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (orientation == null && playerMovement != null)
        {
            orientation = playerMovement.orientation;
        }
    }

    private void OnEnable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnSpeedChanged += HandleSpeedChanged;
            playerMovement.OnJumped += HandleJumped;
        }
        else
        {
            Debug.LogError("Didn't fill playerMovement in Inspector!");
        }
    }

    private void Update()
    {
        CalculateTurnParameter();
    }

    private void CalculateTurnParameter()
    {
        if (playerMovement == null || orientation == null) return;

        Vector2 inputVec = playerMovement.CurrentInput;
        float targetTurn = 0f;

        // ถ้ามีการกดปุ่มเดิน และตัวละครมีทิศทางการเคลื่อนที่
        if (inputVec.sqrMagnitude > 0.01f)
        {
            // คำนวณหาเวกเตอร์ทิศทางโลก (World Space) ที่ผู้เล่นต้องการจะไปตามปุ่มที่กด
            Vector3 moveDir = (orientation.forward * inputVec.y + orientation.right * inputVec.x).normalized;

            if (moveDir != Vector3.zero)
            {
                // หาความแตกต่างของมุมระหว่าง "หน้าที่ตัวละครหันไป" กับ "ทิศทางที่จะเดินไป"
                // ใช้ Vector3.SignedAngle เพื่อดูว่าเลี้ยวซ้าย (-) หรือเลี้ยวขวา (+)
                float angleDiff = Vector3.SignedAngle(transform.forward, moveDir, Vector3.up);

                // แปลงค่ามุมให้อยู่ในช่วงประมาณ -1 ถึง 1 (หรือปรับสเกลตามความเหมาะสมของอนิเมชั่น Turn ใน Blend Tree ของคุณ)
                // ปกติหากใช้มุม -180 ถึง 180 อาจจะกว้างไปสำหรับการ Blend ให้หารด้วย 90 หรือ 180 ตามสเกล Blend Tree
                targetTurn = angleDiff / 90f;
                targetTurn = Mathf.Clamp(targetTurn, -1f, 1f);
            }
        }

        // เกลี่ยค่า Turn ให้เปลี่ยนอย่างนุ่มนวล ไม่กระชาก
        currentTurnValue = Mathf.Lerp(currentTurnValue, targetTurn, Time.deltaTime * turnSmoothSpeed);

        // ส่งค่าเข้า Animator ตามชื่อพารามิเตอร์ Turn
        animator.SetFloat(TurnHash, currentTurnValue);
    }

    private void Start()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnMovementStateChanged += HandleMovementStateChanged;
            StateManager.Instance.OnCrouchChanged += HandleCrouchChanged;

            animator.SetBool(IsCrouchHash, StateManager.Instance.IsCrouching);
        }
        else
        {
            Debug.LogError("StateManager.Instance ยังเป็น null ตอน Start!");
        }
    }

    private void OnDisable()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnMovementStateChanged -= HandleMovementStateChanged;
            StateManager.Instance.OnCrouchChanged -= HandleCrouchChanged;
        }

        if (playerMovement != null)
        {
            playerMovement.OnSpeedChanged -= HandleSpeedChanged;
            playerMovement.OnJumped -= HandleJumped;
        }
    }

    private void HandleMovementStateChanged(StateManager.MovementState oldState, StateManager.MovementState newState)
    {
        animator.SetBool(IsWalkingHash, newState == StateManager.MovementState.Walking);
        animator.SetBool(IsRunningHash, newState == StateManager.MovementState.Running);
    }

    private void HandleCrouchChanged(bool isCrouching)
    {
        animator.SetBool(IsCrouchHash, isCrouching);
    }

    private void HandleSpeedChanged(float speed)
    {
        animator.SetFloat(SpeedHash, speed);
    }

    private void HandleJumped()
    {
        animator.SetTrigger(JumpHash);
    }
}
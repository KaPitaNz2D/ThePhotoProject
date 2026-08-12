using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag PlayerMovement into, for Speed/Jump")]
    public PlayerMovement playerMovement;

    private Animator animator;

    // Cache hash ไว้ล่วงหน้า ลด overhead จากการ hash string ทุกเฟรม
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsCrouchHash = Animator.StringToHash("IsCrouch");

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // Subscribe fromn PlayerMovement
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

    private void Start()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnMovementStateChanged += HandleMovementStateChanged;
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
        }

        if (playerMovement != null)
        {
            playerMovement.OnSpeedChanged -= HandleSpeedChanged;
            playerMovement.OnJumped -= HandleJumped;
        }
    }

    private void HandleMovementStateChanged(StateManager.MovementState oldState, StateManager.MovementState newState)
    {
        // prevent old bool state
        animator.SetBool(IsWalkingHash, newState == StateManager.MovementState.Walking);
        animator.SetBool(IsRunningHash, newState == StateManager.MovementState.Running);
        animator.SetBool(IsCrouchHash, newState == StateManager.MovementState.Crouch);
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
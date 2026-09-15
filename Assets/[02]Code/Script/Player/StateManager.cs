using System;
using UnityEngine;

public class StateManager : MonoBehaviour
{
    public static StateManager Instance { get; private set; }

    // ==================== State Definitions ====================
    // ✨ เอา Crouch ออกจาก enum นี้แล้ว เพราะ crouch ไม่ใช่ "ท่าเดิน" แต่เป็นคนละมิติที่ซ้อนกับ Idle/Walking/Running ได้
    public enum MovementState
    {
        Idle,
        Walking,
        Running
    }

    public enum SystemState
    {
        Normal,
        Photograph,
        Talking,
        Pause,
        Journal,
        Storage
    }

    [Header("Current States (Read-only ดูใน Inspector)")]
    [SerializeField] private MovementState currentMovementState = MovementState.Idle;
    [SerializeField] private SystemState currentSystemState = SystemState.Normal;
    [SerializeField] private bool isCrouching; // แยกจาก MovementState

    public MovementState CurrentMovementState => currentMovementState;
    public SystemState CurrentSystemState => currentSystemState;
    public bool IsCrouching => isCrouching;

    public MovementState PreviousMovementState { get; private set; }
    public SystemState PreviousSystemState { get; private set; }

    // ==================== Events ====================
    public event Action<MovementState, MovementState> OnMovementStateChanged;
    public event Action<SystemState, SystemState> OnSystemStateChanged;
    public event Action<bool> OnCrouchChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ==================== Movement State ====================
    public void SetMovementState(MovementState newState)
    {
        if (currentMovementState == newState) return;

        PreviousMovementState = currentMovementState;
        currentMovementState = newState;

        OnMovementStateChanged?.Invoke(PreviousMovementState, currentMovementState);
    }

    public bool IsMovementState(MovementState state) => currentMovementState == state;

    // ==================== Crouch (แยกใหม่) ====================
    public void SetCrouch(bool value)
    {
        if (isCrouching == value) return;

        isCrouching = value;
        OnCrouchChanged?.Invoke(isCrouching);
    }

    // ==================== System State ====================
    public void SetSystemState(SystemState newState)
    {
        if (currentSystemState == newState) return;

        PreviousSystemState = currentSystemState;
        currentSystemState = newState;

        OnSystemStateChanged?.Invoke(PreviousSystemState, currentSystemState);
    }

    public bool IsSystemState(SystemState state) => currentSystemState == state;

    public bool CanControlPlayer()
    {
        switch (currentSystemState)
        {
            case SystemState.Normal:
                return true;
            case SystemState.Photograph:
            case SystemState.Talking:
            case SystemState.Pause:
            case SystemState.Journal:
            case SystemState.Storage:
                return false;
            default:
                return false;
        }
    }

    public bool CanCrouch()
    {
        switch (currentSystemState)
        {
            case SystemState.Normal:
            case SystemState.Photograph:
                return true;
            case SystemState.Talking:
            case SystemState.Pause:
            case SystemState.Journal:
            case SystemState.Storage:
                return false;
            default:
                return false;
        }
    }
}
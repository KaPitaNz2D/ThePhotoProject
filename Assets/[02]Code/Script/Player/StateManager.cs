using System;
using UnityEngine;

/// <summary>
/// จัดการ State กลางของเกม แบ่งเป็น 2 กลุ่ม:
/// - MovementState : สถานะการเคลื่อนไหวของตัวละคร (ใช้กับ Animator เป็นหลัก)
/// - SystemState   : สถานะระบบเกมโดยรวม (ใช้คุม Input/UI/Gameplay flow)
///
/// ระบบอื่นๆ (Animation, PlayerMovement, UI) ไม่จำเป็นต้อง Reference กันตรงๆ
/// แค่ Subscribe Event จากตัวนี้แทน ทำให้เพิ่ม/แก้ State ในอนาคตได้ง่าย
/// </summary>
public class StateManager : MonoBehaviour
{
    public static StateManager Instance { get; private set; }

    // ==================== State Definitions ====================
    // เพิ่ม State ใหม่ในอนาคตแค่เติมเข้าไปใน enum ตรงนี้ได้เลย
    public enum MovementState
    {
        Idle,
        Walking,
        Running,
        Crouch
    }

    public enum SystemState
    {
        Normal,      // เล่นปกติ
        Photograph,  // โหมดถ่ายรูป
        Talking,     // คุยกับ NPC
        Pause,       // เมนู Option
        Journal      // เปิดสมุดบันทึก
    }

    [Header("Current States (Read-only ดูใน Inspector)")]
    [SerializeField] private MovementState currentMovementState = MovementState.Idle;
    [SerializeField] private SystemState currentSystemState = SystemState.Normal;

    public MovementState CurrentMovementState => currentMovementState;
    public SystemState CurrentSystemState => currentSystemState;

    public MovementState PreviousMovementState { get; private set; }
    public SystemState PreviousSystemState { get; private set; }

    // ==================== Events ====================
    // ระบบอื่นเช่น Animator Controller หรือ PlayerMovement มา Subscribe ตรงนี้
    // ตัวอย่าง: StateManager.Instance.OnMovementStateChanged += HandleMovementChanged;
    public event Action<MovementState, MovementState> OnMovementStateChanged; // (old, new)
    public event Action<SystemState, SystemState> OnSystemStateChanged;       // (old, new)

    private void Awake()
    {
        // ป้องกันการมี StateManager ซ้ำซ้อนในซีน
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // ถ้าอยากให้คงอยู่ข้ามซีน ให้เปิดบรรทัดนี้
        // DontDestroyOnLoad(gameObject);
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

    // ==================== System State ====================
    public void SetSystemState(SystemState newState)
    {
        if (currentSystemState == newState) return;

        PreviousSystemState = currentSystemState;
        currentSystemState = newState;

        OnSystemStateChanged?.Invoke(PreviousSystemState, currentSystemState);
    }

    public bool IsSystemState(SystemState state) => currentSystemState == state;

    /// <summary>
    /// Helper สำหรับเช็คว่าตอนนี้ควรให้ผู้เล่นควบคุมตัวละครได้ปกติหรือไม่
    /// เช่น ระบบ PlayerMovement เรียกเช็คก่อนรับ Input ทุกครั้ง
    /// เพิ่ม/ลด SystemState ที่ควร "บล็อกการเคลื่อนที่" ได้ตรงนี้ที่เดียว
    /// </summary>
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
                return false;
            default:
                return false;
        }
    }
}
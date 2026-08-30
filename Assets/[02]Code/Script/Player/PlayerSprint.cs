using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// เพิ่มระบบวิ่ง (Sprint) ให้ PlayerMovement โดยไม่ต้องแก้ Logic การเคลื่อนที่เดิม
/// วิธีใช้: ลาก Component นี้ไปแปะที่ตัว Player เดียวกับ PlayerMovement
/// แล้วผูก Input Action (เช่น Left Shift / Right Trigger) เข้ากับ sprintInput
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerSprint : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement playerMovement;

    [Header("Input")]
    [Tooltip("ลาก Input Action ของปุ่มวิ่ง (เช่น Shift) มาใส่ช่องนี้")]
    public InputActionReference sprintInput;

    [Header("Sprint Settings")]
    [Tooltip("ความเร็วตอนวิ่ง (จะ override moveSpeed ของ PlayerMovement ชั่วคราว)")]
    public float sprintSpeed = 10f;

    [Tooltip("ความเร็วในการไล่ปรับ (Lerp) จาก Walk ไป Sprint และกลับ ยิ่งมากยิ่งเปลี่ยนเร็ว")]
    public float speedTransitionRate = 8f;

    [Header("Conditions")]
    [Tooltip("อนุญาตให้วิ่งเฉพาะตอนติดพื้นเท่านั้น (ปิดไว้ถ้าอยากวิ่งกลางอากาศได้ด้วย)")]
    public bool requireGrounded = true;

    [Tooltip("อนุญาตให้วิ่งเฉพาะตอนกำลังเดินไปข้างหน้า (มีการกดปุ่มเคลื่อนที่)")]
    public bool requireMovingInput = true;

    [Header("Optional: Stamina")]
    [Tooltip("เปิดใช้ระบบ Stamina จำกัดเวลาวิ่ง")]
    public bool useStamina = false;
    public float maxStamina = 5f;
    public float staminaDrainRate = 1f;
    public float staminaRegenRate = 0.75f;
    [Range(0f, 1f)]
    [Tooltip("ต้องมี Stamina อย่างน้อยกี่ % ถึงจะเริ่มวิ่งได้อีกครั้งหลัง Stamina หมด")]
    public float staminaRegenThreshold = 0.3f;

    public float CurrentStamina { get; private set; }
    public bool IsSprinting { get; private set; }

    private float walkSpeed;      // ค่า moveSpeed เดิมของ PlayerMovement ตอนเริ่มเกม
    private bool sprintHeld;
    private bool staminaLocked;   // true = stamina หมดแล้ว ต้องรอ regen ถึง threshold ก่อนวิ่งใหม่ได้

    private void Start()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement == null)
        {
            Debug.LogError("[PlayerSprint] ไม่พบ PlayerMovement บน GameObject นี้!");
            enabled = false;
            return;
        }

        walkSpeed = playerMovement.moveSpeed;
        CurrentStamina = maxStamina;

        if (sprintInput != null)
        {
            sprintInput.action.Enable();
            sprintInput.action.performed += OnSprintPerformed;
            sprintInput.action.canceled += OnSprintCanceled;
        }
        else
        {
            Debug.LogError("[PlayerSprint] ยังไม่ได้ใส่ sprintInput ใน Inspector!");
        }
    }

    private void OnDestroy()
    {
        if (sprintInput != null)
        {
            sprintInput.action.performed -= OnSprintPerformed;
            sprintInput.action.canceled -= OnSprintCanceled;
        }
    }

    private void OnSprintPerformed(InputAction.CallbackContext ctx) => sprintHeld = true;
    private void OnSprintCanceled(InputAction.CallbackContext ctx) => sprintHeld = false;

    private void Update()
    {
        if (playerMovement == null) return;

        bool canControl = StateManager.Instance == null || StateManager.Instance.CanControlPlayer();

        bool wantsSprint = sprintHeld && canControl;

        if (requireGrounded)
            wantsSprint &= playerMovement.IsGrounded;

        if (requireMovingInput)
            wantsSprint &= playerMovement.CurrentInput.sqrMagnitude > 0.01f;

        if (useStamina)
        {
            wantsSprint = HandleStamina(wantsSprint);
        }

        IsSprinting = wantsSprint;

        // ไล่ปรับ moveSpeed แบบ smooth แทนการสลับค่าแบบทันที
        // เพื่อไม่ให้ Rigidbody กระตุกตอนเริ่ม/หยุดวิ่ง
        float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
        playerMovement.moveSpeed = Mathf.Lerp(
            playerMovement.moveSpeed,
            targetSpeed,
            Time.deltaTime * speedTransitionRate
        );

        // แจ้ง StateManager ว่ากำลังวิ่งอยู่ (ถ้ามี Running state ในระบบ ให้ไปเพิ่ม case ใน StateManager เอง)
        UpdateMovementState();
    }

    private bool HandleStamina(bool wantsSprint)
    {
        if (wantsSprint && !staminaLocked && CurrentStamina > 0f)
        {
            CurrentStamina -= staminaDrainRate * Time.deltaTime;
            if (CurrentStamina <= 0f)
            {
                CurrentStamina = 0f;
                staminaLocked = true; // หมดแล้ว ต้อง regen ถึง threshold ก่อนถึงจะวิ่งได้อีก
            }
            return true;
        }

        // ไม่ได้วิ่ง หรือ Stamina หมด -> regen
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenRate * Time.deltaTime);

        if (staminaLocked && CurrentStamina >= maxStamina * staminaRegenThreshold)
            staminaLocked = false;

        return false;
    }

    private void UpdateMovementState()
    {
        if (StateManager.Instance == null) return;

        // หมายเหตุ: ตอนนี้ StateManager.MovementState ยังไม่มี "Running"
        // ถ้าต้องการแยก Animation/State ของการวิ่ง ให้เพิ่ม enum ค่า Running
        // ใน StateManager แล้วเปลี่ยนบรรทัดด้านล่างเป็น:
        //
        // if (IsSprinting)
        //     StateManager.Instance.SetMovementState(StateManager.MovementState.Running);
    }
}

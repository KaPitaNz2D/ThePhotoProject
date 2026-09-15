using UnityEngine;

/// <summary>
/// ฟังเปลี่ยน State จาก CreatureAI แล้วสั่ง Animator (บน child mesh) ให้เล่นท่าตาม
/// แยกออกมาเป็นคนละ component เพราะ Animator อยู่บน GameObject ลูก (Deer_Testing_Animation)
/// ไม่ใช่ตัวเดียวกับที่ติด CreatureAI — ไม่ผูก RequireComponent(Animator) ตรงๆ จึงต้องหาเองผ่าน GetComponentInChildren
///
/// ต้องมี Parameter ใน Animator Controller ตรงชื่อนี้: State (Int), Speed (Float)
/// </summary>
[RequireComponent(typeof(CreatureAI))]
public class CreatureAnimatorController : MonoBehaviour
{
    private static readonly int StateParam = Animator.StringToHash("State");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    [Header("Animator Target")]
    [SerializeField]private Animator animator;
    private CreatureAI ai;

    private void Awake()
    {
        ai = GetComponent<CreatureAI>();
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError($"[CreatureAnimatorController] {gameObject.name} หา Animator ใน children ไม่เจอ " +
                            "เช็คว่า mesh ลูกยังติด Animator component อยู่ไหม");
        }
    }

    private void OnEnable() => ai.OnStateChanged += HandleStateChanged;
    private void OnDisable() => ai.OnStateChanged -= HandleStateChanged;

    private void HandleStateChanged(CreatureAI.CreatureState oldState, CreatureAI.CreatureState newState)
    {
        if (animator == null) return;

        animator.SetInteger(StateParam, MapToAnimatorState(newState));
        animator.SetFloat(SpeedParam, GetSpeedMultiplier(newState));
    }

    // แม็พเองตรงๆ ไม่ cast enum เป็น int เพราะ Animator Controller ผูกเลข State ไว้ตายตัว
    // ถ้าใครแก้ลำดับ enum ใน CreatureAI ภายหลัง การ cast ตรงๆ จะทำให้ Animator เล่นท่าผิดโดยไม่มี error เตือน
    private int MapToAnimatorState(CreatureAI.CreatureState state)
    {
        switch (state)
        {
            case CreatureAI.CreatureState.Idle: return 0;
            case CreatureAI.CreatureState.Walking: return 1;
            case CreatureAI.CreatureState.Alert: return 2;
            case CreatureAI.CreatureState.Run: return 3;
            default: return 0;
        }
    }

    // Run ใช้ clip เดินตัวเดียวกับ Walk (ยังไม่มี clip วิ่งจริง) เลยเร่ง playback speed แทนตามอัตราส่วน runSpeed/walkSpeed
    private float GetSpeedMultiplier(CreatureAI.CreatureState state)
    {
        if (ai.profile == null || ai.profile.walkSpeed <= 0f) return 1f;

        if (state == CreatureAI.CreatureState.Run)
            return ai.profile.runSpeed / ai.profile.walkSpeed;

        return 1f;
    }
}

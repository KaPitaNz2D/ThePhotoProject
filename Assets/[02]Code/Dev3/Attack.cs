using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;

    [Header("Input Setup")]
    // กำหนดปุ่มคลิกซ้าย (<Mouse>/leftButton) ไว้ในโค้ดโดยตรง
    public InputAction attackAction = new InputAction("Attack", binding: "<Mouse>/leftButton");

    private void OnEnable()
    {
        // เปิดรับ Input และผูก Event เมื่อมีการกดปุ่ม
        attackAction.Enable();
        attackAction.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        // ปิดรับ Input และยกเลิก Event เมื่อ Object ถูกปิดการทำงาน
        attackAction.Disable();
        attackAction.performed -= OnAttackPerformed;
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (animator != null)
        {
            // สั่งเล่น Animation โดยใช้ Trigger ชื่อ "Attack"
            animator.SetTrigger("Attack");
        }
    }
}
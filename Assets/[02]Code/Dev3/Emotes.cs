using UnityEngine;
using UnityEngine.InputSystem; // เรียกใช้งาน New Input System

public class EmoteDirectInput : MonoBehaviour
{
    [SerializeField] private Animator animator;

    void Update()
    {
        // ตรวจสอบว่าคีย์บอร์ดพร้อมใช้งานไหม
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // เช็คการกดปุ่ม 1, 2, 3 ด้วย New Input System
        if (kb.digit1Key.wasPressedThisFrame)
        {
            TriggerEmote(1);
        }
        else if (kb.digit2Key.wasPressedThisFrame)
        {
            TriggerEmote(2);
        }
        else if (kb.digit3Key.wasPressedThisFrame)
        {
            TriggerEmote(3);
        }
    }

    private void TriggerEmote(int index)
    {
        animator.SetInteger("Emote_index", index);
        animator.SetTrigger("PlayEmote");
        Debug.Log("Play Emote: " + index);
    }
}
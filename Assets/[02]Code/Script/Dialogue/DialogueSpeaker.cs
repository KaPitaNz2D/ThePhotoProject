using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ติดไว้ที่ตัวละครที่พูดใน Yarn ได้ (NPC / ผู้เล่น) — SpeechBubblePresenter หาตัวพูดจากชื่อหน้าบรรทัด (เช่น "NPC: ...")
/// แล้วเอาตำแหน่งหัวของตัวนี้ไปให้หางบับเบิลชี้
/// </summary>
public class DialogueSpeaker : MonoBehaviour
{
    private static readonly List<DialogueSpeaker> activeSpeakers = new List<DialogueSpeaker>();

    [Tooltip("ต้องตรงกับชื่อหน้าบรรทัดในไฟล์ .yarn เช่น NPC หรือ Player (ไม่สนตัวพิมพ์เล็ก/ใหญ่)")]
    public string characterName = "NPC";
    [Tooltip("จุดหัวที่หางบับเบิลจะชี้ไป ถ้าว่างไว้จะใช้ Head bone ของ Animator (Humanoid) แทน ถ้าไม่มีอีกก็ใช้ตำแหน่งตัวเอง")]
    public Transform headAnchor;
    [Tooltip("ยกจุดที่หางชี้ขึ้นจากหัว (เมตร) กันบับเบิลบังหน้า")]
    public float headOffset = 0.2f;

    private void OnEnable() => activeSpeakers.Add(this);
    private void OnDisable() => activeSpeakers.Remove(this);

    public Vector3 HeadPosition
    {
        get
        {
            if (headAnchor == null)
            {
                // หาแบบ Lazy เพราะ Animator อาจยังไม่พร้อมตอน Awake
                Animator animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman) headAnchor = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            Vector3 head = headAnchor != null ? headAnchor.position : transform.position;
            return head + Vector3.up * headOffset;
        }
    }

    public static DialogueSpeaker Find(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return null;
        foreach (DialogueSpeaker speaker in activeSpeakers)
        {
            if (string.Equals(speaker.characterName, characterName, StringComparison.OrdinalIgnoreCase)) return speaker;
        }
        return null;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

/// <summary>
/// จัดการสถานะ Quest ของแต่ละสิ่งมีชีวิต/พืช (ผูกด้วย creatureId เดียวกับระบบ Photo/Journal ทั้งหมด)
/// เชื่อมกับ Yarn Spinner ผ่าน [YarnCommand]/[YarnFunction] ให้ Node เรียกใช้ได้ตรงๆ ไม่ต้องเขียน Bridge เพิ่ม
///
/// ตาม Quest Diagram:
///   คุยกับ NPC -> start_quest (ขึ้นจุดแดงใน Journal, ไม่บล็อกการไปถ่ายรูปก่อนเลย)
///   ถ่ายรูปแล้ว (เช็คผ่าน has_photo) -> ยังไม่ Complete จนกว่าจะกลับมาคุยกับ NPC
///   กลับมาคุยกับ NPC -> complete_quest (จุดแดงหาย, Journal ปลดล็อกข้อมูลเพิ่มเติม)
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public enum QuestStatus { NotStarted, Active, Completed }

    [Header("Data Source")]
    [Tooltip("ใช้เช็คว่าผู้เล่นถ่ายรูปสิ่งมีชีวิตนั้นๆ มาแล้วหรือยัง")]
    public JournalManager journalManager;

    private Dictionary<string, QuestStatus> questStates = new Dictionary<string, QuestStatus>();

    // creatureId ที่ NPC เคยเล่าข้อมูลให้ฟังแล้ว (ผ่าน mark_informed ใน Yarn) — Journal ใช้เช็คก่อนโชว์คำอธิบาย
    // ตั้งใจแยกจาก has_photo เพราะ "มีรูป" กับ "ได้ฟังความรู้จาก NPC แล้ว" เป็นคนละเงื่อนไขกัน
    private HashSet<string> informedCreatureIds = new HashSet<string>();

    /// <summary>ยิงทุกครั้งที่ Quest เปลี่ยนสถานะ (creatureId, สถานะใหม่) — JournalUI มา Subscribe อัพเดทจุดแดงได้</summary>
    public event Action<string, QuestStatus> OnQuestStatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // journalManager ถูกผูกไว้ตอน Edit-time กับ Instance ในซีนเดิม พอ Reload Scene (เช่นจาก Debug Reset)
    // Instance เดิมถูกทำลายไปแล้วแต่ QuestManager (DontDestroyOnLoad) ยังถือ Reference ค้างอยู่
    // เลยต้องหา JournalManager ตัวใหม่ในซีนที่เพิ่งโหลดมาผูกใหม่ทุกครั้ง กัน HasPhoto() คืนค่า false ค้างตลอดไป
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        JournalManager foundJournalManager = FindFirstObjectByType<JournalManager>();
        if (foundJournalManager != null)
        {
            journalManager = foundJournalManager;
        }
    }

    /// <summary>รีเซ็ตสถานะ Quest + ความรู้ที่ NPC เคยเล่าให้ฟังทั้งหมด — ใช้กับ Debug Reset เพื่อเริ่มใหม่โดยไม่ต้องปิดเกม</summary>
    public void ResetAllQuests()
    {
        questStates.Clear();
        informedCreatureIds.Clear();
    }

    public QuestStatus GetStatus(string creatureId)
    {
        if (string.IsNullOrEmpty(creatureId)) return QuestStatus.NotStarted;
        return questStates.TryGetValue(creatureId, out QuestStatus status) ? status : QuestStatus.NotStarted;
    }

    /// <summary>เรียกจาก Yarn Node: &lt;&lt;start_quest "deer_01"&gt;&gt;</summary>
    [YarnCommand("start_quest")]
    public static void StartQuest(string creatureId)
    {
        if (Instance == null || string.IsNullOrEmpty(creatureId)) return;
        if (Instance.GetStatus(creatureId) != QuestStatus.NotStarted) return; // กันรับซ้ำ/รับทับ Quest ที่เสร็จไปแล้ว

        Instance.questStates[creatureId] = QuestStatus.Active;
        Instance.OnQuestStatusChanged?.Invoke(creatureId, QuestStatus.Active);
    }

    /// <summary>เรียกจาก Yarn Node: &lt;&lt;complete_quest "deer_01"&gt;&gt;</summary>
    [YarnCommand("complete_quest")]
    public static void CompleteQuest(string creatureId)
    {
        if (Instance == null || string.IsNullOrEmpty(creatureId)) return;
        if (Instance.GetStatus(creatureId) != QuestStatus.Active) return; // ต้อง Active อยู่ก่อนถึงจะ Complete ได้

        Instance.questStates[creatureId] = QuestStatus.Completed;
        Instance.OnQuestStatusChanged?.Invoke(creatureId, QuestStatus.Completed);
    }

    /// <summary>เรียกจาก Yarn Node: &lt;&lt;if has_photo("deer_01")&gt;&gt; — เช็คว่าถ่ายรูปมาแล้วหรือยัง</summary>
    [YarnFunction("has_photo")]
    public static bool HasPhoto(string creatureId)
    {
        if (Instance == null || Instance.journalManager == null || string.IsNullOrEmpty(creatureId)) return false;

        foreach (JournalEntry entry in Instance.journalManager.allEntries)
        {
            if (entry != null && entry.creatureId == creatureId)
            {
                return Instance.journalManager.IsUnlocked(entry);
            }
        }
        return false;
    }

    /// <summary>เรียกจาก Yarn Node: &lt;&lt;if quest_status("deer_01") == "Active"&gt;&gt;</summary>
    [YarnFunction("quest_status")]
    public static string GetStatusYarn(string creatureId)
    {
        if (Instance == null) return QuestStatus.NotStarted.ToString();
        return Instance.GetStatus(creatureId).ToString();
    }

    /// <summary>เรียกจาก Yarn Node หลัง NPC เล่าข้อมูลของสิ่งนั้นให้ฟังจบแล้ว: &lt;&lt;mark_informed "Chan"&gt;&gt;</summary>
    [YarnCommand("mark_informed")]
    public static void MarkInformed(string creatureId)
    {
        if (Instance == null || string.IsNullOrEmpty(creatureId)) return;
        Instance.informedCreatureIds.Add(creatureId);
    }

    /// <summary>เรียกจาก Yarn Node: &lt;&lt;if is_informed("Chan")&gt;&gt; — กันไม่ให้ NPC เล่าเรื่องเดิมซ้ำอีก</summary>
    [YarnFunction("is_informed")]
    public static bool IsInformedYarn(string creatureId)
    {
        return Instance != null && Instance.IsInformed(creatureId);
    }

    /// <summary>ใช้จาก Journal (C#) เช็คว่า NPC เคยเล่าข้อมูลของสิ่งนี้ให้ฟังแล้วหรือยัง ก่อนจะโชว์คำอธิบาย</summary>
    public bool IsInformed(string creatureId)
    {
        return !string.IsNullOrEmpty(creatureId) && informedCreatureIds.Contains(creatureId);
    }
}
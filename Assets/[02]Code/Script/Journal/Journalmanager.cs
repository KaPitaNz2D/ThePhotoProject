using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ศูนย์กลางของระบบสมุดบันทึก:
///   - รับรายชื่อ JournalEntry ทั้งหมดจาก Inspector (ลาก Asset ScriptableObject เข้ามา จัดการจากภายนอกได้เต็มที่)
///   - ฟัง PhotoShooter.OnPhotoCaptured แล้วจับคู่ผ่าน creatureId เพื่อปลดล็อก Entry ที่ถูกต้อง
///   - จัดหน้าสมุดแบบกางสองหน้า (ซ้าย/ขวา) และมีฟังก์ชันเปลี่ยนหน้าที่เรียกจาก UI ปุ่มข้างนอกได้
/// </summary>
public class JournalManager : MonoBehaviour
{
    [Header("Data — ลาก JournalEntry Asset ทั้งหมดมาใส่ตรงนี้ เรียงลำดับหน้าตามที่ต้องการ")]
    public List<JournalEntry> allEntries = new List<JournalEntry>();

    [Header("UI References — หน้ากระดาษซ้าย/ขวาของสมุดที่กางอยู่")]
    public JournalPageUI leftPage;
    public JournalPageUI rightPage;

    [Header("Photo Source")]
    [Tooltip("ถ้าไม่ลากใส่ไว้ จะหา PhotoShooter ตัวแรกที่เจอในซีนให้เอง")]
    public PhotoShooter photoShooter;

    private class RuntimeData
    {
        public bool isUnlocked;
        public Texture2D photo;
    }

    // เก็บสถานะปลดล็อก/ภาพล่าสุด แยกจาก JournalEntry (Asset) โดยตั้งใจ
    // เพราะ JournalEntry เป็นไฟล์ในโปรเจกต์ ไม่ควรเขียนทับข้อมูล Runtime ลงไปในนั้น
    private Dictionary<string, RuntimeData> runtimeStates = new Dictionary<string, RuntimeData>();

    // ดัชนีของ "คู่หน้า" ที่กำลังเปิดอยู่ (Spread 0 = Entry index 0-1, Spread 1 = Entry index 2-3, ...)
    private int currentSpreadIndex = 0;

    private void Awake()
    {
        foreach (JournalEntry entry in allEntries)
        {
            if (entry == null) continue;
            if (!runtimeStates.ContainsKey(entry.creatureId))
            {
                runtimeStates[entry.creatureId] = new RuntimeData { isUnlocked = false, photo = null };
            }
        }

        if (photoShooter == null)
        {
            photoShooter = FindFirstObjectByType<PhotoShooter>();
        }
    }

    private void OnEnable()
    {
        if (photoShooter != null)
        {
            photoShooter.OnPhotoCaptured += HandlePhotoCaptured;
        }
        else
        {
            Debug.LogError("[JournalManager] หา PhotoShooter ไม่เจอ! ลาก Reference ใส่เอง หรือเช็คว่ามีอยู่ในซีนหรือยัง");
        }
    }

    private void OnDisable()
    {
        if (photoShooter != null)
        {
            photoShooter.OnPhotoCaptured -= HandlePhotoCaptured;
        }
    }

    private void Start()
    {
        RefreshCurrentSpread();
    }

    // ==================== รับผลถ่ายรูปมาปลดล็อก Entry ====================
    private void HandlePhotoCaptured(Texture2D photo, List<GameObject> subjects)
    {
        bool anyUnlocked = false;

        foreach (GameObject subject in subjects)
        {
            PhotoSubject photoSubject = subject.GetComponent<PhotoSubject>();
            if (photoSubject == null) continue;

            if (runtimeStates.TryGetValue(photoSubject.creatureId, out RuntimeData data))
            {
                data.isUnlocked = true;
                data.photo = photo; // ถ่ายซ้ำจะอัพเดทเป็นภาพล่าสุดเสมอ
                anyUnlocked = true;
            }
            else
            {
                // ถ่ายติดวัตถุที่มี PhotoSubject แต่ creatureId ไม่ตรงกับ Entry ไหนในสมุดเลย
                Debug.LogWarning($"[JournalManager] creatureId '{photoSubject.creatureId}' ไม่มี Entry ในสมุด " +
                                  "ลืมสร้าง JournalEntry หรือพิมพ์ creatureId ไม่ตรงกันหรือเปล่า?");
            }
        }

        if (anyUnlocked)
        {
            RefreshCurrentSpread(); // เผื่อเปิดสมุดค้างอยู่ตอนถ่ายติดพอดี จะได้เห็นผลทันที
        }
    }

    // ==================== เปิดหน้าไปหา Entry ที่ต้องการ (เรียกจากภายนอกได้) ====================
    /// <summary>เปิดสมุดไปหน้าของ creatureId ที่ระบุทันที เช่นเรียกตอนถ่ายรูปสำเร็จแล้วอยากโชว์หน้านั้นเลย</summary>
    public void OpenToEntry(string creatureId)
    {
        int index = allEntries.FindIndex(e => e != null && e.creatureId == creatureId);
        if (index < 0) return;

        currentSpreadIndex = index / 2;
        RefreshCurrentSpread();
    }

    // ==================== เปลี่ยนหน้า (ผูกกับปุ่ม UI ข้างนอกได้ตรงๆ ผ่าน OnClick) ====================
    public void NextSpread()
    {
        int maxSpread = allEntries.Count > 0 ? (allEntries.Count - 1) / 2 : 0;
        currentSpreadIndex = Mathf.Clamp(currentSpreadIndex + 1, 0, maxSpread);
        RefreshCurrentSpread();
    }

    public void PreviousSpread()
    {
        currentSpreadIndex = Mathf.Max(currentSpreadIndex - 1, 0);
        RefreshCurrentSpread();
    }

    // ==================== วาดหน้าปัจจุบันลง UI ====================
    private void RefreshCurrentSpread()
    {
        int leftIndex = currentSpreadIndex * 2;
        int rightIndex = leftIndex + 1;

        ApplyEntryToPage(leftPage, leftIndex);
        ApplyEntryToPage(rightPage, rightIndex);
    }

    private void ApplyEntryToPage(JournalPageUI page, int entryIndex)
    {
        if (page == null) return;

        if (entryIndex < 0 || entryIndex >= allEntries.Count || allEntries[entryIndex] == null)
        {
            page.ShowEmpty();
            return;
        }

        JournalEntry entry = allEntries[entryIndex];
        bool isUnlocked = runtimeStates.TryGetValue(entry.creatureId, out RuntimeData data) && data.isUnlocked;

        if (isUnlocked)
        {
            page.ShowUnlocked(entry, data.photo);
        }
        else
        {
            page.ShowLocked(entry);
        }
    }
}
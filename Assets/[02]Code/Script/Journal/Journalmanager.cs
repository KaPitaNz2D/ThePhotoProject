using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// เก็บรายชื่อ JournalEntry ทั้งหมด (ตั้งค่าจาก Inspector) และเป็นคนกลางดึงข้อมูล/ภาพจาก PhotoStorage
/// มาจับคู่กับ Entry แต่ละตัวผ่าน creatureId
///
/// สำคัญ: พอปลดล็อก Entry ไหนครั้งแรก จะ "ทำสำเนา Texture2D เก็บไว้ถาวรในตัวเอง" ทันที
/// (Subscribe PhotoStorage.OnPhotoAdded) ไม่อ้างอิงไฟล์ใน Storage อีกเลยหลังจากนั้น
/// ต่อให้ผู้เล่นไปลบภาพต้นฉบับทิ้งจาก Storage ทีหลัง Journal จะไม่หายตามเด็ดขาด
/// </summary>
public class JournalManager : MonoBehaviour
{
    [Header("Data — ลาก JournalEntry Asset ทั้งหมดมาใส่ตรงนี้")]
    public List<JournalEntry> allEntries = new List<JournalEntry>();

    // สำเนาถาวรของภาพที่ปลดล็อกแล้ว (creatureId -> Texture2D ของ Journal เอง ไม่ใช่ตัวเดียวกับ Storage)
    private Dictionary<string, Texture2D> unlockedPhotos = new Dictionary<string, Texture2D>();

    private void Start()
    {
        if (PhotoStorage.Instance != null)
        {
            PhotoStorage.Instance.OnPhotoAdded += HandlePhotoAdded;
        }
        else
        {
            Debug.LogError("[JournalManager] หา PhotoStorage.Instance ไม่เจอ! วาง GameObject PhotoStorage ไว้ในซีนหรือยัง");
        }
    }

    private void OnDestroy()
    {
        if (PhotoStorage.Instance != null)
        {
            PhotoStorage.Instance.OnPhotoAdded -= HandlePhotoAdded;
        }

        // ทำลาย Texture2D ที่ค้างไว้ทั้งหมดตอนปิดเกม/เปลี่ยนซีน ป้องกัน RAM ค้าง
        foreach (Texture2D texture in unlockedPhotos.Values)
        {
            if (texture != null) Destroy(texture);
        }
        unlockedPhotos.Clear();
    }

    /// <summary>
    /// เรียกทุกครั้งที่ PhotoStorage เพิ่มภาพใหม่สำเร็จ — ทำสำเนาถาวรทันทีถ้า Entry นั้นยังไม่เคยปลดล็อกมาก่อน
    /// ตั้งใจไม่ Overwrite สำเนาเดิมถ้าปลดล็อกไปแล้ว (เก็บภาพแรกที่ถ่ายติดไว้เสมอ)
    /// </summary>
    private void HandlePhotoAdded(PhotoStorage.StoredPhoto photo)
    {
        if (photo.creatureIds == null) return;

        foreach (string creatureId in photo.creatureIds)
        {
            if (string.IsNullOrEmpty(creatureId) || unlockedPhotos.ContainsKey(creatureId)) continue;

            Texture2D texture = PhotoStorage.Instance.LoadPhotoTexture(photo);
            if (texture != null)
            {
                unlockedPhotos[creatureId] = texture;
            }
        }
    }

    /// <summary>คืน Entry ทั้งหมดในหมวดที่ระบุ เรียงตามลำดับใน allEntries</summary>
    public List<JournalEntry> GetEntriesByCategory(JournalEntry.JournalCategory category)
    {
        return allEntries.Where(e => e != null && e.category == category).ToList();
    }

    /// <summary>เช็คว่า Entry นี้เคยถ่ายติดมาก่อนหรือยัง — เช็คจากสำเนาของตัวเอง ไม่เช็คจาก Storage สดๆ อีกแล้ว</summary>
    public bool IsUnlocked(JournalEntry entry)
    {
        return entry != null && unlockedPhotos.ContainsKey(entry.creatureId);
    }

    /// <summary>
    /// คืนสำเนา Texture2D ถาวรที่ Journal เก็บไว้เอง — "ยืม" มาใช้เท่านั้น ห้าม Destroy()
    /// เพราะเป็น Object เดียวกับที่ JournalManager ถืออยู่ตลอด Session ไม่ใช่ Texture ชั่วคราวอีกต่อไป
    /// </summary>
    public Texture2D LoadPhotoForEntry(JournalEntry entry)
    {
        if (entry == null) return null;
        return unlockedPhotos.TryGetValue(entry.creatureId, out Texture2D texture) ? texture : null;
    }
}
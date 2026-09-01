using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// เก็บรายชื่อ JournalEntry ทั้งหมด (ตั้งค่าจาก Inspector) และเป็นคนกลางดึงข้อมูล/ภาพจาก PhotoStorage
/// มาจับคู่กับ Entry แต่ละตัวผ่าน creatureId
///
/// ไม่ Subscribe Event จาก PhotoShooter ตรงๆ เหมือนเวอร์ชันเก่าอีกต่อไป — ให้ JournalUI เรียกฟังก์ชันในนี้
/// "ไปดึงข้อมูลเอาเอง" ตอนเปิดใช้งานแทน (ตรงกับสถาปัตยกรรมใหม่ที่ให้ Storage เป็นแหล่งข้อมูลกลาง)
/// </summary>
public class JournalManager : MonoBehaviour
{
    [Header("Data — ลาก JournalEntry Asset ทั้งหมดมาใส่ตรงนี้")]
    public List<JournalEntry> allEntries = new List<JournalEntry>();

    /// <summary>คืน Entry ทั้งหมดในหมวดที่ระบุ เรียงตามลำดับใน allEntries</summary>
    public List<JournalEntry> GetEntriesByCategory(JournalEntry.JournalCategory category)
    {
        return allEntries.Where(e => e != null && e.category == category).ToList();
    }

    /// <summary>เช็คว่า Entry นี้เคยถ่ายติดมาก่อนหรือยัง (มี Metadata ใน PhotoStorage ที่ creatureId ตรงกัน)</summary>
    public bool IsUnlocked(JournalEntry entry)
    {
        if (entry == null || PhotoStorage.Instance == null) return false;

        foreach (PhotoStorage.StoredPhoto photo in PhotoStorage.Instance.Photos)
        {
            if (photo.creatureIds != null && photo.creatureIds.Contains(entry.creatureId))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// โหลดภาพแรกที่ถ่ายติด Entry นี้จาก PhotoStorage มาเป็น Texture2D ของตัวเอง
    /// (Journal "Copy ภาพไปเก็บเอง" ตามที่ออกแบบไว้ ไม่ใช้ Texture2D ตัวเดียวกับ Storage)
    /// ผู้เรียกต้อง Destroy() เองเมื่อเลิกใช้ ป้องกัน RAM ค้าง — ดูตัวอย่างใน JournalUI.ClearDetailTexture()
    /// </summary>
    public Texture2D LoadPhotoForEntry(JournalEntry entry)
    {
        if (entry == null || PhotoStorage.Instance == null) return null;

        foreach (PhotoStorage.StoredPhoto photo in PhotoStorage.Instance.Photos)
        {
            if (photo.creatureIds != null && photo.creatureIds.Contains(entry.creatureId))
            {
                return PhotoStorage.Instance.LoadPhotoTexture(photo);
            }
        }
        return null;
    }
}
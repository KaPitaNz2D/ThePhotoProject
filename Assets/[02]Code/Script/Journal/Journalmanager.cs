using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// เก็บรายชื่อ JournalEntry ทั้งหมด (ตั้งค่าจาก Inspector) และเป็นคนกลางดึงข้อมูล/ภาพจาก PhotoStorage
/// มาจับคู่กับ Entry แต่ละตัวผ่าน creatureId
///
/// สำคัญ: พอปลดล็อก Entry ไหนครั้งแรก จะ "ทำสำเนาถาวรของตัวเอง" ทันที (Subscribe PhotoStorage.OnPhotoAdded)
/// ไม่อ้างอิงไฟล์ใน Storage อีกเลยหลังจากนั้น ต่อให้ผู้เล่นไปลบภาพต้นฉบับทิ้งจาก Storage ทีหลัง Journal จะไม่หายตามเด็ดขาด
/// สำเนามี 2 ส่วน:
///   1) รูปย่อ (Texture2D เล็กๆ ค้างใน RAM) — ใช้โชว์ในหน้า Journal ทันที ไม่ต้องอ่านไฟล์
///   2) ไฟล์ภาพเต็มของ Journal เอง (copy ไว้ในโฟลเดอร์ JournalPhotos) — โหลดมาดูภาพใหญ่ได้เมื่อต้องการผ่าน LoadFullPhotoForEntry()
///      (เตรียมไว้สำหรับกดดูรูปใหญ่/เปลี่ยนรูปใน Journal) ลบทิ้งตอน JournalManager ถูกทำลาย
/// </summary>
public class JournalManager : MonoBehaviour
{
    private const string PhotoFolderName = "JournalPhotos";

    [Header("Data — ลาก JournalEntry Asset ทั้งหมดมาใส่ตรงนี้")]
    public List<JournalEntry> allEntries = new List<JournalEntry>();

    /// <summary>ภาพของ Entry 1 ตัวที่ Journal เก็บไว้เอง</summary>
    private class JournalPhoto
    {
        public Texture2D thumbnail;   // รูปย่อสำหรับแสดงผล (ของ Journal เอง ไม่ใช่ตัวเดียวกับ Storage)
        public string fullFilePath;   // ไฟล์ภาพเต็มสำเนาของ Journal เอง (null ถ้า copy ไม่สำเร็จ)
    }

    // creatureId -> ภาพของ Journal
    private Dictionary<string, JournalPhoto> unlockedPhotos = new Dictionary<string, JournalPhoto>();

    private string FolderPath => Path.Combine(Application.persistentDataPath, PhotoFolderName);

    private void Awake()
    {
        // เริ่มโฟลเดอร์ใหม่ทุกครั้ง กันไฟล์ค้างจากรอบก่อนที่เกมปิดผิดปกติ (ตอนนี้ยังไม่มีระบบ Save/Load ข้าม Session)
        try
        {
            if (Directory.Exists(FolderPath)) Directory.Delete(FolderPath, true);
            Directory.CreateDirectory(FolderPath);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[JournalManager] เตรียมโฟลเดอร์ {FolderPath} ไม่สำเร็จ: {e.Message}");
        }
    }

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

        // ทำลายรูปย่อและลบไฟล์สำเนาที่ค้างไว้ทั้งหมดตอนปิดเกม/เปลี่ยนซีน ป้องกัน RAM/ดิสก์ค้าง
        foreach (JournalPhoto photo in unlockedPhotos.Values)
        {
            if (photo.thumbnail != null) Destroy(photo.thumbnail);
            DeleteFileQuietly(photo.fullFilePath);
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

            JournalPhoto journalPhoto = CreateJournalPhoto(photo, creatureId);
            if (journalPhoto != null)
            {
                unlockedPhotos[creatureId] = journalPhoto;
            }
        }
    }

    private JournalPhoto CreateJournalPhoto(PhotoStorage.StoredPhoto photo, string creatureId)
    {
        // รูปย่อ: ทำสำเนาจากรูปย่อของ Storage (ไม่ถอดรหัส PNG ขนาดจริง) — ต้องเป็นสำเนาของเราเอง เพราะ Storage จะ Destroy รูปย่อตอนลบภาพ
        Texture2D storageThumbnail = PhotoStorage.Instance.GetThumbnail(photo);
        if (storageThumbnail == null) return null;

        JournalPhoto journalPhoto = new JournalPhoto { thumbnail = Instantiate(storageThumbnail) };

        // ไฟล์ภาพเต็ม: copy ไว้เป็นของ Journal เอง (ไม่โหลดเข้า RAM) ไว้ใช้ดูภาพใหญ่ภายหลัง
        try
        {
            string safeId = string.Concat(creatureId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string destination = Path.Combine(FolderPath, $"journal_{unlockedPhotos.Count}_{safeId}.png");
            File.Copy(photo.filePath, destination, true);
            journalPhoto.fullFilePath = destination;
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[JournalManager] copy ไฟล์ภาพเต็มของ '{creatureId}' ไม่สำเร็จ (ยังมีรูปย่อโชว์ได้): {e.Message}");
        }

        return journalPhoto;
    }

    private static void DeleteFileQuietly(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // ลบไม่ได้ก็ปล่อย โฟลเดอร์จะถูกล้างตอน Awake ครั้งหน้า
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
    /// คืน "รูปย่อ" ของ Entry สำหรับแสดงผลในหน้า Journal — "ยืม" มาใช้เท่านั้น ห้าม Destroy()
    /// เพราะเป็น Object เดียวกับที่ JournalManager ถืออยู่ตลอด Session ไม่ใช่ Texture ชั่วคราว
    /// </summary>
    public Texture2D LoadPhotoForEntry(JournalEntry entry)
    {
        if (entry == null) return null;
        return unlockedPhotos.TryGetValue(entry.creatureId, out JournalPhoto photo) ? photo.thumbnail : null;
    }

    /// <summary>
    /// โหลด "ภาพเต็ม" ของ Entry จากไฟล์สำเนาของ Journal (สำหรับกดดูรูปใหญ่) — ผู้เรียกเป็นเจ้าของ Texture ต้อง Destroy() เองเมื่อเลิกใช้
    /// คืน null ถ้า Entry ยังไม่ปลดล็อกหรือไม่มีไฟล์
    /// </summary>
    public Texture2D LoadFullPhotoForEntry(JournalEntry entry)
    {
        if (entry == null) return null;
        if (!unlockedPhotos.TryGetValue(entry.creatureId, out JournalPhoto photo)) return null;
        if (string.IsNullOrEmpty(photo.fullFilePath) || !File.Exists(photo.fullFilePath)) return null;

        Texture2D texture = new Texture2D(2, 2); // ขนาดจริงจะถูกแทนที่อัตโนมัติตอน LoadImage
        texture.LoadImage(File.ReadAllBytes(photo.fullFilePath));
        return texture;
    }
}

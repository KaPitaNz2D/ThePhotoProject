using System;
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
/// สำเนาเก็บในโฟลเดอร์ JournalPhotos 2 ไฟล์ต่อ Entry:
///   1) รูปย่อ (_thumb.png) — โหลดเป็น Texture2D เล็กๆ ค้างใน RAM ตอนมีคนขอครั้งแรก ใช้โชว์ในหน้า Journal
///   2) ไฟล์ภาพเต็ม — โหลดมาดูภาพใหญ่ได้เมื่อต้องการผ่าน LoadFullPhotoForEntry()
///
/// เซฟข้ามการปิดเกม: รายชื่อ Entry ที่ปลดล็อก (+ไฟล์ภาพ), creatureId ที่เคยเล่นเฟดเผยรูปแล้ว และหน้าสุดท้ายที่เปิด Journal ค้างไว้
/// อยู่ใน Save/journal.json — เซฟทุกครั้งที่เปลี่ยน โหลดกลับตอน Awake
/// </summary>
public class JournalManager : MonoBehaviour
{
    private const string PhotoFolderName = SaveSystem.JournalPhotoFolderName;
    private const string SaveFileName = "journal.json";
    private const string ThumbnailSuffix = "_thumb.png";

    [Header("Data — ลาก JournalEntry Asset ทั้งหมดมาใส่ตรงนี้")]
    public List<JournalEntry> allEntries = new List<JournalEntry>();

    /// <summary>ภาพของ Entry 1 ตัวที่ Journal เก็บไว้เอง</summary>
    private class JournalPhoto
    {
        public Texture2D thumbnail;   // รูปย่อสำหรับแสดงผล (ของ Journal เอง) — null จนกว่าจะมีคนขอครั้งแรก
        public string fullFilePath;   // ไฟล์ภาพเต็มสำเนาของ Journal เอง (null ถ้า copy ไม่สำเร็จ)
        public string thumbnailFilePath;
    }

    /// <summary>หน้า Journal ที่เปิดค้างไว้ล่าสุด (JournalUI เป็นคนแปลความหมาย) — เซฟไว้ให้เปิดเกมใหม่แล้วเจอหน้าเดิม</summary>
    [Serializable]
    public class SavedPage
    {
        public int pageType;
        public bool animals;
        public string creatureId;
        public int subPage;
    }

    [Serializable]
    private class JournalSaveData
    {
        public List<EntryRecord> entries = new List<EntryRecord>();
        public List<string> revealedCreatureIds = new List<string>();
        public bool hasLastPage;
        public SavedPage lastPage = new SavedPage();
    }

    [Serializable]
    private class EntryRecord
    {
        public string creatureId;
        public string fullFileName;
        public string thumbnailFileName;
    }

    // creatureId -> ภาพของ Journal
    private Dictionary<string, JournalPhoto> unlockedPhotos = new Dictionary<string, JournalPhoto>();

    // creatureId ที่เคยเล่นเฟดเผยรูปใน Detail ไปแล้ว (JournalUI ใช้) — เซฟด้วย เปิดเกมใหม่จะไม่เฟดซ้ำ
    private HashSet<string> revealedCreatureIds = new HashSet<string>();
    private SavedPage lastPage;

    // เลขรันชื่อไฟล์สำเนา — ไม่ใช้ unlockedPhotos.Count เพราะตอนเปลี่ยนรูป (SetPhotoForEntry) Count ไม่ขยับ ชื่อไฟล์จะชนของเดิม
    // ใส่เวลาในชื่อไฟล์ด้วย กันชนไฟล์ที่โหลดกลับมาจากเซฟรอบก่อน
    private int photoFileCounter;

    private string FolderPath => Path.Combine(Application.persistentDataPath, PhotoFolderName);

    private void Awake()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[JournalManager] เตรียมโฟลเดอร์ {FolderPath} ไม่สำเร็จ: {e.Message}");
        }

        LoadFromDisk();
        SaveSystem.OnSaveDeleted += ClearAll;
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
        SaveSystem.OnSaveDeleted -= ClearAll;

        // ทำลายรูปย่อใน RAM เท่านั้น — ไฟล์บนดิสก์เก็บไว้ให้เปิดเกมรอบหน้า
        DestroyThumbnails();
    }

    private void DestroyThumbnails()
    {
        foreach (JournalPhoto photo in unlockedPhotos.Values)
        {
            if (photo.thumbnail != null) Destroy(photo.thumbnail);
            photo.thumbnail = null;
        }
    }

    // ==================== Save / Load ====================
    private void LoadFromDisk()
    {
        JournalSaveData data = SaveSystem.Load<JournalSaveData>(SaveFileName);
        HashSet<string> keepFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data != null)
        {
            foreach (EntryRecord record in data.entries)
            {
                if (record == null || string.IsNullOrEmpty(record.creatureId)) continue;
                JournalPhoto photo = new JournalPhoto
                {
                    fullFilePath = ToFullPath(record.fullFileName),
                    thumbnailFilePath = ToFullPath(record.thumbnailFileName)
                };
                // ไม่มีไฟล์เหลือสักไฟล์ = ข้อมูลหาย ถือว่ายังไม่ปลดล็อก
                if (!File.Exists(photo.fullFilePath ?? "") && !File.Exists(photo.thumbnailFilePath ?? "")) continue;

                unlockedPhotos[record.creatureId] = photo;
                if (photo.fullFilePath != null) keepFiles.Add(photo.fullFilePath);
                if (photo.thumbnailFilePath != null) keepFiles.Add(photo.thumbnailFilePath);
            }
            foreach (string id in data.revealedCreatureIds)
            {
                if (!string.IsNullOrEmpty(id)) revealedCreatureIds.Add(id);
            }
            if (data.hasLastPage) lastPage = data.lastPage;
        }

        // ลบไฟล์ที่ไม่มีใครอ้างถึง (ค้างจากเกมแครช/เปลี่ยนรูป)
        if (Directory.Exists(FolderPath))
        {
            foreach (string file in Directory.GetFiles(FolderPath))
            {
                if (!keepFiles.Contains(file)) DeleteFileQuietly(file);
            }
        }
    }

    private void SaveToDisk()
    {
        JournalSaveData data = new JournalSaveData();
        foreach (KeyValuePair<string, JournalPhoto> pair in unlockedPhotos)
        {
            data.entries.Add(new EntryRecord
            {
                creatureId = pair.Key,
                fullFileName = Path.GetFileName(pair.Value.fullFilePath),
                thumbnailFileName = Path.GetFileName(pair.Value.thumbnailFilePath)
            });
        }
        data.revealedCreatureIds.AddRange(revealedCreatureIds);
        data.hasLastPage = lastPage != null;
        if (lastPage != null) data.lastPage = lastPage;
        SaveSystem.Save(SaveFileName, data);
    }

    private string ToFullPath(string fileName) => string.IsNullOrEmpty(fileName) ? null : Path.Combine(FolderPath, fileName);

    // SaveSystem.DeleteAll() (ปุ่ม Reset / ปุ่ม R) — ล้างใน RAM ทั้งหมด ไฟล์ SaveSystem ลบให้เอง
    private void ClearAll()
    {
        DestroyThumbnails();
        unlockedPhotos.Clear();
        revealedCreatureIds.Clear();
        lastPage = null;
    }

    /// <summary>JournalUI เช็คว่าเคยเล่นเฟดเผยรูปของ creatureId นี้ไปแล้วหรือยัง</summary>
    public bool IsRevealed(string creatureId) => !string.IsNullOrEmpty(creatureId) && revealedCreatureIds.Contains(creatureId);

    /// <summary>JournalUI บอกว่าเล่นเฟดเผยรูปของ creatureId นี้แล้ว (เซฟทันที)</summary>
    public void MarkRevealed(string creatureId)
    {
        if (!string.IsNullOrEmpty(creatureId) && revealedCreatureIds.Add(creatureId)) SaveToDisk();
    }

    /// <summary>หน้าสุดท้ายที่เปิดค้างไว้ (null = ยังไม่เคยเปิด)</summary>
    public SavedPage LastPage => lastPage;

    /// <summary>JournalUI เรียกตอนปิด Journal — เซฟหน้าปัจจุบันไว้ให้เปิดเกมรอบหน้าเจอหน้าเดิม</summary>
    public void SetLastPage(SavedPage page)
    {
        lastPage = page;
        SaveToDisk();
    }

    /// <summary>
    /// เรียกทุกครั้งที่ PhotoStorage เพิ่มภาพใหม่สำเร็จ — ทำสำเนาถาวรทันทีถ้า Entry นั้นยังไม่เคยปลดล็อกมาก่อน
    /// ตั้งใจไม่ Overwrite สำเนาเดิมถ้าปลดล็อกไปแล้ว (เก็บภาพแรกที่ถ่ายติดไว้เสมอ)
    /// </summary>
    private void HandlePhotoAdded(PhotoStorage.StoredPhoto photo)
    {
        if (photo.creatureIds == null) return;

        bool changed = false;
        foreach (string creatureId in photo.creatureIds)
        {
            if (string.IsNullOrEmpty(creatureId) || unlockedPhotos.ContainsKey(creatureId)) continue;

            JournalPhoto journalPhoto = CreateJournalPhoto(photo, creatureId);
            if (journalPhoto != null)
            {
                unlockedPhotos[creatureId] = journalPhoto;
                changed = true;
            }
        }
        if (changed) SaveToDisk();
    }

    private JournalPhoto CreateJournalPhoto(PhotoStorage.StoredPhoto photo, string creatureId)
    {
        // รูปย่อ: ทำสำเนาจากรูปย่อของ Storage (ไม่ถอดรหัส PNG ขนาดจริง) — ต้องเป็นสำเนาของเราเอง เพราะ Storage จะ Destroy รูปย่อตอนลบภาพ
        Texture2D storageThumbnail = PhotoStorage.Instance.GetThumbnail(photo);
        if (storageThumbnail == null) return null;

        JournalPhoto journalPhoto = new JournalPhoto { thumbnail = Instantiate(storageThumbnail) };

        string safeId = string.Concat(creatureId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string baseName = $"journal_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{photoFileCounter++}_{safeId}";

        // รูปย่อลงไฟล์ด้วย ไว้โหลดกลับตอนเปิดเกมรอบหน้า (เล็กมาก เข้ารหัสเร็ว)
        try
        {
            string thumbnailPath = Path.Combine(FolderPath, baseName + ThumbnailSuffix);
            File.WriteAllBytes(thumbnailPath, journalPhoto.thumbnail.EncodeToPNG());
            journalPhoto.thumbnailFilePath = thumbnailPath;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JournalManager] เขียนรูปย่อของ '{creatureId}' ไม่สำเร็จ: {e.Message}");
        }

        // ไฟล์ภาพเต็ม: copy ไว้เป็นของ Journal เอง (ไม่โหลดเข้า RAM) ไว้ใช้ดูภาพใหญ่ภายหลัง
        try
        {
            string destination = Path.Combine(FolderPath, baseName + ".png");
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
            // ลบไม่ได้ก็ปล่อย จะถูกล้างตอนโหลดรอบหน้า (ไฟล์ที่ไม่มีใครอ้างถึง)
        }
    }

    /// <summary>
    /// เปลี่ยนรูปประจำ Entry เป็นภาพที่ผู้เล่นเลือกจาก Storage (หน้า Change Photos ใน Journal)
    /// ทำสำเนาใหม่ของ Journal เองเหมือนตอนปลดล็อก แล้วทิ้งสำเนาเก่า — คืน true ถ้าเปลี่ยนสำเร็จ
    /// </summary>
    public bool SetPhotoForEntry(JournalEntry entry, PhotoStorage.StoredPhoto photo)
    {
        if (entry == null || photo == null || PhotoStorage.Instance == null) return false;

        JournalPhoto newPhoto = CreateJournalPhoto(photo, entry.creatureId);
        if (newPhoto == null) return false;

        if (unlockedPhotos.TryGetValue(entry.creatureId, out JournalPhoto oldPhoto))
        {
            if (oldPhoto.thumbnail != null) Destroy(oldPhoto.thumbnail);
            DeleteFileQuietly(oldPhoto.fullFilePath);
            DeleteFileQuietly(oldPhoto.thumbnailFilePath);
        }
        unlockedPhotos[entry.creatureId] = newPhoto;
        SaveToDisk();
        return true;
    }

    /// <summary>ภาพใน Storage ที่ถ่ายติด Entry นี้ เรียงตามลำดับใน Storage — ใช้เป็นตัวเลือกในหน้า Change Photos</summary>
    public List<PhotoStorage.StoredPhoto> GetStoragePhotosFor(JournalEntry entry)
    {
        if (entry == null || PhotoStorage.Instance == null) return new List<PhotoStorage.StoredPhoto>();
        return PhotoStorage.Instance.Photos
            .Where(p => p.creatureIds != null && p.creatureIds.Contains(entry.creatureId))
            .ToList();
    }

    /// <summary>คืน Entry ทั้งหมดในหน้า Plants (Flora + Fungus) หรือ Animals เรียงตามลำดับใน allEntries</summary>
    public List<JournalEntry> GetEntriesForSection(bool animals)
    {
        return allEntries.Where(e => e != null && e.IsPlant != animals).ToList();
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
    /// Entry ที่โหลดกลับมาจากเซฟ: อ่านไฟล์รูปย่อครั้งแรกที่ขอ (ไม่มีไฟล์รูปย่อก็ย่อจากภาพเต็มแทน)
    /// </summary>
    public Texture2D LoadPhotoForEntry(JournalEntry entry)
    {
        if (entry == null) return null;
        if (!unlockedPhotos.TryGetValue(entry.creatureId, out JournalPhoto photo)) return null;

        if (photo.thumbnail == null)
        {
            if (!string.IsNullOrEmpty(photo.thumbnailFilePath) && File.Exists(photo.thumbnailFilePath))
            {
                Texture2D thumbnail = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (thumbnail.LoadImage(File.ReadAllBytes(photo.thumbnailFilePath)))
                {
                    thumbnail.wrapMode = TextureWrapMode.Clamp;
                    photo.thumbnail = thumbnail;
                }
                else Destroy(thumbnail);
            }
            if (photo.thumbnail == null)
            {
                Texture2D full = LoadFullPhotoForEntry(entry);
                if (full != null)
                {
                    int maxSize = PhotoStorage.Instance != null ? PhotoStorage.Instance.thumbnailMaxSize : 384;
                    photo.thumbnail = PhotoThumbnail.Create(full, maxSize);
                    Destroy(full);
                }
            }
        }
        return photo.thumbnail;
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

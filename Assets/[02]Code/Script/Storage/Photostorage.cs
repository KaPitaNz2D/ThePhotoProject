using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// ที่เก็บภาพกลางของเกม (Singleton) — เก็บแค่ Path ไฟล์ PNG บนดิสก์ + Metadata เท่านั้น
/// ไม่เก็บ Texture2D ค้างไว้ใน RAM เลย เพื่อประหยัดหน่วยความจำตามที่ต้องการ
/// โหลดเป็น Texture2D ก็ต่อเมื่อต้องการดูภาพขยายจริงๆ ผ่าน LoadPhotoTexture() เท่านั้น
///
/// จำกัดจำนวนภาพสูงสุด (maxCapacity) — เต็มแล้วต้องลบภาพเก่าเองผ่าน UI Storage ก่อนถึงจะถ่ายเพิ่มได้
/// (PhotoShooter เป็นคนเช็ค IsFull ก่อนอนุญาตให้กดชัตเตอร์)
/// </summary>
public class PhotoStorage : MonoBehaviour
{
    public static PhotoStorage Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("จำนวนภาพสูงสุดที่เก็บได้ ครบแล้วถ่ายเพิ่มไม่ได้จนกว่าจะลบภาพเก่าทิ้ง")]
    public int maxCapacity = 30;
    [Tooltip("โฟลเดอร์ย่อยใน Application.persistentDataPath ที่จะเก็บไฟล์ภาพ")]
    public string saveFolderName = "Photos";

    /// <summary>ข้อมูล 1 ภาพที่เก็บไว้ — เก็บแค่ Path ไม่เก็บ Texture2D</summary>
    [Serializable]
    public class StoredPhoto
    {
        public string filePath;
        public List<string> creatureIds; // ถ่ายติดสัตว์/พืชชนิดไหนบ้าง (เก็บเป็น id ไม่ใช่ GameObject กันปัญหาข้าม Scene)
        public DateTime capturedAt;
    }

    /// <summary>รายการภาพทั้งหมดที่เก็บอยู่ตอนนี้ (เรียงตามลำดับที่ถ่าย เก่า -> ใหม่)</summary>
    public IReadOnlyList<StoredPhoto> Photos => storedPhotos;

    /// <summary>เต็มแล้วหรือยัง — ใช้เช็คก่อนอนุญาตให้ถ่ายรูปเพิ่ม</summary>
    public bool IsFull => storedPhotos.Count >= maxCapacity;

    /// <summary>ยิงทุกครั้งที่รายการภาพเปลี่ยน (เพิ่ม/ลบ) — UI Storage ในอนาคตมา Subscribe รีเฟรชรายการได้</summary>
    public event Action OnStorageChanged;

    private List<StoredPhoto> storedPhotos = new List<StoredPhoto>();
    private string FolderPath => Path.Combine(Application.persistentDataPath, saveFolderName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }
    }

    private void OnApplicationQuit()
    {
        // ลบไฟล์ภาพทั้งหมดออกจากดิสก์ทุกครั้งที่ปิดเกม/หยุด Play Mode
        // (เรียกทำงานทั้งตอนกด Stop ใน Editor และตอนปิด Build จริง)
        // ตอนนี้ยังไม่มีระบบ Save/Load ข้าม Session เลยไม่จำเป็นต้องเก็บภาพเก่าไว้ข้ามรอบเล่น
        ClearAllPhotosFromDisk();
    }

    private void ClearAllPhotosFromDisk()
    {
        if (!Directory.Exists(FolderPath)) return;

        string[] files = Directory.GetFiles(FolderPath, "*.png");
        foreach (string file in files)
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// บันทึกไฟล์ PNG ลงดิสก์ + เก็บ Metadata ไว้ใน List
    /// คืนค่า false ถ้า Storage เต็มแล้ว (ไม่บันทึกอะไรเลย)
    /// </summary>
    public bool TryStorePhoto(byte[] pngBytes, List<string> creatureIds)
    {
        if (IsFull)
        {
            return false;
        }
        if (pngBytes == null || pngBytes.Length == 0)
        {
            Debug.LogWarning("[PhotoStorage] pngBytes ว่างเปล่า ไม่บันทึกไฟล์");
            return false;
        }

        string fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        string fullPath = Path.Combine(FolderPath, fileName);
        File.WriteAllBytes(fullPath, pngBytes);

        storedPhotos.Add(new StoredPhoto
        {
            filePath = fullPath,
            creatureIds = creatureIds ?? new List<string>(),
            capturedAt = DateTime.Now
        });

        OnStorageChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// โหลดภาพเต็มจาก Path มาเป็น Texture2D — เรียกเฉพาะตอนต้องการแสดงผลจริงๆ (เช่นกดดูภาพขยาย)
    /// ไม่ Cache ไว้ ผู้เรียกต้อง Destroy() เมื่อเลิกใช้แล้ว ป้องกัน RAM บวม
    /// </summary>
    public Texture2D LoadPhotoTexture(StoredPhoto storedPhoto)
    {
        if (storedPhoto == null || !File.Exists(storedPhoto.filePath))
        {
            Debug.LogWarning($"[PhotoStorage] ไม่พบไฟล์ภาพที่ {storedPhoto?.filePath}");
            return null;
        }

        byte[] fileData = File.ReadAllBytes(storedPhoto.filePath);
        Texture2D texture = new Texture2D(2, 2); // ขนาดจริงจะถูกแทนที่อัตโนมัติตอน LoadImage
        texture.LoadImage(fileData);
        return texture;
    }

    /// <summary>ลบภาพทิ้งตาม Index ใน List (เรียกจาก UI Storage ตอนกดลบ) คืนค่า true ถ้าลบสำเร็จ</summary>
    public bool DeletePhoto(int index)
    {
        if (index < 0 || index >= storedPhotos.Count) return false;

        string path = storedPhotos[index].filePath;
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        storedPhotos.RemoveAt(index);
        OnStorageChanged?.Invoke();
        return true;
    }
}
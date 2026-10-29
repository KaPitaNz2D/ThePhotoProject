using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// ที่เก็บภาพกลางของเกม (Singleton) — ภาพเต็มเก็บเป็นไฟล์ PNG บนดิสก์ (เก็บแค่ Path + Metadata) ไม่ค้างภาพขนาดจริงไว้ใน RAM
/// โหลดภาพเต็มเป็น Texture2D ก็ต่อเมื่อต้องการดูภาพขยายจริงๆ ผ่าน LoadPhotoTexture() เท่านั้น
/// ส่วนหน้า Storage Grid / Journal ใช้ "รูปย่อ" (thumbnailMaxSize) ที่สร้างไว้ตอนถ่ายและเก็บค้างใน RAM (เล็กมาก) ผ่าน GetThumbnailSprite()
/// เปิดหน้าคลังภาพจึงไม่ต้องอ่าน/ถอดรหัส PNG ขนาดจริงทุกภาพอีก
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

    [Header("Thumbnail")]
    [Tooltip("ด้านยาวสุดของรูปย่อ (พิกเซล) ที่ใช้โชว์ใน Storage Grid และ Journal — ตั้งให้ใกล้ขนาดช่องใน UI " +
             "(ตอนนี้ช่อง Storage 384x216 ที่ความละเอียดอ้างอิง 1920x1080) ยิ่งใหญ่ยิ่งคมแต่กิน RAM มากขึ้น (RGB24 ต่อภาพ ≈ กว้าง x สูง x 3 ไบต์)")]
    public int thumbnailMaxSize = 384;

    /// <summary>
    /// ข้อมูล 1 ภาพที่เก็บไว้ — ไฟล์ภาพเต็มอยู่บนดิสก์ (เก็บแค่ Path) ส่วนรูปย่อเล็กๆ เก็บค้างใน RAM ไว้โชว์ใน UI ทันที
    /// ไม่ต้องถอดรหัสไฟล์ PNG ขนาดจริงซ้ำทุกครั้งที่เปิดหน้าคลังภาพ
    /// </summary>
    [Serializable]
    public class StoredPhoto
    {
        public string filePath;
        public List<string> creatureIds; // ถ่ายติดสัตว์/พืชชนิดไหนบ้าง (เก็บเป็น id ไม่ใช่ GameObject กันปัญหาข้าม Scene)
        public DateTime capturedAt;

        // รูปย่อ + Sprite ที่ห่อไว้ — PhotoStorage เป็นเจ้าของ ลบภาพเมื่อไหร่จะ Destroy ให้ ผู้อื่นห้าม Destroy
        // ถ้าต้องการเก็บต่อหลังภาพถูกลบ (เช่น Journal) ให้ทำสำเนาของตัวเอง
        [NonSerialized] public Texture2D thumbnail;
        [NonSerialized] public Sprite thumbnailSprite;
    }

    /// <summary>รายการภาพทั้งหมดที่เก็บอยู่ตอนนี้ (เรียงตามลำดับที่ถ่าย เก่า -> ใหม่)</summary>
    public IReadOnlyList<StoredPhoto> Photos => storedPhotos;

    /// <summary>เต็มแล้วหรือยัง — ใช้เช็คก่อนอนุญาตให้ถ่ายรูปเพิ่ม (นับภาพที่กำลังบันทึกอยู่เบื้องหลังด้วย)</summary>
    public bool IsFull => storedPhotos.Count + pendingSaves >= maxCapacity;

    /// <summary>ยิงทุกครั้งที่รายการภาพเปลี่ยน (เพิ่ม/ลบ) — UI Storage มา Subscribe รีเฟรชรายการได้</summary>
    public event Action OnStorageChanged;

    /// <summary>
    /// ยิงเฉพาะตอน "เพิ่มภาพใหม่สำเร็จ" เท่านั้น (ไม่ยิงตอนลบ) — JournalManager ใช้จุดนี้ทำสำเนาถาวรของตัวเอง
    /// เพื่อไม่ให้ผูกกับวงจรชีวิตของไฟล์ใน Storage อีกต่อไป (ลบใน Storage แล้ว Journal ต้องไม่หายตาม)
    /// </summary>
    public event Action<StoredPhoto> OnPhotoAdded;

    private List<StoredPhoto> storedPhotos = new List<StoredPhoto>();
    private string FolderPath => Path.Combine(Application.persistentDataPath, saveFolderName);

    // จำนวนภาพที่กำลังเข้ารหัส/เขียนไฟล์อยู่เบื้องหลัง (นับรวมเข้า IsFull) และรอบการล้างคลัง
    // ClearAllPhotos() เพิ่มรอบ งานที่เริ่มก่อนล้างและเพิ่งเสร็จจะถูกทิ้ง ไม่โผล่กลับเข้าคลังที่เพิ่งล้าง
    private int pendingSaves;
    private int clearGeneration;
    private int saveCounter;

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
    /// <param name="sourceTexture">รูปเต็มที่เพิ่งถ่าย (ถ้าส่งมา จะสร้างรูปย่อจากตัวนี้ทันทีโดยไม่ต้องถอดรหัส PNG ซ้ำ — ผู้เรียกยังเป็นเจ้าของ Texture ตัวนี้ ไม่ถูก Destroy ที่นี่) ไม่ส่งมาก็ได้ จะสร้างรูปย่อตอนมีคนขอครั้งแรกแทน</param>
    public bool TryStorePhoto(byte[] pngBytes, List<string> creatureIds, Texture sourceTexture = null)
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

        StoredPhoto newPhoto = new StoredPhoto
        {
            filePath = fullPath,
            creatureIds = creatureIds ?? new List<string>(),
            capturedAt = DateTime.Now
        };
        if (sourceTexture != null)
        {
            newPhoto.thumbnail = PhotoThumbnail.Create(sourceTexture, thumbnailMaxSize);
            newPhoto.thumbnailSprite = PhotoThumbnail.ToSprite(newPhoto.thumbnail);
        }
        storedPhotos.Add(newPhoto);

        OnStorageChanged?.Invoke();
        OnPhotoAdded?.Invoke(newPhoto);
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

    /// <summary>ผลจากงานเบื้องหลัง: รูปย่อ RGB24 ที่ย่อไว้แล้ว (สร้างเป็น Texture2D ต้องทำบน Main Thread)</summary>
    private struct EncodedPhoto
    {
        public byte[] thumbnailRgb;
        public int thumbnailWidth;
        public int thumbnailHeight;
    }

    /// <summary>
    /// บันทึกภาพแบบ "ไม่กระตุก": รับพิกเซลดิบ RGBA32 (แถวล่างสุดก่อน เหมือน Texture2D) แล้วทำงานหนักทั้งหมดเบื้องหลัง —
    /// เข้ารหัส PNG, เขียนไฟล์, ย่อรูปย่อ — เสร็จแล้วค่อยกลับ Main Thread มาเพิ่มเข้า List และยิง OnStorageChanged / OnPhotoAdded
    /// (ภาพจึงโผล่ใน Storage/Journal ช้ากว่าตอนกดชัตเตอร์ราว 0.3-0.5 วินาที) คืน false ถ้า Storage เต็ม (รวมภาพที่กำลังบันทึกอยู่)
    /// pixels ถูกส่งต่อให้ Thread เบื้องหลังใช้เอง ผู้เรียกห้ามแก้/นำไปใช้ต่อหลังเรียก
    /// </summary>
    public bool TryStorePhotoAsync(byte[] pixels, int width, int height, List<string> creatureIds)
    {
        if (IsFull) return false;
        if (pixels == null || pixels.Length != width * height * 4)
        {
            Debug.LogWarning("[PhotoStorage] ข้อมูลพิกเซลไม่ถูกต้อง ไม่บันทึกไฟล์");
            return false;
        }

        // ใส่เลขลำดับท้ายชื่อด้วย กันชื่อซ้ำถ้าบันทึกสองภาพในมิลลิวินาทีเดียวกัน (ไม่งั้นงานหนึ่งอาจเขียนทับ/ลบไฟล์ของอีกงาน)
        string fullPath = Path.Combine(FolderPath, $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{saveCounter++}.png");
        DateTime capturedAt = DateTime.Now;
        int generation = clearGeneration;
        int thumbnailSize = thumbnailMaxSize;

        pendingSaves++;
        EncodeAndStoreAsync(pixels, width, height, fullPath, capturedAt, creatureIds ?? new List<string>(), generation, thumbnailSize);
        return true;
    }

    private async void EncodeAndStoreAsync(byte[] pixels, int width, int height, string fullPath, DateTime capturedAt,
                                           List<string> creatureIds, int generation, int thumbnailSize)
    {
        EncodedPhoto result = default;
        bool succeeded = false;
        try
        {
            result = await System.Threading.Tasks.Task.Run(() => EncodeAndWrite(pixels, width, height, fullPath, thumbnailSize));
            succeeded = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PhotoStorage] บันทึกภาพเบื้องหลังล้มเหลว: {e.Message}");
        }

        // ต่อจากนี้อยู่บน Main Thread แล้ว (await คืนกลับมาที่ Unity เอง)
        if (this == null) return;
        pendingSaves = Mathf.Max(0, pendingSaves - 1);

        if (!succeeded) return;

        if (generation != clearGeneration)
        {
            // ระหว่างบันทึกมีการล้างคลัง (Debug Reset) ภาพนี้ไม่ต้องโผล่กลับมาแล้ว
            if (File.Exists(fullPath)) File.Delete(fullPath);
            return;
        }

        StoredPhoto newPhoto = new StoredPhoto
        {
            filePath = fullPath,
            creatureIds = creatureIds,
            capturedAt = capturedAt,
            thumbnail = PhotoThumbnail.FromRgb24(result.thumbnailRgb, result.thumbnailWidth, result.thumbnailHeight)
        };
        newPhoto.thumbnailSprite = PhotoThumbnail.ToSprite(newPhoto.thumbnail);
        storedPhotos.Add(newPhoto);

        OnStorageChanged?.Invoke();
        OnPhotoAdded?.Invoke(newPhoto);
    }

    // รันบน Thread เบื้องหลัง — ห้ามเรียก API ของ Unity ที่ต้องใช้ Main Thread (ImageConversion.EncodeArrayToPNG ใช้จาก Thread อื่นได้)
    private static EncodedPhoto EncodeAndWrite(byte[] pixels, int width, int height, string fullPath, int thumbnailSize)
    {
        // ทำให้ทึบแสง: RenderTexture อาจมี Alpha ไม่เต็ม ซึ่งจะทำให้ PNG โปร่งใส (เดิมเซฟเป็น RGB24 ไม่มี Alpha)
        for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;

        PhotoThumbnail.GetThumbnailSize(width, height, thumbnailSize, out int thumbWidth, out int thumbHeight);
        byte[] thumbnailRgb = PhotoThumbnail.DownscaleRgbaToRgb24(pixels, width, height, thumbWidth, thumbHeight);

        byte[] png = ImageConversion.EncodeArrayToPNG(
            pixels, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height, 0);
        File.WriteAllBytes(fullPath, png);

        return new EncodedPhoto { thumbnailRgb = thumbnailRgb, thumbnailWidth = thumbWidth, thumbnailHeight = thumbHeight };
    }

    /// <summary>
    /// คืนรูปย่อของภาพนี้ (Texture2D ที่ PhotoStorage เป็นเจ้าของ ห้าม Destroy) — ไม่มีก็สร้างจากไฟล์เต็มให้ครั้งเดียวแล้วเก็บไว้
    /// ภาพที่เพิ่งถ่ายมีรูปย่อพร้อมอยู่แล้ว (สร้างตอน TryStorePhoto) เลยปกติไม่ต้องอ่านไฟล์เลย
    /// </summary>
    public Texture2D GetThumbnail(StoredPhoto storedPhoto)
    {
        EnsureThumbnail(storedPhoto);
        return storedPhoto?.thumbnail;
    }

    /// <summary>เหมือน GetThumbnail แต่คืนเป็น Sprite ที่ห่อไว้แล้ว (ไม่ต้อง Sprite.Create ใหม่ทุกครั้งที่เปิด UI) ห้าม Destroy</summary>
    public Sprite GetThumbnailSprite(StoredPhoto storedPhoto)
    {
        EnsureThumbnail(storedPhoto);
        return storedPhoto?.thumbnailSprite;
    }

    private void EnsureThumbnail(StoredPhoto storedPhoto)
    {
        if (storedPhoto == null || storedPhoto.thumbnail != null) return;

        Texture2D full = LoadPhotoTexture(storedPhoto);
        if (full == null) return;

        storedPhoto.thumbnail = PhotoThumbnail.Create(full, thumbnailMaxSize);
        storedPhoto.thumbnailSprite = PhotoThumbnail.ToSprite(storedPhoto.thumbnail);
        Destroy(full);
    }

    private static void DestroyThumbnail(StoredPhoto storedPhoto)
    {
        if (storedPhoto.thumbnailSprite != null) Destroy(storedPhoto.thumbnailSprite);
        if (storedPhoto.thumbnail != null) Destroy(storedPhoto.thumbnail);
        storedPhoto.thumbnailSprite = null;
        storedPhoto.thumbnail = null;
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

        DestroyThumbnail(storedPhotos[index]);
        storedPhotos.RemoveAt(index);
        OnStorageChanged?.Invoke();
        return true;
    }

    /// <summary>ลบภาพทั้งหมดทั้งไฟล์บนดิสก์และ Metadata ทันที — ใช้กับ Debug Reset เพื่อเริ่ม Storage ใหม่โดยไม่ต้องปิดเกม</summary>
    public void ClearAllPhotos()
    {
        clearGeneration++;
        ClearAllPhotosFromDisk();
        foreach (StoredPhoto photo in storedPhotos) DestroyThumbnail(photo);
        storedPhotos.Clear();
        OnStorageChanged?.Invoke();
    }
}
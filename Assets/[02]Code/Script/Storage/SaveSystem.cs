using System;
using System.IO;
using UnityEngine;

/// <summary>
/// ตัวช่วยเซฟ/โหลดความคืบหน้าข้ามการปิดเกม — ทุกระบบเก็บไฟล์ JSON ของตัวเองใน Application.persistentDataPath/Save
/// (photos.json, quests.json, journal.json, boards.json, yarn.json) ส่วนไฟล์ภาพ PNG อยู่ในโฟลเดอร์ Photos / JournalPhotos ตามเดิม
///
/// แต่ละระบบโหลดของตัวเองตอนเริ่มซีน (Awake/Start) และเซฟทันทีที่ข้อมูลเปลี่ยน (ไม่ต้องรอปิดเกม กันข้อมูลหายตอนเกมแครช)
/// DeleteAll() = ล้างเซฟทั้งหมด (ปุ่ม Reset ในเมนูหลัก / ปุ่ม R) — ยิง OnSaveDeleted ให้ระบบที่ยังเปิดอยู่ล้างข้อมูลใน RAM ด้วย
/// </summary>
public static class SaveSystem
{
    public const string SaveFolderName = "Save";
    // ชื่อโฟลเดอร์ภาพ — ต้องตรงกับ PhotoStorage.saveFolderName (ค่าเริ่มต้น) และ JournalManager.PhotoFolderName
    public const string PhotoFolderName = "Photos";
    public const string JournalPhotoFolderName = "JournalPhotos";

    /// <summary>ยิงก่อนลบไฟล์เซฟทั้งหมด — ระบบที่ยังเปิดอยู่ในซีนล้างข้อมูลใน RAM ของตัวเอง กันเซฟของเก่ากลับลงไปทีหลัง</summary>
    public static event Action OnSaveDeleted;

    public static string SaveFolder => Path.Combine(Application.persistentDataPath, SaveFolderName);

    public static string GetPath(string fileName) => Path.Combine(SaveFolder, fileName);

    /// <summary>มีเซฟอะไรอยู่บ้างไหม (ไฟล์ JSON หรือภาพ) — ใช้เปิด/ปิดปุ่ม Reset ในเมนูหลัก</summary>
    public static bool HasAnySave()
    {
        return HasFiles(SaveFolder) || HasFiles(Path.Combine(Application.persistentDataPath, PhotoFolderName))
            || HasFiles(Path.Combine(Application.persistentDataPath, JournalPhotoFolderName));
    }

    /// <summary>อ่านไฟล์ JSON เป็น T — คืน null ถ้าไม่มีไฟล์หรือไฟล์เสีย (เริ่มใหม่จากว่างแทน)</summary>
    public static T Load<T>(string fileName) where T : class
    {
        string path = GetPath(fileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] อ่าน {path} ไม่สำเร็จ เริ่มจากว่างแทน: {e.Message}");
            return null;
        }
    }

    /// <summary>เขียน data เป็น JSON — เขียนลงไฟล์ .tmp ก่อนแล้วค่อยแทนที่ กันไฟล์เสียครึ่งๆ กลางๆ ถ้าเกมปิดระหว่างเขียน</summary>
    public static void Save<T>(string fileName, T data)
    {
        string path = GetPath(fileName);
        try
        {
            Directory.CreateDirectory(SaveFolder);
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] เซฟ {path} ไม่สำเร็จ: {e.Message}");
        }
    }

    /// <summary>ลบเซฟทั้งหมด: ไฟล์ JSON ทุกตัว + ภาพทั้งหมดใน Photos / JournalPhotos</summary>
    public static void DeleteAll()
    {
        OnSaveDeleted?.Invoke();

        DeleteFolder(SaveFolder);
        DeleteFolder(Path.Combine(Application.persistentDataPath, PhotoFolderName));
        DeleteFolder(Path.Combine(Application.persistentDataPath, JournalPhotoFolderName));
        Debug.Log("[SaveSystem] ลบเซฟทั้งหมดแล้ว");
    }

    private static bool HasFiles(string folder)
    {
        return Directory.Exists(folder) && Directory.GetFiles(folder).Length > 0;
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[SaveSystem] ลบโฟลเดอร์ {folder} ไม่สำเร็จ: {e.Message}");
        }
    }
}

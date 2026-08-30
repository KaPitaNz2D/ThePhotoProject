using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Subscribe Event OnPhotoCaptured จาก PhotoShooter แล้วจัดการเรื่อง "เก็บภาพ" โดยเฉพาะ
/// ตอนนี้ทำแค่ 2 อย่าง: เซฟไฟล์ PNG ลงเครื่อง + เก็บ List ไว้ในหน่วยความจำ พร้อม Debug.Log ยืนยันผล
/// (ยังไม่เชื่อมกับ Field Guide/Journal — รอระบบนั้นพร้อมค่อยมาอ่านจาก capturedPhotos ต่อ)
/// </summary>
public class PhotoSaveHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ถ้าไม่ลากใส่ไว้ จะพยายาม GetComponent หาเองบน GameObject เดียวกัน")]
    public PhotoShooter photoShooter;

    [Header("Save Settings")]
    [Tooltip("เซฟไฟล์ PNG ลงเครื่องจริงไหม (ปิดไว้ได้ถ้าตอนนี้อยากเห็นแค่ Debug ไม่อยากเปลืองพื้นที่ดิสก์ตอนเทส)")]
    public bool saveToDisk = true;
    [Tooltip("โฟลเดอร์ย่อยใน Application.persistentDataPath ที่จะเก็บภาพ")]
    public string saveFolderName = "Photos";

    /// <summary>ข้อมูลภาพที่ถ่ายได้ 1 ใบ เก็บไว้ใช้ต่อกับระบบ Field Guide/Journal ในอนาคต</summary>
    public class PhotoData
    {
        public Texture2D texture;
        public List<GameObject> subjects;
        public string filePath; // ว่างไว้ถ้า saveToDisk = false
        public DateTime capturedAt;
    }

    /// <summary>เก็บภาพทั้งหมดที่ถ่ายมาแล้วในหน่วยความจำ ระหว่างเล่นเกมรอบนี้</summary>
    public List<PhotoData> capturedPhotos { get; private set; } = new List<PhotoData>();

    private void Awake()
    {
        if (photoShooter == null)
        {
            photoShooter = GetComponent<PhotoShooter>();
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
            Debug.LogError("[PhotoSaveHandler] หา PhotoShooter ไม่เจอ! ลาก Reference ใส่ หรือแปะสคริปต์นี้ไว้ที่ Object เดียวกับ PhotoShooter");
        }
    }

    private void OnDisable()
    {
        if (photoShooter != null)
        {
            photoShooter.OnPhotoCaptured -= HandlePhotoCaptured;
        }
    }

    private void HandlePhotoCaptured(Texture2D photo, List<GameObject> subjects)
    {
        if (photo == null)
        {
            Debug.LogWarning("[PhotoSaveHandler] ได้ภาพเป็น null มา — Capture ล้มเหลว ข้ามการเซฟรอบนี้");
            return;
        }

        string filePath = saveToDisk ? SavePhotoToDisk(photo) : string.Empty;

        PhotoData data = new PhotoData
        {
            texture = photo,
            subjects = subjects,
            filePath = filePath,
            capturedAt = DateTime.Now
        };
        capturedPhotos.Add(data);

        // TODO: ลบ/ปรับ Debug.Log นี้ทิ้งตอนมีระบบ Field Guide/UI แสดงผลจริงแล้ว
        string subjectNames = subjects.Count > 0
            ? string.Join(", ", subjects.ConvertAll(s => s.name))
            : "(ไม่เจอวัตถุที่ถ่ายติด)";

        Debug.Log(
            $"[PhotoSaveHandler] เก็บภาพสำเร็จ #{capturedPhotos.Count} — วัตถุ: {subjectNames}" +
            (saveToDisk ? $" — เซฟไว้ที่: {filePath}" : " — (ไม่ได้เซฟลงดิสก์ เพราะ Save To Disk ปิดอยู่)")
        );
    }

    private string SavePhotoToDisk(Texture2D photo)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, saveFolderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        byte[] pngBytes = photo.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngBytes);

        return fullPath;
    }
}
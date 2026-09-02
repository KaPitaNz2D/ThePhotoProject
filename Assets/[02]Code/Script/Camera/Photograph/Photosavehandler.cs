using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subscribe Event OnPhotoCaptured จาก PhotoShooter แล้วส่งต่อไปเก็บที่ PhotoStorage
///
/// ไม่เก็บ Texture2D ไว้เองเลย — Encode เป็น PNG bytes แล้ว Destroy Texture2D ทิ้งทันที
/// เพื่อประหยัด RAM ตามที่ต้องการ (ปล่อยให้ PhotoStorage เก็บแค่ Path บนดิสก์แทน)
/// </summary>
public class PhotoSaveHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ถ้าไม่ลากใส่ไว้ จะพยายาม GetComponent หาเองบน GameObject เดียวกัน")]
    public PhotoShooter photoShooter;

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
            Debug.LogWarning("[PhotoSaveHandler] ได้ภาพเป็น null มา — Capture ล้มเหลว ข้ามการบันทึกรอบนี้");
            return;
        }

        if (PhotoStorage.Instance == null)
        {
            Debug.LogError("[PhotoSaveHandler] หา PhotoStorage.Instance ไม่เจอ! วาง GameObject ที่มี PhotoStorage.cs ไว้ในซีนหรือยัง");
            Destroy(photo);
            return;
        }

        // ดึง creatureId จากวัตถุที่ถ่ายติด เก็บเป็น string เท่านั้น (ไม่เก็บ GameObject reference ข้าม Session)
        List<string> creatureIds = new List<string>();
        foreach (GameObject subject in subjects)
        {
            PhotoSubject photoSubject = subject.GetComponent<PhotoSubject>();
            if (photoSubject != null)
            {
                creatureIds.Add(photoSubject.creatureId);
            }
        }

        byte[] pngBytes = photo.EncodeToPNG();
        Destroy(photo); // Encode เสร็จแล้ว ไม่ต้องเก็บ Texture2D ไว้ใน RAM อีกต่อไป

        bool success = PhotoStorage.Instance.TryStorePhoto(pngBytes, creatureIds);

        string subjectNames = creatureIds.Count > 0 ? string.Join(", ", creatureIds) : "(ไม่เจอวัตถุที่ถ่ายติด)";

        if (success)
        {
            Debug.Log($"[PhotoSaveHandler] บันทึกภาพสำเร็จ ({PhotoStorage.Instance.Photos.Count}/{PhotoStorage.Instance.maxCapacity}) — วัตถุ: {subjectNames}");
        }
        else
        {
            // ปกติไม่ควรเกิดเคสนี้ เพราะ PhotoShooter เช็ค IsFull ก่อนให้ถ่ายอยู่แล้ว
            // แต่กันไว้เผื่อกรณี Race Condition หรือมีการเรียก TryStorePhoto จากที่อื่นด้วย
            Debug.LogWarning("[PhotoSaveHandler] Storage เต็มแล้ว! บันทึกภาพไม่สำเร็จ ต้องลบภาพเก่าก่อนถึงจะถ่ายเพิ่มได้");
        }
    }
}
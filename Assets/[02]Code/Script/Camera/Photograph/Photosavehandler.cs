using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Subscribe Event OnPhotoCaptured จาก PhotoShooter แล้วส่งต่อไปเก็บที่ PhotoStorage — แบบไม่ทำให้เฟรมค้าง:
///   1) อ่านพิกเซลจาก RenderTexture ด้วย AsyncGPUReadback (ไม่บังคับ CPU รอ GPU) ได้ข้อมูลหลังจากนั้น 1-3 เฟรม
///   2) ส่งพิกเซลให้ PhotoStorage.TryStorePhotoAsync เข้ารหัส PNG + เขียนไฟล์ + ย่อรูปย่อบน Thread เบื้องหลัง
/// ไม่เก็บ Texture2D ตัวเต็มไว้เลย (ไม่สร้างด้วยซ้ำ) ประหยัด RAM ตามที่ออกแบบไว้ PhotoStorage เก็บแค่ Path ไฟล์ + รูปย่อเล็กๆ
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

    private void HandlePhotoCaptured(RenderTexture photo, List<GameObject> subjects)
    {
        if (photo == null)
        {
            Debug.LogWarning("[PhotoSaveHandler] ได้ภาพเป็น null มา — Capture ล้มเหลว ข้ามการบันทึกรอบนี้");
            return;
        }

        if (PhotoStorage.Instance == null)
        {
            Debug.LogError("[PhotoSaveHandler] หา PhotoStorage.Instance ไม่เจอ! วาง GameObject ที่มี PhotoStorage.cs ไว้ในซีนหรือยัง");
            return;
        }

        // ดึง creatureId จากวัตถุที่ถ่ายติด ผ่าน CreatureProfile ที่ผูกไว้กับ PhotoSubject (กันพิมพ์ผิด)
        // ทำตอนนี้เลย (ไม่รอ Readback) เพราะวัตถุอาจถูก Despawn ไปก่อนข้อมูลภาพจะมาถึง
        List<string> creatureIds = new List<string>();
        foreach (GameObject subject in subjects)
        {
            PhotoSubject photoSubject = subject.GetComponent<PhotoSubject>();
            if (photoSubject == null) continue;

            string id = photoSubject.CreatureId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[PhotoSaveHandler] '{subject.name}' มี PhotoSubject แต่ยังไม่ได้ผูก CreatureProfile ไว้ — ข้ามไปก่อน");
                continue;
            }
            creatureIds.Add(id);
        }

        // เช็คก่อนว่ายังมีที่ว่าง (ปกติ PhotoShooter เช็ค IsFull ก่อนให้ถ่ายอยู่แล้ว แต่กันไว้เผื่อมีการถ่ายซ้อนกัน/เรียกจากที่อื่น)
        if (PhotoStorage.Instance.IsFull)
        {
            Debug.LogWarning("[PhotoSaveHandler] Storage เต็มแล้ว! บันทึกภาพไม่สำเร็จ ต้องลบภาพเก่าก่อนถึงจะถ่ายเพิ่มได้");
            return;
        }

        int width = photo.width;
        int height = photo.height;

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            AsyncGPUReadback.Request(photo, 0, TextureFormat.RGBA32,
                request => OnReadbackFinished(request, width, height, creatureIds));
        }
        else
        {
            // แพลตฟอร์มที่ไม่รองรับ Async Readback: ยอมอ่านแบบเดิม (เฟรมค้างครั้งเดียว) แต่ยังเข้ารหัส PNG เบื้องหลังอยู่
            StorePixels(ReadPixelsBlocking(photo), width, height, creatureIds);
        }
    }

    private void OnReadbackFinished(AsyncGPUReadbackRequest request, int width, int height, List<string> creatureIds)
    {
        if (request.hasError)
        {
            Debug.LogWarning("[PhotoSaveHandler] อ่านพิกเซลจาก GPU ไม่สำเร็จ — ข้ามการบันทึกรอบนี้");
            return;
        }

        // ข้อมูลใน request ใช้ได้เฉพาะใน Callback นี้ — ต้องคัดลอกออกมาก่อนส่งให้ Thread เบื้องหลัง
        StorePixels(request.GetData<byte>().ToArray(), width, height, creatureIds);
    }

    private void StorePixels(byte[] pixels, int width, int height, List<string> creatureIds)
    {
        if (PhotoStorage.Instance == null) return;

        bool accepted = PhotoStorage.Instance.TryStorePhotoAsync(pixels, width, height, creatureIds);

        if (accepted)
        {
            string subjectNames = creatureIds.Count > 0 ? string.Join(", ", creatureIds) : "(ไม่เจอวัตถุที่ถ่ายติด)";
            Debug.Log($"[PhotoSaveHandler] กำลังบันทึกภาพเบื้องหลัง — วัตถุ: {subjectNames}");
        }
        else
        {
            Debug.LogWarning("[PhotoSaveHandler] Storage เต็มแล้ว! บันทึกภาพไม่สำเร็จ ต้องลบภาพเก่าก่อนถึงจะถ่ายเพิ่มได้");
        }
    }

    private static byte[] ReadPixelsBlocking(RenderTexture source)
    {
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = source;

        Texture2D temp = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        temp.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        byte[] pixels = temp.GetRawTextureData();

        RenderTexture.active = previousActive;
        Destroy(temp);
        return pixels;
    }
}

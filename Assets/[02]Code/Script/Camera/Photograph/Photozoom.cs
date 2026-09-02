using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// จัดการซูมเข้า-ออกของกล้องถ่ายรูป โดยปรับ Field of View ทั้งฝั่งที่แสดงผลจริง (photoVcam)
/// และฝั่งที่ใช้ Capture ภาพ (captureCamera) ให้ตรงกันเสมอ — ไม่งั้นภาพที่ถ่ายได้จะไม่ตรงกับที่เห็นบนจอตอนซูม
///
/// ทำงานเฉพาะตอน SystemState.Photograph เท่านั้น
/// </summary>
public class PhotoZoom : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Cinemachine Camera ของ PhotoCam ตัวที่แสดงผลจริงบนจอ")]
    public CinemachineCamera photoVcam;
    [Tooltip("Camera component เดียวกับที่ PhotoShooter ใช้ Capture ภาพ (photoCamera field ใน PhotoShooter)")]
    public Camera captureCamera;

    [Header("Input")]
    [Tooltip("Action แบบ Axis/float เช่น Mouse Scroll Y")]
    public InputActionReference zoomInput;

    [Header("Zoom Limits — ปรับได้ตรงนี้ และดึงไปโชว์ UI ได้ผ่าน CurrentZoomNormalized")]
    [Tooltip("มุมมองกว้างสุด (ซูมออกสุด)")]
    public float maxFOV = 60f;
    [Tooltip("มุมมองแคบสุด (ซูมเข้าสุด)")]
    public float minFOV = 15f;
    public float zoomSpeed = 20f;
    [Tooltip("ความนุ่มนวลตอนกล้องไล่ตามค่าซูมเป้าหมาย ยิ่งมากยิ่งไว")]
    public float fovSmoothing = 10f;

    [Header("Motion Blur ตอนซูม")]
    [Tooltip("Volume ที่มี Depth of Field หรือ Motion Blur Override ไว้ (Weight จะถูกคุมจากสคริปต์นี้)")]
    public Volume zoomBlurVolume;
    public float blurFadeSpeed = 6f;
    [Tooltip("ค่าความต่างระหว่าง FOV ปัจจุบันกับเป้าหมาย ที่ต่ำกว่านี้ถือว่า \"หยุดนิ่งแล้ว\" ให้เบลอจางหาย")]
    public float stationaryThreshold = 0.05f;

    [Header("Audio — ลาก AudioClip ใส่ได้ตามต้องการ ไม่ใส่ก็ได้ไม่ error")]
    public AudioClip zoomLimitSound;

    /// <summary>ค่าซูมปัจจุบัน Normalize เป็น 0-1 (0 = ซูมออกสุด, 1 = ซูมเข้าสุด) — เอาไปทำ UI แถบซูมได้ตรงๆ</summary>
    public float CurrentZoomNormalized => Mathf.InverseLerp(maxFOV, minFOV, targetFOV);
    public float MinFOV => minFOV;
    public float MaxFOV => maxFOV;

    /// <summary>ยิง Event ทุกครั้งที่ค่าซูมเปลี่ยน ส่งค่า Normalize (0-1) ไปให้ UI ที่ Subscribe ไว้</summary>
    public event Action<float> OnZoomChanged;

    private float targetFOV;
    private float currentFOV;
    private bool wasAtLimit;

    private void Start()
    {
        targetFOV = maxFOV;
        currentFOV = maxFOV;
        ApplyFOV(currentFOV);

        if (zoomInput != null) zoomInput.action.Enable();
    }

    private void Update()
    {
        // ซูมได้เฉพาะตอนอยู่ในโหมดถ่ายรูปเท่านั้น
        if (StateManager.Instance != null && !StateManager.Instance.IsSystemState(StateManager.SystemState.Photograph))
        {
            return;
        }
        if (zoomInput == null) return;

        ReadZoomInput();

        float previousFOV = currentFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovSmoothing);
        ApplyFOV(currentFOV);

        // ถือว่า "กำลังเคลื่อนไหวอยู่" ถ้ายังไล่ FOV ไม่ถึงเป้าหมาย หรือเพิ่งขยับเมื่อเฟรมที่แล้ว
        bool isMoving = Mathf.Abs(currentFOV - targetFOV) > stationaryThreshold
                         || Mathf.Abs(currentFOV - previousFOV) > 0.001f;
        UpdateBlur(isMoving);

        OnZoomChanged?.Invoke(CurrentZoomNormalized);
    }

    private void ReadZoomInput()
    {
        float scrollValue = zoomInput.action.ReadValue<float>();
        if (Mathf.Abs(scrollValue) < 0.01f) return;

        targetFOV -= scrollValue * zoomSpeed * Time.deltaTime;

        bool clamped = targetFOV < minFOV || targetFOV > maxFOV;
        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);

        // เล่นเสียง "คลิก" ตอนชนขอบซูมพอดี (เล่นครั้งเดียวตอนชนใหม่ ไม่เล่นซ้ำทุกเฟรมที่ยังกดค้าง)
        if (clamped && !wasAtLimit)
        {
            AudioManager.Instance?.PlaySFX(zoomLimitSound);
        }
        wasAtLimit = clamped;
    }

    private void ApplyFOV(float fov)
    {
        if (photoVcam != null)
        {
            LensSettings lens = photoVcam.Lens;
            lens.FieldOfView = fov;
            photoVcam.Lens = lens;
        }
        if (captureCamera != null)
        {
            // สำคัญ: ต้องตั้งให้ตรงกับฝั่งแสดงผลเสมอ ไม่งั้นภาพที่ถ่ายได้จะซูมไม่ตรงกับที่เห็นบนจอ
            captureCamera.fieldOfView = fov;
        }
    }

    private void UpdateBlur(bool isMoving)
    {
        if (zoomBlurVolume == null) return;
        float targetWeight = isMoving ? 1f : 0f;
        zoomBlurVolume.weight = Mathf.Lerp(zoomBlurVolume.weight, targetWeight, Time.deltaTime * blurFadeSpeed);
    }
}
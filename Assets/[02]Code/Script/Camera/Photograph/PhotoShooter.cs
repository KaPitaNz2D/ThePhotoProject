using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// จัดการหน้าที่หลักของระบบถ่ายรูป:
///   1) รับปุ่มเข้า/ออกโหมดถ่ายรูป -> สั่งเปลี่ยน StateManager.SystemState
///   2) รับปุ่มชัตเตอร์ -> ยิง SphereCast เช็คว่าถ่ายติดวัตถุที่มี Tag "Photographable"
///   3) Capture ภาพผ่าน RenderTexture แล้วส่งต่อผ่าน Event
///   4) อัปเดตสีจุดเล็งเป้าหมาย (Crosshair) กลางจอแบบ Real-time
///   5) ยืดระยะ SphereCast อัตโนมัติตามระดับซูม (ผูกกับ PhotoZoom)
/// </summary>
public class PhotoShooter : MonoBehaviour
{
    [Header("Camera References")]
    public Camera photoCamera;
    public Transform castOrigin;

    [Header("UI Crosshair")]
    [Tooltip("UI Image จุดเล็งกลางจอ")]
    public Image centerDotImage;
    public Color normalColor = new Color(1f, 1f, 1f, 0.3f);
    public Color detectedColor = new Color(0.6f, 1f, 0.6f, 0.8f);

    [Header("Input")]
    public InputActionReference enterPhotoModeInput;
    public InputActionReference shutterInput;

    [Header("Detection Settings")]
    [Tooltip("ระยะยิง SphereCast พื้นฐาน (ตอนไม่ได้ซูมเลย)")]
    public float castDistance = 20f;
    public float castRadius = 0.5f;
    [Tooltip("Layer ที่ SphereCast จะชนด้วย — ห้ามปล่อยเป็น \"Nothing\" ไม่งั้นตรวจจับอะไรไม่ได้เลยสักชิ้น")]
    public LayerMask detectableLayer;
    public string photographableTag = "Photographable";

    [Header("Zoom Integration")]
    [Tooltip("ถ้าใส่ไว้ ระยะ SphereCast (ทั้ง Crosshair และตอนกดชัตเตอร์) จะยืดออกอัตโนมัติตามระดับซูม")]
    public PhotoZoom photoZoom;
    [Tooltip("เพดานตัวคูณระยะสูงสุด กันค่าพุ่งเกินจริงตอนซูมสุดขีด")]
    public float maxZoomCastDistanceMultiplier = 5f;

    [Header("Render Texture")]
    public RenderTexture photoRenderTexture;
    public int photoWidth = 1920;
    public int photoHeight = 1080;

    [Header("Shutter Settings")]
    public float shutterCooldown = 1f;
    public PhotoTransitionUI transitionUI;

    [Header("Audio")]
    public AudioClip shutterSound;
    public AudioClip enterPhotoModeSound;
    public AudioClip exitPhotoModeSound;

    [Header("Debug")]
    public bool showDebugGizmo = true;

    private float lastShotTime = -999f;
    private int lastDetectedCount = -1;

    /// <summary>
    /// ยิงตอนกดชัตเตอร์ พร้อม RenderTexture ที่เพิ่งเรนเดอร์ภาพเสร็จ (ยังไม่ได้อ่านพิกเซลออกมา — ผู้ฟังอ่านเองแบบ async เช่น AsyncGPUReadback
    /// เพื่อไม่ให้เฟรมค้าง) กับรายชื่อวัตถุที่ถ่ายติด RenderTexture นี้ใช้ซ้ำทุกช็อต ผู้ฟังห้ามเก็บอ้างอิงไว้ใช้ต่อหลัง Request อ่านพิกเซลไปแล้ว
    /// </summary>
    public event Action<RenderTexture, List<GameObject>> OnPhotoCaptured;

    private RenderTexture ownedCaptureTexture; // สร้างเองครั้งเดียวเมื่อไม่ได้ลาก photoRenderTexture ใส่ (เดิมสร้างใหม่ทุกช็อตแล้วไม่เคยปล่อย)

    /// <summary>
    /// ระยะยิง SphereCast จริง ณ ตอนนี้ — คำนวณจากอัตราส่วน FOV ปัจจุบันเทียบกับ FOV ตอนไม่ซูม
    /// ใช้ร่วมกันทั้ง Crosshair (Update ทุกเฟรม) และตอนกดชัตเตอร์จริง ให้ตรงกันเป๊ะเสมอ
    /// </summary>
    private float EffectiveCastDistance
    {
        get
        {
            if (photoZoom == null) return castDistance;

            float currentFOV = Mathf.Lerp(photoZoom.MaxFOV, photoZoom.MinFOV, photoZoom.CurrentZoomNormalized);
            float magnification = photoZoom.MaxFOV / currentFOV;
            magnification = Mathf.Clamp(magnification, 1f, maxZoomCastDistanceMultiplier);

            return castDistance * magnification;
        }
    }

    private void Awake()
    {
        if (castOrigin == null && photoCamera != null)
        {
            castOrigin = photoCamera.transform;
        }

        // เตือนไว้ก่อนตั้งแต่ Awake ถ้าลืมตั้ง Detectable Layer จะได้เห็น Warning ทันทีตั้งแต่เปิดเกม
        // ไม่ต้องรอไปเจอปัญหา "ถ่ายไม่ติดอะไรเลย" ตอนเล่นจริงแล้วงงว่าทำไม
        if (detectableLayer.value == 0)
        {
            Debug.LogWarning("[PhotoShooter] Detectable Layer ถูกตั้งเป็น \"Nothing\" อยู่! " +
                              "SphereCast จะตรวจจับอะไรไม่ได้เลยสักชิ้น ต้องเลือก Layer ของสิ่งที่ถ่ายได้ใน Inspector ก่อน");
        }
    }

    private void Start()
    {
        // บังคับปิดจุดเล็งไว้ก่อนตั้งแต่ตอนเริ่มเกม ไม่รอให้ Update() เป็นคนสั่งซ่อนทีหลัง
        // (กันเคส StateManager.Instance ยังไม่พร้อมในเฟรมแรกๆ ทำให้ Update() ข้ามไปเงียบๆ
        // แล้วจุดเล็งค้างโชว์ตามค่า Enabled เดิมที่ตั้งไว้ใน Editor)
        if (centerDotImage != null) centerDotImage.enabled = false;
    }

    private void OnEnable()
    {
        if (enterPhotoModeInput != null)
        {
            enterPhotoModeInput.action.Enable();
            enterPhotoModeInput.action.performed += OnToggleEnterPhotoMode;
        }
        if (shutterInput != null)
        {
            shutterInput.action.Enable();
            shutterInput.action.performed += OnShutterPressed;
        }
    }

    private void OnDisable()
    {
        if (enterPhotoModeInput != null) enterPhotoModeInput.action.performed -= OnToggleEnterPhotoMode;
        if (shutterInput != null) shutterInput.action.performed -= OnShutterPressed;
    }

    // ==================== เช็คเป้าหมายอัปเดต UI ====================
    private void Update()
    {
        if (StateManager.Instance == null) return;

        if (StateManager.Instance.CurrentSystemState != StateManager.SystemState.Photograph)
        {
            if (centerDotImage != null) centerDotImage.enabled = false;
            return;
        }

        if (centerDotImage != null)
        {
            centerDotImage.enabled = true;
            UpdateCrosshairColor();
        }
    }

    private void UpdateCrosshairColor()
    {
        if (castOrigin == null) return;

        RaycastHit[] hits = Physics.SphereCastAll(
            castOrigin.position,
            castRadius,
            castOrigin.forward,
            EffectiveCastDistance,
            detectableLayer
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag(photographableTag))
            {
                PhotoSubject subject = hit.collider.GetComponentInParent<PhotoSubject>();
                if (subject != null)
                {
                    centerDotImage.color = detectedColor;
                    return;
                }
            }
        }

        centerDotImage.color = normalColor;
    }

    // ==================== เข้า/ออกโหมดถ่ายรูป ====================
    private void OnToggleEnterPhotoMode(InputAction.CallbackContext ctx) => ToggleEnterPhotoMode();

    /// <summary>เรียกจากปุ่ม UI (Button.onClick) หรือ Input Action ก็ได้ — สลับเข้า/ออกโหมดถ่ายรูป</summary>
    public void ToggleEnterPhotoMode()
    {
        if (StateManager.Instance == null) return;

        StateManager.SystemState current = StateManager.Instance.CurrentSystemState;

        if (current == StateManager.SystemState.Photograph)
        {
            StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
            AudioManager.Instance?.PlaySFX(exitPhotoModeSound);
        }
        else if (current == StateManager.SystemState.Normal)
        {
            StateManager.Instance.SetSystemState(StateManager.SystemState.Photograph);
            AudioManager.Instance?.PlaySFX(enterPhotoModeSound);
        }
    }

    // ==================== กดชัตเตอร์ ====================
    private void OnShutterPressed(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null) return;
        if (StateManager.Instance.CurrentSystemState != StateManager.SystemState.Photograph) return;

        if (PhotoStorage.Instance != null && PhotoStorage.Instance.IsFull)
        {
            Debug.LogWarning("[PhotoShooter] Storage เต็มแล้ว ถ่ายเพิ่มไม่ได้");
            return;
        }

        if (Time.time - lastShotTime < shutterCooldown) return;
        lastShotTime = Time.time;

        TakePhoto();
    }

    private void TakePhoto()
    {
        List<GameObject> subjects = DetectSubjects();
        RenderTexture photo = RenderPhoto();

        if (transitionUI != null) transitionUI.PlayShutterFlash();
        AudioManager.Instance?.PlaySFX(shutterSound);
        // ประกาศเสียงชัตเตอร์ให้สัตว์ในระยะได้ยิน (สัตว์เช็คระยะเองตามหูของสายพันธุ์)
        CreatureNoise.EmitShutter(castOrigin != null ? castOrigin.position : transform.position);

        lastDetectedCount = subjects.Count;
        OnPhotoCaptured?.Invoke(photo, subjects);
    }

    // ==================== SphereCast เช็ควัตถุ (กดชัตเตอร์) ====================
    private List<GameObject> DetectSubjects()
    {
        List<GameObject> hitSubjects = new List<GameObject>();
        if (castOrigin == null) return hitSubjects;

        RaycastHit[] hits = Physics.SphereCastAll(
            castOrigin.position,
            castRadius,
            castOrigin.forward,
            EffectiveCastDistance,
            detectableLayer
        );

        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.CompareTag(photographableTag)) continue;

            PhotoSubject subject = hit.collider.GetComponentInParent<PhotoSubject>();
            if (subject == null) continue;

            if (!hitSubjects.Contains(subject.gameObject))
            {
                hitSubjects.Add(subject.gameObject);
            }
        }

        return hitSubjects;
    }

    // ==================== Capture ภาพ ====================
    // เรนเดอร์ภาพลง RenderTexture แล้วคืนตัว RenderTexture เลย — ไม่ ReadPixels ที่นี่ เพราะ ReadPixels บังคับให้ CPU รอ GPU เรนเดอร์จบ
    // (เฟรมค้าง) และ Apply() ที่เคยตามมายังอัปโหลดภาพ 6 MB กลับขึ้น GPU โดยไม่จำเป็น การอ่านพิกเซลไปทำแบบ async ใน PhotoSaveHandler แทน
    private RenderTexture RenderPhoto()
    {
        if (photoCamera == null) return null;

        RenderTexture rt = photoRenderTexture;
        if (rt == null)
        {
            if (ownedCaptureTexture == null) ownedCaptureTexture = new RenderTexture(photoWidth, photoHeight, 24);
            rt = ownedCaptureTexture;
        }

        RenderTexture previousTarget = photoCamera.targetTexture;

        photoCamera.targetTexture = rt;
        photoCamera.Render();
        photoCamera.targetTexture = previousTarget;

        return rt;
    }

    private void OnDestroy()
    {
        if (ownedCaptureTexture != null) ownedCaptureTexture.Release();
    }

    // ==================== Debug Gizmos ====================
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmo) return;

        Transform origin = castOrigin != null ? castOrigin : (photoCamera != null ? photoCamera.transform : null);
        if (origin == null) return;

        Gizmos.color = lastDetectedCount > 0 ? Color.green : (lastDetectedCount == 0 ? Color.red : Color.yellow);

        Vector3 startPos = origin.position;
        Vector3 endPos = startPos + origin.forward * EffectiveCastDistance;

        Gizmos.DrawWireSphere(startPos, castRadius);
        Gizmos.DrawWireSphere(endPos, castRadius);

        Vector3 up = origin.up * castRadius;
        Vector3 right = origin.right * castRadius;
        Gizmos.DrawLine(startPos + up, endPos + up);
        Gizmos.DrawLine(startPos - up, endPos - up);
        Gizmos.DrawLine(startPos + right, endPos + right);
        Gizmos.DrawLine(startPos - right, endPos - right);
        Gizmos.DrawLine(startPos, endPos);
    }
}
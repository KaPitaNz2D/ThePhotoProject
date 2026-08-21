using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// จัดการ 3 หน้าที่หลักของระบบถ่ายรูป:
///   1) รับปุ่มเข้า/ออกโหมดถ่ายรูป -> สั่งเปลี่ยน StateManager.SystemState (กล้องจะสลับตามผ่าน CameraController)
///   2) รับปุ่มชัตเตอร์ -> ยิง SphereCast เช็คว่าถ่ายติดวัตถุที่มี Tag "Photographable" ตัวไหนบ้าง
///   3) Capture ภาพผ่าน RenderTexture แล้วส่งต่อผ่าน Event ให้สคริปต์อื่นจัดการ (เซฟไฟล์ / อัพเดท Field Guide ฯลฯ)
///
/// สคริปต์นี้ "ไม่เซฟไฟล์เอง ไม่ unlock Field Guide เอง" -- แค่ยิง Event ออกไปให้สคริปต์อื่น Subscribe
/// </summary>
public class PhotoShooter : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("Camera component จริงที่ใช้ Capture ภาพ (แนะนำใช้กล้องแยกต่างหากจาก Main Camera " +
             "เพื่อกำหนด Culling Mask ตัดชั้น UI/HUD ออกได้ง่าย ไม่ให้ติดไปในรูป)")]
    public Camera photoCamera;
    [Tooltip("จุดเริ่มยิง SphereCast ถ้าไม่ใส่ไว้ จะใช้ Transform ของ photoCamera แทน")]
    public Transform castOrigin;

    [Header("Input")]
    [Tooltip("ปุ่มสลับเข้า/ออกโหมดถ่ายรูป (Button) เช่นคลิกขวา")]
    public InputActionReference enterPhotoModeInput;
    [Tooltip("ปุ่มชัตเตอร์ถ่ายรูป (Button) เช่นคลิกซ้าย — ทำงานเฉพาะตอนอยู่ในโหมดถ่ายรูปแล้วเท่านั้น")]
    public InputActionReference shutterInput;

    [Header("Detection Settings")]
    [Tooltip("ระยะยิง SphereCast")]
    public float castDistance = 20f;
    [Tooltip("รัศมีของ SphereCast (ยิ่งใหญ่ยิ่งจับวัตถุที่ไม่ได้อยู่กึ่งกลางจอเป๊ะๆ ได้ง่ายขึ้น)")]
    public float castRadius = 0.5f;
    [Tooltip("Layer ที่ SphereCast จะชนด้วย (ตั้งเฉพาะ Layer ของสิ่งมีชีวิต/วัตถุที่ถ่ายได้ ลด False Positive)")]
    public LayerMask detectableLayer;
    [Tooltip("Tag ที่ถือว่าเป็น \"สิ่งที่ถ่ายรูปได้\"")]
    public string photographableTag = "Photographable";

    [Header("Render Texture")]
    [Tooltip("RenderTexture ที่จะใช้ Capture ภาพ ถ้าไม่ใส่ไว้จะสร้างขึ้นมาเองตาม Photo Width/Height")]
    public RenderTexture photoRenderTexture;
    public int photoWidth = 1920;
    public int photoHeight = 1080;

    [Header("Shutter Settings")]
    [Tooltip("ระยะเวลาขั้นต่ำระหว่างการถ่ายรูปแต่ละครั้ง (วินาที) กันสแปมชัตเตอร์")]
    public float shutterCooldown = 1f;
    [Tooltip("ถ้าใส่ไว้ จะเล่นเอฟเฟกต์ Flash ดำสั้นๆ ทุกครั้งที่ถ่ายรูปสำเร็จ")]
    public PhotoTransitionUI transitionUI;

    [Header("Audio — ลาก AudioClip ใส่ได้ตามต้องการ ไม่ใส่ก็ได้ไม่ error")]
    public AudioClip shutterSound;
    public AudioClip enterPhotoModeSound;
    public AudioClip exitPhotoModeSound;

    private float lastShotTime = -999f;

    /// <summary>
    /// Event ยิงออกไปทุกครั้งที่ถ่ายรูปสำเร็จ พร้อมภาพที่ Capture ได้ และ List วัตถุที่ถ่ายติด
    /// สคริปต์อื่น (เช่น PhotoSaveHandler, FieldGuideManager) มา Subscribe ตรงนี้เพื่อทำงานต่อ
    /// </summary>
    public event Action<Texture2D, List<GameObject>> OnPhotoCaptured;

    private void Awake()
    {
        if (castOrigin == null && photoCamera != null)
        {
            castOrigin = photoCamera.transform;
        }
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
        if (enterPhotoModeInput != null)
        {
            enterPhotoModeInput.action.performed -= OnToggleEnterPhotoMode;
        }
        if (shutterInput != null)
        {
            shutterInput.action.performed -= OnShutterPressed;
        }
    }

    // ==================== เข้า/ออกโหมดถ่ายรูป ====================
    private void OnToggleEnterPhotoMode(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null) return;

        StateManager.SystemState current = StateManager.Instance.CurrentSystemState;

        if (current == StateManager.SystemState.Photograph)
        {
            // อยู่ในโหมดถ่ายรูปอยู่แล้ว -> กดซ้ำเพื่อออก กลับไป Normal
            StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
            AudioManager.Instance?.PlaySFX(exitPhotoModeSound);
        }
        else if (current == StateManager.SystemState.Normal)
        {
            // เข้าโหมดถ่ายรูปได้เฉพาะตอนอยู่ Normal เท่านั้น
            // กันไม่ให้เผลอเข้าโหมดถ่ายรูปซ้อนตอนกำลัง Talking/Pause/Journal อยู่
            StateManager.Instance.SetSystemState(StateManager.SystemState.Photograph);
            AudioManager.Instance?.PlaySFX(enterPhotoModeSound);
        }
    }

    // ==================== กดชัตเตอร์ ====================
    private void OnShutterPressed(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance == null) return;
        if (StateManager.Instance.CurrentSystemState != StateManager.SystemState.Photograph) return;

        // กันสแปมชัตเตอร์ — ต้องรอครบ Cooldown ก่อนถึงจะถ่ายรอบถัดไปได้
        if (Time.time - lastShotTime < shutterCooldown) return;
        lastShotTime = Time.time;

        TakePhoto();
    }

    private void TakePhoto()
    {
        List<GameObject> subjects = DetectSubjects();
        Texture2D photo = CaptureRenderTexture();

        if (transitionUI != null)
        {
            transitionUI.PlayShutterFlash();
        }
        AudioManager.Instance?.PlaySFX(shutterSound);

        // TODO: ลบ Debug.Log นี้ทิ้งตอนมีสคริปต์แสดงผล/เซฟภาพแล้ว — ไว้ใช้เช็คว่า Logic ทำงานถูกไหมชั่วคราว
        Debug.Log($"[PhotoShooter] ถ่ายรูปแล้ว! เจอวัตถุ {subjects.Count} ชิ้น: " +
                  string.Join(", ", subjects.ConvertAll(s => s.name)));

        lastDetectedCount = subjects.Count; // เก็บไว้ใช้เปลี่ยนสี Gizmo ตอน Debug
        OnPhotoCaptured?.Invoke(photo, subjects);
    }

    // ==================== Debug Gizmos ====================
    [Header("Debug")]
    [Tooltip("วาดรูปทรง SphereCast ใน Scene View ให้เห็นว่ายิงไกล/กว้างแค่ไหน (เห็นได้แม้ไม่ได้กด Play)")]
    public bool showDebugGizmo = true;

    private int lastDetectedCount = -1; // -1 = ยังไม่เคยถ่าย, ใช้แยกสีเทาไว้ก่อน

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmo) return;

        Transform origin = castOrigin != null ? castOrigin : (photoCamera != null ? photoCamera.transform : null);
        if (origin == null) return;

        // สีเขียว = ครั้งล่าสุดถ่ายติดวัตถุ, สีแดง = ถ่ายไม่ติดเลย, สีเหลือง = ยังไม่เคยถ่าย
        Gizmos.color = lastDetectedCount > 0 ? Color.green : (lastDetectedCount == 0 ? Color.red : Color.yellow);

        Vector3 startPos = origin.position;
        Vector3 endPos = startPos + origin.forward * castDistance;

        // วาดทรงกลมหัว-ท้าย + เส้นเชื่อมด้านข้าง ให้เห็นรูปทรง Capsule จริงของ SphereCast
        Gizmos.DrawWireSphere(startPos, castRadius);
        Gizmos.DrawWireSphere(endPos, castRadius);

        Vector3 up = origin.up * castRadius;
        Vector3 right = origin.right * castRadius;
        Gizmos.DrawLine(startPos + up, endPos + up);
        Gizmos.DrawLine(startPos - up, endPos - up);
        Gizmos.DrawLine(startPos + right, endPos + right);
        Gizmos.DrawLine(startPos - right, endPos - right);

        // เส้นกึ่งกลางบอกทิศทางยิงชัดๆ
        Gizmos.DrawLine(startPos, endPos);
    }

    // ==================== SphereCast เช็ควัตถุ ====================
    private List<GameObject> DetectSubjects()
    {
        List<GameObject> hitSubjects = new List<GameObject>();
        if (castOrigin == null) return hitSubjects;

        RaycastHit[] hits = Physics.SphereCastAll(
            castOrigin.position,
            castRadius,
            castOrigin.forward,
            castDistance,
            detectableLayer
        );

        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.CompareTag(photographableTag)) continue;

            // ไม่นับ Collider ย่อยตรงๆ (เช่น Neck, Head) แต่ไล่หา PhotoSubject ที่ Root แทน
            // ทำให้ต่อให้ถ่ายติดหลายส่วนของสิ่งมีชีวิตตัวเดียวกัน (คอ+หัว+เขา ฯลฯ) จะนับรวมเป็นชิ้นเดียว
            PhotoSubject subject = hit.collider.GetComponentInParent<PhotoSubject>();
            if (subject == null)
            {
                // กันลืม: ติด Tag Photographable ไว้แล้ว แต่ลืมแปะ PhotoSubject ไว้ที่ Root
                Debug.LogWarning($"[PhotoShooter] '{hit.collider.name}' ติด Tag {photographableTag} " +
                                  "แต่หา PhotoSubject ที่ Root ไม่เจอ — ข้ามไปก่อน ลองเช็คว่าลืมแปะ Component ไว้หรือเปล่า");
                continue;
            }

            if (!hitSubjects.Contains(subject.gameObject))
            {
                hitSubjects.Add(subject.gameObject);
            }
        }

        return hitSubjects;
    }

    // ==================== Capture ภาพ ====================
    private Texture2D CaptureRenderTexture()
    {
        if (photoCamera == null) return null;

        RenderTexture rt = photoRenderTexture != null
            ? photoRenderTexture
            : new RenderTexture(photoWidth, photoHeight, 24);

        RenderTexture previousTarget = photoCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        // สั่งกล้องเรนเดอร์ลง RenderTexture แทนที่จะขึ้นจอ ป้องกัน HUD ติดไปในภาพ
        // (ถ้า photoCamera เป็นกล้องแยกต่างหาก ตั้ง Culling Mask ไม่รวม Layer UI ไว้ตั้งแต่แรกด้วย)
        photoCamera.targetTexture = rt;
        photoCamera.Render();

        RenderTexture.active = rt;
        Texture2D result = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        result.Apply();

        // คืนค่าเดิมกลับไป ไม่ให้กระทบการเรนเดอร์ปกติของกล้องตัวนี้ในเฟรมถัดไป
        photoCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;

        return result;
    }
}
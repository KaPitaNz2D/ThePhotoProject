using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // เพิ่มใช้งาน UI

/// <summary>
/// จัดการ 3 หน้าที่หลักของระบบถ่ายรูป:
///   1) รับปุ่มเข้า/ออกโหมดถ่ายรูป -> สั่งเปลี่ยน StateManager.SystemState
///   2) รับปุ่มชัตเตอร์ -> ยิง SphereCast เช็คว่าถ่ายติดวัตถุที่มี Tag "Photographable"
///   3) Capture ภาพผ่าน RenderTexture แล้วส่งต่อผ่าน Event
///   4) อัปเดตสีจุดเล็งเป้าหมาย (Crosshair) กลางจอแบบ Real-time
/// </summary>
public class PhotoShooter : MonoBehaviour
{
    [Header("Camera References")]
    public Camera photoCamera;
    public Transform castOrigin;

    [Header("UI Crosshair")]
    [Tooltip("UI Image จุดเล็งกลางจอ")]
    public Image centerDotImage;
    public Color normalColor = new Color(1f, 1f, 1f, 0.3f); // สีขาวโปร่งใส
    public Color detectedColor = new Color(0.6f, 1f, 0.6f, 0.8f); // สีเขียวสว่างเมื่อเจอเป้าหมาย

    [Header("Input")]
    public InputActionReference enterPhotoModeInput;
    public InputActionReference shutterInput;

    [Header("Detection Settings")]
    public float castDistance = 20f;
    public float castRadius = 0.5f;
    public LayerMask detectableLayer;
    public string photographableTag = "Photographable";

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

    private float lastShotTime = -999f;
    public event Action<Texture2D, List<GameObject>> OnPhotoCaptured;
    private int lastDetectedCount = -1;

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
        if (enterPhotoModeInput != null) enterPhotoModeInput.action.performed -= OnToggleEnterPhotoMode;
        if (shutterInput != null) shutterInput.action.performed -= OnShutterPressed;
    }

    // ==================== เช็คเป้าหมายอัปเดต UI (เพิ่มใหม่) ====================
    private void Update()
    {
        if (StateManager.Instance == null) return;

        // ถ้าไม่ได้เปิดกล้องอยู่ ให้ซ่อนเป้าเล็ง
        if (StateManager.Instance.CurrentSystemState != StateManager.SystemState.Photograph)
        {
            if (centerDotImage != null) centerDotImage.enabled = false;
            return;
        }

        // เปิดกล้องอยู่ ให้โชว์เป้าเล็งและยิง SphereCast เช็คสี
        if (centerDotImage != null)
        {
            centerDotImage.enabled = true;
            UpdateCrosshairColor();
        }
    }

    private void UpdateCrosshairColor()
    {
        if (castOrigin == null) return;

        // เปลี่ยนมาใช้ SphereCastAll แบบเดียวกับตอนถ่ายรูป เพื่อกวาดวัตถุทุกชิ้นในรัศมี
        RaycastHit[] hits = Physics.SphereCastAll(
            castOrigin.position,
            castRadius,
            castOrigin.forward,
            castDistance,
            detectableLayer
        );

        foreach (RaycastHit hit in hits)
        {
            // เช็คว่ามีชิ้นไหนในกลุ่มที่โดนชน มี Tag ที่ถูกต้องหรือไม่
            if (hit.collider.CompareTag(photographableTag))
            {
                PhotoSubject subject = hit.collider.GetComponentInParent<PhotoSubject>();
                if (subject != null)
                {
                    // เจอเป้าหมายปุ๊บ เปลี่ยนจุดเป็นสีเขียว แล้วจบการทำงานทันที
                    centerDotImage.color = detectedColor;
                    return;
                }
            }
        }

        // ถ้าวนลูปจนจบแล้วยังไม่เจอเป้าหมายที่ตรงเงื่อนไขเลย ให้กลับเป็นสีปกติ
        centerDotImage.color = normalColor;
    }
    // ==================== เข้า/ออกโหมดถ่ายรูป ====================
    private void OnToggleEnterPhotoMode(InputAction.CallbackContext ctx)
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
            Debug.LogWarning($"[PhotoShooter] Storage เต็มแล้ว ถ่ายเพิ่มไม่ได้");
            return;
        }

        if (Time.time - lastShotTime < shutterCooldown) return;
        lastShotTime = Time.time;

        TakePhoto();
    }

    private void TakePhoto()
    {
        List<GameObject> subjects = DetectSubjects();
        Texture2D photo = CaptureRenderTexture();

        if (transitionUI != null) transitionUI.PlayShutterFlash();
        AudioManager.Instance?.PlaySFX(shutterSound);

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
            castDistance,
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
    private Texture2D CaptureRenderTexture()
    {
        if (photoCamera == null) return null;

        RenderTexture rt = photoRenderTexture != null
            ? photoRenderTexture
            : new RenderTexture(photoWidth, photoHeight, 24);

        RenderTexture previousTarget = photoCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        photoCamera.targetTexture = rt;
        photoCamera.Render();

        RenderTexture.active = rt;
        Texture2D result = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        result.Apply();

        photoCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;

        return result;
    }

    // ==================== Debug Gizmos ====================
    [Header("Debug")]
    public bool showDebugGizmo = true;

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmo) return;

        Transform origin = castOrigin != null ? castOrigin : (photoCamera != null ? photoCamera.transform : null);
        if (origin == null) return;

        Gizmos.color = lastDetectedCount > 0 ? Color.green : (lastDetectedCount == 0 ? Color.red : Color.yellow);

        Vector3 startPos = origin.position;
        Vector3 endPos = startPos + origin.forward * castDistance;

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
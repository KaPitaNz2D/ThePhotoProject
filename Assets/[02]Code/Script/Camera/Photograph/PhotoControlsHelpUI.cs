using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// แผงสอนปุ่มของโหมดถ่ายรูป (ฝั่งขวาจอ) — วางเป็นลูกของ GridOverlay เลยโชว์เฉพาะตอนอยู่ในโหมดถ่ายรูปเอง (PhotoGridUI เปิด/ปิดให้)
/// เข้าโหมดถ่ายรูปครั้งแรกแผงจะเปิดอยู่ กด Tab (Photograph/ToggleHelp) = เลื่อนแผงทั้งหมดออกไปพ้นขอบขวาจอ / เลื่อนกลับมา
/// จำสถานะไว้ตลอด Session (ปิดไว้แล้วเข้าโหมดถ่ายรูปรอบหน้าก็ยังปิดอยู่) ไม่ได้บันทึกข้ามการเปิดเกม
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PhotoControlsHelpUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("ปุ่มเปิด/ปิดแผง — เว้นว่างได้ จะหา Action \"Photograph/ToggleHelp\" ให้เองผ่าน zoomInput ของ PhotoZoom")]
    public InputActionReference toggleInput;
    [Tooltip("ใช้เป็นทางไปหา InputActionAsset ตอน toggleInput ว่าง (ดู PhotoInputLookup)")]
    public PhotoZoom photoZoom;

    [Header("Slide")]
    [Tooltip("ตำแหน่ง X (anchoredPosition) ตอนแผงเปิดอยู่ — Anchor/Pivot ของแผงอยู่ขอบขวา ค่าติดลบ = เข้ามาในจอ")]
    public float shownX = -83f;
    [Tooltip("ระยะเผื่อพ้นขอบจอตอนซ่อน (px)")]
    public float hiddenMargin = 20f;
    public float slideDuration = 0.25f;

    private static bool isOpen = true; // เข้าโหมดถ่ายรูปครั้งแรกให้เปิดไว้ก่อน

    private RectTransform panel;
    private InputAction toggleAction;
    private float slideT; // 0 = เปิด, 1 = ซ่อน

    private float HiddenX => panel.rect.width + hiddenMargin;

    private void Awake()
    {
        panel = (RectTransform)transform;
    }

    private void OnEnable()
    {
        toggleAction = PhotoInputLookup.Resolve(toggleInput, photoZoom != null ? photoZoom.zoomInput : null, "Photograph/ToggleHelp");
        if (toggleAction != null)
        {
            toggleAction.Enable();
            toggleAction.performed += OnToggle;
        }
        else
        {
            Debug.LogWarning("[PhotoControlsHelpUI] หา Action Photograph/ToggleHelp ไม่เจอ — ปุ่ม Tab จะไม่ทำงาน");
        }

        // เข้าโหมดถ่ายรูปแล้ววางแผงตามสถานะล่าสุดทันที ไม่ต้องเล่นแอนิเมชัน
        slideT = isOpen ? 0f : 1f;
        ApplyPosition();
    }

    private void OnDisable()
    {
        if (toggleAction != null) toggleAction.performed -= OnToggle;
    }

    private void OnToggle(InputAction.CallbackContext ctx) => Toggle();

    public void Toggle()
    {
        isOpen = !isOpen;
    }

    private void Update()
    {
        float target = isOpen ? 0f : 1f;
        if (Mathf.Approximately(slideT, target)) return;

        float step = slideDuration > 0f ? Time.unscaledDeltaTime / slideDuration : 1f;
        slideT = Mathf.MoveTowards(slideT, target, step);
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        float eased = Mathf.SmoothStep(0f, 1f, slideT);
        Vector2 pos = panel.anchoredPosition;
        pos.x = Mathf.Lerp(shownX, HiddenX, eased);
        panel.anchoredPosition = pos;
    }
}

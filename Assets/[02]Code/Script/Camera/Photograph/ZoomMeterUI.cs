using UnityEngine;

/// <summary>
/// แถบ Zoom meter บนกล้องถ่ายรูป — ตัวเลื่อน (handle) ไหลจากซ้าย (ซูมออกสุด) ไปขวา (ซูมเข้าสุด)
/// ฟังค่าจาก PhotoZoom.OnZoomChanged (0-1) แล้ววาง handle ตามสัดส่วนความกว้างของ track
/// วางสคริปต์นี้ไว้บน GameObject ที่ซ่อน/แสดงพร้อม Overlay ของโหมดถ่ายรูป (เช่น GridOverlay)
/// </summary>
public class ZoomMeterUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PhotoZoom ที่จะอ่านค่าซูมมาแสดง")]
    public PhotoZoom photoZoom;
    [Tooltip("RectTransform ของเส้นราง (ความกว้างของรางคือระยะที่ handle เคลื่อนที่ได้)")]
    public RectTransform track;
    [Tooltip("RectTransform ของตัวเลื่อน — ควรตั้ง Anchor/Pivot ที่ขอบซ้ายของ track (anchor x = 0, pivot x = 0)")]
    public RectTransform handle;

    [Header("Motion")]
    [Tooltip("ระยะกันขอบ (px) ซ้าย-ขวา เพื่อให้ handle ไม่ล้นรางตอนสุดทาง")]
    public float padding = 0f;
    [Tooltip("ความนุ่มนวลตอน handle ไล่ตามค่าซูม ยิ่งมากยิ่งไว (0 = ตามทันที)")]
    public float smoothing = 0f;

    private float shown;

    private void OnEnable()
    {
        if (photoZoom == null) return;
        photoZoom.OnZoomChanged += HandleZoomChanged;
        shown = photoZoom.CurrentZoomNormalized;
        Apply(shown);
    }

    private void OnDisable()
    {
        if (photoZoom != null) photoZoom.OnZoomChanged -= HandleZoomChanged;
    }

    private void HandleZoomChanged(float normalized)
    {
        shown = smoothing > 0f ? Mathf.Lerp(shown, normalized, Time.unscaledDeltaTime * smoothing) : normalized;
        Apply(shown);
    }

    private void Apply(float normalized)
    {
        if (track == null || handle == null) return;
        float travel = Mathf.Max(0f, track.rect.width - handle.rect.width - padding * 2f);
        Vector2 pos = handle.anchoredPosition;
        pos.x = padding + travel * Mathf.Clamp01(normalized);
        handle.anchoredPosition = pos;
    }
}

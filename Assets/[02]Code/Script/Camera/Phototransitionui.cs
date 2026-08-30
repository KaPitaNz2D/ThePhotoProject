using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ควบคุม Alpha ของ Image สีดำเต็มจอ (ผ่าน CanvasGroup) สำหรับ 2 เอฟเฟกต์:
///   1) PlayTransition — ตัดกล้อง (Third Person <-> PhotoCam) ทันทีที่เรียก ไม่รอเฟดจบก่อน
///      ส่วนเฟดดำเล่นเป็น Overlay คู่ขนานไปพร้อมกัน (แบบม่านกล้องกระพริบ ไม่ใช่รอปิดสนิทก่อนค่อยตัด)
///   2) PlayShutterFlash — Flash ดำสั้นๆ ตอนกดชัตเตอร์ถ่ายรูป จำลองความรู้สึกกล้องจริง
/// </summary>
public class PhotoTransitionUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("CanvasGroup ของ Image สีดำเต็มจอ (Screen Space Overlay)")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Fade Settings (ตอนเข้า/ออกโหมดถ่ายรูป)")]
    public float fadeDuration = 0.25f;

    [Header("Shutter Flash Settings (ตอนกดถ่ายรูป)")]
    public float shutterFlashDuration = 0.1f;

    private Coroutine currentRoutine;

    private void Awake()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false; // ปกติไม่บล็อก Input เวลาจอใส
        }
    }

    /// <summary>
    /// ตัดกล้อง (onBlackout) ทันทีที่เรียกฟังก์ชันนี้ ไม่รอให้เฟดดำเสร็จก่อน
    /// ส่วนเฟด (ดำ -> ใส) เล่นเป็น Overlay คู่ขนานไปพร้อมกันเฉยๆ เพื่อกลบรอยต่อของการตัดกล้อง
    /// </summary>
    public void PlayTransition(Action onBlackout)
    {
        onBlackout?.Invoke(); // ตัดกล้องทันที ไม่หน่วงรอเฟด

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FadeFlashRoutine(fadeDuration));
    }

    private IEnumerator FadeFlashRoutine(float duration)
    {
        yield return Fade(0f, 1f, duration);
        yield return Fade(1f, 0f, duration);
    }

    /// <summary>Flash ดำสั้นๆ ตอนกดชัตเตอร์ ไม่มี Callback เพราะไม่ต้องสลับอะไร</summary>
    public void PlayShutterFlash()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FadeFlashRoutine(shutterFlashDuration));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }
}
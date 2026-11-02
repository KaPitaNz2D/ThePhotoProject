using UnityEngine;
using Yarn.Unity;

/// <summary>
/// วาง Line Presenter (บับเบิลคำพูด) ไว้เหนือหัวของตัวที่กำลังพูด — ติดไว้ที่ GameObject เดียวกับ LinePresenter
/// และต้องอยู่ใน Dialogue Presenters ของ DialogueRunner ด้วย (ใช้ RunLineAsync แค่รับชื่อผู้พูดของบรรทัดนั้น)
///
/// ตำแหน่ง: หางบับเบิลชี้หัวตัวพูด (DialogueSpeaker ที่ characterName ตรงกับชื่อหน้าบรรทัดใน Yarn)
///   - หัวอยู่นอกจอ/หลังกล้อง -> ดันจุดเป้าหมายมาติดขอบจอตามทิศของหัว
///   - ด้านบนไม่พอ -> พลิกบับเบิลลงล่างหัว หางชี้ขึ้น
///   - ชนขอบซ้าย/ขวา -> เลื่อนบับเบิลเข้าจอ แล้วพลิกซ้าย/ขวาให้หางเอียงไปทางหัว
///   บับเบิลไม่มีทางหลุดขอบจอ (เว้น screenMargin)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SpeechBubblePresenter : DialoguePresenterBase
{
    [Header("Parts")]
    [Tooltip("รูปบับเบิล (มีหาง) — พลิกด้วย localScale")]
    public RectTransform background;
    // ใช้แทนการจับใส่ Container เพราะ Line Presenter เป็น Nested Prefab ของ Yarn ย้าย Parent ลูกเดิมไม่ได้
    [Tooltip("ของที่อยู่บนตัวบับเบิล (ป้ายชื่อ, ข้อความ, ปุ่ม) วางไว้ตามรูปตอนหางอยู่ล่าง — ตอนพลิกหางขึ้นจะเลื่อนลงให้เอง")]
    public RectTransform[] bodyParts;

    [Header("Art Layout (normalized ของรูปตอนยังไม่พลิก)")]
    [Tooltip("ปลายหางในรูป Bubble.png (0-1 จากมุมซ้ายล่าง)")]
    public Vector2 tailTip = new Vector2(0.513f, 0.10f);
    [Tooltip("ระยะ (px) ที่ตัวบับเบิลเลื่อนลงตอนพลิกหางขึ้น = ความสูงหาง - ขอบใสด้านบนของรูป")]
    public float flipBodyShift = 52f;

    [Header("Placement")]
    [Tooltip("ระยะห่าง (px) ระหว่างปลายหางกับหัว")]
    public float headGap = 8f;
    [Tooltip("ระยะกันขอบจอ (px ของ Canvas)")]
    public float screenMargin = 16f;
    [Tooltip("ถ้าไม่เจอ DialogueSpeaker ของบรรทัดนี้ ให้หางชี้ตรงนี้ (Viewport 0-1)")]
    public Vector2 fallbackViewportPoint = new Vector2(0.5f, 0.3f);

    private RectTransform bubble;
    private DialogueSpeaker currentSpeaker;
    private bool dialogueActive;
    private bool flipY;
    private Vector2[] bodyPartBasePositions;

    private void Awake()
    {
        bubble = (RectTransform)transform;
        bodyPartBasePositions = new Vector2[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++) bodyPartBasePositions[i] = bodyParts[i].anchoredPosition;
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        dialogueActive = true;
        flipY = false;
        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        dialogueActive = false;
        currentSpeaker = null;
        return YarnTask.CompletedTask;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        // แค่จำว่าใครพูด — LinePresenter เป็นคนแสดงข้อความ/รอกดเดินบทเอง
        currentSpeaker = DialogueSpeaker.Find(line.CharacterName);
        UpdatePlacement();
        return YarnTask.CompletedTask;
    }

    private void LateUpdate()
    {
        if (dialogueActive) UpdatePlacement();
    }

    private void UpdatePlacement()
    {
        RectTransform canvasRect = bubble.parent as RectTransform;
        Camera cam = Camera.main;
        if (canvasRect == null || cam == null) return;

        Vector2 half = canvasRect.rect.size * 0.5f;
        Vector2 size = bubble.rect.size;
        Vector2 target = GetTargetPoint(cam, half);

        // บน/ล่าง: ด้านบนหัวไม่พอ -> พลิกลง (hysteresis 40px กันกระพริบตอนอยู่ตรงเส้นพอดี)
        float topIfAbove = target.y + headGap + (1f - tailTip.y) * size.y;
        float roomNeeded = half.y - screenMargin - (flipY ? 40f : 0f);
        flipY = topIfAbove > roomNeeded;

        float tipY = flipY ? 1f - tailTip.y : tailTip.y;
        float centerY = flipY
            ? target.y - headGap - (tipY - 0.5f) * size.y
            : target.y + headGap + (0.5f - tipY) * size.y;
        float maxY = Mathf.Max(0f, half.y - screenMargin - size.y * 0.5f);
        centerY = Mathf.Clamp(centerY, -maxY, maxY);

        // ซ้าย/ขวา: เลื่อนเข้าจอ ถ้าหัวไปอยู่ซ้ายของกลางบับเบิลก็พลิกให้หางเอียงไปทางซ้าย
        float maxX = Mathf.Max(0f, half.x - screenMargin - size.x * 0.5f);
        float centerX = Mathf.Clamp(target.x - (tailTip.x - 0.5f) * size.x, -maxX, maxX);
        bool flipX = target.x < centerX;
        if (flipX) centerX = Mathf.Clamp(target.x - (0.5f - tailTip.x) * size.x, -maxX, maxX);

        bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
        bubble.pivot = new Vector2(0.5f, 0.5f);
        bubble.anchoredPosition = new Vector2(centerX, centerY);

        if (background != null) background.localScale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);
        for (int i = 0; i < bodyParts.Length; i++)
        {
            bodyParts[i].anchoredPosition = bodyPartBasePositions[i] + (flipY ? Vector2.down * flipBodyShift : Vector2.zero);
        }
    }

    /// <summary>จุดหัวของผู้พูดในพิกัด Canvas (0,0 = กลางจอ) ที่ถูกดันให้อยู่ในจอเสมอ</summary>
    private Vector2 GetTargetPoint(Camera cam, Vector2 half)
    {
        Vector3 viewport = currentSpeaker != null
            ? cam.WorldToViewportPoint(currentSpeaker.HeadPosition)
            : (Vector3)fallbackViewportPoint + Vector3.forward;

        Vector2 local = new Vector2((viewport.x - 0.5f) * half.x * 2f, (viewport.y - 0.5f) * half.y * 2f);

        // อยู่หลังกล้อง: Viewport กลับด้าน -> กลับคืนแล้วดันไปไกลๆ ให้ไปติดขอบจอฝั่งที่หัวอยู่
        if (viewport.z < 0f)
        {
            local = -local;
            if (local.sqrMagnitude < 1f) local = Vector2.down;
            local = local.normalized * 100000f;
        }

        Vector2 limit = half - Vector2.one * screenMargin;
        float scale = 1f;
        if (Mathf.Abs(local.x) > limit.x) scale = Mathf.Min(scale, limit.x / Mathf.Abs(local.x));
        if (Mathf.Abs(local.y) > limit.y) scale = Mathf.Min(scale, limit.y / Mathf.Abs(local.y));
        return local * scale;
    }
}

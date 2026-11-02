using System.Threading;
using TMPro;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;

/// <summary>
/// เสียงพูด "ติ๊ดๆ" ตามตัวอักษรที่ Typewriter ของ LinePresenter พิมพ์ออกมา สุ่ม Pitch ทุกครั้ง
/// ติดไว้ที่ GameObject เดียวกับ LinePresenter แล้วใส่ในช่อง Event Handlers ของ LinePresenter
/// (LinePresenter ต้องตั้ง Typewriter Style เป็น By Letter ถึงจะเรียกทีละตัว)
/// </summary>
public class DialogueTalkingSound : ActionMarkupHandler
{
    [Tooltip("เสียงสั้นๆ เล่นซ้ำระหว่างตัวอักษรขึ้น (Assets/[05]Audio/SFX/UI/Talking.wav)")]
    public AudioClip talkingClip;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("สุ่ม Pitch ในช่วงนี้ทุกครั้งที่เล่น")]
    public Vector2 pitchRange = new Vector2(0.85f, 1.25f);
    [Tooltip("เล่น 1 ครั้งทุกกี่ตัวอักษร (ไม่นับช่องว่าง/เครื่องหมาย) — 1 = ทุกตัว")]
    [Min(1)] public int charactersPerSound = 2;
    [Tooltip("เว้นอย่างน้อยกี่วินาทีระหว่างเสียง กันเสียงรัวตอนกดข้ามให้ข้อความขึ้นทั้งบรรทัด")]
    public float minInterval = 0.05f;

    private string lineText;
    private int charactersSinceSound;
    private float nextAllowedTime;

    public override void OnPrepareForLine(MarkupParseResult line, TMP_Text text)
    {
        lineText = line.Text;
        charactersSinceSound = charactersPerSound; // ตัวแรกของบรรทัดมีเสียงเลย
    }

    public override void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text) { }

    public override YarnTask OnCharacterWillAppear(int currentCharacterIndex, MarkupParseResult line, CancellationToken cancellationToken)
    {
        if (lineText == null || currentCharacterIndex >= lineText.Length) return YarnTask.CompletedTask;

        char c = lineText[currentCharacterIndex];
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c)) return YarnTask.CompletedTask;

        charactersSinceSound++;
        if (charactersSinceSound < charactersPerSound || Time.unscaledTime < nextAllowedTime) return YarnTask.CompletedTask;

        charactersSinceSound = 0;
        nextAllowedTime = Time.unscaledTime + minInterval;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(talkingClip, volume, Random.Range(pitchRange.x, pitchRange.y));
        }
        return YarnTask.CompletedTask;
    }

    public override void OnLineDisplayComplete() { }

    public override void OnLineWillDismiss() { }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FaceController : MonoBehaviour
{
    public enum FaceExpression
    {
        Neutral,
        Happy,
        Angry,
        Sad,
        Blink,
        EasterEgg,
    }

    [System.Serializable]
    public class FaceAnimation
    {
        public FaceExpression expression;
        public Texture2D[] frames;
        public float frameRate = 12f;
    }

    [Header("References")]
    [SerializeField] Renderer faceRenderer;

    [Header("Face Animations")]
    [SerializeField] FaceAnimation[] animations;

    [Header("Blink Settings")]
    [SerializeField] float blinkMinInterval = 2f;
    [SerializeField] float blinkMaxInterval = 5f;

    MaterialPropertyBlock mpb;
    Dictionary<FaceExpression, (int start, int count)> animRanges;
    Coroutine currentAnim;
    bool isEmoting = false;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        BuildTextureArray();
        StartCoroutine(BlinkRoutine());
    }

    void BuildTextureArray()
    {
        animRanges = new Dictionary<FaceExpression, (int, int)>();
        var allFrames = new List<Texture2D>();

        foreach (var anim in animations)
        {
            if (anim.frames == null || anim.frames.Length == 0)
            {
                Debug.LogError($"FaceAnimation '{anim.expression}' ไม่มี frame เลย ข้ามไป");
                continue;
            }

            animRanges[anim.expression] = (allFrames.Count, anim.frames.Length);
            allFrames.AddRange(anim.frames);
        }

        if (allFrames.Count == 0)
        {
            Debug.LogError("ไม่มี face texture ให้ build เลย ตรวจสอบ animations array ใน Inspector");
            return;
        }

        Texture2D first = allFrames[0];
        Texture2DArray array = new Texture2DArray(
            first.width, first.height, allFrames.Count,
            first.format, false);

        for (int i = 0; i < allFrames.Count; i++)
        {
            if (allFrames[i].width != first.width || allFrames[i].height != first.height || allFrames[i].format != first.format)
            {
                Debug.LogError($"Texture '{allFrames[i].name}' ขนาดหรือ format ไม่ตรงกับ '{first.name}' — ต้องเท่ากันทุกใบ");
                continue;
            }
            Graphics.CopyTexture(allFrames[i], 0, 0, array, i, 0);
        }

        array.Apply(false);
        faceRenderer.material.SetTexture("_FaceArray", array);
    }

    public void PlayExpression(FaceExpression expression)
    {
        if (!animRanges.ContainsKey(expression))
        {
            Debug.LogError($"ไม่มี animation ของ '{expression}' ถูกตั้งค่าไว้ใน Inspector");
            return;
        }

        isEmoting = expression != FaceExpression.Neutral;

        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(PlayAnimRoutine(expression));
    }

    IEnumerator PlayAnimRoutine(FaceExpression expression)
    {
        var (start, count) = animRanges[expression];
        float frameRate = animations.First(a => a.expression == expression).frameRate;
        float frameTime = 1f / Mathf.Max(frameRate, 0.01f);

        for (int i = 0; i < count; i++)
        {
            SetFaceIndex(start + i);
            yield return new WaitForSeconds(frameTime);
        }
    }

    void SetFaceIndex(int index)
    {
        faceRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_FaceIndex", index);
        faceRenderer.SetPropertyBlock(mpb);
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkMinInterval, blinkMaxInterval));

            if (!isEmoting && animRanges.ContainsKey(FaceExpression.Blink))
            {
                yield return PlayAnimRoutine(FaceExpression.Blink);
                SetFaceIndex(animRanges[FaceExpression.Neutral].start);
            }
        }
    }
}
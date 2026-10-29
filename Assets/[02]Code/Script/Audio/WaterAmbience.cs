using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// เสียงแม่น้ำ/ลำธารตามตำแหน่งน้ำจริง — เสียงวนต่อเนื่อง (Loop) 1 จุด ที่ย้ายไปอยู่ "จุดน้ำที่ใกล้ผู้เล่นที่สุด" เสมอ
/// เดินเข้าใกล้แม่น้ำเสียงดังขึ้นและมาจากทิศของแม่น้ำ เดินเลียบไปตามริมน้ำเสียงเลื่อนตามไป ถ้าอยู่ไกลเกิน maxHearDistance เสียงเงียบแล้วหยุดเล่น
///
/// หาน้ำจาก WaterSurface (ผิวน้ำ + Terrain ใต้น้ำ — ที่ไหนพื้นต่ำกว่าผิวน้ำคือน้ำ) สุ่มตรวจเป็นตารางรอบผู้เล่นทุก updateInterval วินาที
/// ไม่ต้องวางจุดเสียงตามแม่น้ำเอง แก้รูปแม่น้ำใน Terrain แล้วเสียงตามให้อัตโนมัติ
/// ใช้ AudioSource ของตัวเอง ส่งออกกลุ่ม Mixer Ambient (ปรับที่ช่อง Ambient ในหน้า Setting) — วางคู่กับ AmbientDome ใน SoundController
/// </summary>
public class WaterAmbience : MonoBehaviour
{
    [Header("เสียง")]
    [Tooltip("เสียงน้ำไหลแบบวนได้ (Water/River1)")]
    public AudioClip clip;
    public AudioMixerGroup outputGroup;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("ระยะ")]
    [Tooltip("ไกลกว่านี้ (เมตร จากน้ำที่ใกล้ที่สุด) ไม่ได้ยิน ค่ามาก = ตรวจหาน้ำไกลขึ้น (ใช้แรงขึ้นเล็กน้อย)")]
    public float maxHearDistance = 70f;
    [Tooltip("ใกล้กว่านี้ (เมตร) เสียงดังเต็มที่ ไกลออกไปค่อยๆ เบาลงจนเงียบที่ maxHearDistance")]
    public float fullVolumeDistance = 6f;
    [Tooltip("ความโค้งของการเบาลง 1 = เป็นเส้นตรง ยิ่งมากยิ่งเบาเร็วตอนเริ่มออกห่าง")]
    [Range(1f, 4f)] public float fadeCurve = 2f;
    [Tooltip("กรองเสียงแหลมที่ระยะไกลสุด (เสียงน้ำไกลๆ จะอู้ลง) 22000 = ไม่กรอง")]
    public float lowPassAtMaxDistance = 2500f;

    [Header("การตรวจหาน้ำ")]
    [Tooltip("ระยะห่างจุดตรวจ (เมตร) ลำธารแคบกว่านี้อาจหลุด")]
    public float searchStep = 3f;
    [Tooltip("ตรวจหาจุดน้ำใหม่ทุกกี่วินาที")]
    public float updateInterval = 0.25f;
    [Tooltip("ความเร็วที่จุดเสียงเลื่อนตามจุดน้ำใหม่ (เมตร/วินาที) กันเสียงกระโดดตอนเดินผ่านโค้งแม่น้ำ")]
    public float moveSpeed = 40f;
    [Tooltip("ความเร็วเสียงขึ้น/ลงเมื่อเข้า/ออกระยะ (หน่วยความดัง/วินาที)")]
    public float fadeSpeed = 1.5f;

    [Header("อ้างอิง")]
    [Tooltip("ว่างไว้ = หา AudioListener ในซีนเอง")]
    public Transform listener;

    [Header("Debug")]
    public bool drawGizmos = true;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private Vector3 targetPoint;
    private bool hasTarget;
    private float targetDistance = float.MaxValue;
    private float currentFade;
    private float nextSearch;

    private void Awake()
    {
        GameObject go = new GameObject("WaterEmitter");
        go.transform.SetParent(transform, false);

        source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        // ความดังตามระยะคุมเองด้วย fade ใน Update (Logarithmic ของ Unity ไม่เงียบสนิทที่ maxDistance และจะซ้อนกับ fade) — เก็บไว้แค่การแพนซ้าย/ขวาตามทิศ
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
        source.minDistance = Mathf.Max(0.1f, fullVolumeDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 1f, maxHearDistance * 2f);
        source.outputAudioMixerGroup = outputGroup;
        source.volume = 0f;

        lowPass = go.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
    }

    private void Start()
    {
        if (listener == null)
        {
            AudioListener found = FindFirstObjectByType<AudioListener>();
            if (found != null) listener = found.transform;
        }
    }

    private void Update()
    {
        if (listener == null || clip == null) return;

        float dt = Time.unscaledDeltaTime;

        if (Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + updateInterval;
            FindNearestWater();
        }

        // ไม่มีน้ำในระยะ = เฟดเสียงลงจนเงียบแล้วหยุดเล่น (ไม่เปลืองเสียงที่ไม่ได้ยิน)
        float targetFade = 0f;
        if (hasTarget)
        {
            float t = Mathf.InverseLerp(fullVolumeDistance, maxHearDistance, targetDistance);
            targetFade = Mathf.Pow(1f - t, fadeCurve);
        }
        currentFade = Mathf.MoveTowards(currentFade, targetFade, fadeSpeed * dt);

        if (hasTarget)
        {
            Transform emitter = source.transform;
            // เพิ่งเริ่มเล่น = วางทันที ไม่ไถลมาจากจุดเริ่มต้น
            emitter.position = source.isPlaying
                ? Vector3.MoveTowards(emitter.position, targetPoint, moveSpeed * dt)
                : targetPoint;
        }

        source.volume = volume * currentFade;
        float distToEmitter = Vector3.Distance(listener.position, source.transform.position);
        lowPass.cutoffFrequency = Mathf.Lerp(22000f, lowPassAtMaxDistance, Mathf.InverseLerp(fullVolumeDistance, maxHearDistance, distToEmitter));
        // ยืนอยู่ในน้ำ/ริมน้ำ = เสียงรอบตัว, อยู่ไกล = เสียงแคบมาจากทิศเดียว
        source.spread = Mathf.Lerp(150f, 20f, Mathf.InverseLerp(0f, fullVolumeDistance * 3f, distToEmitter));

        if (currentFade > 0.001f)
        {
            if (!source.isPlaying) source.Play();
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }

    // ตรวจเป็นตารางรอบผู้เล่น (วงกลมรัศมี maxHearDistance) หาจุดที่มีน้ำและใกล้ที่สุด
    private void FindNearestWater()
    {
        Vector3 origin = listener.position;
        float step = Mathf.Max(0.5f, searchStep);
        float maxSqr = maxHearDistance * maxHearDistance;
        int cells = Mathf.CeilToInt(maxHearDistance / step);

        float bestSqr = float.MaxValue;
        Vector3 best = default;

        for (int ix = -cells; ix <= cells; ix++)
        {
            for (int iz = -cells; iz <= cells; iz++)
            {
                float dx = ix * step, dz = iz * step;
                float sqr = dx * dx + dz * dz;
                if (sqr > maxSqr || sqr >= bestSqr) continue;

                Vector3 probe = new Vector3(origin.x + dx, 0f, origin.z + dz);
                if (!WaterSurface.TryGetSurface(probe, out float surfaceY)) continue;

                bestSqr = sqr;
                best = new Vector3(probe.x, surfaceY, probe.z);
            }
        }

        hasTarget = bestSqr < float.MaxValue;
        if (hasTarget)
        {
            targetPoint = best;
            targetDistance = Mathf.Sqrt(bestSqr);
        }
        else
        {
            targetDistance = float.MaxValue;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !Application.isPlaying || listener == null) return;

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(listener.position, maxHearDistance);
        if (!hasTarget) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawSphere(source.transform.position, 1f);
        Gizmos.DrawLine(listener.position, source.transform.position);
    }
}

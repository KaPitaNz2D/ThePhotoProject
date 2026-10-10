using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// "โดมเสียง" รอบตัวผู้เล่น: คอยสุ่มเสียงสั้นๆ (นก, ใบไม้ไหว, แมลง ฯลฯ) แล้ววางเป็นจุด 3D ในพื้นที่รอบตัว ตามผู้เล่นไปทุกที่
/// ทำให้ Ambient ไม่เป็นไฟล์เสียงยาวที่วนซ้ำแบบเพลง แต่เป็นเหตุการณ์เล็กๆ ที่เกิดขึ้นรอบตัวจากทิศทางต่างๆ
///
/// แต่ละ AmbientSoundSet คุมจังหวะ/ระยะ/ความสูง/ความดัง/เวลา/พื้นผิวของตัวเอง ที่นี่แค่ลูปตั้งเวลาและวางจุดเสียง
/// จุดเสียงอยู่กับที่ตั้งแต่ถูกวางจนจบเสียง (ไม่ตามผู้เล่น) เลยเดินผ่านแล้วรู้สึกว่าเสียงอยู่ตรงนั้นจริง
/// เสียงที่ต้องมาจากที่เฉพาะตลอดเวลา (น้ำ, เตาไฟ) ไม่ใช้โดมนี้ ใช้จุดเสียงที่ผูกกับสถานที่แทน
///
/// ใช้ AudioSource ของตัวเอง (แยกจาก Pool ใน AudioManager) กันเสียงบรรยากาศจำนวนมากไปแย่งตัวเล่นเสียง SFX เช่นชัตเตอร์
/// </summary>
public class AmbientDome : MonoBehaviour
{
    [Header("ชุดเสียง")]
    public AmbientSoundSet[] soundSets;

    [Header("Output")]
    [Tooltip("กลุ่ม Mixer ที่ส่งเสียงออก (ปกติกลุ่ม Ambient)")]
    public AudioMixerGroup outputGroup;

    [Header("จุดเสียงที่เล่นพร้อมกันได้")]
    [Tooltip("จำนวนสูงสุดที่เล่นพร้อมกัน เต็มแล้วรอบนั้นข้ามไป")]
    public int maxSimultaneous = 12;

    [Header("อ้างอิง")]
    [Tooltip("ว่างไว้ = หา AudioListener ในซีนเอง (ตามกล้องหลัก)")]
    public Transform listener;
    [Tooltip("ใช้อ่านชั่วโมงในเกมไว้เลือกชุดเสียงกลางวัน/กลางคืน ว่างไว้ = หา TimeManager ในซีนเอง ถ้าไม่มีเลยถือว่าเป็นเที่ยงวัน")]
    public TimeManager timeManager;

    [Header("หาความสูงพื้น")]
    [Tooltip("Layer ของพื้น/Terrain ที่ใช้ยิง Raycast หาความสูงพื้นตรงจุดที่วางเสียง")]
    public LayerMask groundMask = 1 << 6;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logSpawns = false;

    private class Slot
    {
        public AmbientSoundSet set;
        public float timer;
        public AudioClip lastClip;
    }

    private class Emitter
    {
        public AudioSource source;
        public AudioLowPassFilter lowPass;
        public AmbientSoundSet set;
        public float busyUntil; // เวลาที่เสียงนี้จะเล่นจบ (ไม่พึ่ง isPlaying ซึ่งอาจยังเป็น false ในเฟรมแรกหลัง Play)
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly List<Emitter> emitters = new List<Emitter>();

    // เก็บจุดที่วางล่าสุดไว้วาด Gizmo ให้เห็นว่าเสียงตกตรงไหน
    private readonly List<(Vector3 position, float time)> recentSpawns = new List<(Vector3, float)>();

    private void Awake()
    {
        for (int i = 0; i < maxSimultaneous; i++)
        {
            GameObject go = new GameObject($"AmbientEmitter_{i}");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.spread = 30f;
            source.outputAudioMixerGroup = outputGroup;

            AudioLowPassFilter lowPass = go.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;

            emitters.Add(new Emitter { source = source, lowPass = lowPass });
        }
    }

    private void Start()
    {
        if (listener == null)
        {
            AudioListener found = FindFirstObjectByType<AudioListener>();
            if (found != null) listener = found.transform;
        }
        if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>();

        foreach (AmbientSoundSet set in soundSets)
        {
            if (set == null || set.clips == null || set.clips.Length == 0) continue;

            // ครั้งแรกสุ่มเริ่มกระจายกัน ไม่ให้ทุกชุดเล่นพร้อมกันตอนเริ่มเกม
            slots.Add(new Slot { set = set, timer = Random.Range(0f, set.intervalRange.y) });
        }
    }

    private void Update()
    {
        if (listener == null) return;

        // ปรับตัวกรองเสียงแหลมของจุดที่กำลังเล่นตามระยะจริงตอนนี้ (ผู้เล่นเดินห่างออกไป เสียงจะอู้ลง)
        foreach (Emitter emitter in emitters)
        {
            if (Time.time >= emitter.busyUntil) continue;

            float distance = Vector3.Distance(listener.position, emitter.source.transform.position);
            float t = Mathf.InverseLerp(emitter.set.minDistance, emitter.set.maxDistance, distance);
            emitter.lowPass.cutoffFrequency = Mathf.Lerp(22000f, emitter.set.lowPassAtMaxDistance, t);
        }

        int hour = timeManager != null ? timeManager.Hours : 12;

        foreach (Slot slot in slots)
        {
            slot.timer -= Time.deltaTime;
            if (slot.timer > 0f) continue;

            slot.timer = Random.Range(slot.set.intervalRange.x, slot.set.intervalRange.y);

            if (Random.value > slot.set.playChance) continue;
            if (!slot.set.IsActiveAtHour(hour)) continue;
            if (!string.IsNullOrEmpty(slot.set.requiredTerrainLayerKeyword) &&
                !GrazeSurface.IsGrass(listener.position, slot.set.requiredTerrainLayerKeyword, slot.set.terrainLayerMinCoverage))
                continue;

            Spawn(slot);
        }
    }

    private void Spawn(Slot slot)
    {
        Emitter emitter = emitters.Find(e => Time.time >= e.busyUntil);
        if (emitter == null) return; // ทุกจุดกำลังเล่นอยู่ ข้ามรอบนี้

        AmbientSoundSet set = slot.set;
        AudioClip clip = PickClip(slot);

        // จุดสุ่มบนวงแหวนรอบผู้เล่น (ทิศ 360° ระยะตามชุดเสียง) แล้วยกขึ้นจากพื้นตามช่วงความสูงของชุดเสียง
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(set.distanceRange.x, set.distanceRange.y);
        Vector3 flat = listener.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        float groundY = GetGroundHeight(flat);
        Vector3 position = new Vector3(flat.x, groundY + Random.Range(set.heightAboveGround.x, set.heightAboveGround.y), flat.z);

        AudioSource source = emitter.source;
        source.transform.position = position;
        source.clip = clip;
        source.volume = Random.Range(set.volumeRange.x, set.volumeRange.y);
        source.pitch = Random.Range(set.pitchRange.x, set.pitchRange.y);
        source.minDistance = set.minDistance;
        source.maxDistance = set.maxDistance;
        emitter.set = set;
        emitter.busyUntil = Time.time + clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f;
        emitter.lowPass.cutoffFrequency = 22000f;
        source.Play();

        slot.lastClip = clip;
        recentSpawns.Add((position, Time.time));
        if (recentSpawns.Count > 24) recentSpawns.RemoveAt(0);
        if (logSpawns) Debug.Log($"[AmbientDome] {set.name}: {clip.name} @ {position} ({distance:F0} m)");
    }

    // สุ่มเสียง ไม่ซ้ำตัวเดิมที่เพิ่งเล่น (ถ้ามีมากกว่า 1 ตัว)
    private static AudioClip PickClip(Slot slot)
    {
        AudioClip[] clips = slot.set.clips;
        if (clips.Length == 1) return clips[0];

        AudioClip picked;
        do { picked = clips[Random.Range(0, clips.Length)]; }
        while (picked == slot.lastClip);
        return picked;
    }

    private float GetGroundHeight(Vector3 position)
    {
        Vector3 origin = new Vector3(position.x, listener.position.y + 80f, position.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 300f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null) return terrain.SampleHeight(position) + terrain.transform.position.y;

        return listener.position.y;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || soundSets == null) return;

        Vector3 center = listener != null ? listener.position : transform.position;

        float maxRadius = 0f;
        foreach (AmbientSoundSet set in soundSets)
        {
            if (set != null) maxRadius = Mathf.Max(maxRadius, set.distanceRange.y);
        }
        Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(center, maxRadius);

        // จุดที่เพิ่งวางเสียง (จางลงตามเวลา)
        foreach (var spawn in recentSpawns)
        {
            float age = Time.time - spawn.time;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, Mathf.Clamp01(1f - age / 8f));
            Gizmos.DrawSphere(spawn.position, 0.6f);
            Gizmos.DrawLine(center, spawn.position);
        }
    }
}

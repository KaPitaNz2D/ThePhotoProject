using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

/// <summary>
/// เสียงฝีเท้าสัตว์ (กวาง/มูส) — ใช้ชุดเสียงเดียวกับผู้เล่น (PlayerFootsteps: หญ้า / ดิน / ลุยน้ำ)
/// ช่อง Clip ว่างไว้ = ดึงจาก PlayerFootsteps ในซีนตอนเริ่ม เปลี่ยนเสียงเท้าผู้เล่นทีเดียวสัตว์ได้ตามเลย
/// จังหวะก้าวนับจากระยะทางที่เดินได้จริง (ไม่ผูก Animation เพราะสัตว์สี่ขาไม่มีเฟสลงเท้าให้วัด):
///   ระยะต่อก้าวเลื่อนจาก walkStepDistance -> runStepDistance ตามความเร็ว (profile.walkSpeed -> runSpeed)
/// พื้นผิวและการผสมเสียงหญ้า/ดินแบบ equal-power เหมือนผู้เล่น เป็นเสียง 3D ตามตัวสัตว์ ส่งเข้า Ambient group
/// </summary>
[RequireComponent(typeof(CreatureAI))]
public class CreatureFootsteps : MonoBehaviour
{
    [Header("Clips — ว่างไว้ = ใช้ของ PlayerFootsteps")]
    public AudioClip[] grassClips;
    public AudioClip[] dirtClips;
    public AudioClip[] waterClips;

    [Header("Surface")]
    [Tooltip("ชื่อ Terrain Layer ที่ถือว่าเป็นหญ้า (ที่เหลือถือเป็นดิน)")]
    public string grassLayerKeyword = "Grass";
    [Tooltip("ความลึกน้ำ (เมตร) ขั้นต่ำที่นับว่าเดินในน้ำ")]
    public float waterMinDepth = 0.05f;

    [Header("Step Rhythm")]
    [Tooltip("ระยะทาง (เมตร) ต่อเสียงก้าวตอนเดิน")]
    public float walkStepDistance = 0.7f;
    [Tooltip("ระยะทาง (เมตร) ต่อเสียงก้าวตอนวิ่ง")]
    public float runStepDistance = 2f;
    [Tooltip("ช้ากว่านี้ (เมตร/วินาที) ถือว่ายืนอยู่ ไม่มีเสียง")]
    public float minMoveSpeed = 0.2f;

    [Header("Sound")]
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("ตัวคูณความดังตอนวิ่ง")]
    public float runVolumeMultiplier = 1.3f;
    public Vector2 pitchRange = new Vector2(0.95f, 1.1f);
    public float minDistance = 2f;
    public float maxDistance = 25f;

    private const int SourceCount = 3;

    private CreatureAI ai;
    private NavMeshAgent agent;
    private AudioSource[] sources;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private int nextSource;
    private float distanceSinceStep;
    private int lastGrass = -1;
    private int lastDirt = -1;
    private int lastWater = -1;

    private void Awake()
    {
        ai = GetComponent<CreatureAI>();
        agent = GetComponent<NavMeshAgent>();

        sources = new AudioSource[SourceCount];
        for (int i = 0; i < SourceCount; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            sources[i] = s;
        }
    }

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            // เสียงเท้านับเป็นเสียงบรรยากาศเหมือนของผู้เล่น
            AudioMixerGroup group = AudioManager.Instance.AmbientGroup != null ? AudioManager.Instance.AmbientGroup : AudioManager.Instance.sfxGroup;
            foreach (AudioSource s in sources) s.outputAudioMixerGroup = group;
        }

        PlayerFootsteps player = FindFirstObjectByType<PlayerFootsteps>();
        if (player != null)
        {
            if (grassClips == null || grassClips.Length == 0) grassClips = player.grassClips;
            if (dirtClips == null || dirtClips.Length == 0) dirtClips = player.dirtClips;
            if (waterClips == null || waterClips.Length == 0) waterClips = player.waterClips;
        }
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return; // Pause

        Vector3 v = agent.velocity;
        v.y = 0f;
        float speed = v.magnitude;
        if (!agent.enabled || speed < minMoveSpeed)
        {
            // หยุดแล้วเดินใหม่ ก้าวแรกมาเร็ว ไม่ต้องรอครบระยะเต็ม
            distanceSinceStep = StepDistance(speed) * 0.5f;
            return;
        }

        distanceSinceStep += speed * Time.deltaTime;
        float stepDistance = StepDistance(speed);
        if (distanceSinceStep < stepDistance) return;
        distanceSinceStep -= stepDistance;

        PlayStep(speed);
    }

    private float RunAmount(float speed)
    {
        if (ai.profile == null) return 0f;
        return Mathf.InverseLerp(ai.profile.walkSpeed, ai.profile.runSpeed, speed);
    }

    private float StepDistance(float speed) => Mathf.Lerp(walkStepDistance, runStepDistance, RunAmount(speed));

    private void PlayStep(float speed)
    {
        float baseVolume = volume * Mathf.Lerp(1f, runVolumeMultiplier, RunAmount(speed));

        if (waterClips != null && waterClips.Length > 0 && WaterSurface.GetDepth(transform.position) >= waterMinDepth)
        {
            Play(PickClip(waterClips, ref lastWater), baseVolume);
            return;
        }

        float grass = SampleGrassAmount();
        Play(PickClip(grassClips, ref lastGrass), baseVolume * Mathf.Sqrt(grass));
        Play(PickClip(dirtClips, ref lastDirt), baseVolume * Mathf.Sqrt(1f - grass));
    }

    /// <summary>0 = ดินล้วน, 1 = หญ้าล้วน (พื้นที่ไม่ใช่ Terrain = ดิน)</summary>
    private float SampleGrassAmount()
    {
        // สัตว์อยู่ Layer เดียวกับพื้น (Default) กรองด้วย Layer ไม่ได้ — เอาจุดชนที่ใกล้สุดที่ไม่ใช่ Collider ของตัวเอง
        int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.5f, Vector3.down, groundHits, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit? ground = null;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].transform.IsChildOf(transform)) continue;
            if (ground == null || groundHits[i].distance < ground.Value.distance) ground = groundHits[i];
        }

        if (ground == null || !(ground.Value.collider is TerrainCollider)) return 0f;
        return GrazeSurface.GetCoverage(ground.Value.point, grassLayerKeyword);
    }

    private static AudioClip PickClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        int index;
        do { index = Random.Range(0, clips.Length); } while (index == lastIndex);
        lastIndex = index;
        return clips[index];
    }

    private void Play(AudioClip clip, float clipVolume)
    {
        if (clip == null || clipVolume < 0.05f) return;

        AudioSource s = sources[nextSource];
        nextSource = (nextSource + 1) % SourceCount;
        s.pitch = Random.Range(pitchRange.x, pitchRange.y);
        s.PlayOneShot(clip, clipVolume);
    }
}

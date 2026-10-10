using UnityEngine;

/// <summary>
/// เสียงฝีเท้าผู้เล่น — ผูกกับ Animation: อ่านเฟส (normalizedTime) ของ State ที่ Animator กำลังเล่นอยู่
/// แล้วเล่นเสียงตอนที่เฟสข้ามจุด "เท้าแตะพื้น" ที่ตั้งไว้ใน stepStates (ความเร็ว Animation เปลี่ยน เสียงตามเอง)
/// พื้นผิวอ่านจากสัดส่วน Terrain Layer ใต้เท้า (GrazeSurface.GetCoverage): หญ้า / ดิน
/// ถ้าอยู่ก้ำกึ่งจะเล่นทั้งสองเสียงพร้อมกันและผสมความดังตามสัดส่วน (equal-power)
/// ถ้าพื้นใต้เท้าไม่ใช่ Terrain (เช่นพื้นกระท่อม) จะใช้เสียงดินล้วน
/// วางไว้บน GameObject เดียวกับ PlayerMovement
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerFootsteps : MonoBehaviour
{
    [System.Serializable]
    public class StepState
    {
        [Tooltip("ชื่อ State ใน Animator Controller (ชื่อ State ไม่ใช่ชื่อ Clip) เช่น Plr_Walking")]
        public string stateName;
        [Tooltip("เฟสของรอบ Animation (0-1) ที่เท้าแตะพื้น เช่น 0.21 และ 0.69 = สองก้าวต่อหนึ่งรอบ")]
        public float[] phases;
    }

    [Header("Clips — สุ่มเล่นไม่ซ้ำตัวเดิมติดกัน")]
    public AudioClip[] grassClips;
    public AudioClip[] dirtClips;

    [Header("Surface")]
    [Tooltip("ชื่อ Terrain Layer ที่ถือว่าเป็นหญ้า (ที่เหลือถือเป็นดิน)")]
    public string grassLayerKeyword = "Grass";

    [Header("Animation Sync")]
    [Tooltip("Animator ของตัวละคร ว่างไว้ = หาจากลูกของผู้เล่นให้เอง")]
    public Animator animator;
    [Tooltip("State ที่มีเสียงก้าว พร้อมเฟสที่เท้าแตะพื้น (วัดจากกระดูกเท้าของ clip: เท้าอยู่ไกลสุดด้านหน้า = จังหวะลงเท้า) ลองขยับเฟสได้ถ้าเสียงเร็ว/ช้ากว่าเท้า")]
    public StepState[] stepStates =
    {
        new StepState { stateName = "Plr_Walking", phases = new[] { 0.21f, 0.69f } },
        new StepState { stateName = "Plr_Run", phases = new[] { 0.23f, 0.77f } },
        new StepState { stateName = "mixamo_com", phases = new[] { 0.20f, 0.74f } }, // = Plr_CrouchWalking
    };
    [Tooltip("ผู้เล่นช้ากว่านี้ (เมตร/วินาที) ถือว่าไม่ได้ขยับจริง (เช่นเดินชนกำแพง) ไม่เล่นเสียง")]
    public float minMoveSpeed = 0.3f;
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    [Header("Volume")]
    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("ตัวคูณความดังตอนย่อ/เดินช้าในโหมดกล้อง")]
    [Range(0f, 1f)] public float slowWalkVolumeMultiplier = 0.45f;
    [Tooltip("ตัวคูณความดังตอนวิ่งสปรินต์")]
    public float sprintVolumeMultiplier = 1.2f;
    [Tooltip("เสียงเบากว่านี้ไม่เล่น (ตัดเสียงที่ผสมแล้วเบามากทิ้ง)")]
    public float minAudibleVolume = 0.05f;

    private const int SourceCount = 4;

    private PlayerMovement movement;
    private PlayerSprint sprint;
    private AudioSource[] sources;
    private int nextSource;
    private Vector3 lastPosition;
    private int lastStateHash;
    private float lastPhase;
    private int lastGrass = -1;
    private int lastDirt = -1;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        sprint = movement.playerSprint != null ? movement.playerSprint : GetComponent<PlayerSprint>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) Debug.LogWarning("[PlayerFootsteps] หา Animator ไม่เจอ — ไม่มีเสียงฝีเท้า");

        sources = new AudioSource[SourceCount];
        for (int i = 0; i < SourceCount; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            sources[i] = s;
        }
    }

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            foreach (AudioSource s in sources) s.outputAudioMixerGroup = AudioManager.Instance.sfxGroup;
        }
        lastPosition = transform.position;
    }

    private void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;

        if (animator == null) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        float phase = Mathf.Repeat(info.normalizedTime, 1f);
        float previous = lastPhase;
        bool sameState = info.fullPathHash == lastStateHash;
        lastStateHash = info.fullPathHash;
        lastPhase = phase;

        // เปลี่ยน State = เริ่มรอบใหม่ ยังไม่มีเฟสก่อนหน้าให้เทียบ
        if (!sameState) return;
        if (!movement.IsGrounded || speed < minMoveSpeed) return;

        StepState step = FindStepState(info);
        if (step == null || step.phases == null) return;

        // ข้อมูลเฟสที่ผ่านในเฟรมนี้ (ข้ามขอบรอบ 1 -> 0 ได้)
        float end = phase >= previous ? phase : phase + 1f;
        foreach (float ph in step.phases)
        {
            if (CrossedPhase(previous, end, ph) || CrossedPhase(previous, end, ph + 1f))
            {
                PlayStep();
                break; // กันเล่นซ้อนสองครั้งในเฟรมเดียว
            }
        }
    }

    private static bool CrossedPhase(float from, float to, float target)
    {
        return target > from && target <= to;
    }

    private StepState FindStepState(AnimatorStateInfo info)
    {
        if (stepStates == null) return null;
        foreach (StepState s in stepStates)
        {
            if (s != null && !string.IsNullOrEmpty(s.stateName) && info.IsName(s.stateName)) return s;
        }
        return null;
    }

    private void PlayStep()
    {
        float grass = SampleGrassAmount();

        float multiplier = movement.IsSlowWalking ? slowWalkVolumeMultiplier
            : (sprint != null && sprint.IsSprinting ? sprintVolumeMultiplier : 1f);
        float baseVolume = volume * multiplier;

        // equal-power: ผสมแล้วความดังรวมไม่ตกตอนอยู่กลางๆ ระหว่างสองพื้น
        float grassVolume = baseVolume * Mathf.Sqrt(grass);
        float dirtVolume = baseVolume * Mathf.Sqrt(1f - grass);

        if (grassVolume >= minAudibleVolume) Play(PickClip(grassClips, ref lastGrass), grassVolume);
        if (dirtVolume >= minAudibleVolume) Play(PickClip(dirtClips, ref lastDirt), dirtVolume);
    }

    /// <summary>0 = ดินล้วน, 1 = หญ้าล้วน</summary>
    private float SampleGrassAmount()
    {
        float rayLength = movement.playerHeight * 0.5f + 0.6f;
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, movement.groundLayer, QueryTriggerInteraction.Ignore))
            return 0f;

        if (!(hit.collider is TerrainCollider)) return 0f; // พื้นอื่น (กระท่อม/หิน) ใช้เสียงดิน

        return GrazeSurface.GetCoverage(hit.point, grassLayerKeyword);
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
        if (clip == null) return;

        AudioSource s = sources[nextSource];
        nextSource = (nextSource + 1) % SourceCount;
        s.pitch = Random.Range(pitchRange.x, pitchRange.y);
        s.PlayOneShot(clip, clipVolume);
    }
}

using UnityEngine;

/// <summary>
/// เสียงร้อง/ส่งเสียงของสัตว์ ผูกกับ CreatureAI ผ่าน OnStateChanged (ไม่แตะ Logic ของ AI)
///   - Normal (Idle/Walking/Eating): สุ่มเล่นเสียงเป็นระยะ สุ่ม Clip ตามน้ำหนัก + สุ่ม Pitch
///   - Stop / Alert: เล่น Snort ตอนเข้า State (Alert pitch สูงกว่า)
///   - Run: เล่นเสียงตื่นตระหนกตอนเข้า State
///   - Spawn: CreatureSpawner เรียก PlaySpawnCall() ตอนเกิด ร้อง 1 ครั้งทันที (สุ่มจาก normalClips) บอกผู้เล่นว่ามีสัตว์แถวนี้
///     ใช้ AudioSource แยกที่ได้ยินไกลกว่า (spawnCallMaxDistance)
/// ใช้ AudioSource ของตัวเอง 2 ตัว (3D ตามตัวสัตว์: เสียงทั่วไป + เสียงตอนเกิด) ส่งเข้า SFX group ของ AudioManager
/// </summary>
[RequireComponent(typeof(CreatureAI))]
public class CreatureVoice : MonoBehaviour
{
    [System.Serializable]
    public class WeightedClip
    {
        public AudioClip clip;
        [Tooltip("น้ำหนักการสุ่ม เช่น 8 กับ 2 = 80% / 20%")]
        public float weight = 1f;
    }

    [Header("Normal — สุ่มเล่นระหว่าง Idle/Walking/Eating")]
    public WeightedClip[] normalClips;
    [Tooltip("ช่วงเวลา (วินาที) ระหว่างเสียงแต่ละครั้ง")]
    public Vector2 normalInterval = new Vector2(8f, 20f);
    public Vector2 normalPitch = new Vector2(0.9f, 1.1f);
    [Range(0f, 1f)] public float normalVolume = 0.8f;

    [Header("Spawn — ร้อง 1 ครั้งตอนเกิด (ใช้ normalClips)")]
    [Tooltip("ดังกว่าเสียงร้องปกติได้ เพราะสัตว์เกิดไกลผู้เล่น (40m ขึ้นไป)")]
    [Range(0f, 1f)] public float spawnCallVolume = 1f;
    [Tooltip("ระยะได้ยินเสียงร้องตอนเกิด (เมตร) แยกจาก Max Distance ของเสียงอื่น — สัตว์เกิดห่างผู้เล่น 40m ขึ้นไป ค่าต้องเกินระยะนั้นพอสมควรถึงจะได้ยิน")]
    public float spawnCallMaxDistance = 120f;

    [Header("Snort — เล่นตอนเข้า Stop และ Alert")]
    public AudioClip snortClip;
    public Vector2 stopSnortPitch = new Vector2(0.95f, 1.05f);
    [Tooltip("Alert ตื่นตัวกว่า Stop เลยให้ pitch สูงกว่า")]
    public Vector2 alertSnortPitch = new Vector2(1.15f, 1.3f);
    [Range(0f, 1f)] public float snortVolume = 1f;

    [Header("Run — เล่นตอนเข้า Run")]
    public AudioClip runAlertClip;
    public Vector2 runAlertPitch = new Vector2(0.95f, 1.05f);
    [Range(0f, 1f)] public float runAlertVolume = 1f;

    [Header("3D Sound")]
    public float minDistance = 5f;
    public float maxDistance = 60f;

    private CreatureAI ai;
    private AudioSource source;
    private AudioSource spawnSource; // แยกจาก source เพราะระยะได้ยินไกลกว่า
    private float normalTimer;

    private void Awake()
    {
        ai = GetComponent<CreatureAI>();

        source = CreateSource(maxDistance);
        spawnSource = CreateSource(spawnCallMaxDistance);
    }

    private AudioSource CreateSource(float range)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = minDistance;
        s.maxDistance = range;
        return s;
    }

    private void OnEnable()
    {
        ai.OnStateChanged += HandleStateChanged;
        ResetNormalTimer();
    }

    private void OnDisable()
    {
        if (ai != null) ai.OnStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        AssignMixerGroup();
    }

    /// <summary>ร้อง 1 ครั้งตอนเกิด — เรียกจาก CreatureSpawner (เรียกได้ก่อน Start ของตัวนี้)</summary>
    public void PlaySpawnCall()
    {
        Play(spawnSource, PickWeighted(normalClips), normalPitch, spawnCallVolume);
        ResetNormalTimer(); // เพิ่งร้องไป ไม่ต้องร้องซ้ำเร็วเกิน
    }

    // ตอนเกิดจาก Spawner เสียงแรกเล่นก่อน Start เลยต้องผูก SFX group ตรงนี้ด้วย ไม่งั้นข้าม Volume ของ Settings
    private void AssignMixerGroup()
    {
        if (AudioManager.Instance == null) return;
        if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = AudioManager.Instance.sfxGroup;
        if (spawnSource.outputAudioMixerGroup == null) spawnSource.outputAudioMixerGroup = AudioManager.Instance.sfxGroup;
    }

    private void Update()
    {
        if (!IsNormalState(ai.CurrentState)) return;

        normalTimer -= Time.deltaTime;
        if (normalTimer > 0f) return;

        Play(source, PickWeighted(normalClips), normalPitch, normalVolume);
        ResetNormalTimer();
    }

    private void HandleStateChanged(CreatureAI.CreatureState oldState, CreatureAI.CreatureState newState)
    {
        switch (newState)
        {
            case CreatureAI.CreatureState.Stop:
                Play(source, snortClip, stopSnortPitch, snortVolume);
                break;
            case CreatureAI.CreatureState.Alert:
                Play(source, snortClip, alertSnortPitch, snortVolume);
                break;
            case CreatureAI.CreatureState.Run:
                Play(source, runAlertClip, runAlertPitch, runAlertVolume);
                break;
        }

        // กลับเข้า Normal จาก State อื่น ให้เว้นช่วงก่อนร้องครั้งถัดไป ไม่ร้องทันที
        if (IsNormalState(newState) && !IsNormalState(oldState)) ResetNormalTimer();
    }

    private static bool IsNormalState(CreatureAI.CreatureState state)
    {
        return state == CreatureAI.CreatureState.Idle
            || state == CreatureAI.CreatureState.Walking
            || state == CreatureAI.CreatureState.Eating;
    }

    private void ResetNormalTimer()
    {
        normalTimer = Random.Range(normalInterval.x, normalInterval.y);
    }

    private static AudioClip PickWeighted(WeightedClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        float total = 0f;
        foreach (WeightedClip c in clips) if (c != null && c.clip != null) total += Mathf.Max(0f, c.weight);
        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (WeightedClip c in clips)
        {
            if (c == null || c.clip == null) continue;
            roll -= Mathf.Max(0f, c.weight);
            if (roll <= 0f) return c.clip;
        }
        return null;
    }

    private void Play(AudioSource target, AudioClip clip, Vector2 pitchRange, float volume)
    {
        if (clip == null) return;
        AssignMixerGroup();
        target.pitch = Random.Range(pitchRange.x, pitchRange.y);
        target.PlayOneShot(clip, volume);
    }
}

using UnityEngine;

/// <summary>
/// เสียงร้อง/ส่งเสียงของสัตว์ ผูกกับ CreatureAI ผ่าน OnStateChanged (ไม่แตะ Logic ของ AI)
///   - Normal (Idle/Walking/Eating): สุ่มเล่นเสียงเป็นระยะ สุ่ม Clip ตามน้ำหนัก + สุ่ม Pitch
///   - Stop / Alert: เล่น Snort ตอนเข้า State (Alert pitch สูงกว่า)
///   - Run: เล่นเสียงตื่นตระหนกตอนเข้า State
/// ใช้ AudioSource ของตัวเอง (3D ตามตัวสัตว์) ส่งเข้า SFX group ของ AudioManager
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
    private float normalTimer;

    private void Awake()
    {
        ai = GetComponent<CreatureAI>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
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
        if (AudioManager.Instance != null) source.outputAudioMixerGroup = AudioManager.Instance.sfxGroup;
    }

    private void Update()
    {
        if (!IsNormalState(ai.CurrentState)) return;

        normalTimer -= Time.deltaTime;
        if (normalTimer > 0f) return;

        PlayWeighted(normalClips, normalPitch, normalVolume);
        ResetNormalTimer();
    }

    private void HandleStateChanged(CreatureAI.CreatureState oldState, CreatureAI.CreatureState newState)
    {
        switch (newState)
        {
            case CreatureAI.CreatureState.Stop:
                Play(snortClip, stopSnortPitch, snortVolume);
                break;
            case CreatureAI.CreatureState.Alert:
                Play(snortClip, alertSnortPitch, snortVolume);
                break;
            case CreatureAI.CreatureState.Run:
                Play(runAlertClip, runAlertPitch, runAlertVolume);
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

    private void PlayWeighted(WeightedClip[] clips, Vector2 pitchRange, float volume)
    {
        if (clips == null || clips.Length == 0) return;

        float total = 0f;
        foreach (WeightedClip c in clips) if (c != null && c.clip != null) total += Mathf.Max(0f, c.weight);
        if (total <= 0f) return;

        float roll = Random.value * total;
        foreach (WeightedClip c in clips)
        {
            if (c == null || c.clip == null) continue;
            roll -= Mathf.Max(0f, c.weight);
            if (roll <= 0f)
            {
                Play(c.clip, pitchRange, volume);
                return;
            }
        }
    }

    private void Play(AudioClip clip, Vector2 pitchRange, float volume)
    {
        if (clip == null) return;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume);
    }
}

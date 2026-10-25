using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// ระบบเสียงกลางของเกม — โค้ดส่วนไหนก็เรียก AudioManager.Instance.PlaySFX(clip) ได้จากทุกที่
/// ไม่ต้องมี AudioSource ของตัวเองในทุก GameObject ที่อยากเล่นเสียง (Pattern เดียวกับ StateManager)
///
/// ใช้ Pool ของ AudioSource หมุนเวียนกันเล่น รองรับเสียงซ้อนกันหลายตัวพร้อมกันได้
/// (เช่นกดชัตเตอร์รัวๆ เสียงจะไม่ตัดกันเอง เพราะแต่ละครั้งได้ AudioSource คนละตัวจาก Pool)
///
/// หมายเหตุ: ระบบนี้ออกแบบมาสำหรับเสียง "ยิงแล้วจบ" (One-shot) เท่านั้น
/// เสียง Loop ต่อเนื่องที่ต้องคุม Start/Stop เอง (เช่นเสียงมอเตอร์ซูมตอนกำลังซูมอยู่)
/// ควรใช้ AudioSource เฉพาะของสคริปต์นั้นแยกต่างหาก ไม่ผ่านระบบนี้
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("Mixer หลักของเกม (Assets/[05]Audio/MainMixer.mixer — สร้างจากเมนู Tools > Photo Project > Create Audio Mixer)")]
    public AudioMixer mixer;
    [Tooltip("กลุ่ม SFX ใน Mixer ที่ Pool ของ AudioManager ส่งเสียงออก")]
    public AudioMixerGroup sfxGroup;

    [Header("SFX Pool")]
    [Tooltip("จำนวน AudioSource ที่เตรียมไว้ล่วงหน้า สำหรับเล่นเสียงซ้อนกันพร้อมกัน")]
    public int poolSize = 8;
    [Range(0f, 1f)]
    public float masterSFXVolume = 1f;

    /// <summary>ช่องปรับเสียงในหน้า Setting (หนึ่งช่องอาจคุมหลายกลุ่มใน Mixer — SFX คุมทั้ง SFX และ UI)</summary>
    public enum VolumeChannel { Master, Ambient, Music, SFX }

    private static readonly Dictionary<VolumeChannel, string[]> ChannelParameters = new Dictionary<VolumeChannel, string[]>
    {
        { VolumeChannel.Master,  new[] { "MasterVolume" } },
        { VolumeChannel.Ambient, new[] { "AmbientVolume" } },
        { VolumeChannel.Music,   new[] { "MusicVolume" } },
        { VolumeChannel.SFX,     new[] { "SFXVolume", "UIVolume" } },
    };

    private List<AudioSource> sourcePool = new List<AudioSource>();
    private int nextSourceIndex = 0;
    private AudioMixerGroup ambientGroup;

    /// <summary>กลุ่ม Ambient ใน Mixer (เสียงเท้า/นก/หญ้า) หาจากชื่อในตัว Mixer เอง ไม่ต้องลากใส่ — null ถ้าไม่มี Mixer</summary>
    public AudioMixerGroup AmbientGroup
    {
        get
        {
            if (ambientGroup == null && mixer != null)
            {
                AudioMixerGroup[] found = mixer.FindMatchingGroups("Ambient");
                if (found.Length > 0) ambientGroup = found[0];
            }
            return ambientGroup;
        }
    }

    private void Start()
    {
        // AudioMixer.SetFloat ใน Awake ไม่มีผล (ข้อจำกัดของ Unity) ต้องตั้งค่าที่เซฟไว้ตอน Start
        foreach (VolumeChannel channel in ChannelParameters.Keys) SetVolume(channel, GetSavedVolume(channel), false);
    }

    /// <summary>ความดังที่ผู้เล่นตั้งไว้ (0..1) — เซฟข้ามการเปิดเกมด้วย PlayerPrefs ค่าเริ่มต้น 1</summary>
    public static float GetSavedVolume(VolumeChannel channel) => PlayerPrefs.GetFloat("Volume." + channel, 1f);

    /// <summary>ตั้งความดังของช่อง (0..1) แล้วเซฟไว้ — ใช้จากหน้า Setting</summary>
    public void SetVolume(VolumeChannel channel, float linear01, bool save = true)
    {
        linear01 = Mathf.Clamp01(linear01);
        foreach (string parameter in ChannelParameters[channel]) SetGroupVolume(parameter, linear01);
        if (save) PlayerPrefs.SetFloat("Volume." + channel, linear01);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // ค่าเริ่มต้นเป็นเสียง 2D (UI/กล้อง) ปรับเป็น 3D อัตโนมัติตอนเรียก PlaySFXAtPoint
            source.outputAudioMixerGroup = sfxGroup;
            sourcePool.Add(source);
        }
    }

    /// <summary>
    /// ตั้งความดังของกลุ่มเสียงใน Mixer ด้วยสเกล 0..1 (0 = เงียบ, 1 = ปกติ) เช่น SetGroupVolume("AmbientVolume", 0.5f)
    /// ชื่อพารามิเตอร์ที่ใช้ได้: MasterVolume, MusicVolume, AmbientVolume, SFXVolume, UIVolume
    /// ใช้ทำเมนูตั้งค่าเสียง หรือหรี่เสียงบรรยากาศชั่วคราว (เช่นตอนเปิดสมุด/คุย NPC) ด้วยการเรียกซ้ำตามเวลา
    /// </summary>
    public void SetGroupVolume(string exposedParameter, float linear01)
    {
        if (mixer == null)
        {
            Debug.LogWarning("[AudioManager] ยังไม่ได้ผูก Mixer ใน AudioManager — ตั้งความดังกลุ่มเสียงไม่ได้");
            return;
        }

        float decibels = linear01 <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
        if (!mixer.SetFloat(exposedParameter, decibels))
        {
            Debug.LogWarning($"[AudioManager] ไม่พบพารามิเตอร์ '{exposedParameter}' ใน Mixer (ต้องเปิด Expose ไว้)");
        }
    }

    /// <summary>คืนความดังปัจจุบันของกลุ่มเสียงในสเกล 0..1 (1 = ปกติ) คืน 1 ถ้าไม่มี Mixer/พารามิเตอร์</summary>
    public float GetGroupVolume(string exposedParameter)
    {
        if (mixer == null || !mixer.GetFloat(exposedParameter, out float decibels)) return 1f;
        return decibels <= -79.9f ? 0f : Mathf.Pow(10f, decibels / 20f);
    }

    /// <summary>เล่นเสียง 2D ไม่มีตำแหน่งในโลก เช่นเสียง UI, เสียงกล้อง, เสียงชัตเตอร์</summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSource();
        source.spatialBlend = 0f;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * masterSFXVolume);
    }

    /// <summary>เล่นเสียงมีตำแหน่งในโลก (3D) เช่นเสียงสัตว์ร้อง, เสียงฝีเท้า NPC</summary>
    public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSource();
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * masterSFXVolume);
    }

    private AudioSource GetNextSource()
    {
        AudioSource source = sourcePool[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % sourcePool.Count;
        return source;
    }
}
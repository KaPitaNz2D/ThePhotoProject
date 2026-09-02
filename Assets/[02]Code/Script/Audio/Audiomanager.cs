using System.Collections.Generic;
using UnityEngine;

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

    [Header("SFX Pool")]
    [Tooltip("จำนวน AudioSource ที่เตรียมไว้ล่วงหน้า สำหรับเล่นเสียงซ้อนกันพร้อมกัน")]
    public int poolSize = 8;
    [Range(0f, 1f)]
    public float masterSFXVolume = 1f;

    private List<AudioSource> sourcePool = new List<AudioSource>();
    private int nextSourceIndex = 0;

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
            sourcePool.Add(source);
        }
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
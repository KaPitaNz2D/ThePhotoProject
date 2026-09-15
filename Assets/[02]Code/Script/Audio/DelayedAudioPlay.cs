using System.Collections;
using UnityEngine;

// เล่น AudioSource วนซ้ำแบบมีช่วงเว้นระหว่างรอบ (Loop ปกติของ AudioSource ต่อกันทันทีไม่มีช่องว่างให้)
// คุมจังหวะเล่นเองทั้งหมด ปิด Loop ของ AudioSource ให้อัตโนมัติกันเล่นซ้อนกัน
[RequireComponent(typeof(AudioSource))]
public class DelayedAudioPlay : MonoBehaviour
{
    [Tooltip("หน่วงกี่วินาทีก่อนเริ่มเล่นครั้งแรก")]
    public float delaySeconds = 20f;
    [Tooltip("ช่วงเว้นระหว่างแต่ละรอบที่เล่นจบ (0 = เล่นต่อกันทันทีเหมือน Loop ปกติ)")]
    public float gapBetweenLoops = 5f;

    private AudioSource source;

    private void Start()
    {
        source = GetComponent<AudioSource>();
        source.loop = false; // คุมจังหวะเองทั้งหมดแล้ว ปิด Loop ในตัวกันเล่นซ้อนกัน
        StartCoroutine(PlayLoopRoutine());
    }

    private IEnumerator PlayLoopRoutine()
    {
        yield return new WaitForSeconds(delaySeconds);

        while (true)
        {
            source.Play();
            yield return new WaitForSeconds(source.clip.length);
            yield return new WaitForSeconds(gapBetweenLoops);
        }
    }
}

using UnityEngine;

/// <summary>
/// ชุดเสียงสุ่มชุดเดียวสำหรับ AmbientDome (เช่น "นกกลางวัน", "ใบไม้ไหว", "จิ้งหรีดกลางคืน") — สร้างเป็น Asset แยกต่อชุด
/// คลิกขวาใน Project -> Create -> Audio -> Ambient Sound Set  ปรับค่าได้ใน Inspector โดยไม่ต้องแตะโค้ด
/// AmbientDome จะสุ่มเสียงจากชุดนี้เป็นระยะ แล้ววางเป็นจุด 3D รอบตัวผู้เล่น
/// </summary>
[CreateAssetMenu(fileName = "New Ambient Sound Set", menuName = "Audio/Ambient Sound Set")]
public class AmbientSoundSet : ScriptableObject
{
    [Header("Clips")]
    [Tooltip("เสียงที่จะสุ่มเล่น (ไม่เล่นตัวเดิมซ้ำติดกันถ้ามีมากกว่า 1 ตัว)")]
    public AudioClip[] clips;

    [Header("จังหวะ")]
    [Tooltip("ช่วงเวลา (วินาที) ระหว่างครั้งที่เล่น สุ่มระหว่างสองค่านี้")]
    public Vector2 intervalRange = new Vector2(4f, 12f);
    [Tooltip("โอกาสที่จะเล่นจริงเมื่อถึงรอบ (1 = เล่นทุกรอบ) ใช้ทำให้ไม่สม่ำเสมอเกินไป")]
    [Range(0f, 1f)] public float playChance = 1f;

    [Header("ตำแหน่ง (วางรอบตัวผู้เล่น)")]
    [Tooltip("ระยะห่างจากผู้เล่นในแนวราบ (หน่วยเมตร) สุ่มระหว่างสองค่านี้")]
    public Vector2 distanceRange = new Vector2(8f, 35f);
    [Tooltip("ความสูงเหนือพื้น ณ จุดนั้น (เมตร) เช่น นกบนต้นไม้ 4-20, ใบหญ้า 0-1, แมลง 0-2")]
    public Vector2 heightAboveGround = new Vector2(3f, 15f);

    [Header("ความดัง / เสียงสูงต่ำ")]
    public Vector2 volumeRange = new Vector2(0.5f, 1f);
    [Tooltip("pitch สุ่มเล็กน้อยให้เสียงเดิมฟังไม่ซ้ำ")]
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    [Header("การลดเสียงตามระยะ (3D)")]
    [Tooltip("ใกล้กว่านี้เสียงดังเต็มที่")]
    public float minDistance = 4f;
    [Tooltip("ไกลกว่านี้ไม่ได้ยิน")]
    public float maxDistance = 45f;
    [Tooltip("กรองเสียงแหลมออกตามระยะ (เสียงไกลๆ จะอู้ลง) ตั้ง 22000 = ไม่กรอง")]
    public float lowPassAtMaxDistance = 6000f;

    [Header("เงื่อนไข: ช่วงเวลาในเกม")]
    [Tooltip("ใช้เฉพาะเมื่อชั่วโมงในเกม (TimeManager.Hours) อยู่ในช่วงนี้ เช่น นก 6-18, จิ้งหรีด 19-5 (ข้ามเที่ยงคืนได้) ถ้าไม่ติ๊ก = ทุกเวลา")]
    public bool limitByHour = false;
    [Range(0, 23)] public int fromHour = 6;
    [Range(0, 23)] public int toHour = 18;

    [Header("เงื่อนไข: พื้นผิวใต้เท้าผู้เล่น")]
    [Tooltip("ใช้เฉพาะเมื่อพื้นใต้ผู้เล่นเป็น Terrain Layer ที่ชื่อมีคำนี้ (เช่น Grass) เว้นว่าง = ทุกพื้น")]
    public string requiredTerrainLayerKeyword = "";
    [Range(0.05f, 1f)] public float terrainLayerMinCoverage = 0.4f;

    /// <summary>ชั่วโมงนี้เล่นชุดนี้ได้ไหม (รองรับช่วงข้ามเที่ยงคืน)</summary>
    public bool IsActiveAtHour(int hour)
    {
        if (!limitByHour) return true;
        return fromHour <= toHour
            ? hour >= fromHour && hour < toHour
            : hour >= fromHour || hour < toHour;
    }
}

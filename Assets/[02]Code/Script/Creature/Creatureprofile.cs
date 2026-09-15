using UnityEngine;

/// <summary>
/// ข้อมูลกลางของ "สัตว์" 1 สายพันธุ์ (เช่น กวาง, กระต่าย) — สืบทอด Identity (CreatureId/JournalEntry)
/// จาก JournalSubjectProfile แล้วเพิ่มข้อมูลเฉพาะของสัตว์ (Vision, Wander, Run, Detection Timing)
/// ที่พืชไม่จำเป็นต้องมี (ดูคู่กับ PlantProfile ที่ไม่มีส่วนนี้เลย)
///
/// สร้าง Asset ผ่าน Create > Creature > Creature Profile แล้วลากไปใส่ทั้ง CreatureAI และ CreatureVision
/// ของ Prefab สายพันธุ์นั้น (ในอนาคต CreatureSpawn จะมาอ่าน Asset นี้ด้วยเช่นกันตามที่ออกแบบไว้)
/// </summary>
[CreateAssetMenu(fileName = "New Creature Profile", menuName = "Creature/Creature Profile")]
public class CreatureProfile : JournalSubjectProfile
{
    [Header("Vision Cone (ด้านหน้า)")]
    [Tooltip("มุมกว้างของโคนสายตารวม (องศา)")]
    public float viewAngle = 110f;
    [Tooltip("ระยะไกลสุดที่มองเห็น")]
    public float viewRadius = 15f;

    [Header("Awareness Radius (รอบตัว)")]
    public float awarenessRadius = 3f;

    [Header("Vision Detection Settings")]
    [Tooltip("Layer ของสิ่งกีดขวางที่บัง Line of Sight ได้ — ห้ามใส่ Layer ของพื้น/Terrain")]
    public LayerMask obstacleLayer;
    public float eyeHeight = 1f;
    [Tooltip("ความสูงจุดปลาย Raycast บนตัวผู้เล่น (นับจาก Root/เท้า) ต้องเล็งไปที่ระดับอก/หัว")]
    public float playerHeightOffset = 1.4f;

    [Header("Stealth เมื่อผู้เล่นย่อ")]
    [Range(0.1f, 1f)]
    public float crouchRangeMultiplier = 0.6f;

    [Header("Wander Settings (Idle <-> Walking)")]
    [Tooltip("รัศมีที่สุ่มจุดเดินไปจากจุดเกิด — ยิ่งกว้างยิ่งเดินนานขึ้นเพราะระยะทางไกลขึ้น")]
    public float wanderRadius = 10f;
    public float idleMinDuration = 2f;
    public float idleMaxDuration = 6f;
    public float walkSpeed = 1.5f;
    [Tooltip("เวลาสูงสุดที่ยอมให้อยู่ในสถานะ Walking ต่อรอบ ถ้าเดินไม่ถึงจุดหมายภายในเวลานี้ (เช่นติดสิ่งกีดขวาง) จะยกเลิกแล้วกลับ Idle เอง กันเดินติดค้างถาวร")]
    public float maxWalkDuration = 20f;

    [Header("Run Settings")]
    public float runSpeed = 6f;
    public float fleeDistance = 10f;
    public float safeDistance = 20f;
    public float fleeRecalculateInterval = 1f;
    [Tooltip("มุมสุ่มเบี่ยงจากทิศตรงข้ามผู้เล่นตอนวิ่งหนี (± องศา) กันวิ่งหนีเป็นเส้นตรงเป๊ะๆ ทุกครั้ง")]
    public float fleeAngleVariance = 45f;

    [Header("Detection Timing (Vision Cone เท่านั้น — Alert State)")]
    [Tooltip("ต้องเห็นผู้เล่นในโคนสายตาต่อเนื่องกี่วินาที ถึงจะเริ่มวิ่งหนี")]
    public float visionDetectionTime = 1f;
    [Tooltip("ตัวคูณเพิ่มเวลาที่ต้องใช้ตรวจจับ ตอนผู้เล่นย่ออยู่")]
    public float crouchDetectionTimeMultiplier = 2f;
}
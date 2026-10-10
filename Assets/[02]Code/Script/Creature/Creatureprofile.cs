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
    [Tooltip("ผู้เล่นเดินเข้ามาในวงนี้ (ไม่ต้องเห็นด้วยตา) -> กวางหยุดงง (Stop) ไม่ได้วิ่งหนีทันที ยกเว้นผู้เล่นกำลังวิ่งอยู่ (ดู Sprint Panic) " +
             "และตราบใดที่ผู้เล่นยังอยู่ในวงนี้ กวางจะวน Stop -> Look around ไม่กลับไป Normal")]
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
    [Tooltip("รัศมีที่สุ่มจุดเดินรอบตำแหน่งที่ยืนอยู่ตอนนั้น (ไม่ยึดจุดเกิด) — ยิ่งกว้างยิ่งเดินนานขึ้นเพราะระยะทางไกลขึ้น")]
    public float wanderRadius = 10f;
    public float idleMinDuration = 2f;
    public float idleMaxDuration = 6f;
    public float walkSpeed = 1.5f;
    [Tooltip("เวลาสูงสุดที่ยอมให้อยู่ในสถานะ Walking ต่อรอบ ถ้าเดินไม่ถึงจุดหมายภายในเวลานี้ (เช่นติดสิ่งกีดขวาง) จะยกเลิกแล้วกลับ Idle เอง กันเดินติดค้างถาวร")]
    public float maxWalkDuration = 20f;

    [Header("Run Settings")]
    public float runSpeed = 6f;
    public float fleeDistance = 10f;
    [Tooltip("วิ่งหนีจนห่างผู้เล่นเท่านี้ถึงหยุด ควรมากกว่า View Radius เสมอ ไม่งั้นกวางหยุดวิ่งทั้งที่ยังอยู่ในระยะมองเห็น แล้วเห็นผู้เล่น -> Alert -> Run ซ้ำเป็นลูป")]
    public float safeDistance = 20f;
    public float fleeRecalculateInterval = 1f;
    [Tooltip("มุมสุ่มเบี่ยงจากทิศตรงข้ามผู้เล่นตอนวิ่งหนี (± องศา) กันวิ่งหนีเป็นเส้นตรงเป๊ะๆ ทุกครั้ง")]
    public float fleeAngleVariance = 45f;

    [Header("Detection Timing (Vision Cone เท่านั้น — Alert State)")]
    [Tooltip("ต้องเห็นผู้เล่นในโคนสายตาต่อเนื่องกี่วินาที ถึงจะเริ่มวิ่งหนี")]
    public float visionDetectionTime = 1f;
    [Tooltip("ตัวคูณเพิ่มเวลาที่ต้องใช้ตรวจจับ ตอนผู้เล่นย่ออยู่")]
    public float crouchDetectionTimeMultiplier = 2f;

    [Header("Hearing (หู)")]
    [Tooltip("ระยะที่ได้ยินเสียงชัตเตอร์ (ทะลุสิ่งกีดขวางได้) -> เข้า Stop ตอนอยู่ Normal")]
    public float shutterHearingRadius = 25f;

    [Header("Startle (ผู้เล่นประชิดตัว)")]
    [Tooltip("ผู้เล่นเข้ามาใกล้ตัวกวางในระยะนี้ (เดิน/ย่อ/ยืน ก็ได้ และไม่ต้องเห็นด้วยตา) -> วิ่งหนีทันทีจากทุก State กันเดินไปยืนประชิดด้านหลังกวางที่ยังงงอยู่ " +
             "ตั้ง 0 = ปิด (ระหว่างก้มกินหญ้าวงนี้ลดตาม Eating Perception Multiplier ด้วย)")]
    public float startleRadius = 3f;

    [Header("Sprint Panic (ผู้เล่นวิ่งเข้ามา)")]
    [Tooltip("ผู้เล่นกำลังวิ่ง (Sprint) อยู่ในระยะนี้ -> วิ่งหนีทันทีจากทุก State ไม่ผ่าน Stop/Alert (กันกรณีวิ่งเข้าหาจากด้านหลัง)")]
    public float sprintPanicRadius = 8f;

    [Header("Eating (กินหญ้า — Normal)")]
    [Tooltip("โอกาสที่จะไปกินหญ้าแทนเดินต่อ ตอนหมดเวลา Idle (ต้องยืนบนพื้นหญ้าด้วย)")]
    [Range(0f, 1f)] public float eatChance = 0.35f;
    public float eatMinDuration = 8f;
    public float eatMaxDuration = 15f;
    [Tooltip("ตัวคูณ Vision Cone / Awareness / ระยะได้ยิน ตอนกำลังก้มกินหญ้า (0.5 = แคบลงครึ่งหนึ่ง)")]
    [Range(0.1f, 1f)] public float eatPerceptionMultiplier = 0.5f;
    [Tooltip("กินหญ้าได้เฉพาะพื้น Terrain Layer ที่ชื่อมีคำนี้ (ไม่สนตัวพิมพ์เล็ก-ใหญ่) เว้นว่าง = กินได้ทุกที่")]
    public string grazeSurfaceKeyword = "Grass";
    [Tooltip("สัดส่วนสีของ Layer หญ้า ณ จุดที่ยืนอย่างน้อยเท่านี้ถึงนับว่ามีหญ้า")]
    [Range(0.05f, 1f)] public float grazeMinCoverage = 0.5f;

    [Header("Head Look (หันเฉพาะหัว)")]
    [Tooltip("ความเร็วหันหัว (องศา/วินาที)")]
    public float headTurnSpeed = 200f;
    [Tooltip("โอกาสที่ตอน Idle จะหันมองข้างๆ (ซ้าย/ขวา/ทั้งสองข้าง)")]
    [Range(0f, 1f)] public float idleLookChance = 0.3f;
    public float idleLookAngle = 55f;
    [Tooltip("ค้างหัวไว้ที่มุมสุดแต่ละข้างกี่วินาที")]
    public float lookHoldTime = 0.6f;

    [Header("Confuse (Stop -> Look around)")]
    [Tooltip("Stop นิ่งกี่วินาทีถึงเข้า Look around (เข้าเร็วกว่านั้นถ้าผู้เล่นเดินเข้าใกล้เพิ่มหรือเข้าโหมดกล้อง)")]
    public float stopDuration = 2f;
    [Tooltip("ใน Stop ผู้เล่นเข้ามาใกล้กว่าตอนเริ่ม Stop เท่านี้ = นับว่าเดินเข้าใกล้")]
    public float stopApproachDistance = 1f;
    [Tooltip("Look around นานกี่วินาที ถ้าไม่เจออะไรกลับ Normal")]
    public float lookAroundDuration = 5f;
    public float lookAroundAngle = 65f;
    [Tooltip("เห็นผู้เล่นในโคนสายตาสะสมรวมกี่วินาทีใน Look around ถึงเข้า Alert")]
    public float lookAroundSpotTime = 1.5f;
    [Header("Look around — หมุนตัว (มองรอบๆ ได้ถึงด้านหลัง)")]
    [Tooltip("ทุกๆ lookAroundTurnInterval วินาทีใน Look around กวางสุ่มว่าจะหมุนตัวไปทิศอื่นไหม (นอกเหนือจากหันหัว) โอกาสต่อรอบ")]
    [Range(0f, 1f)] public float lookAroundTurnChance = 0.5f;
    public float lookAroundTurnInterval = 2f;
    [Tooltip("มุมหมุนตัวต่อครั้ง สุ่มระหว่างค่าต่ำสุด-สูงสุด (องศา) ซ้ายหรือขวาสุ่ม")]
    public float lookAroundTurnMinAngle = 60f;
    public float lookAroundTurnMaxAngle = 150f;
    [Tooltip("ความเร็วหมุนตัวตอน Look around (องศา/วินาที)")]
    public float lookAroundTurnSpeed = 100f;

    [Header("Alert (เผชิญหน้า)")]
    [Tooltip("ผู้เล่นยืนนิ่งใน Alert สะสมครบกี่วินาที -> กวางวิ่งหนี (ผู้เล่นขยับ/ถอยหลังอยู่ = ไม่นับ)")]
    public float alertStillTime = 5f;
    [Tooltip("ผู้เล่นเดินเข้าใกล้กว่าระยะตอนเข้า Alert เกินเท่านี้ -> วิ่งหนีทันที")]
    public float alertApproachDistance = 5f;
    [Tooltip("ผู้เล่นถอยห่างกว่าระยะตอนเข้า Alert เกินเท่านี้ -> กลับไป Stop")]
    public float alertRetreatDistance = 10f;
    [Tooltip("ความเร็วหมุนตัวหันหน้าหาผู้เล่นตอน Alert (องศา/วินาที)")]
    public float alertTurnSpeed = 180f;
    [Tooltip("ความเร็วผู้เล่น (ม./วินาที) ต่ำกว่านี้นับว่า \"ไม่ขยับ\"")]
    public float playerStillSpeed = 0.3f;
}
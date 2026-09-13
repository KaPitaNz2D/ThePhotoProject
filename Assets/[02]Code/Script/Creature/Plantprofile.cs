using UnityEngine;

/// <summary>
/// ข้อมูลของพืช 1 ชนิด — มีแค่ Identity สำหรับ Journal เท่านั้น (สืบทอดจาก JournalSubjectProfile)
/// ไม่มี Vision Cone, Awareness Radius, หรือ Behavior Tree เพราะพืชไม่ขยับและไม่มองเห็นผู้เล่น
/// ถ้าในอนาคตต้องการ Field เฉพาะของพืช (เช่น เก็บเกี่ยวได้ไหม, ฤดูที่ออกดอก) มาเพิ่มในนี้ได้เลย
/// </summary>
[CreateAssetMenu(fileName = "New Plant Profile", menuName = "Creature/Plant Profile")]
public class PlantProfile : JournalSubjectProfile
{
    // ตอนนี้ไม่มี Field เพิ่มเติม — ใช้แค่ CreatureId/JournalEntry จาก Base Class พอ
}
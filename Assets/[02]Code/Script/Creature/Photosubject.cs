using UnityEngine;

/// <summary>
/// แปะไว้ที่ Root GameObject ของสิ่งมีชีวิต/พืชที่ถ่ายรูปได้เท่านั้น (เช่น "Deer", "Berry Bush")
/// ส่วนย่อยๆ ข้างใน (Neck, Head, Antler ฯลฯ) ไม่ต้องแปะ — แค่ติด Tag "Photographable" พอ
///
/// รับ Reference เป็น JournalSubjectProfile (Base Class) ไม่ใช่ CreatureProfile ตรงๆ
/// เพราะงั้นลากได้ทั้ง CreatureProfile (สัตว์) และ PlantProfile (พืช) เข้าช่องเดียวกันนี้
/// CreatureId ดึงมาจาก Asset โดยตรง ป้องกันปัญหาพิมพ์ผิดเหมือนเดิม
/// </summary>
public class PhotoSubject : MonoBehaviour
{
    [Tooltip("ลาก CreatureProfile (สัตว์) หรือ PlantProfile (พืช) ก็ได้ — CreatureId จะดึงมาจาก Asset นี้โดยตรง")]
    public JournalSubjectProfile profile;

    /// <summary>CreatureId ที่แท้จริง ดึงจาก Profile ที่ผูกไว้ — คืนค่า null ถ้ายังไม่ได้ผูก Profile</summary>
    public string CreatureId => profile != null ? profile.CreatureId : null;
}
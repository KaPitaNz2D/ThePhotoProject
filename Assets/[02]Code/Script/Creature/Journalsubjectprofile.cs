using UnityEngine;

/// <summary>
/// ฐานร่วมของทุก Profile ที่จะปรากฏใน Journal ได้ (พืช, สัตว์, หรือ Subject ประเภทอื่นในอนาคต)
/// เก็บแค่ "ตัวตนสำหรับ Journal" เท่านั้น — ข้อมูลเฉพาะทาง (Vision, Behavior ฯลฯ) ให้ Class ลูกเพิ่มเอง
/// เช่น CreatureProfile เพิ่ม Vision/Behavior, PlantProfile ไม่ต้องเพิ่มอะไรเลยเพราะพืชไม่ขยับ/ไม่มองเห็น
/// </summary>
public abstract class JournalSubjectProfile : ScriptableObject, IJournalSubject
{
    [Header("Identity")]
    [Tooltip("ต้องตรงกับ PhotoSubject.CreatureId เป๊ะ ใช้จับคู่ระบบถ่ายรูป/Journal/Quest")]
    [SerializeField] private string creatureId;
    [Tooltip("ลิงก์ไปยัง JournalEntry ของสิ่งนี้")]
    [SerializeField] private JournalEntry journalEntry;

    public string CreatureId => creatureId;
    public JournalEntry JournalEntry => journalEntry;
}
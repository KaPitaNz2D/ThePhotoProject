/// <summary>
/// สัญญาว่าอะไรก็ตามที่จะปรากฏในระบบ Journal ได้ ต้องมีข้อมูลนี้ครบ
/// ใช้เป็น Type กลางให้โค้ดที่ต้องการแค่ "ตัวตนสำหรับ Journal" เขียนแบบ Generic ได้
/// โดยไม่ต้องสนใจว่าเบื้องหลังเป็นพืช สัตว์ หรือ Subject ประเภทอื่นในอนาคต
///
/// หมายเหตุ: Unity Inspector ลาก-วาง Field แบบ Interface ตรงๆ ไม่ได้ (ข้อจำกัดของ Serialization)
/// เพราะงั้นของจริงที่ลากใน Inspector ให้ใช้ JournalSubjectProfile (Abstract Class ที่ Implement Interface นี้) แทน
/// </summary>
public interface IJournalSubject
{
    /// <summary>ต้องตรงกับ PhotoSubject.CreatureId เป๊ะ ใช้จับคู่ระบบถ่ายรูป/Journal/Quest ทั้งหมด</summary>
    string CreatureId { get; }

    /// <summary>ลิงก์ไปยัง JournalEntry ของ Subject นี้</summary>
    JournalEntry JournalEntry { get; }
}
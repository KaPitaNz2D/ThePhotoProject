using UnityEngine;

/// <summary>
/// ข้อมูล 1 รายการในสมุดบันทึก (Field Guide) — สร้างเป็น Asset แยกไฟล์ต่อสัตว์/พืช 1 ชนิด
/// คลิกขวาใน Project -> Create -> Field Guide -> Journal Entry
///
/// creatureId ต้องตรงกับ PhotoSubject.creatureId ของโมเดลตัวนั้นๆ เป๊ะ (ตัวพิมพ์เล็ก-ใหญ่มีผล)
/// เพื่อให้ JournalManager จับคู่รูปที่ถ่ายได้เข้ากับหน้าที่ถูกต้องอัตโนมัติ
/// </summary>
[CreateAssetMenu(fileName = "New Journal Entry", menuName = "Field Guide/Journal Entry")]
public class JournalEntry : ScriptableObject
{
    [Tooltip("ต้องตรงกับ PhotoSubject.creatureId ของโมเดลตัวนั้นเป๊ะ ใช้จับคู่ตอนถ่ายรูปสำเร็จ")]
    public string creatureId;

    [Header("ข้อมูลแสดงผล")]
    public string displayName;
    [TextArea(3, 6)]
    public string description;

    [Header("ตอนยังไม่ปลดล็อก")]
    [Tooltip("รูปเงาดำ/ภาพวาดคร่าวๆ โชว์แทนตอนยังไม่เคยถ่ายติดสิ่งมีชีวิตนี้ (แบบ Pokedex)")]
    public Sprite silhouette;
}
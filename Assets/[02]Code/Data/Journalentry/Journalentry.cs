using UnityEngine;

/// <summary>
/// ข้อมูล 1 รายการในสมุดบันทึก (Field Guide) — สร้างเป็น Asset แยกไฟล์ต่อสัตว์/พืช 1 ชนิด
/// คลิกขวาใน Project -> Create -> Field Guide -> Journal Entry
///
/// creatureId ต้องตรงกับ PhotoSubject.creatureId ของโมเดลตัวนั้นเป๊ะ (ตัวพิมพ์เล็ก-ใหญ่มีผล)
/// ใช้จับคู่กับ Metadata ใน PhotoStorage เพื่อเช็คว่าปลดล็อกแล้วหรือยัง
/// </summary>
[CreateAssetMenu(fileName = "New Journal Entry", menuName = "Field Guide/Journal Entry")]
public class JournalEntry : ScriptableObject
{
    /// <summary>หมวดหมู่ของ Entry — กำหนดว่าจะโผล่ในหน้า Plant หรือ Animal ตอนเลือกหมวดใน Journal</summary>
    public enum JournalCategory { Plant, Animal }

    [Tooltip("ต้องตรงกับ PhotoSubject.creatureId ของโมเดลตัวนั้นเป๊ะ ใช้จับคู่ภาพจาก PhotoStorage")]
    public string creatureId;

    [Tooltip("หมวดหมู่ของ Entry นี้")]
    public JournalCategory category;

    [Header("ข้อมูลแสดงผล (โชว์ในหน้า Detail ตอนปลดล็อกแล้ว)")]
    public string displayName;
    [TextArea(3, 6)]
    public string description;

    [Header("รูปเงา/ภาพมืด")]
    [Tooltip("โชว์เสมอในกริด 9 ช่อง (ไม่ว่าจะปลดล็อกหรือยัง) และโชว์ในหน้า Detail ถ้ายังไม่ปลดล็อก")]
    public Sprite silhouette;
}
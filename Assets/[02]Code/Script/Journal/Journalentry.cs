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
    /// <summary>
    /// หมวดหมู่ของ Entry — กำหนดว่าจะโผล่ในหน้า Flora / Animal / Fungus ตอนเลือกหมวดใน Journal
    /// ค่าตัวเลขถูกบันทึกลง Asset ห้ามสลับลำดับ: Flora = 0 (เดิมคือ Plant), Animal = 1, Fungus = 2 (เพิ่มใหม่)
    /// </summary>
    public enum JournalCategory { Flora = 0, Animal = 1, Fungus = 2 }

    [Tooltip("ต้องตรงกับ PhotoSubject.creatureId ของโมเดลตัวนั้นเป๊ะ ใช้จับคู่ภาพจาก PhotoStorage")]
    public string creatureId;

    [Tooltip("หมวดหมู่ของ Entry นี้")]
    public JournalCategory category;

    [Header("ข้อมูลแสดงผล (โชว์ในหน้า Detail ต่อเมื่อมีรูปแล้ว และ NPC เคยเล่าเรื่องนี้ให้ฟังแล้วเท่านั้น)")]
    public string displayName;
    [TextArea(3, 6)]
    public string description;

    [Header("ข้อมูลสถานที่ — โชว์ทันทีแม้ยังไม่ได้ถ่ายรูป ช่วยผู้เล่นหาตัวเจอ")]
    [Tooltip("รูปสิ่งมีชีวิตแบบชัดเจน (ภาพอ้างอิง ไม่ใช่รูปที่ผู้เล่นถ่ายเอง)")]
    public Sprite referenceImage;
    [Tooltip("รายละเอียดสถานที่ที่คาดว่าจะพบได้ — เขียนในมุม \"บ้าน\" ของมันได้ ผูก Theme ไปในตัว")]
    [TextArea(2, 4)]
    public string habitatInfo;

    [Header("รูปเงา/ภาพมืด")]
    [Tooltip("โชว์เสมอในกริด 9 ช่อง (ไม่ว่าจะปลดล็อกหรือยัง) และโชว์ในหน้า Detail ถ้ายังไม่ปลดล็อก")]
    public Sprite silhouette;
}
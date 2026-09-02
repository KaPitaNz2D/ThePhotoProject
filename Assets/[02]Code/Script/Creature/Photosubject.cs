using UnityEngine;

/// <summary>
/// แปะไว้ที่ Root GameObject ของสิ่งมีชีวิต/วัตถุที่ถ่ายรูปได้เท่านั้น (เช่น "Deer")
/// ส่วนย่อยๆ ข้างใน (Neck, Head, Antler, Leg ฯลฯ) ไม่ต้องแปะ — แค่ติด Tag "Photographable"
/// กับ Collider ของมันพอ ระบบจะไล่หา Component นี้ที่ Parent เพื่อรู้ว่า
/// "ส่วนที่ถ่ายติดนี้ เป็นของสิ่งมีชีวิตตัวไหนกันแน่" ไม่ว่าจะมีกี่ Collider ย่อยก็ตาม
/// </summary>
public class PhotoSubject : MonoBehaviour
{
    [Tooltip("ชื่อ/ID ของสิ่งมีชีวิตนี้ ใช้อ้างอิงตอน Unlock Field Guide หรือระบบอื่นๆ ต่อไป")]
    public string creatureId;
}
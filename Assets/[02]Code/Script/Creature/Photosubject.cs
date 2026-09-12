using UnityEngine;

/// <summary>
/// แปะไว้ที่ Root GameObject ของสิ่งมีชีวิต/วัตถุที่ถ่ายรูปได้เท่านั้น (เช่น "Deer")
/// ส่วนย่อยๆ ข้างใน (Neck, Head, Antler ฯลฯ) ไม่ต้องแปะ — แค่ติด Tag "Photographable" พอ
///
/// อ้างอิง CreatureProfile โดยตรงแทนการพิมพ์ creatureId เอง — creatureId ตัวจริงอยู่ใน
/// CreatureProfile.creatureId ที่เดียวเท่านั้น ป้องกันปัญหาพิมพ์ผิดที่เคยเกิดขึ้นมาก่อน
/// (ลาก Asset ผิดไม่ได้ ต่างจากพิมพ์ String ที่พลาดง่าย)
/// </summary>
public class PhotoSubject : MonoBehaviour
{
    [Tooltip("ลาก CreatureProfile ของสิ่งมีชีวิตนี้ — creatureId จะดึงมาจาก Asset นี้โดยตรง ไม่ต้องพิมพ์เอง")]
    public CreatureProfile profile;

    /// <summary>creatureId ที่แท้จริง ดึงจาก CreatureProfile ที่ผูกไว้ — คืนค่า null ถ้ายังไม่ได้ผูก Profile</summary>
    public string CreatureId => profile != null ? profile.creatureId : null;
}
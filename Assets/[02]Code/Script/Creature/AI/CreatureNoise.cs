using System;
using UnityEngine;

/// <summary>
/// ช่องทางประกาศ "เสียง" ในโลกให้สัตว์ได้ยิน — ผู้ส่งไม่ต้องรู้ว่ามีสัตว์ตัวไหนบ้าง สัตว์แต่ละตัวเช็คระยะเองตามหูของสายพันธุ์
/// ตอนนี้มีแค่เสียงชัตเตอร์กล้อง (PhotoShooter ส่งทุกครั้งที่ถ่าย)
/// </summary>
public static class CreatureNoise
{
    public static event Action<Vector3> OnShutter;

    public static void EmitShutter(Vector3 position) => OnShutter?.Invoke(position);
}

using System;

/// <summary>
/// Framework Behavior Tree แบบเบาที่สุดเท่าที่จำเป็น — ไม่มี Visual Editor, ไม่มี Blackboard ซับซ้อน
/// เขียนต่อ Node เป็นโค้ดตรงๆ ได้เลย เหมาะกับตอนนี้ที่เงื่อนไข AI ยังไม่เยอะ
///
/// พอในอนาคตมีเงื่อนไขซับซ้อนขึ้นมาก (Time Cycle, พฤติกรรมหลายสิบแบบ) ค่อยพิจารณาย้ายไป Asset สำเร็จรูป
/// เช่น NodeCanvas/Behavior Designer ที่มี Visual Editor — ตอนนี้ยังไม่จำเป็น
/// </summary>
public enum BTStatus { Success, Failure, Running }

public abstract class BTNode
{
    public abstract BTStatus Tick();
}

/// <summary>
/// เลือกกิ่งแรกที่ "ไม่ Fail" ตามลำดับความสำคัญ (เหมือน OR ที่มีลำดับความสำคัญ)
/// ใช้ทำ Priority เช่น เช็ค Engage ก่อน ถ้าไม่เข้าเงื่อนไขค่อยตกไปที่ Normal
/// </summary>
public class BTSelector : BTNode
{
    private readonly BTNode[] children;
    public BTSelector(params BTNode[] children) { this.children = children; }

    public override BTStatus Tick()
    {
        foreach (BTNode child in children)
        {
            BTStatus status = child.Tick();
            if (status != BTStatus.Failure) return status;
        }
        return BTStatus.Failure;
    }
}

/// <summary>
/// รันกิ่งลูกเรียงกันตามลำดับ หยุดทันทีที่ตัวไหน Fail (เหมือน AND)
/// ใช้ทำแบบ "ถ้าเงื่อนไขนี้ผ่าน ค่อยทำ Action ต่อ"
/// </summary>
public class BTSequence : BTNode
{
    private readonly BTNode[] children;
    public BTSequence(params BTNode[] children) { this.children = children; }

    public override BTStatus Tick()
    {
        foreach (BTNode child in children)
        {
            BTStatus status = child.Tick();
            if (status != BTStatus.Success) return status;
        }
        return BTStatus.Success;
    }
}

/// <summary>Leaf Node เช็คเงื่อนไข — Success ถ้าจริง, Failure ถ้าเท็จ</summary>
public class BTCondition : BTNode
{
    private readonly Func<bool> condition;
    public BTCondition(Func<bool> condition) { this.condition = condition; }
    public override BTStatus Tick() => condition() ? BTStatus.Success : BTStatus.Failure;
}

/// <summary>Leaf Node ทำงานจริง — คืนค่า Status ตามที่ Action กำหนดเอง</summary>
public class BTAction : BTNode
{
    private readonly Func<BTStatus> action;
    public BTAction(Func<BTStatus> action) { this.action = action; }
    public override BTStatus Tick() => action();
}
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ปุ่มลัดสั่งสัตว์สำหรับอัดคลิป (ใช้คู่กับ FreeCam F8) — สั่งตัวที่อยู่ใกล้กล้องที่สุด หรือตัวที่ลากใส่ช่อง Target
///   F5 = ก้มกินหญ้าค้างไว้
///   F6 = เดินไปจุดที่กล้องเล็งอยู่ (กลางจอ) — ติ๊ก eatOnArrive ให้ถึงแล้วกินหญ้าต่อ
///   F7 = ยืนนิ่ง
///   F9 = คืนให้ AI คุมเองตามปกติ (สั่งตัวที่ถูกสั่งค้างไว้ทุกตัว)
/// ระหว่างถูกสั่ง สัตว์ไม่รับรู้ผู้เล่นและไม่หนี (CreatureAI.IsScripted) — กดปุ่มบน Inspector ของ CreatureAI ตอน Play ก็ได้เหมือนกัน
/// ใช้ได้เฉพาะ Editor/Development Build
/// </summary>
public class CreatureDirector : MonoBehaviour
{
    [Tooltip("สัตว์ที่จะสั่ง ว่างไว้ = ตัวที่ใกล้กล้องที่สุดตอนกดปุ่ม")]
    public CreatureAI target;
    [Tooltip("หาสัตว์ใกล้กล้องในระยะนี้ (เมตร)")]
    public float maxPickDistance = 120f;

    [Header("Keys")]
    public Key eatKey = Key.F5;
    public Key walkKey = Key.F6;
    public Key idleKey = Key.F7;
    public Key releaseKey = Key.F9;

    [Header("Walk")]
    [Tooltip("เดินถึงจุดแล้วก้มกินหญ้าต่อ (ไม่ติ๊ก = ยืนนิ่ง)")]
    public bool eatOnArrive = true;
    [Tooltip("Layer พื้นที่ใช้หาจุดที่กล้องเล็ง")]
    public LayerMask groundMask = 1 << 6;
    public float maxAimDistance = 500f;

    [Header("Debug")]
    public bool logCommands = true;

    private void Update()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[releaseKey].wasPressedThisFrame)
        {
            foreach (CreatureAI ai in FindObjectsByType<CreatureAI>(FindObjectsSortMode.None)) ai.ReleaseScripted();
            Log("คืนให้ AI คุมเองทุกตัว");
            return;
        }

        bool eat = keyboard[eatKey].wasPressedThisFrame;
        bool walk = keyboard[walkKey].wasPressedThisFrame;
        bool idle = keyboard[idleKey].wasPressedThisFrame;
        if (!eat && !walk && !idle) return;

        CreatureAI creature = target != null ? target : FindNearestToCamera();
        if (creature == null)
        {
            Log($"ไม่มีสัตว์ในระยะ {maxPickDistance}m จากกล้อง");
            return;
        }

        if (eat)
        {
            creature.ScriptedEat();
            Log($"{creature.name}: กินหญ้า");
        }
        else if (idle)
        {
            creature.ScriptedIdle();
            Log($"{creature.name}: ยืนนิ่ง");
        }
        else if (TryGetAimPoint(out Vector3 point))
        {
            bool ok = creature.ScriptedWalkTo(point, eatOnArrive);
            Log(ok ? $"{creature.name}: เดินไป {point}" : $"{creature.name}: จุดที่เล็งไม่มี NavMesh ใกล้ๆ เดินไปไม่ได้");
        }
        else
        {
            Log("กล้องไม่ได้เล็งโดนพื้น");
        }
    }

    private CreatureAI FindNearestToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        CreatureAI nearest = null;
        float best = maxPickDistance * maxPickDistance;
        foreach (CreatureAI ai in FindObjectsByType<CreatureAI>(FindObjectsSortMode.None))
        {
            float d = (ai.transform.position - cam.transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = ai;
            }
        }
        return nearest;
    }

    private bool TryGetAimPoint(out Vector3 point)
    {
        point = default;
        Camera cam = Camera.main;
        if (cam == null) return false;

        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, maxAimDistance, groundMask, QueryTriggerInteraction.Ignore)) return false;
        point = hit.point;
        return true;
    }

    private void Log(string message)
    {
        if (logCommands) Debug.Log($"[CreatureDirector] {message}", this);
    }
}

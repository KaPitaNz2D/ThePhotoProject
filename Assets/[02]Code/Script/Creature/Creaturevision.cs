using UnityEngine;

/// <summary>
/// ตรวจจับผู้เล่นด้วย 2 ระบบ ไม่ยุ่งกับ State/พฤติกรรมเลย — แค่ตอบว่า "เจอผู้เล่นไหม" เท่านั้น
///
///   1) Vision Cone (โคนสายตาด้านหน้า) — มีมุมกับระยะที่ปรับได้ ต้องไม่มีอะไรบัง (Line of Sight)
///      ลดลงอัตโนมัติตอนผู้เล่นย่ออยู่ (อ่านจาก StateManager กลาง)
///   2) Awareness Radius (วงรับรู้รอบตัว) — ตรวจจับได้ทุกทิศทางไม่ต้องเห็น ระยะสั้นกว่า Vision Cone
///      ไว้จับกรณีผู้เล่นย่องมาจากด้านหลัง/ด้านข้างใกล้เกินไป (ไม่ลดตาม Crouch)
/// </summary>
public class CreatureVision : MonoBehaviour
{
    [Header("Vision Cone (ด้านหน้า)")]
    [Tooltip("มุมกว้างของโคนสายตารวม (องศา) เช่น 110 = กว้างข้างละ 55 องศาจากกึ่งกลาง")]
    public float viewAngle = 110f;
    [Tooltip("ระยะไกลสุดที่มองเห็น")]
    public float viewRadius = 15f;

    [Header("Awareness Radius (รอบตัว)")]
    [Tooltip("ระยะรับรู้รอบตัวแบบไม่ต้องเห็น ตรวจจับได้ทุกทิศทาง (ควรเล็กกว่า View Radius มาก)")]
    public float awarenessRadius = 3f;

    [Header("Detection Settings")]
    [Tooltip("Layer ของสิ่งกีดขวางที่บัง Line of Sight ได้ เช่นกำแพง/ก้อนหิน/ต้นไม้")]
    public LayerMask obstacleLayer;
    [Tooltip("ความสูงจุดตาโดยประมาณ ใช้เป็นจุดเริ่ม Raycast เช็คสิ่งกีดขวาง")]
    public float eyeHeight = 1f;

    [Header("Stealth เมื่อผู้เล่นย่อ")]
    [Tooltip("ตัวคูณลดระยะและมุมของ Vision Cone ตอนผู้เล่นย่ออยู่ (0.6 = เหลือ 60%) ไม่มีผลกับ Awareness Radius")]
    [Range(0.1f, 1f)]
    public float crouchRangeMultiplier = 0.6f;

    [Header("Debug")]
    [Tooltip("แสดง Gizmo โคนสายตาแบบเต็ม (ไม่ลด) เป็นเส้นจางๆ เทียบกับโคนจริงตอนนี้ ให้เห็นผลต่างชัดๆ")]
    public bool showFullConeComparison = true;
    [Tooltip("Log ใน Console ทุกครั้งที่ผู้เล่นสลับย่อ/ลุก บอกตัวเลขก่อน-หลังของ Vision Cone")]
    public bool logCrouchDetectionChange = true;

    private bool cachedCrouchState;
    private bool crouchStateInitialized;

    /// <summary>เช็คเฉพาะ Awareness Radius (รอบตัว ไม่ต้องเห็นด้วยตา) — ใช้ Trigger ทันทีไม่ต้องจับเวลา</summary>
    public bool IsPlayerInAwarenessRadius(Transform player)
    {
        if (player == null) return false;
        float distance = Vector3.Distance(transform.position, player.position);
        return distance <= awarenessRadius;
    }

    /// <summary>เช็คเฉพาะ Vision Cone (ต้องอยู่ในระยะ, อยู่ในมุมมอง, และไม่มีอะไรบัง) — ใช้คู่กับตัวจับเวลาฝั่ง CreatureAI</summary>
    public bool IsPlayerInVisionCone(Transform player)
    {
        if (player == null) return false;

        GetEffectiveVisionParams(out float effectiveViewRadius, out float effectiveViewAngle);

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > effectiveViewRadius) return false;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > effectiveViewAngle / 2f) return false;

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        if (Physics.Linecast(eyePosition, player.position, obstacleLayer))
        {
            return false; // โดนบัง มองไม่เห็น
        }

        return true;
    }

    /// <summary>
    /// คำนวณระยะ/มุมสายตาที่ใช้จริง ณ ตอนนี้ (ลดแล้วถ้าผู้เล่นย่ออยู่)
    /// รวม Logic เช็คสถานะ Crouch ไว้จุดเดียว ทั้ง IsPlayerInVisionCone และ Gizmo Debug เรียกใช้ตัวเดียวกัน
    /// </summary>
    private void GetEffectiveVisionParams(out float effectiveViewRadius, out float effectiveViewAngle)
    {
        bool playerCrouching = IsPlayerCrouchingNow();
        effectiveViewRadius = playerCrouching ? viewRadius * crouchRangeMultiplier : viewRadius;
        effectiveViewAngle = playerCrouching ? viewAngle * crouchRangeMultiplier : viewAngle;
    }

    private bool IsPlayerCrouchingNow()
    {
        bool crouching = StateManager.Instance != null &&
            StateManager.Instance.CurrentMovementState == StateManager.MovementState.Crouch;

        // Log แค่ตอน "เปลี่ยนสถานะ" เท่านั้น ไม่ Log รัวทุกเฟรม
        if (logCrouchDetectionChange && (!crouchStateInitialized || crouching != cachedCrouchState))
        {
            crouchStateInitialized = true;
            cachedCrouchState = crouching;

            float reducedRadius = viewRadius * crouchRangeMultiplier;
            float reducedAngle = viewAngle * crouchRangeMultiplier;

            if (crouching)
            {
                Debug.Log($"[CreatureVision] {gameObject.name}: ผู้เล่นย่อ -> " +
                          $"View Radius {viewRadius:F1} -> {reducedRadius:F1} | " +
                          $"View Angle {viewAngle:F1}° -> {reducedAngle:F1}°");
            }
            else
            {
                Debug.Log($"[CreatureVision] {gameObject.name}: ผู้เล่นลุกยืน -> " +
                          $"View Radius {reducedRadius:F1} -> {viewRadius:F1} | " +
                          $"View Angle {reducedAngle:F1}° -> {viewAngle:F1}°");
            }
        }

        return crouching;
    }

    // ==================== Debug Gizmos ====================
    private void OnDrawGizmosSelected()
    {
        GetEffectiveVisionParams(out float effectiveViewRadius, out float effectiveViewAngle);

        // โคนเต็ม (ไม่ลด) วาดเป็นเส้นจางๆ ไว้เทียบ — เห็นเฉพาะตอนผู้เล่นกำลังย่ออยู่เท่านั้น (ไม่งั้นจะซ้อนทับโคนจริงพอดี มองไม่เห็นความต่าง)
        if (showFullConeComparison && !Mathf.Approximately(effectiveViewRadius, viewRadius))
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            DrawVisionCone(viewAngle, viewRadius);
        }

        // โคนจริงตอนนี้ (ลดแล้วถ้าผู้เล่นย่ออยู่) วาดเป็นเส้นสว่างชัดเจน
        Gizmos.color = Color.yellow;
        DrawVisionCone(effectiveViewAngle, effectiveViewRadius);

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        DrawCircle(transform.position, awarenessRadius);
    }

    private void DrawVisionCone(float angle, float radius)
    {
        Vector3 forward = transform.forward;
        Vector3 leftDir = Quaternion.AngleAxis(-angle / 2f, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(angle / 2f, Vector3.up) * forward;

        Gizmos.DrawLine(transform.position, transform.position + leftDir * radius);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * radius);

        int segments = 20;
        Vector3 prevPoint = transform.position + leftDir * radius;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -angle / 2f + (angle * i / segments);
            Vector3 point = transform.position + (Quaternion.AngleAxis(currentAngle, Vector3.up) * forward) * radius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        int segments = 32;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (360f / segments) * i * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
}
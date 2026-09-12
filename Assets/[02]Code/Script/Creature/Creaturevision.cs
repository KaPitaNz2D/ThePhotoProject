using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ตรวจจับผู้เล่นด้วย 2 ระบบ ไม่ยุ่งกับ State/พฤติกรรมเลย — แค่ตอบว่า "เจอผู้เล่นไหม" เท่านั้น
/// ค่าปรับแต่งทั้งหมด (มุม, ระยะ, Layer ฯลฯ) ดึงมาจาก CreatureProfile ที่ผูกไว้ ไม่มี Field ของตัวเองอีกต่อไป
/// แก้พฤติกรรมทั้งสายพันธุ์ได้จาก Asset เดียว
///
///   1) Vision Cone (โคนสายตาด้านหน้า) — ต้องไม่มีอะไรบัง (Line of Sight) ลดลงอัตโนมัติตอนผู้เล่นย่ออยู่
///   2) Awareness Radius (วงรับรู้รอบตัว) — ตรวจจับได้ทุกทิศทางไม่ต้องเห็น (ไม่ลดตาม Crouch)
/// </summary>
public class CreatureVision : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Asset ที่กำหนดค่าปรับแต่งทั้งหมดของสายพันธุ์นี้")]
    public CreatureProfile profile;

    [Header("Debug (ค่า Editor ล้วนๆ ไม่ใช่ข้อมูลสายพันธุ์ เลยไม่ย้ายไป Profile)")]
    [Tooltip("แสดง Gizmo โคนสายตาแบบเต็ม (ไม่ลด) จางๆ เทียบกับโคนจริงตอนนี้ ให้เห็นผลต่างชัดๆ")]
    public bool showFullConeComparison = true;
    [Tooltip("Log ใน Console ทุกครั้งที่ผู้เล่นสลับย่อ/ลุก บอกตัวเลขก่อน-หลังของ Vision Cone")]
    public bool logCrouchDetectionChange = true;
    [Tooltip("ความสูงของโคน Debug ที่วาด (ไม่กระทบ Logic การตรวจจับ)")]
    public float debugConeHeight = 2f;

    private bool cachedCrouchState;
    private bool crouchStateInitialized;

    // เก็บผลลัพธ์ Line of Sight ล่าสุดไว้วาด Debug Gizmo
    private bool lastLOSChecked;
    private bool lastLOSBlocked;
    private Vector3 lastEyePosition;
    private Vector3 lastTargetPosition;

    private void Awake()
    {
        if (profile == null)
        {
            Debug.LogError($"[CreatureVision] {gameObject.name} ไม่ได้ผูก CreatureProfile ไว้! " +
                            "ลาก Asset ใส่ช่อง Profile ก่อน ไม่งั้นตรวจจับผู้เล่นไม่ได้เลย");
        }
    }

    /// <summary>เช็คเฉพาะ Awareness Radius (รอบตัว ไม่ต้องเห็นด้วยตา) — ใช้ Trigger ทันทีไม่ต้องจับเวลา</summary>
    public bool IsPlayerInAwarenessRadius(Transform player)
    {
        if (player == null || profile == null) return false;
        float distance = Vector3.Distance(transform.position, player.position);
        return distance <= profile.awarenessRadius;
    }

    /// <summary>เช็คเฉพาะ Vision Cone (ต้องอยู่ในระยะ, อยู่ในมุมมอง, และไม่มีอะไรบัง)</summary>
    public bool IsPlayerInVisionCone(Transform player)
    {
        if (player == null || profile == null) return false;

        GetEffectiveVisionParams(out float effectiveViewRadius, out float effectiveViewAngle);

        // เล็งไปที่ระดับอกผู้เล่น (Root + Offset) ไม่ใช่เท้าตรงๆ — กันเส้นเฉียงลงชนพื้นก่อนถึงตัวผู้เล่น
        Vector3 targetPosition = player.position + Vector3.up * profile.playerHeightOffset;
        Vector3 eyePosition = transform.position + Vector3.up * profile.eyeHeight;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > effectiveViewRadius)
        {
            lastLOSChecked = false;
            return false;
        }

        Vector3 directionToTarget = (targetPosition - eyePosition).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToTarget);
        if (angleToPlayer > effectiveViewAngle / 2f)
        {
            lastLOSChecked = false;
            return false;
        }

        bool blocked = Physics.Linecast(eyePosition, targetPosition, profile.obstacleLayer);

        lastLOSChecked = true;
        lastEyePosition = eyePosition;
        lastTargetPosition = targetPosition;
        lastLOSBlocked = blocked;

        return !blocked;
    }

    /// <summary>คำนวณระยะ/มุมสายตาที่ใช้จริง ณ ตอนนี้ (ลดแล้วถ้าผู้เล่นย่ออยู่)</summary>
    private void GetEffectiveVisionParams(out float effectiveViewRadius, out float effectiveViewAngle)
    {
        bool playerCrouching = IsPlayerCrouchingNow();
        effectiveViewRadius = playerCrouching ? profile.viewRadius * profile.crouchRangeMultiplier : profile.viewRadius;
        effectiveViewAngle = playerCrouching ? profile.viewAngle * profile.crouchRangeMultiplier : profile.viewAngle;
    }

    private bool IsPlayerCrouchingNow()
    {
        bool crouching = StateManager.Instance != null &&
            StateManager.Instance.CurrentMovementState == StateManager.MovementState.Crouch;

        if (logCrouchDetectionChange && (!crouchStateInitialized || crouching != cachedCrouchState))
        {
            crouchStateInitialized = true;
            cachedCrouchState = crouching;

            float reducedRadius = profile.viewRadius * profile.crouchRangeMultiplier;
            float reducedAngle = profile.viewAngle * profile.crouchRangeMultiplier;

            if (crouching)
            {
                Debug.Log($"[CreatureVision] {gameObject.name}: ผู้เล่นย่อ -> " +
                          $"View Radius {profile.viewRadius:F1} -> {reducedRadius:F1} | " +
                          $"View Angle {profile.viewAngle:F1}° -> {reducedAngle:F1}°");
            }
            else
            {
                Debug.Log($"[CreatureVision] {gameObject.name}: ผู้เล่นลุกยืน -> " +
                          $"View Radius {reducedRadius:F1} -> {profile.viewRadius:F1} | " +
                          $"View Angle {reducedAngle:F1}° -> {profile.viewAngle:F1}°");
            }
        }

        return crouching;
    }

    // ==================== Debug Gizmos ====================
    private void OnDrawGizmosSelected()
    {
        if (profile == null) return;

        GetEffectiveVisionParams(out float effectiveViewRadius, out float effectiveViewAngle);

        if (showFullConeComparison && !Mathf.Approximately(effectiveViewRadius, profile.viewRadius))
        {
            DrawSolidVisionCone(profile.viewAngle, profile.viewRadius, new Color(1f, 1f, 0f, 0.08f));
        }

        DrawSolidVisionCone(effectiveViewAngle, effectiveViewRadius, new Color(1f, 1f, 0f, 0.28f));

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        DrawCircle(transform.position, profile.awarenessRadius);

        if (lastLOSChecked)
        {
            Gizmos.color = lastLOSBlocked ? Color.red : Color.green;
            Gizmos.DrawLine(lastEyePosition, lastTargetPosition);
            Gizmos.DrawWireSphere(lastTargetPosition, 0.15f);
        }
    }

    private void DrawSolidVisionCone(float angle, float radius, Color fillColor)
    {
#if UNITY_EDITOR
        Vector3 bottomCenter = transform.position;
        Vector3 topCenter = transform.position + Vector3.up * debugConeHeight;
        Vector3 startDir = Quaternion.AngleAxis(-angle / 2f, Vector3.up) * transform.forward;

        Handles.color = fillColor;
        Handles.DrawSolidArc(bottomCenter, Vector3.up, startDir, angle, radius);
        Handles.DrawSolidArc(topCenter, Vector3.up, startDir, angle, radius);

        Handles.color = new Color(fillColor.r, fillColor.g, fillColor.b, 1f);
        Vector3 leftDir = Quaternion.AngleAxis(-angle / 2f, Vector3.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(angle / 2f, Vector3.up) * transform.forward;
        Vector3 midDir = transform.forward;

        Handles.DrawLine(bottomCenter + leftDir * radius, topCenter + leftDir * radius);
        Handles.DrawLine(bottomCenter + rightDir * radius, topCenter + rightDir * radius);
        Handles.DrawLine(bottomCenter + midDir * radius, topCenter + midDir * radius);

        Handles.DrawWireArc(bottomCenter, Vector3.up, startDir, angle, radius);
        Handles.DrawWireArc(topCenter, Vector3.up, startDir, angle, radius);
#endif
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
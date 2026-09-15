using UnityEngine;

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
        bool crouching = StateManager.Instance != null && StateManager.Instance.IsCrouching;

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

        // โคนเต็ม (ไม่ลด) วาดจางๆ ไว้เทียบ — เห็นเฉพาะตอนผู้เล่นกำลังย่ออยู่เท่านั้น
        if (showFullConeComparison && !Mathf.Approximately(effectiveViewRadius, profile.viewRadius))
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            DrawVisionCone(profile.viewAngle, profile.viewRadius);
        }

        // โคนจริงตอนนี้ (ลดแล้วถ้าผู้เล่นย่ออยู่) วาดเป็นเส้นสว่างชัดเจน
        Gizmos.color = Color.yellow;
        DrawVisionCone(effectiveViewAngle, effectiveViewRadius);

        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        DrawCircle(transform.position, profile.awarenessRadius);

        // เส้น Line of Sight จริงที่ใช้เช็คล่าสุด — เขียว = เห็นผู้เล่น, แดง = โดนบัง
        if (lastLOSChecked)
        {
            Gizmos.color = lastLOSBlocked ? Color.red : Color.green;
            Gizmos.DrawLine(lastEyePosition, lastTargetPosition);
            Gizmos.DrawWireSphere(lastTargetPosition, 0.15f);
        }
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
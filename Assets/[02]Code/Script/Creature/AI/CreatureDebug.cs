using UnityEngine;

/// <summary>
/// เครื่องมือ Debug ของกวาง/สัตว์ — ติดที่ราก Prefab สัตว์ (ปิด Component นี้หรือเอาออกตอนปล่อยเกมจริง)
///   1) ป้ายลอยเหนือหัวใน Game view: State ปัจจุบัน, ระยะถึงผู้เล่น เทียบกับทุกวงของสายพันธุ์ (อยู่ในวงไหม), ตัวจับเวลาของ State นั้น
///   2) วงระยะบนพื้นใน Scene view (หรือ Game view ถ้าเปิด Gizmos): Awareness (แดง), ได้ยินชัตเตอร์ (ฟ้า), Sprint Panic (ส้ม), Startle ประชิดตัว (เหลือง), Safe Distance (เขียว)
///      และตอน Alert มีวงระยะอ้างอิง: ตอนเข้า Alert (ขาว), ระยะเข้าใกล้แล้วหนี (แดงเข้ม), ระยะถอยแล้วกลับ Stop (น้ำเงิน)
///   3) Log ทุกครั้งที่ State เปลี่ยน
/// โคนสายตายังวาดโดย CreatureVision (เลือกตัวสัตว์ใน Hierarchy ถึงจะเห็น)
/// </summary>
[RequireComponent(typeof(CreatureAI))]
public class CreatureDebug : MonoBehaviour
{
    [Header("Label (Game view)")]
    public bool showLabel = true;
    [Tooltip("ไม่แสดงป้ายถ้าสัตว์ไกลจากกล้องเกินระยะนี้")]
    public float labelMaxDistance = 80f;
    public float labelHeight = 2.4f;

    [Header("Range Rings (Gizmos)")]
    public bool showRanges = true;

    [Header("Log")]
    public bool logStateChanges = true;

    private CreatureAI ai;
    private CreatureVision vision;
    private GUIStyle labelStyle;

    private static readonly Color AwarenessColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color HearingColor = new Color(0.2f, 0.9f, 1f);
    private static readonly Color PanicColor = new Color(1f, 0.6f, 0.1f);
    private static readonly Color SafeColor = new Color(0.3f, 1f, 0.3f);

    private void Awake()
    {
        ai = GetComponent<CreatureAI>();
        vision = GetComponent<CreatureVision>();
    }

    private void OnEnable()
    {
        if (ai == null) ai = GetComponent<CreatureAI>();
        ai.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable() => ai.OnStateChanged -= HandleStateChanged;

    private void HandleStateChanged(CreatureAI.CreatureState oldState, CreatureAI.CreatureState newState)
    {
        if (!logStateChanges) return;
        Debug.Log($"[CreatureDebug] {name}: {oldState} -> {newState} | ห่างผู้เล่น {ai.DebugDistanceToPlayer:F1}m");
    }

    // ==================== ป้ายใน Game view ====================
    private void OnGUI()
    {
        if (!showLabel || ai == null || ai.profile == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 worldPos = transform.position + Vector3.up * labelHeight;
        if ((worldPos - cam.transform.position).sqrMagnitude > labelMaxDistance * labelMaxDistance) return;

        Vector3 screen = cam.WorldToScreenPoint(worldPos);
        if (screen.z <= 0f) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                richText = true,
                wordWrap = false
            };
            labelStyle.normal.textColor = Color.white;
        }

        GUIContent content = new GUIContent(BuildLabel());
        Vector2 size = labelStyle.CalcSize(content);
        Rect rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);
        GUI.Box(rect, content, labelStyle);
    }

    private string BuildLabel()
    {
        CreatureProfile p = ai.profile;
        float dist = ai.DebugDistanceToPlayer;
        float mult = vision != null ? vision.PerceptionMultiplier : 1f;

        string state = $"<b><color={StateColor(ai.CurrentState)}>{ai.CurrentState}</color></b>";
        string distLine = $"ห่างผู้เล่น <b>{dist:F1}m</b> | ผู้เล่นเคลื่อนที่ {ai.DebugPlayerSpeed:F1} m/s";

        bool seen = vision != null && ai.player != null && vision.IsPlayerInVisionCone(ai.player);
        string ranges =
            $"วงงง(Stop) {p.awarenessRadius * mult:F0}m {InOut(dist, p.awarenessRadius * mult)}  " +
            $"ประชิด(Startle) {p.startleRadius * mult:F1}m {InOut(dist, p.startleRadius * mult)}  " +
            $"วิ่งชน(Panic) {p.sprintPanicRadius * mult:F0}m {InOut(dist, p.sprintPanicRadius * mult)}  " +
            $"หูชัตเตอร์ {p.shutterHearingRadius * mult:F0}m {InOut(dist, p.shutterHearingRadius * mult)}\n" +
            $"สายตา {p.viewRadius * mult:F0}m  ในโคนสายตา: {(seen ? "<color=#ff6060>เห็นผู้เล่น</color>" : "ไม่เห็น")}";

        return $"{state}\n{distLine}\n{ranges}\n{ai.GetDebugDetail()}";
    }

    private static string InOut(float dist, float radius) =>
        dist >= 0f && dist <= radius ? "<color=#ff6060>[ใน]</color>" : "<color=#9a9a9a>[นอก]</color>";

    private static string StateColor(CreatureAI.CreatureState state)
    {
        switch (state)
        {
            case CreatureAI.CreatureState.Idle: return "#d0d0d0";
            case CreatureAI.CreatureState.Walking: return "#7fd1ff";
            case CreatureAI.CreatureState.Eating: return "#8cff8c";
            case CreatureAI.CreatureState.Stop: return "#ffe066";
            case CreatureAI.CreatureState.LookAround: return "#ffb347";
            case CreatureAI.CreatureState.Alert: return "#ff6b6b";
            case CreatureAI.CreatureState.Run: return "#ff3df2";
            default: return "#ffffff";
        }
    }

    // ==================== วงระยะ ====================
    private void OnDrawGizmos()
    {
        if (!showRanges) return;
        if (ai == null) ai = GetComponent<CreatureAI>();
        if (vision == null) vision = GetComponent<CreatureVision>();
        CreatureProfile p = ai != null ? ai.profile : null;
        if (p == null) return;

        float mult = vision != null ? vision.PerceptionMultiplier : 1f;
        Vector3 c = transform.position + Vector3.up * 0.1f;

        Gizmos.color = AwarenessColor; DrawCircle(c, p.awarenessRadius * mult);
        Gizmos.color = HearingColor; DrawCircle(c, p.shutterHearingRadius * mult);
        Gizmos.color = PanicColor; DrawCircle(c, p.sprintPanicRadius * mult);
        Gizmos.color = Color.yellow; DrawCircle(c, p.startleRadius * mult);
        Gizmos.color = SafeColor; DrawCircle(c, p.safeDistance);

        if (Application.isPlaying && ai.CurrentState == CreatureAI.CreatureState.Alert)
        {
            float entry = ai.DebugAlertEntryDistance;
            Gizmos.color = Color.white; DrawCircle(c, entry);
            Gizmos.color = new Color(0.8f, 0f, 0f); DrawCircle(c, Mathf.Max(0f, entry - p.alertApproachDistance));
            Gizmos.color = new Color(0.2f, 0.3f, 1f); DrawCircle(c, entry + p.alertRetreatDistance);
        }

        if (Application.isPlaying && ai.player != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(c, ai.player.position + Vector3.up * 0.1f);
        }
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        if (radius <= 0f) return;

        const int segments = 48;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.PI * 2f * i / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}

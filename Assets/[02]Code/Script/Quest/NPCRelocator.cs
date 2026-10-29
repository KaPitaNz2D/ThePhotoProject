using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ย้ายจุดยืนของ NPC ระหว่างที่ผู้เล่นเดินไปไกลๆ — เดินกลับมา NPC อาจอยู่จุดเดิมหรือจุดใหม่ ผู้เล่นไม่เห็นตอนย้าย
///
/// จุด: ลาก GameObject แม่ใส่ช่อง pointsRoot — "ลูกทุกตัว" คือ 1 จุด (ตำแหน่ง + ทิศหน้าที่ NPC จะหัน) เพิ่มจุดด้วยการ Duplicate ลูกตัวใดตัวหนึ่ง
/// แล้วลากไปวาง ปิด GameObject ของจุดไว้ชั่วคราว = ไม่ถูกสุ่ม (วางไว้ในซีน ไม่ใช่ลูกของ NPC ไม่งั้นจุดจะเคลื่อนตาม NPC)
///
/// ลำดับ:
///   1) เริ่มซีน NPC ไปอยู่ startPoint เสมอ (ไม่สุ่ม) — เล่นครั้งแรก NPC อยู่จุดที่กำหนดแน่นอน
///   2) ผู้เล่นเข้าใกล้ NPC (≤ rearmDistance) แล้วเดินออกไปไกล (≥ awayDistance) = ถึงรอบสุ่มหนึ่งครั้ง
///   3) สุ่มจากจุดที่ย้ายไปได้ (ไกลจากผู้เล่นพอ + อยู่นอกจอ) รวมกับ "จุดเดิม" ถ้า allowStay — สุ่มได้จุดเดิมคือไม่ย้าย
///   4) ย้ายเฉพาะตอน NPC (จุดปัจจุบัน) และจุดปลายทางอยู่นอกจอผู้เล่น รอจนผู้เล่นหันไปทางอื่น (ลองใหม่ทุก checkInterval)
/// วางบน GameObject เดียวกับ NPCInteractable (ราก NPC.prefab) — ผูก pointsRoot ที่ตัวในซีน
/// </summary>
[DisallowMultipleComponent]
public class NPCRelocator : MonoBehaviour
{
    [Header("จุดที่ NPC ไปอยู่ได้")]
    [Tooltip("GameObject แม่ — ลูกทุกตัว (ที่เปิดอยู่) คือ 1 จุด ตำแหน่ง/ทิศหน้าตามลูกตัวนั้น เพิ่มจุด = Duplicate ลูก แล้วลากไปวาง ปิดลูก = ไม่ใช้จุดนั้น")]
    public Transform pointsRoot;
    [Tooltip("จุดเพิ่มเติมที่ไม่ได้อยู่ใต้ pointsRoot (ไม่จำเป็นต้องใช้)")]
    public Transform[] extraPoints;
    [Tooltip("จุดที่ NPC ไปอยู่ตอนเริ่มซีน (ไม่สุ่ม) ว่างไว้ = จุดแรกของ pointsRoot")]
    public Transform startPoint;
    [Tooltip("ติ๊ก = สุ่มจุดตอนเริ่มซีนเลย (ปกติไม่ติ๊ก เพื่อให้ผู้เล่นเจอ NPC ที่จุดแรกเสมอ)")]
    public bool randomizeAtStart = false;

    [Header("เมื่อไหร่ถึงรอบสุ่ม")]
    [Tooltip("ผู้เล่นต้องอยู่ห่าง NPC อย่างน้อยเท่านี้ (เมตร) ถึงจะสุ่มย้ายที่")]
    public float awayDistance = 40f;
    [Tooltip("ผู้เล่นต้องเคยเข้ามาใกล้ NPC ในระยะนี้ (เมตร) ก่อน ถึงจะนับรอบใหม่ — ไม่เกิน awayDistance")]
    public float rearmDistance = 20f;
    [Tooltip("จุดปลายทางต้องอยู่ห่างผู้เล่นอย่างน้อยเท่านี้ (เมตร) ไม่งั้นจะเห็น NPC โผล่ใกล้ตัว")]
    public float minDistanceToDestination = 30f;
    [Tooltip("ตรวจเงื่อนไขทุกกี่วินาที")]
    public float checkInterval = 0.5f;

    [Header("การสุ่ม")]
    [Tooltip("ติ๊ก = จุดเดิมเป็นตัวเลือกด้วย (อาจสุ่มได้ที่เดิม ไม่ย้าย) ไม่ติ๊ก = ต้องย้ายไปจุดอื่นเสมอ (ถ้ามีจุดให้ไป)")]
    public bool allowStay = true;

    [Header("กันผู้เล่นเห็นตอนย้าย")]
    [Tooltip("กล้องที่ใช้ตรวจ ว่างไว้ = Camera.main")]
    public Camera viewCamera;
    [Tooltip("ถือว่า \"อยู่ในจอ\" ถ้าอยู่ในกรอบจอ + ขอบนอกเท่านี้ (สัดส่วนจอ) กันหันกล้องกลับมาทันที")]
    [Range(0f, 0.5f)] public float screenMargin = 0.2f;
    [Tooltip("ความสูงตัว NPC (เมตร) ใช้ตรวจว่าเท้า/หัวอยู่ในจอไหม")]
    public float npcHeight = 2f;

    [Header("อ้างอิง")]
    [Tooltip("Transform ของผู้เล่น ว่างไว้ = หาจาก Tag \"Player\"")]
    public Transform player;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logRelocations = false;

    /// <summary>ยิงทุกครั้งที่ NPC ย้ายไปจุดใหม่ (ส่งจุดที่ย้ายไป)</summary>
    public event System.Action<Transform> OnRelocated;

    /// <summary>จุดที่ NPC อยู่ตอนนี้</summary>
    public Transform CurrentPoint { get; private set; }

    private readonly List<Transform> points = new List<Transform>();
    private bool playerWasNear;
    private float nextCheck;
    private bool warnedNoPoints;

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        CollectPoints();
        if (points.Count == 0) return;

        Transform first = randomizeAtStart ? points[Random.Range(0, points.Count)]
            : (startPoint != null ? startPoint : points[0]);
        MoveTo(first, raiseEvent: false);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        if (player == null || CurrentPoint == null) return;

        float distance = HorizontalDistance(player.position, transform.position);
        if (distance <= rearmDistance) playerWasNear = true;
        if (!playerWasNear || distance < awayDistance) return;

        // NPC ยังอยู่ในจอผู้เล่น (ไกลแต่มองเห็น) = รอก่อน ลองใหม่รอบหน้า (ยังไม่นับว่าสุ่มแล้ว)
        if (IsOnScreen(transform.position)) return;

        List<Transform> candidates = EligibleDestinations();
        bool stayOption = allowStay;
        if (candidates.Count == 0 && !stayOption) return; // ไม่มีจุดไปได้ตอนนี้ (อยู่ใกล้ผู้เล่น/ในจอ) รอรอบหน้า

        int roll = Random.Range(0, candidates.Count + (stayOption ? 1 : 0));
        playerWasNear = false; // นับรอบนี้แล้ว ต้องเดินกลับมาใกล้ก่อนถึงจะสุ่มอีก

        if (roll >= candidates.Count)
        {
            if (logRelocations) Debug.Log($"[NPCRelocator] สุ่มได้จุดเดิม ({CurrentPoint.name}) ไม่ย้าย", this);
            return;
        }
        MoveTo(candidates[roll], raiseEvent: true);
    }

    // จุดที่ย้ายไปได้ตอนนี้: ไม่ใช่จุดปัจจุบัน, ไกลจากผู้เล่นพอ, อยู่นอกจอ
    private List<Transform> EligibleDestinations()
    {
        CollectPoints();
        var result = new List<Transform>();
        foreach (Transform p in points)
        {
            if (p == CurrentPoint) continue;
            if (HorizontalDistance(player.position, p.position) < minDistanceToDestination) continue;
            if (IsOnScreen(p.position)) continue;
            result.Add(p);
        }
        return result;
    }

    private void CollectPoints()
    {
        points.Clear();
        if (pointsRoot != null)
        {
            foreach (Transform child in pointsRoot)
            {
                if (child.gameObject.activeInHierarchy) points.Add(child);
            }
        }
        if (extraPoints != null)
        {
            foreach (Transform p in extraPoints)
            {
                if (p != null && p.gameObject.activeInHierarchy && !points.Contains(p)) points.Add(p);
            }
        }

        if (points.Count == 0 && !warnedNoPoints)
        {
            warnedNoPoints = true;
            Debug.LogWarning($"[NPCRelocator] {name} ไม่มีจุดให้ย้าย — ลากแม่ของจุดใส่ช่อง Points Root (ลูกที่เปิดอยู่ = จุด) NPC จะอยู่ที่เดิม", this);
        }
    }

    private void MoveTo(Transform point, bool raiseEvent)
    {
        transform.SetPositionAndRotation(point.position, point.rotation);
        CurrentPoint = point;
        if (logRelocations) Debug.Log($"[NPCRelocator] NPC ไปที่ {point.name} {point.position}", this);
        if (raiseEvent) OnRelocated?.Invoke(point);
    }

    // อยู่ในจอไหม (ตรวจทั้งเท้าและหัว มีขอบนอกจอ) — ด้านหลังกล้อง (z<=0) ถือว่าไม่เห็น ไม่ตรวจการบังของฉาก (ต้นไม้ไม่มี Collider)
    private bool IsOnScreen(Vector3 worldPosition)
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return false;

        return SampleOnScreen(cam, worldPosition + Vector3.up * 0.2f) || SampleOnScreen(cam, worldPosition + Vector3.up * npcHeight);
    }

    private bool SampleOnScreen(Camera cam, Vector3 world)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        float lo = -screenMargin, hi = 1f + screenMargin;
        return v.z > 0f && v.x > lo && v.x < hi && v.y > lo && v.y < hi;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        var all = new List<Transform>();
        if (pointsRoot != null) foreach (Transform c in pointsRoot) all.Add(c);
        if (extraPoints != null) foreach (Transform p in extraPoints) if (p != null) all.Add(p);

        foreach (Transform p in all)
        {
            bool active = p.gameObject.activeInHierarchy;
            bool isCurrent = Application.isPlaying && p == CurrentPoint;
            Gizmos.color = !active ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : (isCurrent ? Color.green : new Color(1f, 0.8f, 0.2f, 1f));
            Gizmos.DrawWireSphere(p.position + Vector3.up * npcHeight * 0.5f, 0.5f);
            Gizmos.DrawLine(p.position, p.position + Vector3.up * npcHeight);
            Gizmos.DrawLine(p.position + Vector3.up * npcHeight * 0.5f, p.position + Vector3.up * npcHeight * 0.5f + p.forward * 1.2f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p.position + Vector3.up * (npcHeight + 0.4f), p.name);
#endif
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, awayDistance);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, rearmDistance);
    }

#if UNITY_EDITOR
    /// <summary>คลิกขวาที่หัว Component > Add Spawn Point At Scene View — เพิ่มจุดใหม่ใต้ pointsRoot ตรงที่กล้อง Scene View เล็งอยู่ (ความสูงเหนือพื้นเท่าจุดแรก)</summary>
    [ContextMenu("Add Spawn Point At Scene View")]
    private void AddSpawnPointAtSceneView()
    {
        if (pointsRoot == null)
        {
            Debug.LogWarning("[NPCRelocator] ยังไม่มี Points Root — สร้าง GameObject แม่ในซีนแล้วลากใส่ก่อน", this);
            return;
        }

        var view = UnityEditor.SceneView.lastActiveSceneView;
        Vector3 target = view != null ? view.pivot : transform.position;

        // ความสูงของจุดเหนือพื้น: เอาจากจุดที่มีอยู่ (หรือตัว NPC เอง) เทียบกับพื้นตรงนั้น
        Transform reference = pointsRoot.childCount > 0 ? pointsRoot.GetChild(0) : transform;
        float offset = GroundHeight(reference.position, out float refGround) ? reference.position.y - refGround : 0f;
        if (GroundHeight(target, out float ground)) target.y = ground + offset;

        var go = new GameObject($"Point_{pointsRoot.childCount + 1:00}");
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Add NPC Spawn Point");
        go.transform.SetParent(pointsRoot, false);
        go.transform.SetPositionAndRotation(target, reference.rotation);
        UnityEditor.Selection.activeGameObject = go;
    }

    private static bool GroundHeight(Vector3 position, out float y)
    {
        Terrain t = Terrain.activeTerrain;
        if (t != null)
        {
            y = t.SampleHeight(position) + t.transform.position.y;
            return true;
        }
        y = 0f;
        return false;
    }
#endif
}

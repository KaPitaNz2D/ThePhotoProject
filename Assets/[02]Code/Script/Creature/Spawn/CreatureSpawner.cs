using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Spawn สัตว์บนโหนดของ AINodeNetwork และ Despawn เมื่อวิ่งหนีไปไกลแล้ว
//
// Spawn: ใช้ AISpawnZone ทุกอันที่มีในซีน — ผู้เล่นต้องยืนอยู่ในวงของโซนนั้น, โหนดต้องอยู่ในโซนเดียวกันที่มีสัตว์ใส่ไว้, ห่างผู้เล่นอย่างน้อย minSpawnDistance, และอยู่นอกสายตาผู้เล่น
//        ชนิดสัตว์ที่เกิดถูกสุ่มจากรายชื่อสัตว์ของโซนที่โหนดนั้นอยู่ (ตามน้ำหนัก)
// Despawn: ต้องเคยวิ่งหนี (เข้า state Run) มาแล้ว + อยู่ไกลเกิน despawnDistance ต่อเนื่องนาน despawnDelay วินาที + ไม่อยู่ในสายตาผู้เล่น
//          (ตัวเวลานับใหม่จากศูนย์ทันทีที่ผู้เล่นเข้าใกล้หรือมองเห็น)
// สัตว์ที่วางไว้ในซีนเองไม่ถูกนับและไม่ถูก Despawn — คุมเฉพาะตัวที่ Spawner นี้สร้างเท่านั้น
public class CreatureSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("ถ้าไม่ใส่จะหา AINodeNetwork ในซีนให้เอง")]
    [SerializeField] private AINodeNetwork nodeNetwork;

    [Tooltip("จำนวนสัตว์ที่ Spawner นี้สร้างและยังมีชีวิตอยู่ได้พร้อมกันสูงสุด")]
    [SerializeField] private int maxAlive = 3;

    [Tooltip("ลอง Spawn ทุกกี่วินาที (ถ้ายังไม่ครบ maxAlive)")]
    [SerializeField] private float spawnInterval = 10f;

    [Tooltip("ห่างผู้เล่นอย่างน้อยกี่หน่วยถึงจะ Spawn ได้ (กันเกิดชิดตัวผู้เล่นตอนอยู่หลังกล้อง) — ผู้เล่นต้องอยู่ในโซนด้วย ดังนั้นค่านี้ควรน้อยกว่ารัศมีโซนพอสมควร ไม่งั้นจะไม่มีโหนดไหนไกลพอเหลือให้เกิด")]
    [SerializeField] private float minSpawnDistance = 40f;

    [Tooltip("สุ่มโหนดหาจุด Spawn ที่ผ่านเงื่อนไขกี่ครั้งต่อรอบ")]
    [SerializeField] private int spawnAttempts = 20;

    [Header("Despawn")]
    [Tooltip("ไกลจากผู้เล่นเกินระยะนี้ถึงเริ่มนับเวลา Despawn — ควรไม่มากกว่า Safe Distance ใน CreatureProfile ไม่งั้นสัตว์หยุดวิ่งหนีก่อนถึงระยะนี้")]
    [SerializeField] private float despawnDistance = 30f;

    [Tooltip("ต้องอยู่ไกลเกินระยะข้างบนและพ้นสายตาต่อเนื่องกี่วินาทีถึงจะ Despawn")]
    [SerializeField] private float despawnDelay = 15f;

    [Header("Player Visibility")]
    [Tooltip("ไกลจากกล้องเกินระยะนี้ถือว่าผู้เล่นมองไม่เห็นแล้ว (ต่อให้อยู่หน้ากล้องบนที่โล่ง) ไม่งั้นแทบทุกจุดบนแมพโล่งจะนับว่า \"เห็น\" ทำให้ Spawn/Despawn ไม่เกิด")]
    [SerializeField] private float maxVisibleDistance = 100f;

    [Tooltip("ขอบเผื่อของจอ (สัดส่วน viewport) กันสัตว์ที่ครึ่งตัวโผล่ที่ขอบจอถูกนับว่าไม่เห็น")]
    [SerializeField] private float viewportMargin = 0.1f;

    [Tooltip("ยกจุดเช็คการมองเห็นขึ้นจากพื้นเท่าไหร่ (ประมาณกลางลำตัวสัตว์)")]
    [SerializeField] private float visibilityHeightOffset = 1.2f;

    [Tooltip("Layer ที่บังสายตา (เนิน/ภูเขา) — ต้นไม้ในซีนไม่มี Collider เลยไม่นับว่าบัง")]
    [SerializeField] private LayerMask occlusionMask = 1 << 6;

    /// <summary>ยิงทุกครั้งที่ Spawn สำเร็จ (ตัวสัตว์, ตำแหน่ง) — ให้ระบบเสียงมา Subscribe เล่นเสียงเตือนผู้เล่น</summary>
    public event Action<GameObject, Vector3> OnCreatureSpawned;

    private class TrackedCreature
    {
        public GameObject gameObject;
        public bool hasFled;
        public float farTime;
    }

    private const float DespawnCheckInterval = 0.5f;

    private readonly List<TrackedCreature> tracked = new List<TrackedCreature>();
    private readonly List<(Vector3 node, AISpawnZone zone)> eligibleNodes = new List<(Vector3, AISpawnZone)>();
    private AISpawnZone[] zones;
    private Transform player;
    private float spawnTimer;
    private float despawnCheckTimer;

    private void Awake()
    {
        if (nodeNetwork == null) nodeNetwork = FindFirstObjectByType<AINodeNetwork>();
        zones = FindObjectsByType<AISpawnZone>(FindObjectsSortMode.None);

        // ตั้งให้ครบรอบทันทีเฟรมแรก จะได้ Spawn ตัวแรกเลยไม่ต้องรอ spawnInterval
        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;
            player = playerObj.transform;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            if (AliveCount() < maxAlive) TrySpawn();
        }

        despawnCheckTimer += Time.deltaTime;
        if (despawnCheckTimer >= DespawnCheckInterval)
        {
            EvaluateDespawn(despawnCheckTimer);
            despawnCheckTimer = 0f;
        }
    }

    // ==================== Spawn ====================
    private int AliveCount()
    {
        tracked.RemoveAll(t => t.gameObject == null);
        return tracked.Count;
    }

    private bool TrySpawn()
    {
        if (nodeNetwork == null || nodeNetwork.NodeCount == 0) return false;
        if (!TryFindSpawnPoint(out Vector3 point, out AISpawnZone zone)) return false;
        if (!zone.TryPickCreature(out GameObject creaturePrefab)) return false;

        // NavMeshAgent ยกตัวโมเดลขึ้นจากพื้น NavMesh ตาม baseOffset — วางให้ตรงตั้งแต่แรกกันกระตุกตอนเริ่ม
        NavMeshAgent prefabAgent = creaturePrefab.GetComponent<NavMeshAgent>();
        float lift = prefabAgent != null ? prefabAgent.baseOffset : 0f;

        GameObject creature = Instantiate(creaturePrefab, point + Vector3.up * lift,
            Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), transform);

        TrackedCreature entry = new TrackedCreature { gameObject = creature };
        tracked.Add(entry);

        CreatureAI ai = creature.GetComponent<CreatureAI>();
        if (ai != null)
        {
            ai.OnStateChanged += (from, to) =>
            {
                if (to == CreatureAI.CreatureState.Run) entry.hasFled = true;
            };
        }

        NotifySpawn(creature, point);
        return true;
    }

    private bool TryFindSpawnPoint(out Vector3 point, out AISpawnZone zone)
    {
        // คัดโหนดที่อยู่ในโซนที่มีสัตว์และห่างผู้เล่นพอก่อน แล้วค่อยสุ่มเช็คสายตา — กันเสียรอบสุ่มไปกับโหนดนอกโซน
        eligibleNodes.Clear();
        foreach (Vector3 node in nodeNetwork.Nodes)
        {
            if (Vector3.Distance(node, player.position) < minSpawnDistance) continue;

            AISpawnZone owner = FindZoneContaining(node);
            if (owner != null) eligibleNodes.Add((node, owner));
        }

        for (int i = 0; i < spawnAttempts && eligibleNodes.Count > 0; i++)
        {
            (Vector3 node, AISpawnZone owner) candidate = eligibleNodes[UnityEngine.Random.Range(0, eligibleNodes.Count)];
            if (IsVisibleToPlayer(candidate.node + Vector3.up * visibilityHeightOffset)) continue;

            point = candidate.node;
            zone = candidate.owner;
            return true;
        }

        point = default;
        zone = null;
        return false;
    }

    // โซนจะใช้ Spawn ได้ต่อเมื่อผู้เล่นยืนอยู่ในวงของโซนนั้นเอง — ผู้เล่นอยู่นอกโซน สัตว์ในโซนนั้นไม่เกิด
    // โซนที่ซ้อนทับกัน: ใช้โซนแรกที่เจอ
    private AISpawnZone FindZoneContaining(Vector3 point)
    {
        foreach (AISpawnZone zone in zones)
        {
            if (zone != null && zone.HasCreatures && zone.Contains(player.position) && zone.Contains(point)) return zone;
        }
        return null;
    }

    // ตอนนี้เตือนผ่าน debug log + event — ต่อเสียงจริงทีหลังโดย Subscribe OnCreatureSpawned
    private void NotifySpawn(GameObject creature, Vector3 point)
    {
        float distance = Vector3.Distance(point, player.position);
        Debug.Log($"[CreatureSpawner] (เสียงเตือน) {creature.name} เกิดใหม่ที่ {point} ห่างผู้เล่น {distance:F0} หน่วย");
        OnCreatureSpawned?.Invoke(creature, point);
    }

    // ==================== Despawn ====================
    private void EvaluateDespawn(float elapsed)
    {
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            TrackedCreature entry = tracked[i];
            if (entry.gameObject == null)
            {
                tracked.RemoveAt(i);
                continue;
            }

            if (!entry.hasFled) continue;

            Vector3 position = entry.gameObject.transform.position;
            bool farEnough = Vector3.Distance(position, player.position) > despawnDistance;

            if (farEnough && !IsVisibleToPlayer(position))
            {
                entry.farTime += elapsed;
            }
            else
            {
                entry.farTime = 0f;
            }

            if (entry.farTime >= despawnDelay)
            {
                Debug.Log($"[CreatureSpawner] Despawn {entry.gameObject.name} (วิ่งหนีไกลเกิน {despawnDistance} หน่วย นาน {despawnDelay} วินาที และพ้นสายตาผู้เล่น)");
                Destroy(entry.gameObject);
                tracked.RemoveAt(i);
            }
        }
    }

    // ==================== การมองเห็น ====================
    // เห็น = อยู่ในระยะ maxVisibleDistance + อยู่ใน frustum ของกล้องหลัก (รวมขอบเผื่อ) + ไม่มีเนิน/ภูเขาบังระหว่างกล้องกับจุดนั้น
    private bool IsVisibleToPlayer(Vector3 point)
    {
        Camera cam = Camera.main;
        if (cam == null) return true;

        if ((point - cam.transform.position).sqrMagnitude > maxVisibleDistance * maxVisibleDistance) return false;

        Vector3 viewport = cam.WorldToViewportPoint(point);
        bool inFrustum = viewport.z > 0f
            && viewport.x > -viewportMargin && viewport.x < 1f + viewportMargin
            && viewport.y > -viewportMargin && viewport.y < 1f + viewportMargin;
        if (!inFrustum) return false;

        bool blocked = Physics.Linecast(cam.transform.position, point, occlusionMask, QueryTriggerInteraction.Ignore);
        return !blocked;
    }
}

using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// สมองของ AI สัตว์ — เปลี่ยนจาก FSM ธรรมดามาเป็น Behavior Tree (BTSelector/BTSequence) ตามแผนผังที่ออกแบบไว้
///
/// โครงสร้าง Tree:
///   Root (Selector: ลองกิ่ง Engage ก่อน ถ้าไม่เข้าเงื่อนไขค่อยตกไป Normal)
///   ├── Engage
///   │    ├── Awareness Radius ตรงตัว -> Run ทันที (ไม่ผ่าน Alert)
///   │    ├── กำลัง Run อยู่แล้ว -> ทำต่อจนกว่าจะปลอดภัย
///   │    └── เห็นในโคนสายตา -> เข้า Alert สะสมเวลา ครบแล้วค่อย Run
///   └── Normal
///        ├── Idle -> Walking (ครบเวลา)
///        ├── Walking -> Idle (ถึงจุดหมาย)
///        └── (fallback) เริ่มต้นที่ Idle
///
/// ค่าปรับแต่งทั้งหมดดึงจาก CreatureProfile ไม่มี Field ของตัวเองอีกต่อไป
/// Time Cycle (Normal/Engage แยกตามช่วงเวลา) ยังไม่ทำตามที่ตกลงกันไว้ — Root ยังไม่มี Gate นี้
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CreatureVision))]
public class CreatureAI : MonoBehaviour
{
    public enum CreatureState { Idle, Walking, Alert, Run }

    [Header("Data")]
    [Tooltip("Asset ที่กำหนดค่าปรับแต่งทั้งหมดของสายพันธุ์นี้ (ตัวเดียวกับที่ผูกไว้ใน CreatureVision)")]
    public CreatureProfile profile;

    [Header("References")]
    [Tooltip("Transform ของผู้เล่น ถ้าไม่ใส่ไว้จะหาจาก GameObject ที่ติด Tag \"Player\" ให้เอง")]
    public Transform player;

    public CreatureState CurrentState { get; private set; } = CreatureState.Idle;

    /// <summary>ยิงทุกครั้งที่ State เปลี่ยน (old, new) — สคริปต์ Animator มา Subscribe ตรงนี้ได้เลย</summary>
    public event Action<CreatureState, CreatureState> OnStateChanged;

    private NavMeshAgent agent;
    private CreatureVision vision;
    private Vector3 spawnOrigin;
    private float stateTimer;
    private float fleeTimer;
    private float visionTimer;
    private BTNode root;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        vision = GetComponent<CreatureVision>();
        spawnOrigin = transform.position;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (profile == null)
        {
            Debug.LogError($"[CreatureAI] {gameObject.name} ไม่ได้ผูก CreatureProfile ไว้! " +
                            "ลาก Asset ใส่ช่อง Profile ก่อน ไม่งั้น AI จะไม่ขยับเลย");
        }

        root = BuildTree();
    }

    private void Start()
    {
        EnterIdle();
    }

    private void Update()
    {
        if (player == null || profile == null) return;
        root.Tick();
    }

    // ==================== สร้างโครงสร้าง Behavior Tree ====================
    private BTNode BuildTree()
    {
        BTNode engageBranch = new BTSelector(
            // 1) ผู้เล่นย่องมาใกล้เกินไป -> ตกใจ Trigger ทันที ข้าม Alert ไปเลย
            new BTSequence(
                new BTCondition(() => vision.IsPlayerInAwarenessRadius(player)),
                new BTAction(() => { EnterRun(); return BTStatus.Success; })
            ),
            // 2) กำลังวิ่งหนีอยู่แล้ว -> ทำต่อเนื่องจนกว่าจะปลอดภัย ไม่ให้สลับกลับ Normal ระหว่างวิ่ง
            new BTSequence(
                new BTCondition(() => CurrentState == CreatureState.Run),
                new BTAction(() => { UpdateRun(); return BTStatus.Success; })
            ),
            // 3) เห็นในโคนสายตา -> เข้า/สะสมเวลาใน Alert ครบกำหนดค่อย Run
            new BTSequence(
                new BTCondition(() => vision.IsPlayerInVisionCone(player)),
                new BTAction(() =>
                {
                    if (CurrentState != CreatureState.Alert) EnterAlert();

                    visionTimer += Time.deltaTime;
                    bool playerCrouching = StateManager.Instance != null && StateManager.Instance.IsCrouching;
                    float requiredTime = playerCrouching
                        ? profile.visionDetectionTime * profile.crouchDetectionTimeMultiplier
                        : profile.visionDetectionTime;

                    if (visionTimer >= requiredTime)
                    {
                        EnterRun();
                    }
                    return BTStatus.Success;
                })
            )
        );

        BTNode normalBranch = new BTSelector(
            new BTSequence(
                new BTCondition(() => CurrentState == CreatureState.Idle),
                new BTAction(() => { UpdateIdle(); return BTStatus.Success; })
            ),
            new BTSequence(
                new BTCondition(() => CurrentState == CreatureState.Walking),
                new BTAction(() => { UpdateWalking(); return BTStatus.Success; })
            ),
            // Fallback: หลุดจาก Alert มาโดยไม่ทันเข้า Run (ผู้เล่นหลบออกจากสายตาทัน) -> กลับ Idle
            new BTAction(() => { EnterIdle(); return BTStatus.Success; })
        );

        return new BTSelector(engageBranch, normalBranch);
    }

    // ==================== Idle ====================
    private void EnterIdle()
    {
        ChangeState(CreatureState.Idle);
        agent.isStopped = true;
        visionTimer = 0f; // เผื่อเพิ่งหลุดจาก Alert มา ต้องรีเซ็ตตัวจับเวลาด้วย
        stateTimer = UnityEngine.Random.Range(profile.idleMinDuration, profile.idleMaxDuration);
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            EnterWalking();
        }
    }

    // ==================== Walking (สุ่มเดินไปเดินมา) ====================
    private void EnterWalking()
    {
        ChangeState(CreatureState.Walking);
        agent.isStopped = false;
        agent.speed = profile.walkSpeed;
        agent.SetDestination(GetRandomPointInRadius(spawnOrigin, profile.wanderRadius));
    }

    private void UpdateWalking()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            EnterIdle();
        }
    }

    // ==================== Alert (ระแวง — หยุดนิ่งมองก่อนตัดสินใจวิ่ง) ====================
    private void EnterAlert()
    {
        ChangeState(CreatureState.Alert);
        agent.isStopped = true; // หยุดเดิน/หยุดกิน หันมามองทาง (ท่าทางจริงให้ Animator จัดการต่อผ่าน OnStateChanged)
    }

    // ==================== Run (วิ่งหนีผู้เล่น) ====================
    private void EnterRun()
    {
        ChangeState(CreatureState.Run);
        agent.isStopped = false;
        agent.speed = profile.runSpeed;
        fleeTimer = 0f;
        visionTimer = 0f;
        UpdateFleeDestination();
    }

    private void UpdateRun()
    {
        fleeTimer -= Time.deltaTime;
        if (fleeTimer <= 0f)
        {
            UpdateFleeDestination();
            fleeTimer = profile.fleeRecalculateInterval;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer >= profile.safeDistance)
        {
            EnterIdle();
        }
    }

    private void UpdateFleeDestination()
    {
        Vector3 directionAway = (transform.position - player.position).normalized;
        Vector3 fleeTarget = transform.position + directionAway * profile.fleeDistance;

        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, profile.fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // ==================== Helper ====================
    private Vector3 GetRandomPointInRadius(Vector3 center, float radius)
    {
        Vector3 randomPoint = center + UnityEngine.Random.insideUnitSphere * radius;

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return center;
    }

    private void ChangeState(CreatureState newState)
    {
        if (CurrentState == newState) return;
        CreatureState oldState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }
}
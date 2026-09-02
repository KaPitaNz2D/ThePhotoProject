using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State Machine หลักของ AI สัตว์ — เดินสุ่ม (Walking) / ยืนนิ่ง (Idle) / วิ่งหนีผู้เล่น (Run)
/// ถาม CreatureVision ว่าเจอผู้เล่นไหม แล้วตัดสินใจเปลี่ยน State เอง
///
/// ยังไม่ทำ: สุ่มเกิด, ขอบเขตพื้นที่ (Area), การหายไปตอนวิ่งหนีไกลเกินระยะ — ตามที่ตกลงกันไว้ว่าเก็บไว้ทีหลัง
/// ยังไม่ผูก Animator — แต่มี Event OnStateChanged เตรียมไว้ให้สคริปต์ Animator มา Subscribe ต่อได้เลย
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CreatureVision))]
public class CreatureAI : MonoBehaviour
{
    public enum CreatureState { Idle, Walking, Run }

    [Header("References")]
    [Tooltip("Transform ของผู้เล่น ถ้าไม่ใส่ไว้จะหาจาก GameObject ที่ติด Tag \"Player\" ให้เอง")]
    public Transform player;

    [Header("Wander Settings (Idle <-> Walking)")]
    [Tooltip("รัศมีที่สุ่มเดินได้ วัดจากจุดเกิดตอนเริ่มเกม")]
    public float wanderRadius = 10f;
    public float idleMinDuration = 2f;
    public float idleMaxDuration = 6f;
    public float walkSpeed = 1.5f;

    [Header("Run Settings")]
    public float runSpeed = 6f;
    [Tooltip("ระยะที่วิ่งหนีออกไปทุกครั้งที่คำนวณจุดหนีใหม่")]
    public float fleeDistance = 10f;
    [Tooltip("ระยะห่างจากผู้เล่นที่ถือว่า \"ปลอดภัยแล้ว\" กลับไป Idle ได้ (ควรตั้งมากกว่า View Radius ใน CreatureVision)")]
    public float safeDistance = 20f;
    [Tooltip("ความถี่ในการคำนวณจุดวิ่งหนีใหม่ระหว่างที่ยังวิ่งอยู่ (วินาที)")]
    public float fleeRecalculateInterval = 1f;

    [Header("Detection Timing (Vision Cone เท่านั้น)")]
    [Tooltip("ต้องเห็นผู้เล่นในโคนสายตาต่อเนื่องกี่วินาที ถึงจะเริ่มวิ่งหนี (แบบเกม REPO) " +
             "ไม่มีผลกับ Awareness Radius ซึ่งยังคง Trigger ทันทีเหมือนเดิม")]
    public float visionDetectionTime = 1f;

    /// <summary>ความคืบหน้าการจับเวลาเห็นผู้เล่น (0-1) เอาไปทำ UI แถบจับเวลา/ไอคอนตกใจได้</summary>
    public float DetectionProgress01 => visionDetectionTime > 0f ? Mathf.Clamp01(visionTimer / visionDetectionTime) : 0f;

    public CreatureState CurrentState { get; private set; } = CreatureState.Idle;

    /// <summary>ยิงทุกครั้งที่ State เปลี่ยน (old, new) — สคริปต์ Animator ในอนาคตมา Subscribe ตรงนี้ได้เลย</summary>
    public event Action<CreatureState, CreatureState> OnStateChanged;

    private NavMeshAgent agent;
    private CreatureVision vision;
    private Vector3 spawnOrigin;
    private float stateTimer;
    private float fleeTimer;
    private float visionTimer;

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
    }

    private void Start()
    {
        EnterIdle();
    }

    private void Update()
    {
        if (player == null) return;

        // เช็คการตรวจจับ (ยกเว้นตอนวิ่งหนีอยู่แล้ว ไม่ต้องเช็คซ้ำ)
        if (CurrentState != CreatureState.Run)
        {
            if (vision.IsPlayerInAwarenessRadius(player))
            {
                // ผู้เล่นย่องมาใกล้เกินไป -> ตกใจ Trigger ทันที ไม่ต้องจับเวลา
                EnterRun();
            }
            else if (vision.IsPlayerInVisionCone(player))
            {
                // เห็นในโคนสายตา -> เริ่ม/สะสมจับเวลา ถ้ายังเห็นต่อเนื่องครบ visionDetectionTime ค่อยวิ่งหนี
                visionTimer += Time.deltaTime;
                if (visionTimer >= visionDetectionTime)
                {
                    EnterRun();
                }
            }
            else
            {
                // หลุดจากโคนสายตาไปแล้ว รีเซ็ตตัวจับเวลา (ต้องเริ่มนับใหม่ตั้งแต่ 0 ถ้าเจอใหม่)
                visionTimer = 0f;
            }
        }

        switch (CurrentState)
        {
            case CreatureState.Idle:
                UpdateIdle();
                break;
            case CreatureState.Walking:
                UpdateWalking();
                break;
            case CreatureState.Run:
                UpdateRun();
                break;
        }
    }

    // ==================== Idle ====================
    private void EnterIdle()
    {
        ChangeState(CreatureState.Idle);
        agent.isStopped = true;
        stateTimer = UnityEngine.Random.Range(idleMinDuration, idleMaxDuration);
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
        agent.speed = walkSpeed;
        agent.SetDestination(GetRandomPointInRadius(spawnOrigin, wanderRadius));
    }

    private void UpdateWalking()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            EnterIdle();
        }
    }

    // ==================== Run (วิ่งหนีผู้เล่น) ====================
    private void EnterRun()
    {
        ChangeState(CreatureState.Run);
        agent.isStopped = false;
        agent.speed = runSpeed;
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
            fleeTimer = fleeRecalculateInterval;
        }

        // ห่างจากผู้เล่นพอแล้ว -> เลิกวิ่ง กลับไปยืนพัก
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer >= safeDistance)
        {
            EnterIdle();
        }
    }

    private void UpdateFleeDestination()
    {
        Vector3 directionAway = (transform.position - player.position).normalized;
        Vector3 fleeTarget = transform.position + directionAway * fleeDistance;

        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
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
        return center; // หาจุดบน NavMesh ไม่เจอ -> อยู่ที่จุดเกิดไปก่อน
    }

    private void ChangeState(CreatureState newState)
    {
        if (CurrentState == newState) return;
        CreatureState oldState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }
}
using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// สมองของ AI สัตว์ — Behavior Tree (BTSelector/BTSequence) ตามแผนผัง Animal: Normal / Confuse / Engage
///
/// โครงสร้าง Tree (ลองกิ่งบนก่อน กิ่งที่เข้าเงื่อนไขจะหยุดการเลือก):
///   Root (Selector)
///   ├── Run          กำลังวิ่งหนี -> ทำต่อจนปลอดภัย
///   ├── Panic        ผู้เล่น Sprint เข้ามาในระยะ sprintPanicRadius -> Run ทันทีจากทุก State (ทางลัด)
///   ├── Alert        เผชิญหน้า: ผู้เล่นยืนนิ่งครบเวลา / เข้าใกล้ / ทำอะไรดังๆ -> Run, ถอยห่างมาก -> Stop
///   ├── Confuse      Stop (นิ่งงง) -> Look around (หันหัวมองซ้าย-ขวา) -> Normal หรือ Alert
///   ├── Triggers     (เฉพาะตอนอยู่ Normal) เห็นผู้เล่นครบเวลา -> Alert | ได้ยินชัตเตอร์/ผู้เล่นเข้าวงรอบตัว -> Stop
///   └── Normal       Idle (ยืนเฉยๆ/หันมองข้างๆ) / Walking (เดินตามโหนด) / Eating (กินหญ้า ลดการรับรู้ลงครึ่ง)
///
/// ค่าปรับแต่งทั้งหมดดึงจาก CreatureProfile ไม่มี Field ของตัวเองอีกต่อไป
/// Time Cycle (Normal/Engage แยกตามช่วงเวลา) ยังไม่ทำ — Root ยังไม่มี Gate นี้
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CreatureVision))]
public class CreatureAI : MonoBehaviour
{
    // Idle/Walking/Alert/Run คงลำดับเดิม ส่วนใหม่ต่อท้าย (Animator แม็พเลขแยกใน CreatureAnimatorController)
    public enum CreatureState { Idle, Walking, Alert, Run, Eating, Stop, LookAround }

    [Header("Data")]
    [Tooltip("Asset ที่กำหนดค่าปรับแต่งทั้งหมดของสายพันธุ์นี้ (ตัวเดียวกับที่ผูกไว้ใน CreatureVision)")]
    public CreatureProfile profile;

    [Header("References")]
    [Tooltip("Transform ของผู้เล่น ถ้าไม่ใส่ไว้จะหาจาก GameObject ที่ติด Tag \"Player\" ให้เอง")]
    public Transform player;

    [Tooltip("เครือข่ายโหนดจุดเดิน ถ้าไม่ใส่ไว้จะหา AINodeNetwork ในซีนให้เอง ถ้าซีนไม่มีเลยจะกลับไปสุ่มจุดเดินแบบเดิม")]
    public AINodeNetwork nodeNetwork;

    public CreatureState CurrentState { get; private set; } = CreatureState.Idle;

    /// <summary>ยิงทุกครั้งที่ State เปลี่ยน (old, new) — สคริปต์ Animator มา Subscribe ตรงนี้ได้เลย</summary>
    public event Action<CreatureState, CreatureState> OnStateChanged;

    private NavMeshAgent agent;
    private CreatureVision vision;
    private CreatureHead head;
    private Rigidbody playerBody;
    private float stateTimer;
    private float fleeTimer;
    private float visionTimer;
    private float walkTimer;
    private BTNode root;
    private NavMeshPath nodePath;

    // เหตุการณ์/ตัวนับของกิ่ง Confuse + Alert
    private bool heardShutter;
    private float stopEntryDistance;
    private float spotTimer;
    private float alertEntryDistance;
    private float alertStillTimer;
    private bool lookTurning;
    private Quaternion lookTurnTarget;
    private float lookTurnRollTimer;

    // ความเร็วผู้เล่น (ใช้ตัดสินว่า "ไม่ขยับ")
    private float playerSpeed;
    private Vector3 lastPlayerPos;

    // โหนดที่ใกล้ตัวกว่านี้ไม่นับ กันสุ่มได้โหนดที่ยืนอยู่แล้วจนเดินถึงทันที (Idle ↔ Walking สลับถี่เกิน)
    private const float MinNodeTravelDistance = 3f;
    private const int NodePickAttempts = 8;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        vision = GetComponent<CreatureVision>();
        head = GetComponent<CreatureHead>();
        nodePath = new NavMeshPath();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        if (player != null)
        {
            playerBody = player.GetComponentInParent<Rigidbody>();
            lastPlayerPos = player.position;
        }

        if (nodeNetwork == null)
        {
            nodeNetwork = FindFirstObjectByType<AINodeNetwork>();
        }

        if (profile == null)
        {
            Debug.LogError($"[CreatureAI] {gameObject.name} ไม่ได้ผูก CreatureProfile ไว้! " +
                            "ลาก Asset ใส่ช่อง Profile ก่อน ไม่งั้น AI จะไม่ขยับเลย");
        }
        else if (head != null)
        {
            head.turnSpeed = profile.headTurnSpeed;
        }

        root = BuildTree();
    }

    private void OnEnable() => CreatureNoise.OnShutter += OnShutterHeard;
    private void OnDisable() => CreatureNoise.OnShutter -= OnShutterHeard;

    private void Start()
    {
        EnterIdle();
    }

    private void Update()
    {
        if (player == null || profile == null) return;

        UpdatePlayerSpeed();

        root.Tick();

        heardShutter = false; // เหตุการณ์เสียงมีผลแค่เฟรมที่ได้ยิน
    }

    // ==================== สร้างโครงสร้าง Behavior Tree ====================
    private BTNode BuildTree()
    {
        BTNode runBranch = new BTSequence(
            new BTCondition(() => CurrentState == CreatureState.Run),
            new BTAction(() => { UpdateRun(); return BTStatus.Success; })
        );

        // ผู้เล่นประชิดตัว หรือ Sprint เข้ามาใกล้ -> วิ่งหนีทันทีจากทุก State กันกรณีเข้าหาจากด้านหลัง (ไม่อยู่ในโคนสายตา ลำดับ Stop -> Alert จะไม่ทัน
        // และกวางที่งงอยู่ใน Stop/Look around จะไม่มีทางเห็นคนที่ยืนอยู่ด้านหลังเลย)
        BTNode panicBranch = new BTSequence(
            new BTCondition(IsPanicTriggered),
            new BTAction(() => { EnterRun(); return BTStatus.Success; })
        );

        BTNode alertBranch = new BTSequence(
            new BTCondition(() => CurrentState == CreatureState.Alert),
            new BTAction(() => { UpdateAlert(); return BTStatus.Success; })
        );

        BTNode confuseBranch = new BTSelector(
            new BTSequence(
                new BTCondition(() => CurrentState == CreatureState.Stop),
                new BTAction(() => { UpdateStop(); return BTStatus.Success; })
            ),
            new BTSequence(
                new BTCondition(() => CurrentState == CreatureState.LookAround),
                new BTAction(() => { UpdateLookAround(); return BTStatus.Success; })
            )
        );

        // ตัวกระตุ้นของ Normal: คืน Failure ถ้ายังไม่เข้าเงื่อนไข เพื่อให้ Selector ตกไปทำ Normal ต่อ
        BTNode triggers = new BTSelector(
            new BTAction(CheckSeenLongEnough),
            new BTAction(CheckConfuseTriggers)
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
            new BTSequence(
                new BTCondition(() => CurrentState == CreatureState.Eating),
                new BTAction(() => { UpdateEating(); return BTStatus.Success; })
            ),
            // Fallback: State ที่ไม่มีใครดูแล -> กลับ Idle
            new BTAction(() => { EnterIdle(); return BTStatus.Success; })
        );

        return new BTSelector(runBranch, panicBranch, alertBranch, confuseBranch, triggers, normalBranch);
    }

    // ==================== ตัวกระตุ้นของ Normal ====================
    // เห็นผู้เล่นในโคนสายตาต่อเนื่องครบเวลา (ย่อ = นานขึ้น) -> Alert
    private BTStatus CheckSeenLongEnough()
    {
        if (!vision.IsPlayerInVisionCone(player))
        {
            visionTimer = Mathf.Max(0f, visionTimer - Time.deltaTime);
            return BTStatus.Failure;
        }

        visionTimer += Time.deltaTime;
        bool playerCrouching = StateManager.Instance != null && StateManager.Instance.IsCrouching;
        float requiredTime = playerCrouching
            ? profile.visionDetectionTime * profile.crouchDetectionTimeMultiplier
            : profile.visionDetectionTime;

        if (visionTimer < requiredTime) return BTStatus.Failure;

        EnterAlert();
        return BTStatus.Success;
    }

    // ได้ยินเสียงชัตเตอร์ หรือผู้เล่นเดินเข้าวงรอบตัว (Awareness Radius) -> Stop
    private BTStatus CheckConfuseTriggers()
    {
        bool awarenessTriggered = vision.IsPlayerInAwarenessRadius(player);
        if (!heardShutter && !awarenessTriggered) return BTStatus.Failure;

        EnterStop();
        return BTStatus.Success;
    }

    // ผู้เล่นประชิดตัว (Startle ไม่ว่าทำอะไรอยู่) หรือ Sprint เข้ามาในระยะ Panic -> หนีทันที
    private bool IsPanicTriggered()
    {
        float distance = DistanceToPlayer();
        float mult = vision.PerceptionMultiplier;

        if (profile.startleRadius > 0f && distance <= profile.startleRadius * mult) return true;
        return IsPlayerSprinting() && distance <= profile.sprintPanicRadius * mult;
    }

    // ==================== Idle ====================
    private void EnterIdle()
    {
        ChangeState(CreatureState.Idle);
        agent.isStopped = true;
        visionTimer = 0f; // เผื่อเพิ่งหลุดจาก Alert มา ต้องรีเซ็ตตัวจับเวลาด้วย
        stateTimer = UnityEngine.Random.Range(profile.idleMinDuration, profile.idleMaxDuration);

        if (head != null)
        {
            head.ResetLook();
            if (UnityEngine.Random.value < profile.idleLookChance) StartIdleLook();
        }
    }

    // หันหัวมองข้างๆ: ซ้ายอย่างเดียว / ขวาอย่างเดียว / ทั้งสองข้าง (สุ่ม)
    private void StartIdleLook()
    {
        float a = profile.idleLookAngle;
        float[] yaws;
        switch (UnityEngine.Random.Range(0, 3))
        {
            case 0: yaws = new[] { -a }; break;
            case 1: yaws = new[] { a }; break;
            default:
                yaws = UnityEngine.Random.value < 0.5f ? new[] { -a, a } : new[] { a, -a };
                break;
        }
        head.PlaySequence(yaws, profile.lookHoldTime, false);
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        // หมดเวลายืน -> ลองไปกินหญ้า (ต้องยืนบนพื้นหญ้า) ไม่งั้นเดินต่อ
        if (UnityEngine.Random.value < profile.eatChance &&
            GrazeSurface.IsGrass(transform.position, profile.grazeSurfaceKeyword, profile.grazeMinCoverage))
        {
            EnterEating();
        }
        else
        {
            EnterWalking();
        }
    }

    // ==================== Walking (เดินตามโหนด) ====================
    private void EnterWalking()
    {
        ChangeState(CreatureState.Walking);
        if (head != null) head.ResetLook();
        agent.isStopped = false;
        agent.speed = profile.walkSpeed;

        if (!TrySetNodeDestination())
        {
            agent.SetDestination(GetRandomPointInRadius(transform.position, profile.wanderRadius));
        }
        walkTimer = 0f;
    }

    // เลือกโหนดในรัศมี wanderRadius รอบตำแหน่งปัจจุบัน (ไม่ยึดจุดเกิด กวางที่หนีไปไกลจะไม่เดินกลับบ้านเดิม) แล้วเช็คว่ามี path ถึงจริง (PathComplete) ก่อนเดิน
    // อนาคตถ้าต้องกันสัตว์ไม่ให้เข้าบางโซน ให้กรองโหนดตรงนี้
    // โหนดบางจุดอาจอยู่บนเกาะ NavMesh ที่เดินไปไม่ถึง (เช่นยอดเขาชัน/ล้อมด้วยน้ำ) ข้ามไปสุ่มใหม่ — การหลบสิ่งกีดขวางระหว่างทางยังเป็นหน้าที่ของ NavMeshAgent เหมือนเดิม
    private bool TrySetNodeDestination()
    {
        if (nodeNetwork == null || nodeNetwork.NodeCount == 0) return false;

        for (int i = 0; i < NodePickAttempts; i++)
        {
            if (!nodeNetwork.TryGetRandomNodeNear(transform.position, profile.wanderRadius, out Vector3 node)) return false;
            if ((node - transform.position).sqrMagnitude < MinNodeTravelDistance * MinNodeTravelDistance) continue;

            if (agent.CalculatePath(node, nodePath) && nodePath.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetDestination(node);
                return true;
            }
        }

        return false;
    }

    private void UpdateWalking()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            EnterIdle();
            return;
        }

        // กันเดินติดค้าง (เช่นไปเจอสิ่งกีดขวางที่ NavMesh ไม่ได้กันไว้ให้) - นานเกินไปก็เลิกพยายามแล้วกลับ Idle เอง
        walkTimer += Time.deltaTime;
        if (walkTimer >= profile.maxWalkDuration)
        {
            EnterIdle();
        }
    }

    // ==================== Eating (ก้มกินหญ้า — การรับรู้แคบลง) ====================
    private void EnterEating()
    {
        ChangeState(CreatureState.Eating); // ตัวคูณการรับรู้ถูกตั้งใน ChangeState
        agent.isStopped = true;
        if (head != null) head.ResetLook();
        stateTimer = UnityEngine.Random.Range(profile.eatMinDuration, profile.eatMaxDuration);
    }

    private void UpdateEating()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) EnterIdle();
    }

    // ==================== Stop (หยุดนิ่งงง) ====================
    private void EnterStop()
    {
        ChangeState(CreatureState.Stop);
        agent.isStopped = true; // หยุดกิจกรรมทั้งหมด (ไม่หันหาผู้เล่น — ยังไม่รู้ว่ามีอะไร)
        if (head != null) head.ResetLook();
        visionTimer = 0f;
        stateTimer = profile.stopDuration;
        stopEntryDistance = DistanceToPlayer();
    }

    private void UpdateStop()
    {
        // เห็นผู้เล่นตรงๆ -> Alert เลย
        if (vision.IsPlayerInVisionCone(player))
        {
            EnterAlert();
            return;
        }

        stateTimer -= Time.deltaTime;
        bool approached = stopEntryDistance - DistanceToPlayer() > profile.stopApproachDistance;
        if (stateTimer <= 0f || approached || IsPlayerPhotographing())
        {
            EnterLookAround();
        }
    }

    // ==================== Look around (หันหัวมองซ้าย-ขวา) ====================
    private void EnterLookAround()
    {
        ChangeState(CreatureState.LookAround);
        agent.isStopped = true;
        stateTimer = profile.lookAroundDuration;
        spotTimer = 0f;
        lookTurning = false;
        lookTurnRollTimer = 0.5f; // ให้หันหัวได้ครู่หนึ่งก่อนค่อยสุ่มหมุนตัวรอบแรก

        if (head != null)
        {
            float a = profile.lookAroundAngle * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
            head.PlaySequence(new[] { a, -a }, profile.lookHoldTime, true);
        }
    }

    private void UpdateLookAround()
    {
        if (heardShutter) stateTimer = profile.lookAroundDuration; // ยังมีเสียงอยู่ -> หาต่อ

        UpdateLookAroundBodyTurn();

        // เห็นผู้เล่นในโคนสายตา (ตามทิศที่หันหัว) สะสมครบเวลา -> Alert
        // สะสมรวมตลอด Look around ไม่รีเซ็ตเวลามองไม่เห็น เพราะหัวกวาดผ่านผู้เล่นทีละช่วงสั้นๆ ถ้าต้องเห็นต่อเนื่องจะแทบไม่เคยครบ
        if (vision.IsPlayerInVisionCone(player))
        {
            spotTimer += Time.deltaTime;
            if (spotTimer >= profile.lookAroundSpotTime)
            {
                EnterAlert();
                return;
            }
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            // ผู้เล่นยังอยู่ในวงงง -> ไม่กลับ Normal ไม่ว่ายืนนิ่งหรือเดินเข้ามา กวางค้างอยู่ในวงจร Stop -> Look around ไปเรื่อยๆ
            // จนกว่าผู้เล่นจะออกจากวง (หรือถูกจับได้ -> Alert) กันกรณียืนนิ่งรอจนกวางกลับไปเดินแล้วเดินเข้าไปประชิดตัวได้
            if (vision.IsPlayerInAwarenessRadius(player)) EnterStop();
            else EnterIdle();
        }
    }

    // นอกจากหันหัวซ้าย-ขวา ทุก lookAroundTurnInterval วินาทีสุ่มว่าจะหมุนตัวไปทิศอื่นไหม เพื่อให้มองรอบๆ ได้ถึงด้านหลัง
    // (หัวหันได้แค่ ±lookAroundAngle เลยมองไม่เห็นคนที่ยืนอยู่ด้านหลังตรงๆ)
    private void UpdateLookAroundBodyTurn()
    {
        if (lookTurning)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, lookTurnTarget, profile.lookAroundTurnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, lookTurnTarget) < 0.5f) lookTurning = false;
            return;
        }

        lookTurnRollTimer -= Time.deltaTime;
        if (lookTurnRollTimer > 0f) return;

        lookTurnRollTimer = profile.lookAroundTurnInterval;
        if (UnityEngine.Random.value >= profile.lookAroundTurnChance) return;

        float angle = UnityEngine.Random.Range(profile.lookAroundTurnMinAngle, profile.lookAroundTurnMaxAngle);
        if (UnityEngine.Random.value < 0.5f) angle = -angle;
        lookTurnTarget = Quaternion.AngleAxis(angle, Vector3.up) * transform.rotation;
        lookTurning = true;
    }

    // ==================== Alert (เผชิญหน้า — มองผู้เล่น) ====================
    private void EnterAlert()
    {
        ChangeState(CreatureState.Alert);
        agent.isStopped = true; // หยุดเดิน/หยุดกิน หันมาทางผู้เล่น (ท่าทางจริงให้ Animator จัดการต่อผ่าน OnStateChanged)
        if (head != null) head.ResetLook();
        visionTimer = 0f;
        alertStillTimer = 0f;
        alertEntryDistance = DistanceToPlayer();
    }

    private void UpdateAlert()
    {
        FaceTowards(player.position, profile.alertTurnSpeed);

        float distance = DistanceToPlayer();

        // ผู้เล่นทำอะไรดังๆ (เข้าโหมดกล้อง/ถ่ายรูป/วิ่ง) หรือเดินเข้าใกล้เกินกำหนด -> วิ่งหนีทันที
        if (IsPlayerPhotographing() || IsPlayerSprinting() || heardShutter ||
            alertEntryDistance - distance > profile.alertApproachDistance)
        {
            EnterRun();
            return;
        }

        // ถอยห่างจากระยะตอนเข้า Alert มากพอ -> กลับไปงง (Stop)
        if (distance - alertEntryDistance > profile.alertRetreatDistance)
        {
            EnterStop();
            return;
        }

        // ยืนนิ่งนับเวลา ถ้าขยับ/ถอยหลังอยู่ เวลาหยุดนับ (ไม่รีเซ็ต) ครบแล้วกวางหนีเอง
        if (playerSpeed < profile.playerStillSpeed)
        {
            alertStillTimer += Time.deltaTime;
            if (alertStillTimer >= profile.alertStillTime) EnterRun();
        }
    }

    // ==================== Run (วิ่งหนีผู้เล่น) ====================
    private void EnterRun()
    {
        ChangeState(CreatureState.Run);
        agent.isStopped = false;
        agent.speed = profile.runSpeed;
        if (head != null) head.ResetLook();
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

        if (DistanceToPlayer() >= profile.safeDistance)
        {
            EnterIdle();
        }
    }

    private void UpdateFleeDestination()
    {
        // สุ่มเบี่ยงมุมจากทิศตรงข้ามผู้เล่น กันวิ่งหนีเป็นเส้นตรงเป๊ะๆ ทุกครั้ง
        float randomAngle = UnityEngine.Random.Range(-profile.fleeAngleVariance, profile.fleeAngleVariance);
        Vector3 directionAway = Quaternion.Euler(0f, randomAngle, 0f) * (transform.position - player.position).normalized;
        Vector3 fleeTarget = transform.position + directionAway * profile.fleeDistance;

        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, profile.fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // ==================== ข้อมูลสำหรับ Debug (CreatureDebug อ่านไปแสดง) ====================
    public float DebugDistanceToPlayer => player != null ? DistanceToPlayer() : -1f;
    public float DebugPlayerSpeed => playerSpeed;
    public float DebugAlertEntryDistance => alertEntryDistance;

    /// <summary>ตัวจับเวลา/เงื่อนไขที่เกี่ยวกับ State ปัจจุบัน เป็นข้อความสั้นๆ ให้ดูว่ากำลังนับอะไรอยู่</summary>
    public string GetDebugDetail()
    {
        if (profile == null) return "";

        string detail;
        switch (CurrentState)
        {
            case CreatureState.Idle:
                detail = $"idle เหลือ {Mathf.Max(0f, stateTimer):F1}s | เห็นผู้เล่นสะสม {visionTimer:F1}s";
                break;
            case CreatureState.Walking:
                detail = $"เดินมา {walkTimer:F1}/{profile.maxWalkDuration:F0}s | ถึงจุดหมายอีก {(agent.pathPending ? -1f : agent.remainingDistance):F1}m | เห็นผู้เล่นสะสม {visionTimer:F1}s";
                break;
            case CreatureState.Eating:
                detail = $"กินอีก {Mathf.Max(0f, stateTimer):F1}s | การรับรู้ x{vision.PerceptionMultiplier:F1}";
                break;
            case CreatureState.Stop:
                detail = $"รอเข้า Look around อีก {Mathf.Max(0f, stateTimer):F1}s | ระยะตอนเริ่ม Stop {stopEntryDistance:F1}m";
                break;
            case CreatureState.LookAround:
                detail = $"หาอีก {Mathf.Max(0f, stateTimer):F1}s | เห็นผู้เล่นสะสม {spotTimer:F1}/{profile.lookAroundSpotTime:F1}s | หัวหัน {(head != null ? head.CurrentYaw : 0f):F0}°{(lookTurning ? " | กำลังหมุนตัว" : "")}";
                break;
            case CreatureState.Alert:
                detail = $"ผู้เล่นนิ่งสะสม {alertStillTimer:F1}/{profile.alertStillTime:F1}s | ระยะตอนเข้า Alert {alertEntryDistance:F1}m " +
                         $"(เข้าใกล้ถึง {Mathf.Max(0f, alertEntryDistance - profile.alertApproachDistance):F1}m = หนี, ถอยถึง {alertEntryDistance + profile.alertRetreatDistance:F1}m = Stop)";
                break;
            case CreatureState.Run:
                detail = $"วิ่งหนี จะปลอดภัยเมื่อห่าง {profile.safeDistance:F0}m";
                break;
            default:
                detail = "";
                break;
        }

        return detail;
    }

    // ==================== ประสาทสัมผัส / ข้อมูลผู้เล่น ====================
    private void OnShutterHeard(Vector3 position)
    {
        if (profile == null) return;
        float radius = profile.shutterHearingRadius * (vision != null ? vision.PerceptionMultiplier : 1f);
        if ((position - transform.position).sqrMagnitude <= radius * radius) heardShutter = true;
    }

    private void UpdatePlayerSpeed()
    {
        if (playerBody != null)
        {
            Vector3 v = playerBody.linearVelocity;
            v.y = 0f;
            playerSpeed = v.magnitude;
        }
        else if (Time.deltaTime > 0f)
        {
            Vector3 delta = player.position - lastPlayerPos;
            delta.y = 0f;
            playerSpeed = Mathf.Lerp(playerSpeed, delta.magnitude / Time.deltaTime, 10f * Time.deltaTime);
        }
        lastPlayerPos = player.position;
    }

    private float DistanceToPlayer() => Vector3.Distance(transform.position, player.position);

    private static bool IsPlayerPhotographing() =>
        StateManager.Instance != null && StateManager.Instance.IsSystemState(StateManager.SystemState.Photograph);

    private static bool IsPlayerSprinting() =>
        StateManager.Instance != null && StateManager.Instance.IsMovementState(StateManager.MovementState.Running);

    // ==================== Helper ====================
    private void FaceTowards(Vector3 target, float degreesPerSecond)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
    }

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

        // ก้มกินหญ้า = การรับรู้ทั้งหมดแคบลง ออกจากท่ากินแล้วกลับปกติ
        vision.PerceptionMultiplier = newState == CreatureState.Eating ? profile.eatPerceptionMultiplier : 1f;

        OnStateChanged?.Invoke(oldState, newState);
    }
}

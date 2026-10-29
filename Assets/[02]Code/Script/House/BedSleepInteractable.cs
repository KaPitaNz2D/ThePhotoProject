using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// นอนบนเตียงเพื่อข้ามเวลา — เดินเข้าโซน (BoxCollider Trigger) แล้วกด Interact (E ตัวเดียวกับคุย NPC/บอร์ด)
/// จอเฟดดำ -> ค้างดำ -> เวลากระโดดไปช่วงถัดไปใน wakeHours (ค่าเริ่ม เช้า 8 / เย็น 17 / กลางคืน 20) -> เฟดกลับ
/// ระหว่างนอนตั้ง SystemState.Pause (เดิน/หันกล้องไม่ได้) แล้วคืน Normal ตอนตื่น
///
/// แปะไว้ที่ลูก "SleepZone" ของ Bed.prefab (แยกจาก Collider ทึบของตัวเตียง) — ทุกเตียงในเกมได้ระบบนี้เอง
///
/// เตียงมักชิดผนังบ้าน โซนจึงทะลุออกนอกบ้านได้ — เลยเช็คเพิ่มว่าระหว่างผู้เล่นกับตัวเตียงต้องไม่มีอะไรทึบ (เช่นผนัง) ขวาง
/// ถ้ามีผนังกั้นอยู่ ถึงยืนในโซนก็กดไม่ได้ ไม่ต้องปรับขนาดโซนทีละเตียง
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BedSleepInteractable : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("ไฟล์ PlayerControls.inputactions — หา Action ปุ่ม Interact จากชื่อด้านล่าง")]
    public InputActionAsset inputActions;
    public string interactActionPath = "NPC Interection/Interect";

    [Header("Prompt")]
    [Tooltip("ป้าย \"Press E to sleep\" โชว์ตอนอยู่ในโซนและกดได้ (ปล่อยว่างได้)")]
    public GameObject interactPrompt;

    [Header("Sleep")]
    [Tooltip("ชั่วโมงที่ตื่น เรียงน้อยไปมาก — นอนแล้วไปชั่วโมงถัดไปที่มากกว่าตอนนี้ ถ้าไม่มีแล้วจะวนไปตัวแรกของวันถัดไป")]
    public int[] wakeHours = { 8, 17, 20 };
    public float fadeOutDuration = 0.6f;
    [Tooltip("ค้างจอดำนานเท่าไร (วินาที)")]
    public float holdDuration = 1.5f;
    public float fadeInDuration = 0.8f;

    private static bool isSleeping; // ใช้ร่วมกันทุกเตียง กันกดนอนซ้อน

    [Header("Line of Sight")]
    [Tooltip("Collider ทึบของตัวเตียง ใช้เป็นจุดปลายทางเช็คว่ามีผนังขวางไหม ไม่ใส่จะหาจาก Parent ให้เอง")]
    public Collider bedCollider;

    private InputAction interactAction;
    private bool isInRange;
    private Rigidbody playerBody;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (bedCollider == null && transform.parent != null) bedCollider = transform.parent.GetComponent<Collider>();
    }

    private void OnEnable()
    {
        interactAction = inputActions != null ? inputActions.FindAction(interactActionPath, false) : null;
        if (interactAction == null)
        {
            Debug.LogWarning($"[BedSleepInteractable] หา Action \"{interactActionPath}\" ไม่เจอ — ลาก PlayerControls.inputactions ใส่ช่อง Input Actions ที่ {name}");
            return;
        }
        interactAction.Enable();
        interactAction.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (interactAction != null) interactAction.performed -= OnInteract;
        isInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        isInRange = true;
        playerBody = other.attachedRigidbody;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) isInRange = false;
    }

    // ในโซน + ไม่มีผนังกั้นระหว่างผู้เล่นกับเตียง
    private bool CanReach()
    {
        if (!isInRange || playerBody == null) return false;
        if (bedCollider == null) return true;

        Vector3 from = playerBody.position + Vector3.up * 1f;
        Bounds b = bedCollider.bounds;
        Vector3 to = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 dir = to - from;

        foreach (RaycastHit hit in Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == bedCollider || hit.collider.attachedRigidbody == playerBody) continue;
            return false;
        }
        return true;
    }

    private static bool IsPlayer(Collider other)
    {
        return other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player");
    }

    private static bool CanSleepNow =>
        !isSleeping && StateManager.Instance != null && StateManager.Instance.CurrentSystemState == StateManager.SystemState.Normal;

    private void Update()
    {
        if (interactPrompt != null) interactPrompt.SetActive(CanSleepNow && CanReach());
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!CanSleepNow || !CanReach()) return;
        StartCoroutine(SleepRoutine());
    }

    private IEnumerator SleepRoutine()
    {
        TimeManager time = FindFirstObjectByType<TimeManager>();
        if (time == null)
        {
            Debug.LogWarning("[BedSleepInteractable] ไม่มี TimeManager ในซีน นอนข้ามเวลาไม่ได้");
            yield break;
        }

        isSleeping = true;
        StateManager.Instance.SetSystemState(StateManager.SystemState.Pause);

        int target = NextWakeHour(time.Hours);
        bool jumped = false;
        bool done = false;
        void Jump() { if (jumped) return; jumped = true; time.JumpToHour(target); }

        PhotoTransitionUI fade = FindFirstObjectByType<PhotoTransitionUI>(FindObjectsInactive.Include);
        if (fade != null)
        {
            fade.PlayBlackout(fadeOutDuration, holdDuration, fadeInDuration, Jump, () => done = true);
            // กันค้างถ้ามีเฟดอื่นมาตัดกลางทาง (onDone จะไม่ถูกเรียก)
            float timeout = Time.time + fadeOutDuration + holdDuration + fadeInDuration + 1f;
            yield return new WaitUntil(() => done || Time.time > timeout);
        }
        Jump();

        StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
        isSleeping = false;
    }

    private int NextWakeHour(int currentHour)
    {
        if (wakeHours == null || wakeHours.Length == 0) return currentHour;
        foreach (int h in wakeHours)
        {
            if (h > currentHour) return h;
        }
        return wakeHours[0]; // เลยช่วงสุดท้ายของวันแล้ว -> ตื่นวันถัดไป
    }
}

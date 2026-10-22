using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// แปะไว้ที่ Prefab บอร์ดคู่กับ PhotoBoard — ผู้เล่นเดินเข้าใกล้แล้วกด Interact เพื่อเปิดหน้าบอร์ด (เหมือน NPCInteractable)
/// ตั้ง SystemState.Board ตอนเปิดอยู่ (บล็อกการเดิน เหมือนเข้า Pause) ปลดล็อกกลับ Normal เองตอนปิด
///
/// ระยะ Interact ใช้ BoxCollider (ต้องติ๊ก Is Trigger) แทนระยะวงกลม (Vector3.Distance) เพราะบอร์ดมักอยู่ในบ้าน/ห้อง
/// ถ้าใช้ระยะวงกลมจะกดจากนอกกำแพงได้ (ระยะทะลุกำแพง) — ปรับขนาด/ตำแหน่งกล่องใน Inspector (Edit Collider) ให้ครอบเฉพาะจุดที่ยืนถูกต้องหน้าบอร์ดในห้องเท่านั้น
/// ต้องมี Rigidbody อยู่ที่ตัวผู้เล่น (มีอยู่แล้วที่ Player_Code_Dev) ถึงจะยิง OnTrigger ได้ (กฎฟิสิกส์ของ Unity)
/// </summary>
[RequireComponent(typeof(PhotoBoard))]
[RequireComponent(typeof(BoxCollider))]
public class PhotoBoardInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Action แบบ Button สำหรับกดเข้าบอร์ด (ใช้ตัวเดียวกับ NPC ก็ได้ เช่น E)")]
    public InputActionReference interactInput;
    [Tooltip("UI ที่โชว์ตอนผู้เล่นเข้ามาในโซน Interact เช่น \"กด E เพื่อดูบอร์ด\" (ปล่อยว่างได้ถ้าไม่ต้องการ)")]
    public GameObject interactPrompt;

    [Header("Board UI")]
    [Tooltip("หน้า UI เต็มจอของบอร์ด ถ้าไม่ใส่ไว้จะหาใน Scene ให้เอง (ควรมีตัวเดียวในฉาก ใช้ร่วมกันทุกบอร์ด)")]
    public PhotoBoardUI boardUI;

    private PhotoBoard board;
    private bool isInRange;

    private void Awake()
    {
        board = GetComponent<PhotoBoard>();

        BoxCollider zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;
    }

    private void Start()
    {
        if (interactInput != null)
        {
            interactInput.action.Enable();
            interactInput.action.performed += OnInteractPressed;
        }

        if (boardUI == null)
        {
            boardUI = FindFirstObjectByType<PhotoBoardUI>(FindObjectsInactive.Include);
        }
    }

    private void OnDestroy()
    {
        if (interactInput != null) interactInput.action.performed -= OnInteractPressed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        isInRange = true;
        UpdatePrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        isInRange = false;
        UpdatePrompt();
    }

    private static bool IsPlayer(Collider other)
    {
        return other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player");
    }

    private void Update()
    {
        // SystemState อาจเปลี่ยนระหว่างยืนอยู่ในโซนอยู่แล้ว (เช่นเข้าโหมดถ่ายรูป) ต้องเช็คทุกเฟรมไว้ด้วย ไม่ใช่แค่ตอน Trigger Enter/Exit
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (interactPrompt == null) return;

        bool canInteractNow = isInRange && StateManager.Instance != null
            && StateManager.Instance.CurrentSystemState == StateManager.SystemState.Normal;
        interactPrompt.SetActive(canInteractNow);
    }

    private void OnInteractPressed(InputAction.CallbackContext ctx)
    {
        if (boardUI == null) return;

        // บอร์ดนี้เปิดค้างอยู่แล้ว -> กด E ซ้ำ = ปิดออก (ไม่ต้องเช็คระยะ/SystemState เพราะกำลังอยู่ในนั้นอยู่แล้ว)
        if (boardUI.IsShowing(board))
        {
            boardUI.Close();
            return;
        }

        if (!isInRange) return;

        // เปิดบอร์ดได้เฉพาะตอนอยู่ Normal เท่านั้น กันเผลอเปิดซ้อนตอนถ่ายรูป/คุย/เปิด UI อื่นอยู่
        if (StateManager.Instance != null && StateManager.Instance.CurrentSystemState != StateManager.SystemState.Normal) return;

        boardUI.Open(board);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

/// <summary>
/// NPC Placeholder ไม่ขยับ — ผู้เล่นเดินเข้าใกล้แล้วกด Interact เพื่อเริ่มบทสนทนาผ่าน Yarn Spinner
/// ตั้ง SystemState.Talking ระหว่างคุย (บล็อกการเดิน/หมุนกล้องอัตโนมัติ เพราะ SystemState นี้มีอยู่แล้ว)
/// และปลดล็อกกลับเป็น Normal เองตอนจบบทสนทนา
/// </summary>
public class NPCInteractable : MonoBehaviour
{
    [Header("Yarn Spinner")]
    [Tooltip("DialogueRunner ในซีน (ปกติมีตัวเดียวใช้ร่วมกันทุก NPC)")]
    public DialogueRunner dialogueRunner;
    [Tooltip("ชื่อ Yarn Node ที่จะเริ่มเมื่อคุยกับ NPC ตัวนี้")]
    public string startNode = "NPC_Start";

    [Header("Interaction")]
    [Tooltip("Action แบบ Button สำหรับกดคุย (เช่น E)")]
    public InputActionReference interactInput;
    [Tooltip("ระยะที่ผู้เล่นต้องเข้ามาใกล้ถึงจะกด Interact ได้")]
    public float interactRange = 3f;
    [Tooltip("Transform ของผู้เล่น ถ้าไม่ใส่ไว้จะหาจาก Tag \"Player\" ให้เอง")]
    public Transform player;
    [Tooltip("UI ที่โชว์ตอนผู้เล่นเข้าใกล้พอจะกด Interact ได้ เช่น \"กด E เพื่อคุย\" (ปล่อยว่างได้ถ้าไม่ต้องการ)")]
    public GameObject interactPrompt;

    private bool isInRange;

    private void Start()
    {
        if (interactInput != null)
        {
            interactInput.action.Enable();
            interactInput.action.performed += OnInteractPressed;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        }
        else
        {
            Debug.LogError($"[NPCInteractable] {gameObject.name} ไม่ได้ผูก DialogueRunner ไว้!");
        }
    }

    private void OnDestroy()
    {
        if (interactInput != null) interactInput.action.performed -= OnInteractPressed;
        if (dialogueRunner != null) dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
    }

    private void Update()
    {
        if (player == null) return;
        isInRange = Vector3.Distance(transform.position, player.position) <= interactRange;

        if (interactPrompt != null)
        {
            bool canInteractNow = isInRange && StateManager.Instance != null
                && StateManager.Instance.CurrentSystemState == StateManager.SystemState.Normal;
            interactPrompt.SetActive(canInteractNow);
        }
    }

    private void OnInteractPressed(InputAction.CallbackContext ctx)
    {
        if (!isInRange) return;
        if (dialogueRunner == null || dialogueRunner.IsDialogueRunning) return;

        // เริ่มคุยได้เฉพาะตอนอยู่ Normal เท่านั้น กันเผลอเริ่มคุยซ้อนตอนถ่ายรูป/เปิด UI อื่นอยู่
        if (StateManager.Instance != null && StateManager.Instance.CurrentSystemState != StateManager.SystemState.Normal) return;

        if (StateManager.Instance != null)
        {
            StateManager.Instance.SetSystemState(StateManager.SystemState.Talking);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        dialogueRunner.StartDialogue(startNode);
    }

    private void OnDialogueComplete()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.SetSystemState(StateManager.SystemState.Normal);
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
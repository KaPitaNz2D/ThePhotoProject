using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// แปะไว้ที่ GameObject เดียวกับ Cinemachine Camera (ตัวที่มี CinemachineRotationComposer อยู่)
/// กดปุ่มที่ผูกไว้ (เช่น Q) เพื่อสลับมุมกล้องจากไหล่ซ้าย <-> ไหล่ขวา
/// โดยการกลับเครื่องหมาย Screen Position X ของ Rotation Composer (0.1 <-> -0.1)
/// </summary>
[RequireComponent(typeof(CinemachineRotationComposer))]
public class CameraShoulderSwitch : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action ปุ่มกด (Button) สำหรับสลับไหล่ เช่นผูกกับปุ่ม Q")]
    public InputActionReference switchShoulderInput;

    [Header("Settings")]
    [Tooltip("ค่า Screen Position X ตอนอยู่ไหล่ขวา (ค่าปกติตอนนี้)")]
    public float rightShoulderX = 0.1f;
    [Tooltip("ค่า Screen Position X ตอนอยู่ไหล่ซ้าย (ค่ากลับด้าน)")]
    public float leftShoulderX = -0.1f;
    [Tooltip("ความเร็วในการเลื่อนกล้องข้ามไหล่ (0 = สลับทันทีไม่มี Transition)")]
    public float switchSpeed = 8f;

    private CinemachineRotationComposer rotationComposer;
    private bool isRightShoulder = true;
    private float targetX;

    private void Awake()
    {
        rotationComposer = GetComponent<CinemachineRotationComposer>();
        targetX = rightShoulderX;
    }

    private void OnEnable()
    {
        if (switchShoulderInput != null)
        {
            switchShoulderInput.action.Enable();
            switchShoulderInput.action.performed += OnSwitchShoulder;
        }
    }

    private void OnDisable()
    {
        if (switchShoulderInput != null)
        {
            switchShoulderInput.action.performed -= OnSwitchShoulder;
        }
    }

    private void OnSwitchShoulder(InputAction.CallbackContext ctx)
    {
        isRightShoulder = !isRightShoulder;
        targetX = isRightShoulder ? rightShoulderX : leftShoulderX;
    }

    private void Update()
    {
        if (rotationComposer == null) return;

        Vector2 pos = rotationComposer.Composition.ScreenPosition;

        if (switchSpeed <= 0f)
        {
            pos.x = targetX; // สลับทันที ไม่มี Transition
        }
        else
        {
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * switchSpeed);
        }

        rotationComposer.Composition.ScreenPosition = pos;
    }
}
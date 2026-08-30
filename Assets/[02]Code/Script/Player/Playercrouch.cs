using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// จัดการท่าย่อของผู้เล่น: Toggle ย่อ/ลุก, ปรับความสูง "ทุกกล้องที่ผูกไว้" อัตโนมัติแบบนุ่มนวล
/// (Third Person Camera Follow + Photo Camera Pivot พร้อมกัน) และเป็นตัวสั่ง StateManager.MovementState.Crouch
/// ให้ระบบอื่น (PlayerMovement, CreatureVision ผ่าน CreatureAI) อ่านค่าไปใช้ได้แบบรวมศูนย์
///
/// ตั้งใจให้สลับย่อ/ลุกได้ทั้งตอน SystemState.Normal และ Photograph (เล็งกล้องถ่ายรูปอยู่ก็ย่อได้)
/// แต่ไม่ให้สลับได้ตอน Talking/Pause/Journal (เช็คผ่าน StateManager.CanCrouch())
///
/// ใช้ "ค่าชดเชย" (Offset) แทนความสูงตายตัว เพราะแต่ละกล้องเริ่มต้นอยู่คนละความสูงกัน
/// (Third Person อยู่ระดับหัว, Photo Pivot อยู่ระดับอก) การใช้ Offset ทำให้ไม่ต้องมาคอยตั้งค่าคู่ให้ตรงกันเอง
/// แค่ลาก Transform ไหนใส่ List ก็จะถูกลดระดับลงเท่ากันหมดโดยอัตโนมัติ
/// </summary>
public class PlayerCrouch : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action แบบ Button กด Toggle ย่อ/ลุก")]
    public InputActionReference crouchInput;

    [Header("Camera Height")]
    [Tooltip("ลาก Transform ของทุกกล้องที่อยากให้ลดระดับตอนย่อ เช่น CameraFollow (Third Person) และ PhotoCameraPivot (ถ่ายรูป)")]
    public List<Transform> cameraHeightTargets = new List<Transform>();
    [Tooltip("ระยะที่กล้องลดลงตอนย่อ (ใส่เป็นค่าบวก เช่น 0.5 = ลดลง 0.5 หน่วย) ใช้ค่าเดียวกันกับทุก Transform ใน List ด้านบน")]
    public float crouchHeightOffset = 0.5f;
    [Tooltip("ความนุ่มนวลตอนกล้องเลื่อนความสูง ยิ่งมากยิ่งไว")]
    public float cameraHeightSmoothing = 8f;

    /// <summary>สถานะปัจจุบันว่ากำลังย่ออยู่ไหม — อ่านได้จากภายนอก แต่ตัวจริงที่ระบบอื่นควรอ่านคือ StateManager.CurrentMovementState</summary>
    public bool IsCrouching { get; private set; }

    // เก็บความสูง Local Y "ตอนยืนปกติ" ของแต่ละ Transform ไว้ตั้งแต่เริ่มเกม ใช้เป็นจุดอ้างอิงคำนวณ Offset
    private List<float> standingLocalY = new List<float>();

    private void Start()
    {
        standingLocalY.Clear();
        foreach (Transform t in cameraHeightTargets)
        {
            standingLocalY.Add(t != null ? t.localPosition.y : 0f);
        }

        if (crouchInput != null)
        {
            crouchInput.action.Enable();
            crouchInput.action.performed += OnCrouchPressed;
        }
    }

    private void OnDestroy()
    {
        if (crouchInput != null)
        {
            crouchInput.action.performed -= OnCrouchPressed;
        }
    }

    private void OnCrouchPressed(InputAction.CallbackContext ctx)
    {
        if (StateManager.Instance != null && !StateManager.Instance.CanCrouch()) return;

        IsCrouching = !IsCrouching;

        // หมายเหตุ: ไม่เรียก StateManager.SetMovementState ตรงนี้ เพราะ PlayerMovement.cs
        // เป็นคนตัดสินใจ MovementState ทุกเฟรมอยู่แล้ว (อ่านค่า IsCrouching นี้ไปประกอบการตัดสินใจแทน)
        // ถ้าสั่งจากสองที่จะตีกัน ตัวไหนรันทีหลังจะเขียนทับอีกตัว
    }

    private void Update()
    {
        for (int i = 0; i < cameraHeightTargets.Count; i++)
        {
            Transform t = cameraHeightTargets[i];
            if (t == null) continue;

            float targetY = standingLocalY[i] - (IsCrouching ? crouchHeightOffset : 0f);

            Vector3 localPos = t.localPosition;
            localPos.y = Mathf.Lerp(localPos.y, targetY, Time.deltaTime * cameraHeightSmoothing);
            t.localPosition = localPos;
        }
    }
}
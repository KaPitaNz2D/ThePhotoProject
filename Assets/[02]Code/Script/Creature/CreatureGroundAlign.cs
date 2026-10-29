using UnityEngine;

// NavMeshAgent หมุนแค่แกน Yaw (ทิศทางเดิน) ไม่เอียงตัวตามความชันพื้นให้เอง
// ทำให้เวลาเดินบน Slope โมเดลยืนตรงดิ่งเหมือนเดิม ดูเหมือนขาลอย/จมพื้นสลับกันไปตามความชัน
// สคริปต์นี้ยิง Raycast ลงพื้นทุกเฟรม แล้วเอียงเฉพาะโมเดลลูก (ไม่แตะ root ที่ NavMeshAgent คุมอยู่ กัน path เพี้ยน)
public class CreatureGroundAlign : MonoBehaviour
{
    [Tooltip("Transform ของโมเดล/Animator ที่จะเอียงตามพื้น ถ้าไม่ใส่ไว้จะหาจาก Animator ลูกให้เอง")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("ระยะ Raycast ลงพื้น (นับจากจุดเริ่มที่ยกขึ้นแล้ว)")]
    [SerializeField] private float rayDistance = 3f;

    [Tooltip("ยกจุดเริ่ม Raycast ขึ้นเหนือ pivot เท่าไหร่ กันยิงเริ่มจากใต้พื้นพอดี")]
    [SerializeField] private float rayOriginHeight = 1f;

    [Tooltip("Layer ที่นับเป็นพื้น (Terrain/Ground) — ตั้งใจไม่ใช้ Everything เพราะจะยิงชน Collider ของตัวเองแทนพื้น")]
    [SerializeField] private LayerMask groundMask = 1 << 6; // Ground layer

    [Tooltip("ความเร็วในการหมุนตามพื้น ยิ่งมากยิ่งเอียงไว")]
    [SerializeField] private float alignSpeed = 8f;

    private void Awake()
    {
        if (visualRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null) visualRoot = animator.transform;
        }
    }

    private void LateUpdate()
    {
        if (visualRoot == null) return;

        Vector3 rayOrigin = transform.position + Vector3.up * rayOriginHeight;
        Quaternion targetRotation = transform.rotation;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayOriginHeight + rayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            targetRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
        }

        visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRotation, Time.deltaTime * alignSpeed);
    }
}

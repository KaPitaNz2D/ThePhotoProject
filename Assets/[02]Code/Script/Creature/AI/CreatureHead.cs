using UnityEngine;

/// <summary>
/// หันเฉพาะหัวสัตว์ (ซ้าย/ขวา) ด้วยโค้ด ไม่ต้องมี Animation — บิดกระดูกหัว/คอทับท่าจาก Animator ใน LateUpdate
/// CreatureVision อ่าน CurrentYaw ไปหมุนโคนสายตาตามหัว สัตว์จึง "เห็น" ในทิศที่หันหัวไปจริง
///
/// หมุนรอบแกนตั้งของโมเดล (modelRoot.up) ในพิกัดโลก ไม่ขึ้นกับแกนท้องถิ่นของกระดูก (rig กวางแกนกระดูกไม่ตรงกับโลก)
/// </summary>
public class CreatureHead : MonoBehaviour
{
    [Header("Bones (ว่างไว้ = หาจากชื่อ head.x / neck.x ให้เอง)")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform neckBone;
    [Tooltip("สัดส่วนที่คอหมุนตามหัว (0 = หมุนแค่หัว) ช่วยให้ดูไม่ฝืน")]
    [Range(0f, 1f)] [SerializeField] private float neckShare = 0.3f;
    [Tooltip("Transform โมเดลที่ใช้แกนตั้ง ว่างไว้ = Animator ลูก")]
    [SerializeField] private Transform modelRoot;

    /// <summary>ความเร็วหันหัว (องศา/วินาที) CreatureAI ตั้งให้จาก Profile</summary>
    public float turnSpeed = 200f;

    /// <summary>มุมหัวปัจจุบันเทียบทิศหน้าตัว (องศา) บวก = ขวา</summary>
    public float CurrentYaw { get; private set; }

    private float targetYaw;
    private float[] sequence;
    private int sequenceIndex;
    private float sequenceHold;
    private bool sequenceLoop;
    private float holdTimer;

    private void Awake()
    {
        if (modelRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            modelRoot = animator != null ? animator.transform : transform;
        }
        if (headBone == null) headBone = FindDeep(transform, "head.x");
        if (neckBone == null) neckBone = FindDeep(transform, "neck.x");

        if (headBone == null)
        {
            Debug.LogWarning($"[CreatureHead] {gameObject.name} หากระดูกหัว (head.x) ไม่เจอ — หันหัวไม่ได้ ใส่ช่อง Head Bone เอง");
        }
    }

    /// <summary>หันไปมุมนี้แล้วค้างไว้ (ยกเลิก Sequence ที่เล่นอยู่)</summary>
    public void LookYaw(float yaw)
    {
        sequence = null;
        targetYaw = yaw;
    }

    /// <summary>หันกลับตรงหน้า</summary>
    public void ResetLook() => LookYaw(0f);

    /// <summary>หันไปตามมุมที่ให้ทีละมุม ค้างที่แต่ละมุม holdTime วินาที จบแล้ววนซ้ำ (loop) หรือกลับตรงหน้า</summary>
    public void PlaySequence(float[] yaws, float holdTime, bool loop)
    {
        if (yaws == null || yaws.Length == 0) { ResetLook(); return; }
        sequence = yaws;
        sequenceIndex = 0;
        sequenceHold = holdTime;
        sequenceLoop = loop;
        holdTimer = 0f;
        targetYaw = sequence[0];
    }

    private void Update()
    {
        CurrentYaw = Mathf.MoveTowards(CurrentYaw, targetYaw, turnSpeed * Time.deltaTime);

        if (sequence == null || !Mathf.Approximately(CurrentYaw, targetYaw)) return;

        holdTimer += Time.deltaTime;
        if (holdTimer < sequenceHold) return;

        holdTimer = 0f;
        sequenceIndex++;
        if (sequenceIndex >= sequence.Length)
        {
            if (sequenceLoop)
            {
                sequenceIndex = 0;
            }
            else
            {
                sequence = null;
                targetYaw = 0f;
                return;
            }
        }
        targetYaw = sequence[sequenceIndex];
    }

    private void LateUpdate()
    {
        if (headBone == null || Mathf.Approximately(CurrentYaw, 0f)) return;

        Vector3 axis = modelRoot != null ? modelRoot.up : transform.up;
        if (neckBone != null && neckShare > 0f)
        {
            neckBone.rotation = Quaternion.AngleAxis(CurrentYaw * neckShare, axis) * neckBone.rotation;
        }
        headBone.rotation = Quaternion.AngleAxis(CurrentYaw, axis) * headBone.rotation;
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName) return t;
        }
        return null;
    }
}

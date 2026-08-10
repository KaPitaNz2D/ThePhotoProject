using UnityEngine;

public class FootIK : MonoBehaviour
{
    Animator anim;

    [Header("Raycast Settings")]
    public LayerMask groundLayer;
    public float footOffset = 0.1f;
    public float raycastDistance = 0.8f;
    public float raycastStartHeight = 0.4f;

    [Header("Weight Settings")]
    public float maxFootLift = 0.15f;

    [Header("Hip Adjustment")]
    public bool enableHipAdjustment = true;
    public float hipAdjustSpeed = 7f;
    float currentHipOffset = 0f;

    [Header("Smoothing")]
    public float positionSmoothSpeed = 20f; // ใช้หน่วงความสูงแกน Y บนเนิน/บันได
    public float weightSmoothSpeed = 15f;
    public float releaseWeightMultiplier = 2.5f; // คลาย weight เร็วขึ้นเมื่อยกเท้า

    [Header("Speed Threshold (AAA Trick)")]
    public bool disableIKWhenMovingFast = true;
    public float maxSpeedForIK = 6f; // ความเร็ววิ่งสปริ้นท์ที่ตัด IK

    [Header("Debug")]
    public bool debugForceWeight = false;
    [Range(0f, 1f)] public float debugWeightValue = 1f;
    public bool showDebugGizmos = true;
    public bool logWeights = false;

    Vector3 leftCurrentPos, rightCurrentPos;
    float leftCurrentWeight, rightCurrentWeight;
    Quaternion leftCurrentRot, rightCurrentRot;
    bool initialized = false;

    Vector3 leftHitPoint, rightHitPoint;
    bool leftHit, rightHit;

    Vector3 lastCharacterPos;
    float characterSpeed;

    void Start()
    {
        anim = GetComponent<Animator>();
        lastCharacterPos = transform.position;
    }

    void Update()
    {
        characterSpeed = (transform.position - lastCharacterPos).magnitude / Time.deltaTime;
        lastCharacterPos = transform.position;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        if (!initialized)
        {
            leftCurrentPos = anim.GetIKPosition(AvatarIKGoal.LeftFoot);
            rightCurrentPos = anim.GetIKPosition(AvatarIKGoal.RightFoot);
            leftCurrentRot = anim.GetIKRotation(AvatarIKGoal.LeftFoot);
            rightCurrentRot = anim.GetIKRotation(AvatarIKGoal.RightFoot);
            initialized = true;
        }

        float speedWeightFactor = 1f;
        if (disableIKWhenMovingFast && maxSpeedForIK > 0)
        {
            speedWeightFactor = Mathf.Clamp01(1f - (characterSpeed / maxSpeedForIK));
        }

        if (enableHipAdjustment)
        {
            float leftDelta = CalculateFootDelta(AvatarIKGoal.LeftFoot);
            float rightDelta = CalculateFootDelta(AvatarIKGoal.RightFoot);

            float targetHipOffset = Mathf.Min(Mathf.Min(leftDelta, rightDelta), 0f) * speedWeightFactor;
            currentHipOffset = Mathf.Lerp(currentHipOffset, targetHipOffset, Time.deltaTime * hipAdjustSpeed);

            Vector3 bodyPos = anim.bodyPosition;
            bodyPos.y += currentHipOffset;
            anim.bodyPosition = bodyPos;
        }

        SetFootIK(AvatarIKGoal.LeftFoot, speedWeightFactor);
        SetFootIK(AvatarIKGoal.RightFoot, speedWeightFactor);
    }

    float CalculateFootDelta(AvatarIKGoal foot)
    {
        Vector3 footPos = anim.GetIKPosition(foot);
        Vector3 rayOrigin = new Vector3(footPos.x, transform.position.y + raycastStartHeight, footPos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            return hit.point.y - transform.position.y;
        }

        return 0f;
    }

    void SetFootIK(AvatarIKGoal foot, float speedWeightFactor)
    {
        bool isLeft = foot == AvatarIKGoal.LeftFoot;
        Vector3 footPos = anim.GetIKPosition(foot);

        Vector3 rayOrigin = footPos + Vector3.up * raycastStartHeight;
        bool hitGround = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer);

        Debug.DrawRay(rayOrigin, Vector3.down * raycastDistance, hitGround ? Color.green : Color.red);

        if (isLeft) leftHit = hitGround;
        else rightHit = hitGround;

        float targetWeight;
        Vector3 targetPos;
        Quaternion targetRot;

        if (hitGround)
        {
            float heightAboveGround = footPos.y - hit.point.y;

            if (debugForceWeight)
            {
                targetWeight = debugWeightValue;
            }
            else
            {
                if (heightAboveGround > maxFootLift)
                {
                    float excessHeight = heightAboveGround - maxFootLift;
                    float fadeRange = 0.2f;
                    targetWeight = Mathf.Clamp01(1f - (excessHeight / fadeRange));
                }
                else
                {
                    targetWeight = 1f;
                }
            }

            targetWeight *= speedWeightFactor;
            targetPos = hit.point + Vector3.up * footOffset;
            targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * anim.GetIKRotation(foot);

            if (isLeft) leftHitPoint = hit.point;
            else rightHitPoint = hit.point;
        }
        else
        {
            targetWeight = 0f;
            targetPos = footPos;
            targetRot = anim.GetIKRotation(foot);
        }

        if (isLeft)
        {
            float currentWeightSpeed = targetWeight < leftCurrentWeight ? weightSmoothSpeed * releaseWeightMultiplier : weightSmoothSpeed;

            // ✨ แก้ไขหลัก: แกน X, Z ไปตาม Animation ทันที ไม่โดน Lerp ดึงรั้งไปข้างหลัง
            leftCurrentPos.x = targetPos.x;
            leftCurrentPos.z = targetPos.z;
            // Lerp เฉพาะแกน Y ความสูงพื้น เพื่อความนุ่มนวล
            leftCurrentPos.y = Mathf.Lerp(leftCurrentPos.y, targetPos.y, Time.deltaTime * positionSmoothSpeed);

            leftCurrentWeight = Mathf.Lerp(leftCurrentWeight, targetWeight, Time.deltaTime * currentWeightSpeed);
            leftCurrentRot = Quaternion.Slerp(leftCurrentRot, targetRot, Time.deltaTime * positionSmoothSpeed);

            anim.SetIKPositionWeight(foot, leftCurrentWeight);
            anim.SetIKRotationWeight(foot, leftCurrentWeight);
            anim.SetIKPosition(foot, leftCurrentPos);
            anim.SetIKRotation(foot, leftCurrentRot);
        }
        else
        {
            float currentWeightSpeed = targetWeight < rightCurrentWeight ? weightSmoothSpeed * releaseWeightMultiplier : weightSmoothSpeed;

            // ✨ แก้ไขหลัก: แกน X, Z ไปตาม Animation ทันที ไม่โดน Lerp ดึงรั้งไปข้างหลัง
            rightCurrentPos.x = targetPos.x;
            rightCurrentPos.z = targetPos.z;
            // Lerp เฉพาะแกน Y ความสูงพื้น เพื่อความนุ่มนวล
            rightCurrentPos.y = Mathf.Lerp(rightCurrentPos.y, targetPos.y, Time.deltaTime * positionSmoothSpeed);

            rightCurrentWeight = Mathf.Lerp(rightCurrentWeight, targetWeight, Time.deltaTime * currentWeightSpeed);
            rightCurrentRot = Quaternion.Slerp(rightCurrentRot, targetRot, Time.deltaTime * positionSmoothSpeed);

            anim.SetIKPositionWeight(foot, rightCurrentWeight);
            anim.SetIKRotationWeight(foot, rightCurrentWeight);
            anim.SetIKPosition(foot, rightCurrentPos);
            anim.SetIKRotation(foot, rightCurrentRot);
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        if (leftHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(leftHitPoint, 0.05f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(leftCurrentPos, 0.04f);
        }
        if (rightHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(rightHitPoint, 0.05f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rightCurrentPos, 0.04f);
        }
    }
}
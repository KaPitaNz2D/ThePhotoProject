using UnityEngine;

public class FootIK : MonoBehaviour
{
    Animator anim;
    public LayerMask groundLayer;
    public float footOffset = 0.1f;
    public float raycastDistance = 1.5f;

    void Start() => anim = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        SetFootIK(AvatarIKGoal.LeftFoot);
        SetFootIK(AvatarIKGoal.RightFoot);
    }

    void SetFootIK(AvatarIKGoal foot)
    {
        float weight = anim.GetFloat(foot == AvatarIKGoal.LeftFoot ? "LeftFootWeight" : "RightFootWeight");

        Vector3 footPos = anim.GetIKPosition(foot);
        Ray ray = new Ray(footPos + Vector3.up * raycastDistance * 0.5f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer))
        {
            Vector3 targetPos = hit.point + Vector3.up * footOffset;
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * anim.GetIKRotation(foot);

            anim.SetIKPositionWeight(foot, weight);
            anim.SetIKRotationWeight(foot, weight);
            anim.SetIKPosition(foot, targetPos);
            anim.SetIKRotation(foot, targetRot);
        }
    }
}
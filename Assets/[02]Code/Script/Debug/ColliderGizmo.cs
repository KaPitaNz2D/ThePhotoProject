using UnityEngine;

/// <summary>วาด Collider (Box/Sphere) ให้เห็นใน Scene view ตอน Edit Mode เท่านั้น — ไม่โชว์ตอน Play และไม่เข้า Build</summary>
[RequireComponent(typeof(Collider))]
public class ColliderGizmo : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.1f, 1f);

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        var c = GetComponent<Collider>();

        if (c is BoxCollider box)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (c is SphereCollider sphere)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.25f);
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = color;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}

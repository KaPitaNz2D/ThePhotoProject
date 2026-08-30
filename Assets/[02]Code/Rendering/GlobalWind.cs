using UnityEngine;

// Drives the wind uniforms consumed by Shader Graphs/QuadToBillboard. Those
// properties are declared Global (outside the UnityPerMaterial cbuffer), so they
// are NOT set per-material — nothing writes them unless this component exists.
// With no GlobalWind in the scene every wind uniform reads 0 and the leaves are
// perfectly still.
[ExecuteAlways]
[AddComponentMenu("Rendering/Global Wind")]
public class GlobalWind : MonoBehaviour
{
    [Header("Direction")]
    [Tooltip("Compass direction the wind blows toward, in degrees around Y. 0 = +Z.")]
    [Range(0f, 360f)][SerializeField] private float directionDegrees = 45f;

    [Header("Strength")]
    [Tooltip("Constant downwind lean. This is the term that reads as 'storm'.")]
    [Range(0f, 1f)][SerializeField] private float bend = 0.7f;

    [Tooltip("High-frequency flutter riding on top of the bend.")]
    [Range(0f, 1f)][SerializeField] private float strength = 0.6f;

    [Header("Motion")]
    [Tooltip("Gust travel and flutter rate. 1 = breeze, 4-8 = storm.")]
    [Min(0f)][SerializeField] private float speed = 6f;

    [Tooltip("Spatial frequency of gust fronts in world space. Smaller = broader gusts.")]
    [Min(0.0001f)][SerializeField] private float gustScale = 0.03f;

    private static readonly int DirectionId = Shader.PropertyToID("_Wind_Direction");
    private static readonly int SpeedId = Shader.PropertyToID("_Wind_Speed");
    private static readonly int StrengthId = Shader.PropertyToID("_Wind_Strength");
    private static readonly int BendId = Shader.PropertyToID("_Wind_Bend");
    private static readonly int GustScaleId = Shader.PropertyToID("_Gust_Scale");

    private void OnEnable() => Apply();

    private void OnValidate() => Apply();

    // Globals survive until domain reload, so a static scene only needs one push.
    // Update keeps edit-mode scrubbing and runtime weather changes live.
    private void Update() => Apply();

    private void Apply()
    {
        float rad = directionDegrees * Mathf.Deg2Rad;
        // The shader normalizes this, but feeding a unit vector keeps the
        // inspector value meaningful if anything else ever samples it.
        Shader.SetGlobalVector(DirectionId, new Vector4(Mathf.Sin(rad), Mathf.Cos(rad), 0f, 0f));
        Shader.SetGlobalFloat(SpeedId, speed);
        Shader.SetGlobalFloat(StrengthId, strength);
        Shader.SetGlobalFloat(BendId, bend);
        Shader.SetGlobalFloat(GustScaleId, gustScale);
    }

    private void OnDrawGizmosSelected()
    {
        float rad = directionDegrees * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, dir * 5f);
        Gizmos.DrawWireSphere(transform.position + dir * 5f, 0.35f);
    }
}

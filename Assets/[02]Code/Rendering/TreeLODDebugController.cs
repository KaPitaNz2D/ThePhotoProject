using UnityEngine;

// Dev-only environment review tool: forces every InstancedTreeRenderer terrain to draw trees
// at their highest-detail LOD, regardless of camera distance, so LOD popping/simplification
// doesn't hide art issues while checking the environment. One controller anywhere in the scene
// affects all terrain tiles, since InstancedTreeRenderer.DebugForceHighestLOD is static.
[ExecuteAlways]
public class TreeLODDebugController : MonoBehaviour
{
    [Tooltip("Force all terrain trees to render at their highest-detail LOD, ignoring distance-based LOD switching.")]
    [SerializeField] private bool forceHighestLOD;

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        InstancedTreeRenderer.DebugForceHighestLOD = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Apply();
    }
#endif

    private void Apply()
    {
        InstancedTreeRenderer.DebugForceHighestLOD = forceHighestLOD;
    }
}

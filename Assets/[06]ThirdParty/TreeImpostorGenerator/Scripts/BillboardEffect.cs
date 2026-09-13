using UnityEngine;

namespace Roundy.Vegetation
{
    // For the simple 2 crossed-plane tree cards (not the 4-direction cross-quad impostors baked by
    // TreeCrossQuadImpostorGenerator - those are baked assuming a fixed orientation per quad, so
    // rotating them would show the wrong baked view facing the camera).
    [ExecuteAlways]
    [AddComponentMenu("Roundy/Vegetation/Billboard Effect")]
    public class BillboardEffect : MonoBehaviour
    {
        public enum Mode
        {
            YAxisOnly,
            FullyFaceCamera
        }

        [SerializeField] private Mode mode = Mode.YAxisOnly;
        [Tooltip("Camera to face. Defaults to Camera.main when left empty.")]
        [SerializeField] private Camera targetCamera;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 direction = transform.position - cam.transform.position;

            if (mode == Mode.YAxisOnly)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}

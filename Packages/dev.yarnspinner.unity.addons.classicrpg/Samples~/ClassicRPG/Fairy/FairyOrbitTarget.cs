using UnityEngine;

#nullable enable

namespace Yarn.Unity.Addons.ClassicRPG
{
    [ExecuteAlways]
    public class FairyOrbitTarget : MonoBehaviour
    {
        [SerializeField] Transform? target;
        [SerializeField] Vector3 offset;
        [SerializeField] float orbitSpeed = 50f;
        [SerializeField] float orbitSpeedVariance = 10f;
        [SerializeField] float radius = 1;
        [SerializeField] float speedDamping = 0.1f;

        Vector3 currentVelocity = Vector3.zero;

        float variedOrbitSpeed = 0f;

        /// <summary>
        /// The target we should orbit around. If set, takes precedence over
        /// <see cref="target"/>.
        /// </summary>
        private Transform? overrideTarget;

        /// <summary>
        /// The transform we are currently orbiting around, if any.
        /// </summary>
        private Transform? CurrentTarget
        {
            get
            {
                return overrideTarget != null ? overrideTarget : target;
            }
        }

        // private bool Run => true;
        private bool Run => Application.isPlaying;

        protected void Awake()
        {
            variedOrbitSpeed = orbitSpeed + Random.Range(-orbitSpeedVariance, orbitSpeedVariance);
            if (Random.value > 0.5)
            {
                variedOrbitSpeed *= -1;
            }
        }


        // Update is called once per frame
        void Update()
        {
            Vector3 targetPosition = GetTargetPosition();

            if (Run)
            {
                if (Vector3.Distance(targetPosition, transform.position) > 0)
                {
                    var lookDirection = Quaternion.LookRotation(transform.position - targetPosition);
                    transform.rotation = lookDirection;
                }
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, speedDamping);
            }
            else
            {
                transform.position = targetPosition;
            }

        }

        private Vector3 GetTargetPosition()
        {

            if (CurrentTarget == null)
            {
                return transform.position;
            }

            if (!Run)
            {
                return new Vector3(
                  Mathf.Cos(0) * radius,
                  0,
                  Mathf.Sin(0) * radius
              ) + CurrentTarget.position + offset;
            }


            var currentOffset = (transform.position - CurrentTarget.position + offset);
            var currentAngle = Mathf.Atan2(currentOffset.z, currentOffset.x) * Mathf.Rad2Deg;
            var desiredAngle = currentAngle + variedOrbitSpeed * Time.deltaTime;
            while (desiredAngle < 360)
            {
                desiredAngle += 360;
            }
            while (desiredAngle > 360)
            {
                desiredAngle -= 360;
            }

            var pos = new Vector3(
                Mathf.Cos(Mathf.Deg2Rad * desiredAngle) * radius,
                0,
                Mathf.Sin(Mathf.Deg2Rad * desiredAngle) * radius
            );

            var targetPosition = CurrentTarget.position + offset + pos;
            return targetPosition;
        }

        public void SetOverrideFollowTarget(Component? target)
        {
            overrideTarget = target == null ? null : target.transform;
        }
    }
}
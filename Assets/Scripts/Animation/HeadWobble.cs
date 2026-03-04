using UnityEngine;

namespace Octo.Animation
{
    /// <summary>
    /// Makes the octopus head wobble and react to movement/tentacle forces.
    /// Creates that classic Octodad "struggling to stay upright" look.
    /// 
    /// SETUP:
    /// - Attach to the HEAD bone (Neck_J or similar)
    /// - Optionally assign the OctopusLocomotion reference
    /// </summary>
    public class HeadWobble : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to locomotion for velocity-based wobble (auto-finds if null)")]
        [SerializeField] private Movement.OctopusLocomotion locomotion;

        [Header("Wobble Settings")]
        [Tooltip("How much the head tilts based on movement")]
        [SerializeField] private float movementTilt = 15f;
        [Tooltip("How much the head wobbles when players disagree")]
        [SerializeField] private float struggleTilt = 25f;
        [Tooltip("Base idle wobble amount")]
        [SerializeField] private float idleWobble = 3f;
        [Tooltip("Speed of wobble oscillation")]
        [SerializeField] private float wobbleSpeed = 2f;
        [Tooltip("How fast the head responds to changes")]
        [SerializeField] private float responseSpeed = 5f;

        [Header("Tentacle Pull")]
        [Tooltip("How much tentacle movement pulls the head")]
        [SerializeField] private float tentaclePullStrength = 5f;

        [Header("Look Direction")]
        [Tooltip("How much the head looks in movement direction")]
        [SerializeField] private float lookAhead = 10f;

        // Current wobble rotation
        private Vector3 currentWobble;
        private Vector3 targetWobble;

        // Base rotation from animation
        private Quaternion baseRotation;

        // Random offset for varied wobble
        private float wobbleOffset;

        // All tentacles (for pull calculation)
        private ProceduralTentacle[] tentacles;

        private void Start()
        {
            // Find locomotion if not assigned
            if (locomotion == null)
            {
                locomotion = GetComponentInParent<Movement.OctopusLocomotion>();
            }

            // Find all tentacles
            tentacles = GetComponentInParent<Animator>()?.GetComponentsInChildren<ProceduralTentacle>();
            if (tentacles == null || tentacles.Length == 0)
            {
                tentacles = FindObjectsByType<ProceduralTentacle>(FindObjectsSortMode.None);
            }

            // Random offset so head doesn't wobble in sync with tentacles
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void LateUpdate()
        {
            // Store the current animated rotation as base
            baseRotation = transform.localRotation;

            // Calculate target wobble
            CalculateWobble();

            // Smooth toward target
            currentWobble = Vector3.Lerp(currentWobble, targetWobble, Time.deltaTime * responseSpeed);

            // Apply wobble on top of animation
            Quaternion wobbleRotation = Quaternion.Euler(currentWobble);
            transform.localRotation = baseRotation * wobbleRotation;
        }

        private void CalculateWobble()
        {
            targetWobble = Vector3.zero;

            float time = Time.time * wobbleSpeed + wobbleOffset;

            // Base idle wobble (always present)
            targetWobble.x = Mathf.Sin(time) * idleWobble;
            targetWobble.z = Mathf.Cos(time * 0.7f) * idleWobble * 0.5f;

            // Tentacle pull - head gets tugged by active tentacles
            if (tentacles != null && tentacles.Length > 0)
            {
                Vector2 totalPull = Vector2.zero;
                foreach (var tentacle in tentacles)
                {
                    if (tentacle != null)
                    {
                        totalPull += tentacle.GetCurrentInput();
                    }
                }

                // Average the pull
                totalPull /= tentacles.Length;

                // Apply as head tilt (opposite direction - head resists the pull)
                targetWobble.x -= totalPull.y * tentaclePullStrength;
                targetWobble.z += totalPull.x * tentaclePullStrength;
            }

            if (locomotion != null)
            {
                Vector3 velocity = locomotion.GetVelocity();
                float agreement = locomotion.GetAgreementLevel();

                // Tilt based on acceleration/velocity
                // Lean back when accelerating forward, lean forward when stopping
                if (velocity.magnitude > 0.1f)
                {
                    // Convert world velocity to local space
                    Vector3 localVel = transform.InverseTransformDirection(velocity);

                    // Tilt opposite to movement (head lags behind)
                    targetWobble.x -= localVel.z * movementTilt * 0.1f;
                    targetWobble.z += localVel.x * movementTilt * 0.1f;

                    // Look in movement direction
                    targetWobble.y = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg * 0.1f * lookAhead;
                }

                // Extra wobble when players disagree (struggling)
                if (agreement < 0.7f)
                {
                    float struggleIntensity = 1f - agreement;
                    targetWobble.x += Mathf.Sin(time * 3f) * struggleTilt * struggleIntensity;
                    targetWobble.z += Mathf.Cos(time * 2.3f) * struggleTilt * struggleIntensity * 0.7f;
                    targetWobble.y += Mathf.Sin(time * 1.7f) * struggleTilt * struggleIntensity * 0.3f;
                }
            }

            // Clamp to reasonable values
            targetWobble.x = Mathf.Clamp(targetWobble.x, -45f, 45f);
            targetWobble.y = Mathf.Clamp(targetWobble.y, -30f, 30f);
            targetWobble.z = Mathf.Clamp(targetWobble.z, -30f, 30f);
        }

        /// <summary>
        /// Add an impulse wobble (e.g., when hit or surprised)
        /// </summary>
        public void AddImpulse(Vector3 direction, float strength = 1f)
        {
            currentWobble += direction * strength * 20f;
        }
    }
}

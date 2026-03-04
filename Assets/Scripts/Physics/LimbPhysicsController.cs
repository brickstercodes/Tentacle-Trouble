using UnityEngine;
using Octo.Input;

namespace Octo.Physics
{
    /// <summary>
    /// Active Ragdoll Limb Controller using Configurable Joints.
    /// 
    /// MECHANIC:
    /// - Each limb segment has a ConfigurableJoint attached.
    /// - Player joystick input (Vector2) is converted to a target rotation (Quaternion).
    /// - The joint's Slerp Drive "fights" to reach this target rotation, creating the signature
    ///   wobbly physics of an Octodad-style game.
    /// 
    /// SETUP:
    /// - Attach this to the ROOT bone of each tentacle.
    /// - Child segments will be driven by a simplified physics chain.
    /// </summary>
    [RequireComponent(typeof(ConfigurableJoint))]
    [RequireComponent(typeof(Rigidbody))]
    public class LimbPhysicsController : MonoBehaviour
    {
        [Header("Limb Identity")]
        [Tooltip("Global limb index (0-7). Player 1: 0,1 | Player 2: 2,3 | etc.")]
        [SerializeField] private int limbIndex = 0;

        [Header("Joint Configuration")]
        [SerializeField] private ConfigurableJoint joint;
        [SerializeField] private Rigidbody limbRigidbody;

        [Header("Drive Settings")]
        [Tooltip("How strongly the limb tries to reach target rotation. Higher = stiffer.")]
        [SerializeField] private float drivePositionSpring = 50f;
        [Tooltip("Damping to prevent oscillation. Higher = more stable but sluggish.")]
        [SerializeField] private float drivePositionDamper = 20f;
        [Tooltip("Maximum force the joint can apply.")]
        [SerializeField] private float driveMaxForce = 100f;

        [Header("Input Mapping")]
        [Tooltip("Maximum rotation angle in degrees when joystick is at full tilt.")]
        [SerializeField] private float maxRotationAngle = 45f;
        [Tooltip("Rotation axis for X joystick input (typically left/right tilt).")]
        [SerializeField] private Vector3 xInputAxis = Vector3.forward;
        [Tooltip("Rotation axis for Y joystick input (typically forward/back tilt).")]
        [SerializeField] private Vector3 yInputAxis = Vector3.right;

        [Header("Responsiveness")]
        [Tooltip("How fast the target rotation lerps to the input. Lower = more delay/wobble.")]
        [SerializeField] private float rotationLerpSpeed = 8f;
        [Tooltip("Use smoothed input from AirConsoleInputHandler.")]
        [SerializeField] private bool useSmoothedInput = true;

        [Header("Idle Behavior")]
        [Tooltip("Slight random movement when no input is given.")]
        [SerializeField] private bool enableIdleWobble = false;  // Disabled by default
        [SerializeField] private float idleWobbleIntensity = 2f;  // Reduced intensity
        [SerializeField] private float idleWobbleSpeed = 1f;      // Slower wobble

        [Header("Child Segment Settings")]
        [Tooltip("Child segments (if any) inherit a reduced version of the rotation.")]
        [SerializeField] private LimbPhysicsController[] childSegments;
        [SerializeField] private float childDampeningFactor = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // Runtime state
        private Quaternion initialLocalRotation;
        private Quaternion targetRotation;
        private Quaternion currentSmoothedRotation;
        private Vector2 currentInput;
        private float idleNoiseOffset;

        // Cached references
        private AirConsoleInputHandler inputHandler;

        #region Unity Lifecycle

        private void Awake()
        {
            // Cache components
            if (joint == null)
                joint = GetComponent<ConfigurableJoint>();

            if (limbRigidbody == null)
                limbRigidbody = GetComponent<Rigidbody>();

            // Store initial rotation for calculating offsets
            initialLocalRotation = transform.localRotation;
            targetRotation = Quaternion.identity;
            currentSmoothedRotation = Quaternion.identity;

            // Random offset for idle wobble to desync limbs
            idleNoiseOffset = Random.Range(0f, 100f);
        }

        private void Start()
        {
            inputHandler = AirConsoleInputHandler.Instance;

            // Configure the joint for active ragdoll behavior
            ConfigureJoint();
        }

        private void FixedUpdate()
        {
            // Get input for this limb
            UpdateInputFromHandler();

            // Calculate and apply target rotation
            CalculateTargetRotation();
            ApplyTargetRotation();
        }

        private void OnValidate()
        {
            // Clamp values to sensible ranges (0-15 supports up to 8 players with 2 limbs each)
            limbIndex = Mathf.Clamp(limbIndex, 0, 15);
            drivePositionSpring = Mathf.Max(0, drivePositionSpring);
            drivePositionDamper = Mathf.Max(0, drivePositionDamper);
            driveMaxForce = Mathf.Max(0, driveMaxForce);
            maxRotationAngle = Mathf.Clamp(maxRotationAngle, 0, 180);

            // Update joint settings in editor
            if (joint != null && Application.isPlaying)
            {
                ConfigureJoint();
            }
        }

        #endregion

        #region Joint Configuration

        private void ConfigureJoint()
        {
            if (joint == null) return;

            // Lock ALL motion initially - we control rotation via targetRotation only
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            // Use Limited angular motion for stability
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            // Set angular limits
            SoftJointLimit lowLimit = new SoftJointLimit { limit = -maxRotationAngle };
            SoftJointLimit highLimit = new SoftJointLimit { limit = maxRotationAngle };
            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring { spring = 100f, damper = 10f };

            joint.lowAngularXLimit = lowLimit;
            joint.highAngularXLimit = highLimit;
            joint.angularYLimit = highLimit;
            joint.angularZLimit = highLimit;
            joint.angularXLimitSpring = limitSpring;
            joint.angularYZLimitSpring = limitSpring;

            // Configure Slerp Drive with MUCH lower values for stability
            JointDrive slerpDrive = new JointDrive
            {
                positionSpring = drivePositionSpring,
                positionDamper = drivePositionDamper,
                maximumForce = driveMaxForce
            };

            joint.slerpDrive = slerpDrive;
            joint.rotationDriveMode = RotationDriveMode.Slerp;

            // CRITICAL: Set the target rotation to identity (no rotation offset)
            joint.targetRotation = Quaternion.identity;

            // Make sure connected anchor is set correctly
            joint.autoConfigureConnectedAnchor = true;

            // Enable preprocessing for stability
            joint.enablePreprocessing = true;
        }

        /// <summary>
        /// Dynamically adjust drive strength (useful for "tired" limbs or power-ups)
        /// </summary>
        public void SetDriveStrength(float springMultiplier, float damperMultiplier)
        {
            JointDrive slerpDrive = joint.slerpDrive;
            slerpDrive.positionSpring = drivePositionSpring * springMultiplier;
            slerpDrive.positionDamper = drivePositionDamper * damperMultiplier;
            joint.slerpDrive = slerpDrive;
        }

        #endregion

        #region Input Handling

        private void UpdateInputFromHandler()
        {
            if (inputHandler == null)
            {
                inputHandler = AirConsoleInputHandler.Instance;
                if (inputHandler == null) return;
            }

            // Get input for this specific limb
            currentInput = useSmoothedInput
                ? inputHandler.GetSmoothedLimbInput(limbIndex)
                : inputHandler.GetLimbInput(limbIndex);
        }

        /// <summary>
        /// Manually set input (useful for AI control or scripted sequences)
        /// </summary>
        public void SetManualInput(Vector2 input)
        {
            currentInput = Vector2.ClampMagnitude(input, 1f);
        }

        #endregion

        #region Rotation Calculation

        private void CalculateTargetRotation()
        {
            Vector2 effectiveInput = currentInput;

            // Add idle wobble when no input
            if (enableIdleWobble && currentInput.magnitude < 0.1f)
            {
                float wobbleTime = Time.time * idleWobbleSpeed + idleNoiseOffset;
                effectiveInput = new Vector2(
                    Mathf.PerlinNoise(wobbleTime, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, wobbleTime) - 0.5f
                ) * 2f * (idleWobbleIntensity / maxRotationAngle);
            }

            // Convert Vector2 input to rotation angles
            float xRotation = effectiveInput.x * maxRotationAngle;
            float yRotation = effectiveInput.y * maxRotationAngle;

            // Build target rotation from input
            // The rotation is relative to the joint's initial orientation
            Quaternion xRot = Quaternion.AngleAxis(xRotation, xInputAxis);
            Quaternion yRot = Quaternion.AngleAxis(yRotation, yInputAxis);

            targetRotation = xRot * yRot;

            // Smooth the rotation to prevent jerky movement
            currentSmoothedRotation = Quaternion.Slerp(
                currentSmoothedRotation,
                targetRotation,
                Time.fixedDeltaTime * rotationLerpSpeed
            );
        }

        private void ApplyTargetRotation()
        {
            if (joint == null) return;

            // Set the joint's target rotation
            // Note: ConfigurableJoint uses local space for targetRotation
            joint.targetRotation = currentSmoothedRotation;

            // Propagate to child segments with dampening
            PropagateToChildren();
        }

        private void PropagateToChildren()
        {
            if (childSegments == null) return;

            foreach (var child in childSegments)
            {
                if (child == null) continue;

                // Child receives a dampened version of our rotation
                Vector2 dampenedInput = currentInput * childDampeningFactor;
                child.SetManualInput(dampenedInput);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the current velocity of this limb (for collision detection/feedback)
        /// </summary>
        public Vector3 GetVelocity()
        {
            return limbRigidbody != null ? limbRigidbody.linearVelocity : Vector3.zero;
        }

        /// <summary>
        /// Get the current angular velocity
        /// </summary>
        public Vector3 GetAngularVelocity()
        {
            return limbRigidbody != null ? limbRigidbody.angularVelocity : Vector3.zero;
        }

        /// <summary>
        /// Get the limb index for identification
        /// </summary>
        public int GetLimbIndex() => limbIndex;

        /// <summary>
        /// Get which player controls this limb (0-3)
        /// </summary>
        public int GetPlayerNumber() => limbIndex / 2;

        /// <summary>
        /// Temporarily disable physics control (e.g., when grabbed by environment)
        /// </summary>
        public void SetPhysicsEnabled(bool enabled)
        {
            if (limbRigidbody != null)
            {
                limbRigidbody.isKinematic = !enabled;
            }
        }

        /// <summary>
        /// Apply an impulse force to the limb (for impacts, explosions, etc.)
        /// </summary>
        public void ApplyImpulse(Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            if (limbRigidbody != null)
            {
                limbRigidbody.AddForce(force, mode);
            }
        }

        #endregion

        #region Debug Visualization

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Draw current input direction
            Vector3 inputDir = new Vector3(currentInput.x, 0, currentInput.y);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, inputDir * 0.5f);

            // Draw target rotation axis
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.rotation * (currentSmoothedRotation * Vector3.up) * 0.3f);

            // Draw limb index label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.2f, $"Limb {limbIndex} (P{GetPlayerNumber() + 1})");
        }
#endif

        #endregion
    }
}

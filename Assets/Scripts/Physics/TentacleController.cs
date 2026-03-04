using UnityEngine;
using Octo.Input;

namespace Octo.Physics
{
    /// <summary>
    /// Octodad-style limb controller.
    /// 
    /// Instead of rotating individual joints, this creates a TARGET POINT
    /// that the limb tip tries to reach. The physics chain naturally follows.
    /// 
    /// SETUP:
    /// - Attach to the ROOT bone of each tentacle (e.g., Arm01_L_J)
    /// - Assign the TIP transform (the last bone in the chain)
    /// - The system will make the tip "reach" toward the target
    /// </summary>
    public class TentacleController : MonoBehaviour
    {
        [Header("Limb Identity")]
        [Tooltip("Global limb index (0-5). Player 1: 0,1 | Player 2: 2,3 | Player 3: 4,5")]
        [SerializeField] private int limbIndex = 0;

        [Header("Limb Setup")]
        [Tooltip("The tip/end of the tentacle (last bone in chain)")]
        [SerializeField] private Transform tipBone;
        [Tooltip("All Rigidbodies in this limb chain (auto-detected if empty)")]
        [SerializeField] private Rigidbody[] limbSegments;

        [Header("Movement Settings")]
        [Tooltip("How far the target point extends from the base when joystick is pushed")]
        [SerializeField] private float reachDistance = 2f;
        [Tooltip("Force applied to pull tip toward target")]
        [SerializeField] private float pullForce = 50f;
        [Tooltip("How much the force reduces per segment from tip to base")]
        [SerializeField] private float forceDropoff = 0.5f;

        [Header("Stability")]
        [Tooltip("Damping applied to all segments")]
        [SerializeField] private float segmentDamping = 5f;
        [Tooltip("Return force when no input (pulls limb back to rest)")]
        [SerializeField] private float returnForce = 10f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color targetColor = Color.cyan;

        // Runtime
        private Vector3 targetWorldPosition;
        private Vector3 restPosition;
        private Vector2 currentInput;
        private AirConsoleInputHandler inputHandler;
        private Transform baseTransform;
        private bool isPhysicsActive = false; // Start disabled, AnimationPhysicsBlender will enable

        private void Start()
        {
            inputHandler = AirConsoleInputHandler.Instance;
            baseTransform = transform;

            // Auto-detect tip if not assigned
            if (tipBone == null)
            {
                tipBone = FindTipBone(transform);
            }

            // Auto-detect limb segments if not assigned
            if (limbSegments == null || limbSegments.Length == 0)
            {
                limbSegments = GetComponentsInChildren<Rigidbody>();
            }

            // Store rest position (where tip naturally hangs)
            if (tipBone != null)
            {
                restPosition = tipBone.position;
            }

            // Don't configure segments here - let AnimationPhysicsBlender control them
            // ConfigureSegments();
        }

        private Transform FindTipBone(Transform parent)
        {
            // Recursively find the deepest child (the tip)
            if (parent.childCount == 0)
                return parent;

            Transform deepest = parent;
            int maxDepth = 0;

            foreach (Transform child in parent)
            {
                Transform tip = FindTipBone(child);
                int depth = GetDepth(tip, parent);
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                    deepest = tip;
                }
            }

            return deepest;
        }

        private int GetDepth(Transform child, Transform root)
        {
            int depth = 0;
            Transform current = child;
            while (current != root && current != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        private void ConfigureSegments()
        {
            foreach (var rb in limbSegments)
            {
                rb.linearDamping = segmentDamping;
                rb.angularDamping = segmentDamping;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        private void Update()
        {
            // Skip if physics control is disabled
            if (!isPhysicsActive) return;

            // Get input
            if (inputHandler != null)
            {
                currentInput = inputHandler.GetSmoothedLimbInput(limbIndex);
            }

            // Calculate target position based on input
            CalculateTargetPosition();
        }

        private void FixedUpdate()
        {
            // Skip if physics control is disabled
            if (!isPhysicsActive) return;

            // Apply forces to reach target
            ApplyReachForces();
        }

        /// <summary>
        /// Enable or disable physics control on this tentacle.
        /// Called by AnimationPhysicsBlender when switching between animation and physics.
        /// </summary>
        public void SetPhysicsActive(bool active)
        {
            isPhysicsActive = active;

            // When activating physics, ensure segments are configured
            if (active)
            {
                ConfigureSegments();
            }
        }

        /// <summary>
        /// Check if there's any meaningful input on this tentacle.
        /// Used by AnimationPhysicsBlender to determine when to switch to physics.
        /// </summary>
        public bool HasInput()
        {
            return currentInput.magnitude > 0.1f;
        }

        private void CalculateTargetPosition()
        {
            if (currentInput.magnitude < 0.1f)
            {
                // No input - target returns to rest
                targetWorldPosition = restPosition;
                return;
            }

            // Convert 2D joystick to 3D direction
            // X = left/right, Y = forward/back (relative to octopus facing)
            Vector3 inputDirection = new Vector3(currentInput.x, 0, currentInput.y);

            // Transform to world space based on base orientation
            // Use the octopus body's forward/right, not the individual limb
            Transform bodyReference = baseTransform.root;
            Vector3 worldDirection = bodyReference.TransformDirection(inputDirection);

            // Add some upward bias so tentacles reach up/out
            worldDirection += Vector3.up * 0.3f;
            worldDirection.Normalize();

            // Calculate target position
            targetWorldPosition = baseTransform.position + worldDirection * reachDistance * currentInput.magnitude;
        }

        private void ApplyReachForces()
        {
            if (limbSegments == null || limbSegments.Length == 0) return;

            // Apply force to each segment, strongest at tip
            for (int i = limbSegments.Length - 1; i >= 0; i--)
            {
                Rigidbody segment = limbSegments[i];
                if (segment == null) continue;

                // Calculate force multiplier (1.0 at tip, reduces toward base)
                float distanceFromTip = limbSegments.Length - 1 - i;
                float forceMultiplier = Mathf.Pow(forceDropoff, distanceFromTip);

                // Direction from segment to target
                Vector3 toTarget = targetWorldPosition - segment.position;
                float distance = toTarget.magnitude;

                if (currentInput.magnitude > 0.1f)
                {
                    // Active input - pull toward target
                    Vector3 force = toTarget.normalized * pullForce * forceMultiplier;

                    // Reduce force as we get closer (prevents overshoot)
                    force *= Mathf.Clamp01(distance / reachDistance);

                    segment.AddForce(force, ForceMode.Force);
                }
                else
                {
                    // No input - gentle return force
                    Vector3 returnDir = (restPosition - segment.position).normalized;
                    segment.AddForce(returnDir * returnForce * forceMultiplier, ForceMode.Force);
                }
            }
        }

        /// <summary>
        /// Set the limb index at runtime
        /// </summary>
        public void SetLimbIndex(int index)
        {
            limbIndex = index;
        }

        /// <summary>
        /// Get current target position (for UI feedback)
        /// </summary>
        public Vector3 GetTargetPosition() => targetWorldPosition;

        /// <summary>
        /// Get which player controls this limb
        /// </summary>
        public int GetPlayerNumber() => limbIndex / 2;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw target position
            Gizmos.color = targetColor;
            Gizmos.DrawWireSphere(targetWorldPosition, 0.1f);

            // Draw line from tip to target
            if (tipBone != null)
            {
                Gizmos.DrawLine(tipBone.position, targetWorldPosition);
            }

            // Draw rest position
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(restPosition, 0.05f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Draw reach radius
            Gizmos.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0.2f);
            Gizmos.DrawWireSphere(transform.position, reachDistance);

            // Label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, $"Limb {limbIndex} (P{GetPlayerNumber() + 1})");
        }
#endif
    }
}

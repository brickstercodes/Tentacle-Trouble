using UnityEngine;

namespace Octo.Animation
{
    /// <summary>
    /// Blends between idle animation and physics-based control.
    /// 
    /// When no player input: Plays idle animation
    /// When player inputs: Smoothly transitions to physics control
    /// 
    /// SETUP:
    /// 1. Attach to the Octopus root (same object with Animator)
    /// 2. Assign the Animator component
    /// 3. Set the idle animation clip name
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationPhysicsBlender : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Animator animator;

        [Header("Animation Settings")]
        [Tooltip("Name of the idle animation state in the Animator")]
        [SerializeField] private string idleStateName = "IdleChair";
        [Tooltip("Layer index for the idle animation")]
        [SerializeField] private int animationLayer = 0;

        [Header("Blend Settings")]
        [Tooltip("How fast to blend from animation to physics (seconds)")]
        [SerializeField] private float blendToPhysicsTime = 0.3f;
        [Tooltip("How fast to blend from physics to animation (seconds)")]
        [SerializeField] private float blendToAnimationTime = 0.5f;
        [Tooltip("Time without input before returning to idle animation")]
        [SerializeField] private float idleTimeout = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Current blend state (0 = full animation, 1 = full physics)
        private float physicsBlend = 0f;
        private float lastInputTime;
        private bool hasAnyInput;

        // Rigidbodies to control
        private Rigidbody[] limbRigidbodies;

        // TentacleControllers to enable/disable
        private Physics.TentacleController[] tentacleControllers;

        // Cached input handler reference
        private Input.AirConsoleInputHandler inputHandler;

        private void Start()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            // Find all rigidbodies in the rig (limbs)
            limbRigidbodies = GetComponentsInChildren<Rigidbody>();

            // Find all tentacle controllers
            tentacleControllers = GetComponentsInChildren<Physics.TentacleController>();

            // Start in animation mode - FORCE all limbs to kinematic
            physicsBlend = 0f;
            ForceAnimationMode();

            // Play idle animation
            if (animator != null && !string.IsNullOrEmpty(idleStateName))
            {
                animator.Play(idleStateName, animationLayer, 0f);
            }

            Debug.Log($"[AnimationPhysicsBlender] Initialized with {limbRigidbodies?.Length ?? 0} rigidbodies, " +
                      $"{tentacleControllers?.Length ?? 0} tentacle controllers");
        }

        /// <summary>
        /// Force everything into animation mode (useful at start or for cutscenes)
        /// </summary>
        private void ForceAnimationMode()
        {
            // Disable all physics on all limbs
            if (limbRigidbodies != null)
            {
                foreach (var rb in limbRigidbodies)
                {
                    if (rb == null) continue;
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // Disable tentacle controllers
            SetTentacleControllersActive(false);
        }

        private void Update()
        {
            // Get input handler
            if (inputHandler == null)
            {
                inputHandler = Input.AirConsoleInputHandler.Instance;
            }

            // Check if any player has input
            CheckForInput();

            // Update blend state
            UpdateBlend();

            // Apply blend to animator and physics
            ApplyBlend();
        }

        private void CheckForInput()
        {
            hasAnyInput = false;

            // Try AirConsole input first
            if (inputHandler != null)
            {
                int totalLimbs = inputHandler.GetTotalLimbCount();
                for (int i = 0; i < totalLimbs; i++)
                {
                    if (inputHandler.IsLimbActive(i))
                    {
                        hasAnyInput = true;
                        lastInputTime = Time.time;
                        return;
                    }
                }
            }

            // Keyboard fallback for editor testing
#if UNITY_EDITOR
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.A) ||
                UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.D) ||
                UnityEngine.Input.GetKey(KeyCode.I) || UnityEngine.Input.GetKey(KeyCode.J) ||
                UnityEngine.Input.GetKey(KeyCode.K) || UnityEngine.Input.GetKey(KeyCode.L) ||
                UnityEngine.Input.GetKey(KeyCode.UpArrow) || UnityEngine.Input.GetKey(KeyCode.DownArrow) ||
                UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            {
                hasAnyInput = true;
                lastInputTime = Time.time;
            }
#endif
        }

        private void UpdateBlend()
        {
            float targetBlend;
            float blendSpeed;

            if (hasAnyInput)
            {
                // Blend toward physics
                targetBlend = 1f;
                blendSpeed = 1f / blendToPhysicsTime;
            }
            else if (Time.time - lastInputTime > idleTimeout)
            {
                // No input for a while, blend back to animation
                targetBlend = 0f;
                blendSpeed = 1f / blendToAnimationTime;
            }
            else
            {
                // Still within timeout, hold current state
                return;
            }

            physicsBlend = Mathf.MoveTowards(physicsBlend, targetBlend, blendSpeed * Time.deltaTime);
        }

        private void ApplyBlend()
        {
            if (animator == null) return;

            // Animator weight (inverse of physics blend)
            float animWeight = 1f - physicsBlend;

            // Set animator layer weight
            if (animator.layerCount > animationLayer)
            {
                animator.SetLayerWeight(animationLayer, animWeight);
            }

            // Control rigidbodies
            // When fully animated (blend=0): kinematic
            // When physics (blend=1): non-kinematic
            bool shouldBeKinematic = physicsBlend < 0.5f;

            // Only toggle when crossing the threshold to avoid jitter
            if (limbRigidbodies != null && limbRigidbodies.Length > 0)
            {
                bool currentlyKinematic = limbRigidbodies[0].isKinematic;
                if (currentlyKinematic != shouldBeKinematic)
                {
                    SetRigidbodiesKinematic(shouldBeKinematic);
                    SetTentacleControllersActive(!shouldBeKinematic);
                }
            }

            // When transitioning, update animator speed
            // Slow down animation as we approach physics mode
            animator.speed = Mathf.Lerp(1f, 0f, physicsBlend);
        }

        private void SetRigidbodiesKinematic(bool kinematic)
        {
            if (limbRigidbodies == null) return;

            foreach (var rb in limbRigidbodies)
            {
                if (rb == null) continue;

                // Skip the main body (Hips, Root, Main) - they should stay kinematic
                string boneName = rb.transform.name.ToLower();
                if (boneName.Contains("hips") || boneName.Contains("root") || boneName.Contains("main"))
                {
                    rb.isKinematic = true; // Always keep body kinematic
                    continue;
                }

                rb.isKinematic = kinematic;

                // Always reset velocities when switching modes
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void SetTentacleControllersActive(bool active)
        {
            if (tentacleControllers == null) return;

            foreach (var controller in tentacleControllers)
            {
                if (controller != null)
                {
                    controller.SetPhysicsActive(active);
                }
            }
        }

        /// <summary>
        /// Force a specific blend value (for cutscenes, etc.)
        /// </summary>
        public void SetBlend(float blend)
        {
            physicsBlend = Mathf.Clamp01(blend);
        }

        /// <summary>
        /// Get current blend value
        /// </summary>
        public float GetBlend() => physicsBlend;

        /// <summary>
        /// Is the octopus currently in physics mode?
        /// </summary>
        public bool IsPhysicsActive() => physicsBlend > 0.5f;

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 100));
            GUILayout.Label($"Physics Blend: {physicsBlend:F2}");
            GUILayout.Label($"Has Input: {hasAnyInput}");
            GUILayout.Label($"Mode: {(IsPhysicsActive() ? "PHYSICS" : "ANIMATION")}");
            GUILayout.EndArea();
        }
#endif
    }
}

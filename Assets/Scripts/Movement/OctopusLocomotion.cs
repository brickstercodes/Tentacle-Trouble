using UnityEngine;
using Octo.Input;

namespace Octo.Movement
{
    /// <summary>
    /// Controls octopus locomotion based on COMBINED player inputs.
    /// 
    /// The octopus only moves when players coordinate their inputs!
    /// - All 3 players push right → Octopus moves right
    /// - Conflicting inputs → Octopus struggles/wobbles but doesn't move much
    /// 
    /// SETUP:
    /// - Attach to ANY object (like GameManager)
    /// - Assign the Move Target (Root_J) in inspector
    /// </summary>
    public class OctopusLocomotion : MonoBehaviour
    {
        private const int MAX_PLAYERS = 3;
        [Header("Target")]
        [Tooltip("The transform to move (assign the octopus parent, NOT Root_J)")]
        [SerializeField] private Transform moveTarget;

        [Header("Movement Settings")]
        [Tooltip("Maximum movement speed when both players agree")]
        [SerializeField] private float maxSpeed = 3f;
        [Tooltip("How fast the octopus accelerates")]
        [SerializeField] private float acceleration = 8f;
        [Tooltip("How fast the octopus slows down when no input")]
        [SerializeField] private float deceleration = 5f;
        [Tooltip("Minimum agreement (0-1) needed to move. 0 = any input moves, 1 = perfect agreement needed")]
        [SerializeField] private float agreementThreshold = 0.3f;

        [Header("Struggle Effect")]
        [Tooltip("How much the octopus wobbles when players disagree")]
        [SerializeField] private float struggleWobble = 0.5f;
        [Tooltip("Speed of the struggle wobble")]
        [SerializeField] private float struggleSpeed = 10f;

        [Header("Ground Check")]
        [Tooltip("Layer mask for ground detection")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [Tooltip("How far down to check for ground")]
        [SerializeField] private float groundCheckDistance = 0.5f;
        [Tooltip("Shifts the CharacterController capsule up (+) or down (\u2212) relative to the auto-calculated mesh center. " +
                 "Decrease (negative) to sink the octopus closer to the ground. In LOCAL space \u2014 " +
                 "with a 90x prefab, 0.01 \u2248 0.9 world units.")]
        [SerializeField] private float groundOffset = 0f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Input from each player (computed average of their two limbs)
        private Vector2[] playerInputs;

        // AirConsole input handler
        private AirConsoleInputHandler inputHandler;

        // Current velocity
        private Vector3 currentVelocity;

        // Agreement level (0 = opposite, 1 = same direction)
        private float agreementLevel;

        // Anchor: when true, octopus cannot move (P3 ability)
        private bool isAnchored;

        // Accumulated vertical velocity for CharacterController gravity
        private float verticalVelocity;

        /// <summary>The transform being moved by locomotion (authoritative body position).</summary>
        public Transform MoveTarget => moveTarget;

        // Components
        private Rigidbody rb;
        private CharacterController characterController;

        private void Start()
        {
            playerInputs = new Vector2[MAX_PLAYERS];

            if (moveTarget == null)
            {
                // Find any skeleton root bone, then walk up to the top-level scene object
                string[] boneNames = { "Root_J", "Main_Root" };
                var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var t in allTransforms)
                {
                    foreach (string boneName in boneNames)
                    {
                        if (t.name == boneName)
                        {
                            // Walk up to the scene root (top-level parent)
                            Transform target = t;
                            while (target.parent != null)
                                target = target.parent;
                            moveTarget = target;
                            Debug.Log($"[OctopusLocomotion] Auto-found: {moveTarget.name} (from bone {boneName})");
                            break;
                        }
                    }
                    if (moveTarget != null) break;
                }
            }

            // Guard against a skeleton bone being assigned manually in the Inspector.
            // Animated bones must NOT have a dynamic Rigidbody — the Animator and physics
            // would fight each other every frame, causing the bone to fall through terrain.
            // If the assigned target has no physics component of its own AND is not the scene
            // root, walk up to the top-level object so physics lives on the prefab shell.
            if (moveTarget != null && moveTarget.parent != null)
            {
                bool hasPhysics = moveTarget.GetComponent<Rigidbody>() != null
                               || moveTarget.GetComponent<CharacterController>() != null;
                if (!hasPhysics)
                {
                    Transform root = moveTarget;
                    while (root.parent != null)
                        root = root.parent;
                    Debug.LogWarning($"[OctopusLocomotion] '{moveTarget.name}' is not a scene root and has no physics component " +
                                     $"— it may be an animated bone. Correcting to scene root '{root.name}' to avoid Animator/Rigidbody conflicts.");
                    moveTarget = root;
                }
            }

            if (moveTarget == null)
            {
                Debug.LogError("[OctopusLocomotion] No move target! Assign the octopus top-level object.");
                return;
            }

            rb = moveTarget.GetComponent<Rigidbody>();
            characterController = moveTarget.GetComponent<CharacterController>();

            if (rb == null && characterController == null)
            {
                // Prefer CharacterController over Rigidbody for player character movement.
                // A non-kinematic Rigidbody fights direct transform rotation (used by the
                // camera controller for P2's right-stick octopus rotation), causing stutter.
                // CharacterController handles collision natively without joining the physics
                // simulation, so the camera can freely rotate the root transform.
                characterController = moveTarget.gameObject.AddComponent<CharacterController>();

                // Size the CC properly — values are LOCAL space, so divide by lossyScale
                // to get the right world-space footprint on a 90x scaled prefab.
                Vector3 ls2 = moveTarget.lossyScale;
                float sxz2 = Mathf.Max(Mathf.Abs(ls2.x), Mathf.Abs(ls2.z));
                float sy2  = Mathf.Abs(ls2.y);
                if (sxz2 < 0.0001f) sxz2 = 1f;
                if (sy2  < 0.0001f) sy2  = 1f;

                var smr2 = moveTarget.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr2 != null)
                {
                    Bounds b2 = smr2.bounds;
                    float r2 = Mathf.Max(b2.extents.x, b2.extents.z) * 0.5f / sxz2;
                    float h2 = b2.size.y / sy2;
                    characterController.radius     = r2;
                    characterController.height     = h2;
                    Vector3 cc2center = moveTarget.InverseTransformPoint(b2.center);
                    cc2center.y += groundOffset;
                    characterController.center     = cc2center;
                    characterController.stepOffset = 0.3f / sy2;   // ~0.3 world-unit step
                    characterController.skinWidth  = r2 * 0.1f;
                    Debug.Log($"[OctopusLocomotion] Auto-added & sized CharacterController (lossyScale={ls2}): r={r2:F4} h={h2:F4}");
                }
                else
                {
                    characterController.radius     = 0.5f  / sxz2;
                    characterController.height     = 1.5f  / sy2;
                    characterController.stepOffset = 0.3f  / sy2;
                    characterController.skinWidth  = (0.5f / sxz2) * 0.1f;
                    Debug.Log($"[OctopusLocomotion] Auto-added CharacterController with fallback size (lossyScale={ls2})");
                }
            }

            if (rb != null)
            {
                // If a Rigidbody was added manually: allow Y rotation so the camera
                // controller can freely rotate the octopus (right stick, P2).
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                if (rb.isKinematic) rb.isKinematic = false;
                Debug.Log("[OctopusLocomotion] Rigidbody configured (Y rotation unfrozen for camera control).");
            }
            if (characterController != null)
            {
                Debug.Log($"[OctopusLocomotion] CharacterController ready on '{moveTarget.name}'.");
            }

            // Auto-size the CapsuleCollider on moveTarget if it exists and is tiny
            var cap = moveTarget.GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                // smr.bounds is in WORLD space; collider values are in LOCAL space.
                // Divide by lossyScale so the world-space size is correct even when
                // the transform is scaled (e.g. 90x on the prefab root).
                Vector3 ls = moveTarget.lossyScale;
                float scaleXZ = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
                float scaleY  = Mathf.Abs(ls.y);
                if (scaleXZ < 0.0001f) scaleXZ = 1f;
                if (scaleY  < 0.0001f) scaleY  = 1f;

                var smr = moveTarget.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    Bounds b = smr.bounds;
                    cap.radius = Mathf.Max(b.extents.x, b.extents.z) * 0.5f / scaleXZ;
                    cap.height = b.size.y / scaleY;
                    cap.center = moveTarget.InverseTransformPoint(b.center); // already handles scale
                    Debug.Log($"[OctopusLocomotion] Auto-sized CapsuleCollider (lossyScale={ls}): r={cap.radius:F4} h={cap.height:F4}");
                }
                else
                {
                    // Fallback: 0.5 m radius, 1.5 m height in world space
                    cap.radius = 0.5f / scaleXZ;
                    cap.height = 1.5f / scaleY;
                    Debug.Log($"[OctopusLocomotion] CapsuleCollider default world size (lossyScale={ls}): r={cap.radius:F4} h={cap.height:F4}");
                }
            }

            Debug.Log($"[OctopusLocomotion] Ready - moving: {moveTarget.name}");
        }

        private void Update()
        {
            if (moveTarget == null) return;
            if (isAnchored) return;

            // Gather inputs and compute velocity every frame for responsiveness
            GetInputs();
            currentVelocity = CalculateMovement();
        }

        private void FixedUpdate()
        {
            if (moveTarget == null) return;
            if (isAnchored) return;

            // Apply movement in FixedUpdate so Rigidbody collision detection works correctly
            ApplyMovement(currentVelocity);
        }

        private void GetInputs()
        {
            // Get input handler if not cached
            if (inputHandler == null)
            {
                inputHandler = AirConsoleInputHandler.Instance;
            }

            // Get input from each player (average of their two limbs)
            // AirConsoleInputHandler handles keyboard fallback in editor
            for (int p = 0; p < MAX_PLAYERS; p++)
            {
                playerInputs[p] = Vector2.zero;

                // Skip P2 (player index 1) — their joysticks control the camera, not movement
                if (p == 1) continue;

                if (inputHandler != null)
                {
                    // Each player controls 2 limbs (2*p and 2*p+1)
                    Vector2 limb1 = inputHandler.GetLimbInput(p * 2);
                    Vector2 limb2 = inputHandler.GetLimbInput(p * 2 + 1);

                    // Average the two limb inputs for movement direction
                    playerInputs[p] = (limb1 + limb2) / 2f;
                }
            }
        }

        private Vector3 CalculateMovement()
        {
            // Count active players and calculate combined input
            int activePlayers = 0;
            Vector2 combinedInput = Vector2.zero;

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (playerInputs[i].magnitude > 0.1f)
                {
                    activePlayers++;
                    combinedInput += playerInputs[i];
                }
            }

            // If no input from any player, decelerate
            if (activePlayers == 0)
            {
                agreementLevel = 0f;
                currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, Time.deltaTime * deceleration);
                return currentVelocity;
            }

            // Average the combined input
            combinedInput /= activePlayers;

            // Calculate agreement level based on all active players
            if (activePlayers >= 2)
            {
                // Calculate pairwise agreement (average dot product)
                float totalAgreement = 0f;
                int pairs = 0;

                for (int i = 0; i < MAX_PLAYERS; i++)
                {
                    if (playerInputs[i].magnitude < 0.1f) continue;

                    for (int j = i + 1; j < MAX_PLAYERS; j++)
                    {
                        if (playerInputs[j].magnitude < 0.1f) continue;

                        float dot = Vector2.Dot(playerInputs[i].normalized, playerInputs[j].normalized);
                        totalAgreement += (dot + 1f) / 2f; // Remap [-1,1] to [0,1]
                        pairs++;
                    }
                }

                agreementLevel = pairs > 0 ? totalAgreement / pairs : 0.5f;
            }
            else
            {
                // Only one player inputting - partial movement
                agreementLevel = 0.5f;
            }

            // Movement effectiveness based on agreement
            float effectiveness = Mathf.Clamp01((agreementLevel - agreementThreshold) / (1f - agreementThreshold));

            // Camera-relative movement: joystick up = camera forward, right = camera right
            Camera cam = Camera.main;
            Vector3 targetDirection;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();
                targetDirection = camForward * combinedInput.y + camRight * combinedInput.x;
            }
            else
            {
                targetDirection = new Vector3(combinedInput.x, 0f, combinedInput.y);
            }
            Vector3 targetVelocity = targetDirection * maxSpeed * effectiveness;

            // Add struggle wobble when players disagree
            if (agreementLevel < 0.7f && activePlayers >= 1)
            {
                float wobble = Mathf.Sin(Time.time * struggleSpeed) * struggleWobble * (1f - agreementLevel);
                targetVelocity += new Vector3(
                    Mathf.Cos(Time.time * struggleSpeed * 1.3f) * wobble,
                    0,
                    Mathf.Sin(Time.time * struggleSpeed * 0.7f) * wobble
                );
            }

            // Smooth acceleration
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime * acceleration);

            return currentVelocity;
        }

        private void ApplyMovement(Vector3 movement)
        {
            if (moveTarget == null) return;

            if (characterController != null)
            {
                // Accumulate gravity; reset when grounded so it doesn't stack up
                if (characterController.isGrounded)
                    verticalVelocity = -2f; // small constant keeps CC pressed to ground
                else
                    verticalVelocity += UnityEngine.Physics.gravity.y * Time.deltaTime;

                Vector3 motion = new Vector3(movement.x, verticalVelocity, movement.z);
                characterController.Move(motion * Time.deltaTime);
            }
            else if (rb != null)
            {
                // Set only the horizontal velocity — preserve the Rigidbody's Y velocity
                // so gravity and ground collision work correctly.
                Vector3 vel = rb.linearVelocity;
                vel.x = movement.x;
                vel.z = movement.z;
                // Y is left alone: gravity pulls down, collider prevents falling through ground
                rb.linearVelocity = vel;
            }
            else
            {
                moveTarget.position += movement * Time.deltaTime;
            }
        }

        /// <summary>
        /// Get the current agreement level (for head wobble, etc.)
        /// </summary>
        public float GetAgreementLevel() => agreementLevel;

        /// <summary>Set by OctoGrabSystem when P3 holds the anchor button.</summary>
        public void SetAnchored(bool anchored) => isAnchored = anchored;
        public bool IsAnchored => isAnchored;

        /// <summary>
        /// Get the current velocity (for head physics, etc.)
        /// </summary>
        public Vector3 GetVelocity() => currentVelocity;

        /// <summary>
        /// Get combined input direction from all active players
        /// </summary>
        public Vector2 GetCombinedInput()
        {
            Vector2 combined = Vector2.zero;
            int activePlayers = 0;

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (playerInputs[i].magnitude > 0.1f)
                {
                    combined += playerInputs[i];
                    activePlayers++;
                }
            }

            return activePlayers > 0 ? combined / activePlayers : Vector2.zero;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 120, 250, 180));
            GUILayout.Box("Octopus Movement");
            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (playerInputs != null && i < playerInputs.Length)
                {
                    GUILayout.Label($"Player {i + 1}: {playerInputs[i]}");
                }
            }
            GUILayout.Label($"Agreement: {agreementLevel:P0}");
            GUILayout.Label($"Speed: {currentVelocity.magnitude:F1} m/s");
            GUILayout.EndArea();
        }
#endif
    }
}

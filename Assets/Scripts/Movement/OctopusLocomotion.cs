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

            if (moveTarget == null)
            {
                Debug.LogError("[OctopusLocomotion] No move target! Assign the octopus top-level object.");
                return;
            }

            rb = moveTarget.GetComponent<Rigidbody>();
            characterController = moveTarget.GetComponent<CharacterController>();

            if (rb != null)
            {
                rb.freezeRotation = true;
                rb.useGravity = true;
            }

            Debug.Log($"[OctopusLocomotion] Ready - moving: {moveTarget.name}");
        }

        private void Update()
        {
            if (moveTarget == null) return;

            // Get inputs from both "players"
            GetInputs();

            // Calculate combined movement
            Vector3 movement = CalculateMovement();

            // Apply movement
            ApplyMovement(movement);
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

            // Target velocity
            Vector3 targetDirection = new Vector3(combinedInput.x, 0, combinedInput.y);
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
                // Use CharacterController
                characterController.Move(movement * Time.deltaTime + Vector3.down * 9.81f * Time.deltaTime);
            }
            else if (rb != null)
            {
                // Use Rigidbody (in FixedUpdate would be better, but this works for testing)
                Vector3 newPos = rb.position + movement * Time.deltaTime;
                rb.MovePosition(newPos);
            }
            else
            {
                moveTarget.position += movement * Time.deltaTime;
            }

            // Rotate to face movement direction (optional, smooth rotation)
            // Don't rotate the skeleton root - it can mess up the rig
            // Instead, just move without rotation for now
        }

        /// <summary>
        /// Get the current agreement level (for head wobble, etc.)
        /// </summary>
        public float GetAgreementLevel() => agreementLevel;

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

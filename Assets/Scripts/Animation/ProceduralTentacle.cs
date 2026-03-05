using UnityEngine;
using Octo.Input;

namespace Octo.Animation
{
    /// <summary>
    /// Octodad-style tentacle controller using PROCEDURAL ANIMATION.
    /// No physics, no rigidbodies, no joints - just bone rotations!
    /// 
    /// This works BY ADDING rotations on top of whatever the Animator is doing.
    /// So idle animation plays, and joystick input rotates the tentacle further.
    /// </summary>
    public class ProceduralTentacle : MonoBehaviour
    {
        [Header("Limb Identity")]
        [Tooltip("Global limb index (0-5). Player 1: 0,1 | Player 2: 2,3 | Player 3: 4,5")]
        [SerializeField] private int limbIndex = 0;

        [Tooltip("If true, this limb ignores joystick input (used for camera-controlled player's arms)")]
        [SerializeField] private bool inputDisabled = false;

        [Header("Movement Settings")]
        [Tooltip("How much each segment rotates (degrees) at full joystick")]
        [SerializeField] private float maxRotationPerSegment = 30f;
        [Tooltip("How fast the rotation responds to input")]
        [SerializeField] private float rotationSpeed = 8f;
        [Tooltip("How fast the tentacle returns to rest when no input")]
        [SerializeField] private float returnSpeed = 5f;
        [Tooltip("How much rotation reduces per segment (tip moves more than base)")]
        [SerializeField] private float segmentFalloff = 0.7f;

        [Header("Wobble (Octodad feel)")]
        [Tooltip("Add some wobbly movement for that floppy feel")]
        [SerializeField] private float wobbleAmount = 5f;
        [Tooltip("Speed of the wobble")]
        [SerializeField] private float wobbleSpeed = 3f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // All bone transforms in this tentacle chain
        private Transform[] boneChain;

        // Current and target rotations for smooth movement
        private Vector2 currentInput;
        private Vector2 smoothedInput;

        // Reference to input handler
        private AirConsoleInputHandler inputHandler;

        // Store original local rotations (from animation)
        private Quaternion[] baseRotations;

        // Wobble offset
        private float wobbleOffset;

        private void Start()
        {
            // Collect all bones in the chain (this transform and all children)
            var bones = new System.Collections.Generic.List<Transform>();
            CollectBones(transform, bones);
            boneChain = bones.ToArray();

            // Initialize base rotations array
            baseRotations = new Quaternion[boneChain.Length];

            // Random wobble offset so tentacles don't all move in sync
            wobbleOffset = Random.Range(0f, Mathf.PI * 2f);

            Debug.Log($"[ProceduralTentacle] Limb {limbIndex}: {boneChain.Length} bones");
        }

        private void CollectBones(Transform parent, System.Collections.Generic.List<Transform> list)
        {
            list.Add(parent);
            foreach (Transform child in parent)
            {
                CollectBones(child, list);
            }
        }

        private void LateUpdate()
        {
            // Get input handler
            if (inputHandler == null)
            {
                inputHandler = AirConsoleInputHandler.Instance;
            }

            // Get input for this limb
            GetInput();

            // Smooth the input - faster response when there's input, slower return to rest
            float speed = currentInput.magnitude > 0.1f ? rotationSpeed : returnSpeed;
            smoothedInput = Vector2.Lerp(smoothedInput, currentInput, Time.deltaTime * speed);

            // Snap to zero when very close (prevents tiny lingering rotations)
            if (currentInput.magnitude < 0.1f && smoothedInput.magnitude < 0.01f)
            {
                smoothedInput = Vector2.zero;
            }

            // Apply rotations to each bone
            ApplyRotations();
        }

        private void GetInput()
        {
            // When input is disabled, use externally-set input (from gesture system)
            if (inputDisabled) return;

            currentInput = Vector2.zero;

            // Try AirConsole input
            if (inputHandler != null)
            {
                currentInput = inputHandler.GetLimbInput(limbIndex);
            }

            // Keyboard fallback for editor testing
#if UNITY_EDITOR
            if (currentInput.magnitude < 0.1f)
            {
                currentInput = GetKeyboardInput();
            }
#endif
        }

        private Vector2 GetKeyboardInput()
        {
            Vector2 input = Vector2.zero;

            // Keyboard mapping matches AirConsoleInputHandler:
            // Player 1: Limbs 0,1 = WASD + Arrows
            // Player 2: Limbs 2,3 = IJKL + Numpad
            // Player 3: Limbs 4,5 = No keyboard (use phones)
            switch (limbIndex)
            {
                case 0: // Player 1 Left - WASD
                    if (UnityEngine.Input.GetKey(KeyCode.W)) input.y += 1;
                    if (UnityEngine.Input.GetKey(KeyCode.S)) input.y -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.A)) input.x -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.D)) input.x += 1;
                    break;
                case 1: // Player 1 Right - Arrow keys
                    if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) input.y += 1;
                    if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) input.y -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) input.x += 1;
                    break;
                case 2: // Player 2 Left - IJKL
                    if (UnityEngine.Input.GetKey(KeyCode.I)) input.y += 1;
                    if (UnityEngine.Input.GetKey(KeyCode.K)) input.y -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.J)) input.x -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.L)) input.x += 1;
                    break;
                case 3: // Player 2 Right - Numpad
                    if (UnityEngine.Input.GetKey(KeyCode.Keypad8)) input.y += 1;
                    if (UnityEngine.Input.GetKey(KeyCode.Keypad2)) input.y -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.Keypad4)) input.x -= 1;
                    if (UnityEngine.Input.GetKey(KeyCode.Keypad6)) input.x += 1;
                    break;
                    // Limbs 4,5 (Player 3) - no keyboard, use phones for testing
            }

            return input.normalized;
        }

        private void ApplyRotations()
        {
            if (boneChain == null || boneChain.Length == 0) return;

            float time = Time.time * wobbleSpeed + wobbleOffset;

            for (int i = 0; i < boneChain.Length; i++)
            {
                if (boneChain[i] == null) continue;

                // Calculate how much this segment should rotate
                // Later segments (closer to tip) rotate more
                float segmentInfluence = 1f;
                for (int j = 0; j < i; j++)
                {
                    segmentInfluence *= segmentFalloff;
                }
                // Invert so tip moves more
                segmentInfluence = 1f - segmentInfluence + 0.3f; // Min 0.3 influence

                // Calculate rotation from input
                // X input rotates around local Z (side to side)
                // Y input rotates around local X (forward/back)
                float xRot = smoothedInput.y * maxRotationPerSegment * segmentInfluence;
                float zRot = -smoothedInput.x * maxRotationPerSegment * segmentInfluence;

                // Add wobble
                float wobble = Mathf.Sin(time + i * 0.5f) * wobbleAmount * segmentInfluence;
                float wobble2 = Mathf.Cos(time * 0.7f + i * 0.3f) * wobbleAmount * 0.5f * segmentInfluence;

                // Only add input-based wobble when there's input
                if (smoothedInput.magnitude > 0.1f)
                {
                    xRot += wobble;
                    zRot += wobble2;
                }

                // Apply rotation additively to the current (animated) rotation
                Quaternion additionalRotation = Quaternion.Euler(xRot, 0, zRot);
                boneChain[i].localRotation = boneChain[i].localRotation * additionalRotation;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || boneChain == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < boneChain.Length - 1; i++)
            {
                if (boneChain[i] != null && boneChain[i + 1] != null)
                {
                    Gizmos.DrawLine(boneChain[i].position, boneChain[i + 1].position);
                }
            }
        }

        /// <summary>
        /// Get the current smoothed input for this tentacle
        /// </summary>
        public Vector2 GetCurrentInput() => smoothedInput;

        /// <summary>
        /// Get the limb index
        /// </summary>
        public int GetLimbIndex() => limbIndex;

        /// <summary>
        /// Feed input externally (e.g., gesture system).
        /// Only works when inputDisabled is true.
        /// </summary>
        public void SetExternalInput(Vector2 input)
        {
            if (inputDisabled)
                currentInput = input;
        }

        /// <summary>Whether joystick input is disabled for this limb.</summary>
        public bool InputDisabled
        {
            get => inputDisabled;
            set => inputDisabled = value;
        }
    }
}

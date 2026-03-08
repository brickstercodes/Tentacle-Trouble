using UnityEngine;
using System;
using Octo.Input;
using Octo.Movement;

namespace Octo.Interaction
{
    /// <summary>
    /// Central grab / drop / anchor orchestrator.
    /// Attach to the octopus root object.
    ///
    /// P1 (grab)   – hold button 3 s to grab nearest ObjectGrabbable
    /// P2 (drop)   – tap to instantly drop whatever is held
    /// P3 (anchor) – hold to freeze the octopus in place
    ///
    /// Grab detection uses Physics.OverlapSphere around the body.
    /// Grabbed objects are lerped to a point above the head bone.
    /// </summary>
    public class OctoGrabSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Head bone (auto-finds HeadWobble if null)")]
        [SerializeField] private Transform headBone;

        [Tooltip("The transform that moves with the octopus body — drag Main_Root here")]
        [SerializeField] private Transform bodyCenter;

        [Tooltip("OctopusLocomotion for anchor feature (auto-finds if null)")]
        [SerializeField] private OctopusLocomotion locomotion;

        [Header("Grab Settings")]
        [Tooltip("Radius for proximity detection")]
        [SerializeField] private float grabRadius = 15f;

        [Tooltip("Seconds P1 must hold button before grab completes")]
        [SerializeField] private float grabHoldTime = 1.5f;

        [Tooltip("Height above head bone for the grab point")]
        [SerializeField] private float grabPointHeight = 21f;

        [Tooltip("Layer mask for grabbable objects")]
        [SerializeField] private LayerMask grabbableLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // Runtime state
        private AirConsoleInputHandler inputHandler;
        private ObjectGrabbable currentlyHeld;
        private Transform grabPoint;      // dynamically created
        private bool wasDropPressed;       // for rising-edge detection
        private bool isAnchored;
        private bool grabFired;            // prevents TryGrab from firing every frame
        private bool wasInteractPressed;   // rising-edge for chest interaction

        /// <summary>Fired when an object is grabbed.</summary>
        public event Action OnGrabbed;
        /// <summary>Fired when an object is thrown/dropped.</summary>
        public event Action OnThrown;

        /// <summary>World-space center of the octopus body for proximity checks.</summary>
        private Vector3 BodyCenter => bodyCenter != null ? bodyCenter.position : transform.position;

        private void Start()
        {
            // Auto-find head bone via HeadWobble component
            if (headBone == null)
            {
                // Try children first
                var headWobble = GetComponentInChildren<Octo.Animation.HeadWobble>();
                // Fallback: search the entire scene
                if (headWobble == null)
                    headWobble = FindAnyObjectByType<Octo.Animation.HeadWobble>();
                if (headWobble != null)
                {
                    headBone = headWobble.transform;
                    Debug.Log($"[OctoGrabSystem] Auto-found head bone: {headBone.name}");
                }
                else
                {
                    Debug.LogWarning("[OctoGrabSystem] No HeadWobble found in scene!");
                }
            }

            // Auto-find locomotion
            if (locomotion == null)
                locomotion = FindAnyObjectByType<OctopusLocomotion>();

            // Auto-fill body center from locomotion's move target if not set manually
            if (bodyCenter == null && locomotion != null && locomotion.MoveTarget != null)
            {
                bodyCenter = locomotion.MoveTarget;
                Debug.Log($"[OctoGrabSystem] Body center auto-set to '{bodyCenter.name}' from locomotion.");
            }
            if (bodyCenter == null)
                Debug.LogWarning("[OctoGrabSystem] No Body Center assigned! Drag Main_Root into the Body Center field on OctoGrabSystem.");

            // Create persistent grab-point transform above head
            // Do NOT parent to scaled transforms — keep it at scene root and update in LateUpdate
            var go = new GameObject("OctoGrabPoint");
            grabPoint = go.transform;

            if (headBone != null)
            {
                // Position above head in world space (unaffected by bone scale)
                grabPoint.position = headBone.position + Vector3.up * grabPointHeight;
                Debug.Log($"[OctoGrabSystem] Grab point tracking head bone: {headBone.name}");
            }
            else
            {
                // Fallback: find the Neck_J or Head_J bone manually
                var allTransforms = GetComponentsInChildren<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.Contains("Head") || t.name.Contains("Neck"))
                    {
                        headBone = t;
                        Debug.Log($"[OctoGrabSystem] Found bone by name: {headBone.name}");
                        break;
                    }
                }

                if (headBone != null)
                {
                    grabPoint.position = headBone.position + Vector3.up * grabPointHeight;
                }
                else
                {
                    // Last resort: world-space position above the octopus
                    grabPoint.position = transform.position + Vector3.up * 3f;
                    Debug.LogWarning("[OctoGrabSystem] No head bone found – grab point at world +3m.");
                }
            }

            // Auto-configure grabbable objects in scene
            AutoConfigureGrabbables();

            Debug.Log("[OctoGrabSystem] Initialised.");
        }

        /// <summary>
        /// Scans the scene for Rigidbody objects and auto-adds ObjectGrabbable
        /// if they don't already have it. Skips the octopus itself.
        /// </summary>
        private void AutoConfigureGrabbables()
        {
            int configured = 0;
            foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                // Skip the octopus body itself
                if (rb.transform.IsChildOf(transform)) continue;

                // Skip kinematic / static objects (floors, walls, etc.)
                if (rb.isKinematic) continue;

                if (rb.GetComponent<ObjectGrabbable>() == null)
                {
                    rb.gameObject.AddComponent<ObjectGrabbable>();
                    configured++;
                    Debug.Log($"[OctoGrabSystem] Auto-added ObjectGrabbable to '{rb.name}'");
                }
            }
            if (configured > 0)
                Debug.Log($"[OctoGrabSystem] Auto-configured {configured} grabbable object(s)");

            // Report all grabbables
            var all = FindObjectsByType<ObjectGrabbable>(FindObjectsSortMode.None);
            Debug.Log($"[OctoGrabSystem] Total grabbable objects in scene: {all.Length}");
            foreach (var g in all)
            {
                var col = g.GetComponent<Collider>();
                Debug.Log($"[OctoGrabSystem]   - {g.name} @ {g.transform.position} | collider={col?.GetType().Name ?? "NONE"}");
            }
        }

        private void LateUpdate()
        {
            // Keep grab point tracking head bone in world space (avoids parent scale issues)
            if (grabPoint != null && headBone != null)
            {
                grabPoint.position = headBone.position + Vector3.up * grabPointHeight;
            }
        }

        private void Update()
        {
            if (inputHandler == null)
            {
                inputHandler = AirConsoleInputHandler.Instance;
                if (inputHandler == null) return;
            }

            // If the held object was disabled (e.g. coin entered chest), clear reference
            if (currentlyHeld != null && !currentlyHeld.gameObject.activeInHierarchy)
            {
                Debug.Log($"[OctoGrabSystem] Held object '{currentlyHeld.name}' was deactivated, clearing.");
                currentlyHeld = null;
            }

            HandleGrab();     // P1
            HandleDrop();     // P1 (was P2)
            HandleAnchor();   // P3 – anchor button
            HandleInteract(); // P3 – interact button
        }

        // ─── P1: GRAB ────────────────────────────────────────────────────
        private float lastGrabLogTime;

        private void HandleGrab()
        {
            if (currentlyHeld != null) return; // already holding something

            bool isPressed = inputHandler.IsButtonPressed(0, "grab");
            float holdTime = inputHandler.GetButtonHoldTime(0, "grab");

            // Reset the fired flag when button is released
            if (!isPressed)
            {
                grabFired = false;
                return;
            }

            // Log hold progress every 0.5 s so we can see it in console
            if (Time.time - lastGrabLogTime > 0.5f)
            {
                lastGrabLogTime = Time.time;
                Debug.Log($"[OctoGrabSystem] Grab hold: {holdTime:F1}/{grabHoldTime}s");
            }

            // Fire TryGrab exactly once when threshold is crossed
            if (!grabFired && holdTime >= grabHoldTime)
            {
                grabFired = true;
                TryGrab();
            }
        }

        private void TryGrab()
        {
            Vector3 center = BodyCenter;
            var allGrabbable = FindObjectsByType<ObjectGrabbable>(FindObjectsSortMode.None);

            ObjectGrabbable closest = null;
            float closestDist = float.MaxValue;

            foreach (var og in allGrabbable)
            {
                if (og.IsGrabbed) continue;
                if (!og.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(center, og.transform.position);
                if (d <= grabRadius && d < closestDist)
                {
                    closestDist = d;
                    closest = og;
                }
            }

            if (closest != null)
            {
                // Pass all octopus colliders so they don't push the grabbed object
                var octopusColliders = GetComponentsInChildren<Collider>();
                closest.Grab(grabPoint, octopusColliders);
                currentlyHeld = closest;
                Debug.Log($"[OctoGrabSystem] *** GRABBED {closest.name} (dist={closestDist:F2}) ***");
                OnGrabbed?.Invoke();
            }
            else
            {
                Debug.Log($"[OctoGrabSystem] Nothing in grab radius ({grabRadius}m)");
            }
        }

        // ─── P1: DROP (was P2) ───────────────────────────────────────────
        private void HandleDrop()
        {
            bool dropPressed = inputHandler.IsButtonPressed(0, "drop");

            // Rising edge – drop on first press frame
            if (dropPressed && !wasDropPressed)
            {
                if (currentlyHeld != null)
                {
                    // transform is the prefab root — rotated by the camera controller
                    Vector3 fwd = transform.forward;
                    fwd.y = 0f;
                    fwd.Normalize();
                    Vector3 throwDir = fwd + Vector3.up * 0.3f;
                    Debug.Log($"[OctoGrabSystem] Threw {currentlyHeld.name} dir={throwDir}");
                    currentlyHeld.Throw(throwDir);
                    currentlyHeld = null;
                    OnThrown?.Invoke();
                }
            }

            wasDropPressed = dropPressed;
        }

        // ─── P3: ANCHOR (hold) ──────────────────────────────────────
        private void HandleAnchor()
        {
            bool anchorPressed = inputHandler.IsButtonPressed(2, "anchor");

            if (anchorPressed != isAnchored)
            {
                isAnchored = anchorPressed;

                if (locomotion != null)
                    locomotion.SetAnchored(isAnchored);

                Debug.Log($"[OctoGrabSystem] Anchor {(isAnchored ? "ON" : "OFF")}");
            }
        }

        // ─── P3: INTERACT (tap) – open chest ────────────────────────
        private void HandleInteract()
        {
            bool interactPressed = inputHandler.IsButtonPressed(2, "interact");

            // Rising edge – open chest on first press frame
            if (interactPressed && !wasInteractPressed)
            {
                var chest = FindNearestChest();
                if (chest != null)
                {
                    chest.Open();
                }
                else
                {
                    Debug.Log("[OctoGrabSystem] No chest in range to interact with.");
                }
            }

            wasInteractPressed = interactPressed;
        }

        /// <summary>
        /// Finds the nearest TreasureChest within interaction range.
        /// </summary>
        private TreasureChest FindNearestChest()
        {
            Vector3 pos = BodyCenter;
            TreasureChest closest = null;
            float closestDist = float.MaxValue;

            foreach (var chest in FindObjectsByType<TreasureChest>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(pos, chest.transform.position);
                if (d <= chest.InteractionRadius && d < closestDist)
                {
                    closestDist = d;
                    closest = chest;
                }
            }

            return closest;
        }

        // ─── GIZMOS ─────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            Vector3 center = BodyCenter;
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(center, grabRadius);

            if (grabPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(grabPoint.position, 0.1f);
            }
        }
    }
}

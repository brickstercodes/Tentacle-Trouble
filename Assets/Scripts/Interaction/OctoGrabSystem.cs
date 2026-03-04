using UnityEngine;
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

        [Tooltip("OctopusLocomotion for anchor feature (auto-finds if null)")]
        [SerializeField] private OctopusLocomotion locomotion;

        [Header("Grab Settings")]
        [Tooltip("Radius for proximity detection")]
        [SerializeField] private float grabRadius = 15f;

        [Tooltip("Seconds P1 must hold button before grab completes")]
        [SerializeField] private float grabHoldTime = 3f;

        [Tooltip("Height above head bone for the grab point")]
        [SerializeField] private float grabPointHeight = 0.5f;

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

            HandleGrab();   // P1
            HandleDrop();   // P2
            HandleAnchor(); // P3
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
            // Find closest grabbable object in the entire scene — always grabs nearest, no radius limit
            Vector3 center = transform.position;
            var allGrabbable = FindObjectsByType<ObjectGrabbable>(FindObjectsSortMode.None);

            ObjectGrabbable closest = null;
            float closestDist = float.MaxValue;

            foreach (var og in allGrabbable)
            {
                if (og.IsGrabbed) continue;
                float d = Vector3.Distance(center, og.transform.position);
                Debug.Log($"[OctoGrabSystem]   candidate: {og.name} @ {og.transform.position}, dist={d:F1}");
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = og;
                }
            }

            Debug.Log($"[OctoGrabSystem] TryGrab from {center}, candidates={allGrabbable.Length}");

            if (closest != null)
            {
                closest.Grab(grabPoint);
                currentlyHeld = closest;
                Debug.Log($"[OctoGrabSystem] *** GRABBED {closest.name} (dist={closestDist:F2}) ***");
            }
            else
            {
                Debug.LogWarning("[OctoGrabSystem] No grabbable objects in scene!");
            }
        }

        // ─── P2: DROP ────────────────────────────────────────────────────
        private void HandleDrop()
        {
            bool dropPressed = inputHandler.IsButtonPressed(1, "drop");

            // Rising edge – drop on first press frame
            if (dropPressed && !wasDropPressed)
            {
                if (currentlyHeld != null)
                {
                    Debug.Log($"[OctoGrabSystem] Dropped {currentlyHeld.name}");
                    currentlyHeld.Drop();
                    currentlyHeld = null;
                }
            }

            wasDropPressed = dropPressed;
        }

        // ─── P3: ANCHOR ─────────────────────────────────────────────────
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

        // ─── GIZMOS ─────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            Vector3 center = headBone != null ? headBone.position : transform.position;
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

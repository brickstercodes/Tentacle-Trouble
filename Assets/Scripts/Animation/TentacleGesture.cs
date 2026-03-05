using UnityEngine;
using System.Collections;
using Octo.Interaction;

namespace Octo.Animation
{
    /// <summary>
    /// Drives gesture animations on unmapped tentacles (limbs 2 and 3)
    /// when grab/throw events occur.
    /// 
    /// On Grab: both arms reach upward (celebratory wave).
    /// On Throw: both arms swing forward then relax (throwing assist gesture).
    /// 
    /// Works by feeding input to ProceduralTentacle via SetExternalInput().
    /// Requires those limbs to have inputDisabled = true.
    /// </summary>
    public class TentacleGesture : MonoBehaviour
    {
        [Header("Limbs to animate")]
        [Tooltip("Limb indices to drive with gestures (default: 2,3 = P2's arms)")]
        [SerializeField] private int[] gestureLimbIndices = { 2, 3 };

        [Header("Gesture Timing")]
        [Tooltip("Duration of the grab celebration gesture")]
        [SerializeField] private float grabGestureDuration = 1.2f;
        [Tooltip("Duration of the throw assist gesture")]
        [SerializeField] private float throwGestureDuration = 0.8f;

        private ProceduralTentacle[] allTentacles;
        private OctoGrabSystem grabSystem;
        private Coroutine activeGesture;

        private void Start()
        {
            allTentacles = FindObjectsByType<ProceduralTentacle>(FindObjectsSortMode.None);
            grabSystem = FindAnyObjectByType<OctoGrabSystem>();

            if (grabSystem != null)
            {
                grabSystem.OnGrabbed += OnGrab;
                grabSystem.OnThrown += OnThrow;
            }

            // Auto-disable input on gesture limbs
            foreach (var tentacle in GetGestureLimbs())
            {
                tentacle.InputDisabled = true;
            }

            Debug.Log($"[TentacleGesture] Watching {gestureLimbIndices.Length} limbs for gestures.");
        }

        private void OnDestroy()
        {
            if (grabSystem != null)
            {
                grabSystem.OnGrabbed -= OnGrab;
                grabSystem.OnThrown -= OnThrow;
            }
        }

        private ProceduralTentacle[] GetGestureLimbs()
        {
            var result = new System.Collections.Generic.List<ProceduralTentacle>();
            foreach (var t in allTentacles)
            {
                foreach (int idx in gestureLimbIndices)
                {
                    if (t.GetLimbIndex() == idx)
                        result.Add(t);
                }
            }
            return result.ToArray();
        }

        private void OnGrab()
        {
            if (activeGesture != null)
                StopCoroutine(activeGesture);
            activeGesture = StartCoroutine(GrabGesture());
        }

        private void OnThrow()
        {
            if (activeGesture != null)
                StopCoroutine(activeGesture);
            activeGesture = StartCoroutine(ThrowGesture());
        }

        /// <summary>
        /// Grab celebration: arms wave upward, alternating side to side.
        /// </summary>
        private IEnumerator GrabGesture()
        {
            var limbs = GetGestureLimbs();
            float elapsed = 0f;

            while (elapsed < grabGestureDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / grabGestureDuration;

                // Reach up with a wave pattern
                float upAmount = Mathf.Sin(t * Mathf.PI); // Peaks at midpoint
                float wave = Mathf.Sin(elapsed * 8f) * 0.4f; // Side-to-side wave

                for (int i = 0; i < limbs.Length; i++)
                {
                    // Alternate wave direction per limb
                    float side = (i % 2 == 0) ? wave : -wave;
                    limbs[i].SetExternalInput(new Vector2(side, upAmount));
                }

                yield return null;
            }

            // Relax
            foreach (var limb in limbs)
                limb.SetExternalInput(Vector2.zero);

            activeGesture = null;
        }

        /// <summary>
        /// Throw assist: arms swing forward then relax back.
        /// </summary>
        private IEnumerator ThrowGesture()
        {
            var limbs = GetGestureLimbs();
            float elapsed = 0f;

            while (elapsed < throwGestureDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / throwGestureDuration;

                // Quick forward swing then ease back
                float forward;
                if (t < 0.3f)
                {
                    // Fast forward swing (0 to 1 in first 30%)
                    forward = t / 0.3f;
                }
                else
                {
                    // Ease back to rest (1 to 0 in remaining 70%)
                    forward = 1f - ((t - 0.3f) / 0.7f);
                }

                // Spread arms slightly outward during throw
                float spread = forward * 0.3f;

                for (int i = 0; i < limbs.Length; i++)
                {
                    float side = (i % 2 == 0) ? -spread : spread;
                    limbs[i].SetExternalInput(new Vector2(side, forward));
                }

                yield return null;
            }

            // Relax
            foreach (var limb in limbs)
                limb.SetExternalInput(Vector2.zero);

            activeGesture = null;
        }
    }
}

using UnityEngine;
using System.Collections;

namespace Octo.Interaction
{
    /// <summary>
    /// Attach to any object that should be pick-up-able by the octopus.
    /// Requires a Rigidbody. Based on Code Monkey's PickUpDropObjects tutorial.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ObjectGrabbable : MonoBehaviour
    {
        [Tooltip("Degrees per second to spin while grabbed")]
        [SerializeField] private float grabSpinSpeed = 90f;

        [Tooltip("Forward impulse force applied when thrown")]
        [SerializeField] private float throwForce = 15f;

        [Tooltip("How long the throw arc lasts in seconds")]
        [SerializeField] private float throwDuration = 0.6f;

        private Rigidbody objectRigidbody;
        private Transform objectGrabPointTransform;
        private Transform originalParent;
        private Collider[] ignoredColliders;
        private Coroutine throwRoutine;
        private Coroutine grabLerpRoutine;

        [Tooltip("How long the pickup lerp takes in seconds")]
        [SerializeField] private float grabLerpDuration = 0.35f;

        public bool IsGrabbed => objectGrabPointTransform != null;

        private void Awake()
        {
            objectRigidbody = GetComponent<Rigidbody>();
        }

        public void Grab(Transform grabPoint, Collider[] collidersToIgnore = null)
        {
            // Cancel any in-progress throw arc
            if (throwRoutine != null)
            {
                StopCoroutine(throwRoutine);
                throwRoutine = null;
            }
            if (grabLerpRoutine != null)
            {
                StopCoroutine(grabLerpRoutine);
                grabLerpRoutine = null;
            }

            objectGrabPointTransform = grabPoint;
            objectRigidbody.useGravity = false;
            objectRigidbody.isKinematic = true;

            // Parent to grab point and smoothly lerp from current position
            originalParent = transform.parent;
            transform.SetParent(grabPoint, true);
            // Don't snap — start a smooth lerp from current local offset to zero
            grabLerpRoutine = StartCoroutine(GrabLerp());

            // Prevent the octopus's own colliders from pushing the grabbed object
            ignoredColliders = collidersToIgnore;
            if (ignoredColliders != null)
            {
                var ownColliders = GetComponents<Collider>();
                foreach (var oc in ownColliders)
                    foreach (var ic in ignoredColliders)
                        UnityEngine.Physics.IgnoreCollision(oc, ic, true);
            }
        }

        public void Drop()
        {
            if (grabLerpRoutine != null) { StopCoroutine(grabLerpRoutine); grabLerpRoutine = null; }
            transform.SetParent(originalParent, true);
            objectGrabPointTransform = null;
            objectRigidbody.isKinematic = false;
            objectRigidbody.useGravity = true;
            RestoreCollisions();
        }

        /// <summary>
        /// Drop and throw along a kinematic arc (no physics until arc ends).
        /// </summary>
        public void Throw(Vector3 direction)
        {
            if (grabLerpRoutine != null) { StopCoroutine(grabLerpRoutine); grabLerpRoutine = null; }
            transform.SetParent(originalParent, true);
            objectGrabPointTransform = null;
            RestoreCollisions();

            // Stay kinematic during the arc so collisions don't deflect it
            objectRigidbody.isKinematic = true;
            objectRigidbody.useGravity = false;

            throwRoutine = StartCoroutine(ThrowArc(direction.normalized));
        }

        private IEnumerator ThrowArc(Vector3 dir)
        {
            Vector3 velocity = dir * throwForce;
            float gravity = 9.81f;
            float elapsed = 0f;

            while (elapsed < throwDuration)
            {
                velocity.y -= gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                transform.Rotate(0f, 0f, grabSpinSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Hand back to physics
            objectRigidbody.isKinematic = false;
            objectRigidbody.useGravity = true;
            objectRigidbody.linearVelocity = velocity;
            throwRoutine = null;
        }

        private IEnumerator GrabLerp()
        {
            Vector3 startLocal = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < grabLerpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / grabLerpDuration);
                transform.localPosition = Vector3.Lerp(startLocal, Vector3.zero, t);
                yield return null;
            }

            transform.localPosition = Vector3.zero;
            grabLerpRoutine = null;
        }

        private void RestoreCollisions()
        {
            if (ignoredColliders == null) return;
            var ownColliders = GetComponents<Collider>();
            foreach (var oc in ownColliders)
                foreach (var ic in ignoredColliders)
                    UnityEngine.Physics.IgnoreCollision(oc, ic, false);
            ignoredColliders = null;
        }

        private void LateUpdate()
        {
            if (objectGrabPointTransform != null)
            {
                // Spin around Z axis while grabbed (parenting handles position)
                transform.Rotate(0f, 0f, grabSpinSpeed * Time.deltaTime);
            }
        }
    }
}

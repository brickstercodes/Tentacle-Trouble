using UnityEngine;

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
        [SerializeField] private float throwForce = 8f;

        private Rigidbody objectRigidbody;
        private Transform objectGrabPointTransform;
        private Collider[] ignoredColliders;

        public bool IsGrabbed => objectGrabPointTransform != null;

        private void Awake()
        {
            objectRigidbody = GetComponent<Rigidbody>();
        }

        public void Grab(Transform grabPoint, Collider[] collidersToIgnore = null)
        {
            objectGrabPointTransform = grabPoint;
            objectRigidbody.useGravity = false;

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
            objectGrabPointTransform = null;
            objectRigidbody.useGravity = true;
            RestoreCollisions();
        }

        /// <summary>
        /// Drop with a directional throw impulse.
        /// </summary>
        public void Throw(Vector3 direction)
        {
            objectGrabPointTransform = null;
            objectRigidbody.useGravity = true;
            RestoreCollisions();
            objectRigidbody.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
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

        private void FixedUpdate()
        {
            if (objectGrabPointTransform != null)
            {
                float lerpSpeed = 10f;
                Vector3 newPosition = Vector3.Lerp(
                    transform.position,
                    objectGrabPointTransform.position,
                    Time.deltaTime * lerpSpeed
                );
                objectRigidbody.MovePosition(newPosition);

                // Spin around Y axis while grabbed
                Quaternion spin = Quaternion.Euler(0f, 0f, grabSpinSpeed * Time.deltaTime);
                objectRigidbody.MoveRotation(objectRigidbody.rotation * spin);
            }
        }
    }
}

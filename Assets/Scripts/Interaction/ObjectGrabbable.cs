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

        public bool IsGrabbed => objectGrabPointTransform != null;

        private void Awake()
        {
            objectRigidbody = GetComponent<Rigidbody>();
        }

        public void Grab(Transform grabPoint)
        {
            objectGrabPointTransform = grabPoint;
            objectRigidbody.useGravity = false;
        }

        public void Drop()
        {
            objectGrabPointTransform = null;
            objectRigidbody.useGravity = true;
        }

        /// <summary>
        /// Drop with a directional throw impulse.
        /// </summary>
        public void Throw(Vector3 direction)
        {
            objectGrabPointTransform = null;
            objectRigidbody.useGravity = true;
            objectRigidbody.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
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

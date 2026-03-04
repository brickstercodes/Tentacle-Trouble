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
            }
        }
    }
}

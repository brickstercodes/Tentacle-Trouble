using UnityEngine;

namespace Octo.Physics
{
    /// <summary>
    /// Attach to a kinematic Rigidbody that is a child of a transform moved
    /// directly (not via physics). Syncs the Rigidbody's physics representation
    /// to the transform every FixedUpdate, preventing the capsule collider from
    /// lagging behind the visual mesh.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicFollower : MonoBehaviour
    {
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (!rb.isKinematic)
            {
                Debug.LogWarning("[KinematicFollower] Rigidbody is not kinematic — forcing isKinematic=true.");
                rb.isKinematic = true;
            }
        }

        private void FixedUpdate()
        {
            rb.MovePosition(transform.position);
            rb.MoveRotation(transform.rotation);
        }
    }
}

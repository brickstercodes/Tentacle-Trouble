using UnityEngine;
using Octo.Input;

namespace Octo.Movement
{
    /// <summary>
    /// Third-person orbit camera. Orbits around the Hips_J bone (actual body center).
    /// Player 2's left stick: orbit/tilt.  Right stick: rotate octopus body.
    /// Attach to the Main Camera. Leave Target empty for auto-find.
    /// </summary>
    public class OctopusCameraController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Leave empty — auto-finds the octopus body bone.")]
        [SerializeField] private Transform target;

        [Header("Orbit Settings")]
        [SerializeField] private float orbitSpeed = 120f;
        [SerializeField] private float tiltSpeed = 60f;
        [SerializeField] private float minTilt = 5f;
        [SerializeField] private float maxTilt = 80f;

        [Header("Octopus Rotation (Right Stick)")]
        [SerializeField] private float octopusRotateSpeed = 120f;

        [Header("Distance")]
        [SerializeField] private float distance = 10f;

        [Header("Smoothing")]
        [SerializeField] private float followSmooth = 5f;
        [Tooltip("Height above the body bone to orbit around")]
        [SerializeField] private float heightOffset = 1f;

        private float yaw;
        private float pitch = 25f;
        private AirConsoleInputHandler inputHandler;
        private Transform octopusRoot;
        // The body bone we actually orbit around (at the real body center)
        private Transform bodyBone;

        private void Start()
        {
            // Find a body bone — these are AT the actual octopus mesh, unlike the scene root
            // which can be 13+ units away from the mesh due to model import offset
            string[] bodyBoneNames = { "Hips_J", "Root_J" };
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);

            foreach (string boneName in bodyBoneNames)
            {
                foreach (var t in allTransforms)
                {
                    if (t.name == boneName)
                    {
                        bodyBone = t;
                        break;
                    }
                }
                if (bodyBone != null) break;
            }

            // Scene root for right-stick rotation
            if (target == null && bodyBone != null)
            {
                target = bodyBone;
                while (target.parent != null)
                    target = target.parent;
            }
            octopusRoot = target;

            if (bodyBone != null)
            {
                Vector3 pivot = GetPivot();
                Vector3 dir = transform.position - pivot;
                if (dir.magnitude > 0.5f)
                {
                    yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    pitch = Mathf.Clamp(Mathf.Asin(dir.normalized.y) * Mathf.Rad2Deg, minTilt, maxTilt);
                }
                Debug.Log($"[Camera] Orbiting bone: {bodyBone.name} at {bodyBone.position}");
            }
            else
            {
                Debug.LogError("[Camera] Could not find Hips_J or Root_J bone!");
            }

            distance = Mathf.Max(distance, 3f);
        }

        private Vector3 GetPivot()
        {
            if (bodyBone != null)
                return bodyBone.position + Vector3.up * heightOffset;
            return target != null ? target.position + Vector3.up * heightOffset : transform.position;
        }

        private void LateUpdate()
        {
            if (bodyBone == null && target == null) return;

            if (inputHandler == null)
                inputHandler = AirConsoleInputHandler.Instance;

            Vector2 leftStick = Vector2.zero;
            Vector2 rightStick = Vector2.zero;

            if (inputHandler != null)
            {
                leftStick = inputHandler.GetLimbInput(2);
                rightStick = inputHandler.GetLimbInput(3);
            }

#if UNITY_EDITOR
            if (leftStick.magnitude < 0.1f)
            {
                if (UnityEngine.Input.GetKey(KeyCode.J)) leftStick.x -= 1;
                if (UnityEngine.Input.GetKey(KeyCode.L)) leftStick.x += 1;
                if (UnityEngine.Input.GetKey(KeyCode.I)) leftStick.y += 1;
                if (UnityEngine.Input.GetKey(KeyCode.K)) leftStick.y -= 1;
            }
            if (rightStick.magnitude < 0.1f)
            {
                if (UnityEngine.Input.GetKey(KeyCode.U)) rightStick.x -= 1;
                if (UnityEngine.Input.GetKey(KeyCode.O)) rightStick.x += 1;
            }
#endif

            yaw += leftStick.x * orbitSpeed * Time.deltaTime;
            pitch -= leftStick.y * tiltSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minTilt, maxTilt);

            if (octopusRoot != null && bodyBone != null && Mathf.Abs(rightStick.x) > 0.1f)
                octopusRoot.RotateAround(bodyBone.position, Vector3.up, rightStick.x * octopusRotateSpeed * Time.deltaTime);

            Vector3 pivot = GetPivot();
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredPos = pivot + rotation * (Vector3.back * distance);

            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * followSmooth);
            transform.LookAt(pivot);
        }
    }
}

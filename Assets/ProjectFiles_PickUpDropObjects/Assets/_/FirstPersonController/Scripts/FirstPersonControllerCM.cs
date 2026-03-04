using UnityEngine;
using UnityEngine.InputSystem;
using CodeMonkey.Utils;

/*
 * Based on the Unity Starter Assets First Person Controller
 * https://assetstore.unity.com/packages/essentials/starter-assets-first-person-character-controller-196525
 * */

namespace CodeMonkey.FirstPersonController {

    public class FirstPersonControllerCM : MonoBehaviour {

        [Header("Player")]
        [SerializeField] private float moveSpeed;
        [SerializeField] private float sprintSpeed;
        [SerializeField] private float rotationSmoothTime;
        [SerializeField] private float speedChangeRate;
        [SerializeField] private float lookSensitivity;
        [SerializeField] private float jumpHeight;
        [SerializeField] private float gravity;
        [SerializeField] private float jumpTimeout;
        [SerializeField] private float fallTimeout;

        [Header("IsGrounded")]
        [SerializeField] private bool isGrounded;
        [SerializeField] private float groundedOffset;
        [SerializeField] private float groundedRadius;
        [SerializeField] private LayerMask groundLayers;

        [Header("Cinemachine")]
        [SerializeField] private GameObject cinemachineCameraTarget;
        [SerializeField] private float topClamp = 70.0f;
        [SerializeField] private float bottomClamp = -30.0f;
        [SerializeField] private float cameraAngleOverride = 0.0f;
        [SerializeField] private bool lockCameraPosition = false;



        // cinemachine
        private float cinemachineTargetYaw;
        private float cinemachineTargetPitch;

        // player
        private float speed;
        private float animationBlend;
        private float targetRotation = 0.0f;
        private float rotationVelocity;
        private float verticalVelocity;
        private float terminalVelocity = 53.0f;
        private bool jump;

        // timeout deltatime
        private float jumpTimeoutDelta;
        private float fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        private Animator animator;
        private CharacterController controller;
        private FirstPersonControllerInput firstPersonShooterInput;
        private Transform mainCameraTransform;

        private bool hasAnimator;
        private bool rotateOnMove;

        private void Awake() {
            // get a reference to our main camera
            if (mainCameraTransform == null) {
                mainCameraTransform = Camera.main.transform;
            }

            firstPersonShooterInput = GetComponent<FirstPersonControllerInput>();
            firstPersonShooterInput.OnJump += FirstPersonShooterInput_OnJump;
        }

        private void FirstPersonShooterInput_OnJump(object sender, System.EventArgs e) {
            jump = true;
        }

        private void Start() {
            hasAnimator = TryGetComponent(out animator);
            controller = GetComponent<CharacterController>();

            AssignAnimationIDs();

            // reset our timeouts on start
            jumpTimeoutDelta = jumpTimeout;
            fallTimeoutDelta = fallTimeout;
        }

        private void Update() {
            hasAnimator = TryGetComponent(out animator);

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate() {
            CameraRotation();
        }

        private void AssignAnimationIDs() {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck() {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            isGrounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (hasAnimator) {
                //_animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation() {
            // if there is an input and camera position is not fixed
            if (firstPersonShooterInput.GetLookVector().sqrMagnitude > 0 && !lockCameraPosition) {
                cinemachineTargetYaw += firstPersonShooterInput.GetLookVector().x * lookSensitivity;
                cinemachineTargetPitch += firstPersonShooterInput.GetLookVector().y * lookSensitivity;
            }
            
            // clamp our rotations so our values are limited 360 degrees
            cinemachineTargetYaw = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
            cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);

            // Cinemachine will follow this target
            cinemachineCameraTarget.transform.rotation = Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride, cinemachineTargetYaw, 0.0f);
        }

        private void Move() {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = firstPersonShooterInput.IsSprinting() ? sprintSpeed : moveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (firstPersonShooterInput.GetMoveVector() == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(controller.velocity.x, 0.0f, controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset) {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * speedChangeRate);

                // round speed to 3 decimal places
                speed = Mathf.Round(speed * 1000f) / 1000f;
            } else {
                speed = targetSpeed;
            }
            animationBlend = Mathf.Lerp(animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);

            // normalise input direction
            Vector2 inputMoveVector = firstPersonShooterInput.GetMoveVector();
            Vector3 inputDirection = new Vector3(inputMoveVector.x, 0.0f, inputMoveVector.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (inputMoveVector != Vector2.zero) {
                targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + mainCameraTransform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationSmoothTime);

                // rotate to face input direction relative to camera position
                if (rotateOnMove) {
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, targetRotation, 0.0f) * Vector3.forward;

            // move the player
            controller.Move(targetDirection.normalized * (speed * Time.deltaTime) + new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (hasAnimator) {
                animator.SetFloat(_animIDSpeed, animationBlend, .1f, Time.deltaTime);
                //_animator.SetFloat(_animIDMotionSpeed, inputMagnitude, .1f, Time.deltaTime);
            }
        }

        private void JumpAndGravity() {
            if (isGrounded) {
                // reset the fall timeout timer
                fallTimeoutDelta = fallTimeout;

                // update animator if using character
                if (hasAnimator) {
                    animator.SetBool(_animIDJump, false);
                    animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (verticalVelocity < 0.0f) {
                    verticalVelocity = -2f;
                }

                // Jump
                if (jump && jumpTimeoutDelta <= 0.0f) {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

                    // update animator if using character
                    if (hasAnimator) {
                        animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (jumpTimeoutDelta >= 0.0f) {
                    jumpTimeoutDelta -= Time.deltaTime;
                }
            } else {
                // reset the jump timeout timer
                jumpTimeoutDelta = jumpTimeout;

                // fall timeout
                if (fallTimeoutDelta >= 0.0f) {
                    fallTimeoutDelta -= Time.deltaTime;
                } else {
                    // update animator if using character
                    if (hasAnimator) {
                        //_animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (verticalVelocity < terminalVelocity) {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax) {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected() {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (isGrounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z), groundedRadius);
        }

        public void SetLookSensitivity(float lookSensitivity) {
            this.lookSensitivity = lookSensitivity;
        }

        public void SetRotateOnMove(bool rotateOnMove) {
            this.rotateOnMove = rotateOnMove;
        }

    }

}
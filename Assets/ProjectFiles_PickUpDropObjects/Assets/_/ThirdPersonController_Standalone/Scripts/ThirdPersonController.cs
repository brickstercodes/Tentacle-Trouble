using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Cinemachine;

namespace CodeMonkey.ThirdPersonController {

    public class ThirdPersonController : MonoBehaviour {
         
        [SerializeField] private float normalSensitivity;
        [SerializeField] private Transform mouseWorldPositionTransform;
        [SerializeField] private LayerMask aimColliderLayerMask;

        private ThirdPersonControllerCM thirdPersonController;
        private ThirdPersonControllerInput thirdPersonControllerInput;
        private Animator animator;
        private CinemachineImpulseSource cinemachineImpulseSource;

        private void Awake() {
            thirdPersonController = GetComponent<ThirdPersonControllerCM>();
            thirdPersonControllerInput = GetComponent<ThirdPersonControllerInput>();
            animator = GetComponent<Animator>();
            cinemachineImpulseSource = GetComponent<CinemachineImpulseSource>();
        }

        private void Update() {
            HandleMouseWorldPosition();
        }

        private void HandleMouseWorldPosition() {
            mouseWorldPositionTransform.position = Vector3.Lerp(mouseWorldPositionTransform.position, GetMouseWorldPosition(), Time.deltaTime * 20f);
        }

        private Vector3 GetMouseWorldPosition() {
            Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = Camera.main.ScreenPointToRay(screenCenterPoint);
            if (Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, aimColliderLayerMask)) {
                return raycastHit.point;
            } else {
                return Vector3.zero;
            }
        }

    }

}
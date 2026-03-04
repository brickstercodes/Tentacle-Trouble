using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using CodeMonkey.Utils;

namespace CodeMonkey.FirstPersonController {

    public class FirstPersonController : MonoBehaviour {

        [SerializeField] private float normalSensitivity;
        [SerializeField] private float aimSensitivity;
        [SerializeField] private GameObject cinemachineCameraTarget;
        [SerializeField] private Transform mouseWorldPositionTransform;
        [SerializeField] private LayerMask aimColliderLayerMask;

        private FirstPersonControllerCM firstPersonController;
        private FirstPersonControllerInput firstPersonShooterInput;

        private void Awake() {
            firstPersonController = GetComponent<FirstPersonControllerCM>();
            firstPersonShooterInput = GetComponent<FirstPersonControllerInput>();

            firstPersonShooterInput.OnAimStarted += OnAimStarted;
            firstPersonShooterInput.OnAimStopped += OnAimStopped;
            firstPersonShooterInput.OnShootStarted += OnShootStarted;
            firstPersonShooterInput.OnShootStopped += OnShootStopped;
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

        private void OnShootStopped(object sender, System.EventArgs e) {
        }

        private void OnShootStarted(object sender, System.EventArgs e) {
        }

        private void OnAimStopped(object sender, System.EventArgs e) {
        }

        private void OnAimStarted(object sender, System.EventArgs e) {
        }

    }

}
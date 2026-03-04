using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CodeMonkey.ThirdPersonController {

    public class ThirdPersonControllerInput : MonoBehaviour {

        public event EventHandler OnJump;

        private ThirdPersonControllerInputAsset thirdPersonControllerInputAsset;
        private bool isSprinting;

        private void Awake() {
            thirdPersonControllerInputAsset = new ThirdPersonControllerInputAsset();
            thirdPersonControllerInputAsset.Player.Enable();
            thirdPersonControllerInputAsset.Player.Jump.performed += Jump_performed;
            thirdPersonControllerInputAsset.Player.Sprint.started += Sprint_started;
            thirdPersonControllerInputAsset.Player.Sprint.canceled += Sprint_canceled;

            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Sprint_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            isSprinting = false;
        }

        private void Sprint_started(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            isSprinting = true;
        }

        private void Jump_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            OnJump?.Invoke(this, EventArgs.Empty);
        }

        public Vector2 GetLookVector() {
            return thirdPersonControllerInputAsset.Player.Look.ReadValue<Vector2>();
        }

        public Vector2 GetMoveVector() {
            return thirdPersonControllerInputAsset.Player.Move.ReadValue<Vector2>();
        }

        public bool IsSprinting() {
            return isSprinting;
        }

    }

}
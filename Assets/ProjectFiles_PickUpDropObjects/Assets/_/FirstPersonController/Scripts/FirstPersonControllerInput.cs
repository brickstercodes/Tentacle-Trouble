using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CodeMonkey.FirstPersonController {

    public class FirstPersonControllerInput : MonoBehaviour {

        public event EventHandler OnJump;
        public event EventHandler OnAimStarted;
        public event EventHandler OnAimStopped;
        public event EventHandler OnShootStarted;
        public event EventHandler OnShootStopped;

        private FirstPersonControllerInputAsset firstPersonShooterInputAsset;
        private bool isSprinting;
        private bool isAiming;
        private bool isShooting;

        private void Awake() {
            firstPersonShooterInputAsset = new FirstPersonControllerInputAsset();
            firstPersonShooterInputAsset.Player.Enable();
            firstPersonShooterInputAsset.Player.Jump.performed += Jump_performed;
            firstPersonShooterInputAsset.Player.Sprint.started += Sprint_started;
            firstPersonShooterInputAsset.Player.Sprint.canceled += Sprint_canceled;
            firstPersonShooterInputAsset.Player.Aim.started += Aim_started;
            firstPersonShooterInputAsset.Player.Aim.canceled += Aim_canceled;
            firstPersonShooterInputAsset.Player.Shoot.started += Shoot_started;
            firstPersonShooterInputAsset.Player.Shoot.canceled += Shoot_canceled;

            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Shoot_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            OnShootStopped?.Invoke(this, EventArgs.Empty);
            isShooting = false;
        }

        private void Shoot_started(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            OnShootStarted?.Invoke(this, EventArgs.Empty);
            isShooting = true;
        }

        private void Aim_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            OnAimStopped?.Invoke(this, EventArgs.Empty);
            isAiming = false;
        }

        private void Aim_started(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            OnAimStarted?.Invoke(this, EventArgs.Empty);
            isAiming = true;
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
            return firstPersonShooterInputAsset.Player.Look.ReadValue<Vector2>();
        }

        public Vector2 GetMoveVector() {
            return firstPersonShooterInputAsset.Player.Move.ReadValue<Vector2>();
        }

        public bool IsSprinting() {
            return isSprinting;
        }

        public bool IsAiming() {
            return isAiming;
        }

        public bool IsShooting() {
            return isShooting;
        }

    }

}
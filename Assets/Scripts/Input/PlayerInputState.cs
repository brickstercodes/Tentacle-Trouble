using UnityEngine;
using System.Collections.Generic;

namespace Octo.Input
{
    /// <summary>
    /// Stores the input state for a single player's two limbs.
    /// Each player controls 2 limbs via 2 virtual joysticks.
    /// </summary>
    [System.Serializable]
    public struct LimbInputData
    {
        public Vector2 joystickInput;      // Raw joystick input (-1 to 1)
        public float inputMagnitude;        // Cached magnitude for quick access
        public bool isActive;               // Whether this limb has active input
        public float lastInputTime;         // Time of last input (for idle detection)

        public void Update(Vector2 input)
        {
            joystickInput = input;
            inputMagnitude = input.magnitude;
            isActive = inputMagnitude > 0.1f;
            if (isActive)
            {
                lastInputTime = Time.time;
            }
        }

        public void Clear()
        {
            joystickInput = Vector2.zero;
            inputMagnitude = 0f;
            isActive = false;
        }
    }

    /// <summary>
    /// Complete input state for a player controlling 2 limbs.
    /// </summary>
    [System.Serializable]
    public class PlayerInputState
    {
        public int playerNumber;            // 0-3 for 4 players
        public int deviceId;                // AirConsole device ID
        public bool isConnected;
        public string nickname;

        // Each player controls 2 limbs
        public LimbInputData leftLimb;      // First limb (e.g., limb 0, 2, 4, 6)
        public LimbInputData rightLimb;     // Second limb (e.g., limb 1, 3, 5, 7)

        // Button states: action name -> pressed
        private Dictionary<string, bool> buttonStates = new Dictionary<string, bool>();
        // Button hold start times: action name -> Time.time when pressed
        private Dictionary<string, float> buttonStartTimes = new Dictionary<string, float>();

        public PlayerInputState(int playerNum)
        {
            playerNumber = playerNum;
            deviceId = -1;
            isConnected = false;
            nickname = $"Player {playerNum + 1}";
            leftLimb = new LimbInputData();
            rightLimb = new LimbInputData();
        }

        /// <summary>
        /// Get input for a specific limb index (0 = left, 1 = right for this player)
        /// </summary>
        public LimbInputData GetLimbInput(int localLimbIndex)
        {
            return localLimbIndex == 0 ? leftLimb : rightLimb;
        }

        /// <summary>
        /// Get the global limb index from a local limb index.
        /// Player 0: limbs 0,1 | Player 1: limbs 2,3 | etc.
        /// </summary>
        public int GetGlobalLimbIndex(int localLimbIndex)
        {
            return playerNumber * 2 + localLimbIndex;
        }

        /// <summary>
        /// Set a button's pressed state. Records Time.time on press for hold duration tracking.
        /// </summary>
        public void SetButton(string action, bool pressed)
        {
            buttonStates[action] = pressed;
            if (pressed)
            {
                if (!buttonStartTimes.ContainsKey(action))
                    buttonStartTimes[action] = Time.time;
            }
            else
            {
                buttonStartTimes.Remove(action);
            }
        }

        /// <summary>
        /// Check if a button action is currently pressed.
        /// </summary>
        public bool IsButtonPressed(string action)
        {
            return buttonStates.TryGetValue(action, out bool pressed) && pressed;
        }

        /// <summary>
        /// Get how long a button has been held (seconds). Returns 0 if not pressed.
        /// </summary>
        public float GetButtonHoldTime(string action)
        {
            if (buttonStates.TryGetValue(action, out bool pressed) && pressed &&
                buttonStartTimes.TryGetValue(action, out float startTime))
            {
                return Time.time - startTime;
            }
            return 0f;
        }

        public void ClearInputs()
        {
            leftLimb.Clear();
            rightLimb.Clear();
        }

        /// <summary>
        /// Clear all button states (called on disconnect).
        /// </summary>
        public void ClearButtons()
        {
            buttonStates.Clear();
            buttonStartTimes.Clear();
        }
    }
}

using UnityEngine;

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

        public void ClearInputs()
        {
            leftLimb.Clear();
            rightLimb.Clear();
        }
    }
}

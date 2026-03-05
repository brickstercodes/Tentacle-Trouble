using UnityEngine;
using System;
using System.Collections.Generic;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;

namespace Octo.Input
{
    /// <summary>
    /// AirConsole Input Handler - Bridges AirConsole controller messages to limb input.
    /// 
    /// Expected JSON format from controllers:
    /// {
    ///   "type": "joystick",
    ///   "limb": 0 or 1,           // 0 = left limb, 1 = right limb (relative to player)
    ///   "x": -1.0 to 1.0,
    ///   "y": -1.0 to 1.0
    /// }
    /// 
    /// Alternative format (dual joystick in one message):
    /// {
    ///   "type": "dual_joystick",
    ///   "left": { "x": 0.0, "y": 0.0 },
    ///   "right": { "x": 0.0, "y": 0.0 }
    /// }
    /// </summary>
    public class AirConsoleInputHandler : MonoBehaviour
    {
        public static AirConsoleInputHandler Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private int maxPlayers = 3;  // 3 players for 6 limbs
        [SerializeField] private int limbsPerPlayer = 2;
        [SerializeField] private bool debugMode = true;

        [Header("Input Settings")]
        [SerializeField] private float deadzone = 0.1f;
        [SerializeField] private float inputSmoothTime = 0.05f;

        // Player input states (indexed by player number 0-2)
        private PlayerInputState[] playerStates;

        // Mapping from AirConsole device_id to player number
        private Dictionary<int, int> deviceToPlayerMap = new Dictionary<int, int>();

        // Events for game systems to subscribe to
        public event Action<int, int, Vector2> OnLimbInputReceived;  // (globalLimbIndex, playerNumber, input)
        public event Action<int> OnPlayerConnected;                   // (playerNumber)
        public event Action<int> OnPlayerDisconnected;                // (playerNumber)
        public event Action OnAllPlayersReady;
        public event Action<int, string, bool> OnButtonStateChanged;  // (playerNumber, action, pressed)

        // Total limbs = maxPlayers * limbsPerPlayer
        private Vector2[] smoothedInputs;
        private Vector2[] inputVelocities;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePlayerStates();
        }

        private void Start()
        {
#if !DISABLE_AIRCONSOLE
            RegisterAirConsoleEvents();
#else
            Debug.LogWarning("[AirConsoleInputHandler] AirConsole is disabled. Using keyboard fallback.");
#endif
        }

        private void OnDestroy()
        {
#if !DISABLE_AIRCONSOLE
            UnregisterAirConsoleEvents();
#endif
        }

        private void Update()
        {
            // Apply input smoothing
            ApplyInputSmoothing();

#if UNITY_EDITOR
            // Keyboard fallback for testing in editor
            HandleKeyboardFallback();
#endif
        }

        #endregion

        #region Initialization

        // Tracks which slots are actively used by direct (phone) controllers
        private bool[] directControllerActive;

        private void InitializePlayerStates()
        {
            int totalLimbs = maxPlayers * limbsPerPlayer;
            smoothedInputs = new Vector2[totalLimbs];
            inputVelocities = new Vector2[totalLimbs];
            directControllerActive = new bool[maxPlayers];

            playerStates = new PlayerInputState[maxPlayers];
            for (int i = 0; i < maxPlayers; i++)
            {
                playerStates[i] = new PlayerInputState(i);
            }
        }

#if !DISABLE_AIRCONSOLE
        private void RegisterAirConsoleEvents()
        {
            if (AirConsole.instance == null)
            {
                Debug.LogError("[AirConsoleInputHandler] AirConsole instance not found!");
                return;
            }

            AirConsole.instance.onReady += OnAirConsoleReady;
            AirConsole.instance.onConnect += OnDeviceConnect;
            AirConsole.instance.onDisconnect += OnDeviceDisconnect;
            AirConsole.instance.onMessage += OnAirConsoleMessage;
        }

        private void UnregisterAirConsoleEvents()
        {
            if (AirConsole.instance == null) return;

            AirConsole.instance.onReady -= OnAirConsoleReady;
            AirConsole.instance.onConnect -= OnDeviceConnect;
            AirConsole.instance.onDisconnect -= OnDeviceDisconnect;
            AirConsole.instance.onMessage -= OnAirConsoleMessage;
        }
#endif

        #endregion

        #region AirConsole Event Handlers

#if !DISABLE_AIRCONSOLE
        private void OnAirConsoleReady(string code)
        {
            if (debugMode)
                Debug.Log($"[AirConsoleInputHandler] AirConsole ready! Code: {code}");

            // Set max 4 active players
            AirConsole.instance.SetActivePlayers(maxPlayers);
        }

        private void OnDeviceConnect(int deviceId)
        {
            int playerNumber = AssignPlayerNumber(deviceId);

            if (playerNumber >= 0 && playerNumber < maxPlayers)
            {
                playerStates[playerNumber].isConnected = true;
                playerStates[playerNumber].deviceId = deviceId;
                playerStates[playerNumber].nickname = AirConsole.instance.GetNickname(deviceId) ?? $"Player {playerNumber + 1}";

                if (debugMode)
                    Debug.Log($"[AirConsoleInputHandler] Player {playerNumber + 1} connected (Device: {deviceId}, Nick: {playerStates[playerNumber].nickname})");

                OnPlayerConnected?.Invoke(playerNumber);

                CheckAllPlayersReady();
            }
        }

        private void OnDeviceDisconnect(int deviceId)
        {
            if (deviceToPlayerMap.TryGetValue(deviceId, out int playerNumber))
            {
                playerStates[playerNumber].isConnected = false;
                playerStates[playerNumber].ClearInputs();
                deviceToPlayerMap.Remove(deviceId);

                if (debugMode)
                    Debug.Log($"[AirConsoleInputHandler] Player {playerNumber + 1} disconnected");

                OnPlayerDisconnected?.Invoke(playerNumber);
            }
        }

        private void OnAirConsoleMessage(int fromDeviceId, JToken data)
        {
            if (!deviceToPlayerMap.TryGetValue(fromDeviceId, out int playerNumber))
            {
                if (debugMode)
                    Debug.LogWarning($"[AirConsoleInputHandler] Message from unknown device: {fromDeviceId}");
                return;
            }

            ParseAndApplyInput(playerNumber, data);
        }
#endif

        #endregion

        #region Input Parsing

        private void ParseAndApplyInput(int playerNumber, JToken data)
        {
            try
            {
                string messageType = data["type"]?.ToString();

                switch (messageType)
                {
                    case "joystick":
                        ParseSingleJoystick(playerNumber, data);
                        break;

                    case "dual_joystick":
                        ParseDualJoystick(playerNumber, data);
                        break;

                    case "button":
                        ParseButton(playerNumber, data);
                        break;

                    default:
                        // Try legacy format (direct x/y values)
                        ParseLegacyFormat(playerNumber, data);
                        break;
                }
            }
            catch (Exception e)
            {
                if (debugMode)
                    Debug.LogError($"[AirConsoleInputHandler] Error parsing message: {e.Message}\nData: {data}");
            }
        }

        private void ParseButton(int playerNumber, JToken data)
        {
            string action = data["action"]?.ToString();
            bool pressed = data["state"]?.ToString() == "pressed";

            if (string.IsNullOrEmpty(action)) return;

            playerStates[playerNumber].SetButton(action, pressed);
            OnButtonStateChanged?.Invoke(playerNumber, action, pressed);

            if (debugMode)
                Debug.Log($"[AirConsoleInputHandler] Player {playerNumber + 1} button '{action}' {(pressed ? "pressed" : "released")}");
        }

        private void ParseSingleJoystick(int playerNumber, JToken data)
        {
            int limbIndex = data["limb"]?.ToObject<int>() ?? 0;
            float x = data["x"]?.ToObject<float>() ?? 0f;
            float y = data["y"]?.ToObject<float>() ?? 0f;

            Vector2 input = ApplyDeadzone(new Vector2(x, y));
            ApplyInputToLimb(playerNumber, limbIndex, input);
        }

        private void ParseDualJoystick(int playerNumber, JToken data)
        {
            // Left joystick -> first limb
            JToken leftData = data["left"];
            if (leftData != null)
            {
                float lx = leftData["x"]?.ToObject<float>() ?? 0f;
                float ly = leftData["y"]?.ToObject<float>() ?? 0f;
                Vector2 leftInput = ApplyDeadzone(new Vector2(lx, ly));
                ApplyInputToLimb(playerNumber, 0, leftInput);
            }

            // Right joystick -> second limb
            JToken rightData = data["right"];
            if (rightData != null)
            {
                float rx = rightData["x"]?.ToObject<float>() ?? 0f;
                float ry = rightData["y"]?.ToObject<float>() ?? 0f;
                Vector2 rightInput = ApplyDeadzone(new Vector2(rx, ry));
                ApplyInputToLimb(playerNumber, 1, rightInput);
            }
        }

        private void ParseLegacyFormat(int playerNumber, JToken data)
        {
            // Fallback: treat as single joystick controlling left limb
            float x = data["x"]?.ToObject<float>() ?? 0f;
            float y = data["y"]?.ToObject<float>() ?? 0f;

            if (x != 0f || y != 0f)
            {
                Vector2 input = ApplyDeadzone(new Vector2(x, y));
                ApplyInputToLimb(playerNumber, 0, input);
            }
        }

        private void ApplyInputToLimb(int playerNumber, int localLimbIndex, Vector2 input)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers) return;

            var state = playerStates[playerNumber];

            if (localLimbIndex == 0)
            {
                state.leftLimb.Update(input);
            }
            else
            {
                state.rightLimb.Update(input);
            }

            int globalLimbIndex = state.GetGlobalLimbIndex(localLimbIndex);
            OnLimbInputReceived?.Invoke(globalLimbIndex, playerNumber, input);
        }

        private Vector2 ApplyDeadzone(Vector2 input)
        {
            if (input.magnitude < deadzone)
                return Vector2.zero;

            // Remap to use full range after deadzone
            return input.normalized * ((input.magnitude - deadzone) / (1f - deadzone));
        }

        private void ApplyInputSmoothing()
        {
            for (int p = 0; p < maxPlayers; p++)
            {
                var state = playerStates[p];
                int leftGlobal = state.GetGlobalLimbIndex(0);
                int rightGlobal = state.GetGlobalLimbIndex(1);

                smoothedInputs[leftGlobal] = Vector2.SmoothDamp(
                    smoothedInputs[leftGlobal],
                    state.leftLimb.joystickInput,
                    ref inputVelocities[leftGlobal],
                    inputSmoothTime
                );

                smoothedInputs[rightGlobal] = Vector2.SmoothDamp(
                    smoothedInputs[rightGlobal],
                    state.rightLimb.joystickInput,
                    ref inputVelocities[rightGlobal],
                    inputSmoothTime
                );
            }
        }

        #endregion

        #region Player Assignment

        private int AssignPlayerNumber(int deviceId)
        {
            // Check if already assigned
            if (deviceToPlayerMap.TryGetValue(deviceId, out int existingPlayer))
                return existingPlayer;

            // Find first slot not claimed by a real controller
            for (int i = 0; i < maxPlayers; i++)
            {
                if (!deviceToPlayerMap.ContainsValue(i))
                {
                    deviceToPlayerMap[deviceId] = i;
                    return i;
                }
            }

            if (debugMode)
                Debug.LogWarning($"[AirConsoleInputHandler] No player slot available for device {deviceId}");

            return -1;
        }

        private void CheckAllPlayersReady()
        {
            int connectedCount = 0;
            foreach (var state in playerStates)
            {
                if (state.isConnected) connectedCount++;
            }

            if (connectedCount >= maxPlayers)
            {
                OnAllPlayersReady?.Invoke();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the input state for a specific player (0-2 for 3 players)
        /// </summary>
        public PlayerInputState GetPlayerState(int playerNumber)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers)
                return null;
            return playerStates[playerNumber];
        }

        /// <summary>
        /// Get the raw input for a specific global limb index (0-5 for 6 limbs)
        /// </summary>
        public Vector2 GetLimbInput(int globalLimbIndex)
        {
            int totalLimbs = maxPlayers * limbsPerPlayer;
            if (globalLimbIndex < 0 || globalLimbIndex >= totalLimbs)
                return Vector2.zero;

            int playerNumber = globalLimbIndex / limbsPerPlayer;
            int localLimbIndex = globalLimbIndex % limbsPerPlayer;

            Vector2 raw = playerStates[playerNumber].GetLimbInput(localLimbIndex).joystickInput;

            return raw;
        }

        /// <summary>
        /// Get smoothed input for a specific global limb index (0-5 for 6 limbs)
        /// </summary>
        public Vector2 GetSmoothedLimbInput(int globalLimbIndex)
        {
            int totalLimbs = maxPlayers * limbsPerPlayer;
            if (globalLimbIndex < 0 || globalLimbIndex >= totalLimbs)
                return Vector2.zero;

            return smoothedInputs[globalLimbIndex];
        }

        /// <summary>
        /// Get the total number of connected players
        /// </summary>
        public int GetConnectedPlayerCount()
        {
            int count = 0;
            foreach (var state in playerStates)
            {
                if (state.isConnected) count++;
            }
            return count;
        }

        /// <summary>
        /// Check if a specific limb has active input
        /// </summary>
        public bool IsLimbActive(int globalLimbIndex)
        {
            int totalLimbs = maxPlayers * limbsPerPlayer;
            if (globalLimbIndex < 0 || globalLimbIndex >= totalLimbs)
                return false;

            int playerNumber = globalLimbIndex / limbsPerPlayer;
            int localLimbIndex = globalLimbIndex % limbsPerPlayer;

            return playerStates[playerNumber].GetLimbInput(localLimbIndex).isActive;
        }

        /// <summary>
        /// Get the total number of limbs
        /// </summary>
        public int GetTotalLimbCount() => maxPlayers * limbsPerPlayer;

        /// <summary>
        /// Direct input path for DirectControllerServer (bypasses AirConsole event chain).
        /// Call from main thread only.
        /// </summary>
        public void SetDirectInput(int playerNumber, Vector2 leftJoystick, Vector2 rightJoystick)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers) return;

            directControllerActive[playerNumber] = true;
            playerStates[playerNumber].isConnected = true;

            Vector2 left = ApplyDeadzone(leftJoystick);
            Vector2 right = ApplyDeadzone(rightJoystick);

            playerStates[playerNumber].leftLimb.Update(left);
            playerStates[playerNumber].rightLimb.Update(right);

            int leftGlobal = playerStates[playerNumber].GetGlobalLimbIndex(0);
            int rightGlobal = playerStates[playerNumber].GetGlobalLimbIndex(1);
            OnLimbInputReceived?.Invoke(leftGlobal, playerNumber, left);
            OnLimbInputReceived?.Invoke(rightGlobal, playerNumber, right);
        }

        public void SetPlayerDisconnected(int playerNumber)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers) return;
            directControllerActive[playerNumber] = false;
            playerStates[playerNumber].isConnected = false;
            playerStates[playerNumber].ClearInputs();
            playerStates[playerNumber].ClearButtons();
            OnPlayerDisconnected?.Invoke(playerNumber);
        }

        /// <summary>
        /// Set a button state for a player. Called by DirectControllerServer for the direct input path.
        /// </summary>
        public void SetButtonState(int playerNumber, string action, bool pressed)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers) return;
            playerStates[playerNumber].SetButton(action, pressed);
            OnButtonStateChanged?.Invoke(playerNumber, action, pressed);

            if (debugMode)
                Debug.Log($"[AirConsoleInputHandler] Player {playerNumber + 1} button '{action}' {(pressed ? "pressed" : "released")} (direct)");
        }

        /// <summary>
        /// Check if a player's button action is currently pressed.
        /// </summary>
        public bool IsButtonPressed(int playerNumber, string action)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers) return false;
            return playerStates[playerNumber].IsButtonPressed(action);
        }

        /// <summary>
        /// Get how long a player has been holding a button (seconds). Returns 0 if not pressed.
        /// </summary>
        public float GetButtonHoldTime(int playerNumber, string action)
        {
            if (playerNumber < 0 || playerNumber >= maxPlayers) return 0f;
            return playerStates[playerNumber].GetButtonHoldTime(action);
        }

        #endregion

        #region Editor Testing (Keyboard Fallback)

#if UNITY_EDITOR
        private void HandleKeyboardFallback()
        {
            try
            {
                // Skip keyboard for slots that have a real controller connected
                bool slot0HasController = directControllerActive[0] || deviceToPlayerMap.ContainsValue(0);
                bool slot1HasController = directControllerActive[1] || deviceToPlayerMap.ContainsValue(1);

                if (!slot0HasController)
                {
                    Vector2 p1Left = new Vector2(
                        (UnityEngine.Input.GetKey(KeyCode.D) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.A) ? 1 : 0),
                        (UnityEngine.Input.GetKey(KeyCode.W) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.S) ? 1 : 0)
                    );
                    Vector2 p1Right = new Vector2(
                        (UnityEngine.Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                        (UnityEngine.Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.DownArrow) ? 1 : 0)
                    );
                    if (!playerStates[0].isConnected)
                    {
                        playerStates[0].isConnected = true;
                        playerStates[0].nickname = "Keyboard Player";
                    }
                    playerStates[0].leftLimb.Update(p1Left);
                    playerStates[0].rightLimb.Update(p1Right);
                }

                if (!slot1HasController)
                {
                    Vector2 p2Left = new Vector2(
                        (UnityEngine.Input.GetKey(KeyCode.L) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.J) ? 1 : 0),
                        (UnityEngine.Input.GetKey(KeyCode.I) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.K) ? 1 : 0)
                    );
                    Vector2 p2Right = new Vector2(
                        (UnityEngine.Input.GetKey(KeyCode.Keypad6) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.Keypad4) ? 1 : 0),
                        (UnityEngine.Input.GetKey(KeyCode.Keypad8) ? 1 : 0) - (UnityEngine.Input.GetKey(KeyCode.Keypad2) ? 1 : 0)
                    );
                    if (!playerStates[1].isConnected)
                    {
                        playerStates[1].isConnected = true;
                        playerStates[1].nickname = "Keyboard Player 2";
                    }
                    playerStates[1].leftLimb.Update(p2Left);
                    playerStates[1].rightLimb.Update(p2Right);
                }
            }
            catch (System.InvalidOperationException)
            {
            }
        }
#endif

        #endregion
    }
}

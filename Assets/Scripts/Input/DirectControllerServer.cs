using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Octo.Input;

public class DirectControllerServer : MonoBehaviour
{
    [SerializeField] private int port = 7844;
    private const int MAX_SLOTS = 3;

    private WebSocketServer wsServer;

    private struct InputData
    {
        public int slot;
        public float lx, ly, rx, ry;
    }

    private struct ButtonData
    {
        public int slot;
        public string action;
        public bool pressed;
    }

    public struct ControlEvent
    {
        public enum Type { Connect, Disconnect }
        public Type type;
        public int slot;
    }

    private static readonly ConcurrentQueue<InputData> pendingInputs = new();
    private static readonly ConcurrentQueue<ButtonData> pendingButtons = new();
    internal static readonly ConcurrentQueue<ControlEvent> pendingEvents = new();

    private static readonly object slotLock = new();
    private static readonly Dictionary<string, int> sessionToSlot = new();
    private static readonly bool[] slotUsed = new bool[MAX_SLOTS];

    void Start()
    {
        lock (slotLock)
        {
            sessionToSlot.Clear();
            for (int i = 0; i < MAX_SLOTS; i++) slotUsed[i] = false;
        }

        wsServer = new WebSocketServer(port);
        wsServer.AddWebSocketService<ControllerBehavior>("/controller");
        wsServer.Start();

        string localIP = GetLocalIP();
        Debug.Log($"[DirectController] Server started on port {port}");
        Debug.Log($"[DirectController] Phones connect to: http://{localIP}:7842/controller_direct.html");
    }

    void Update()
    {
        var handler = AirConsoleInputHandler.Instance;
        if (handler == null) return;

        while (pendingEvents.TryDequeue(out ControlEvent evt))
        {
            if (evt.type == ControlEvent.Type.Connect)
                Debug.Log($"[DirectController] Player {evt.slot + 1} joined (slot {evt.slot})");
            else
            {
                handler.SetPlayerDisconnected(evt.slot);
                Debug.Log($"[DirectController] Player {evt.slot + 1} left");
            }
        }

        while (pendingInputs.TryDequeue(out InputData input))
        {
            handler.SetDirectInput(input.slot,
                new Vector2(input.lx, input.ly),
                new Vector2(input.rx, input.ry));
        }

        while (pendingButtons.TryDequeue(out ButtonData btn))
        {
            handler.SetButtonState(btn.slot, btn.action, btn.pressed);
        }
    }

    void OnDestroy()
    {
        if (wsServer != null)
        {
            wsServer.Stop();
            wsServer = null;
        }
        lock (slotLock)
        {
            sessionToSlot.Clear();
            for (int i = 0; i < MAX_SLOTS; i++) slotUsed[i] = false;
        }
    }

    /// <summary>
    /// Thread-safe. Always picks the lowest free slot (0 first).
    /// </summary>
    public static int ClaimSlot(string sessionId)
    {
        lock (slotLock)
        {
            if (sessionToSlot.TryGetValue(sessionId, out int existing))
                return existing;

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (!slotUsed[i])
                {
                    slotUsed[i] = true;
                    sessionToSlot[sessionId] = i;
                    return i;
                }
            }
            return -1;
        }
    }

    public static void ReleaseSlot(string sessionId)
    {
        lock (slotLock)
        {
            if (sessionToSlot.TryGetValue(sessionId, out int slot))
            {
                slotUsed[slot] = false;
                sessionToSlot.Remove(sessionId);
            }
        }
    }

    public static void EnqueueInput(int slot, float lx, float ly, float rx, float ry)
    {
        pendingInputs.Enqueue(new InputData { slot = slot, lx = lx, ly = ly, rx = rx, ry = ry });
    }

    public static void EnqueueButton(int slot, string action, bool pressed)
    {
        pendingButtons.Enqueue(new ButtonData { slot = slot, action = action, pressed = pressed });
    }

    static string GetLocalIP()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return ((System.Net.IPEndPoint)socket.LocalEndPoint).Address.ToString();
        }
        catch { return "localhost"; }
    }
}

public class ControllerBehavior : WebSocketBehavior
{
    private int slot = -1;

    protected override void OnOpen()
    {
        slot = DirectControllerServer.ClaimSlot(ID);
        if (slot < 0) { Context.WebSocket.Close(); return; }

        Send(new JObject
        {
            { "type", "assigned" },
            { "device_id", slot }
        }.ToString());

        DirectControllerServer.pendingEvents.Enqueue(
            new DirectControllerServer.ControlEvent
            {
                type = DirectControllerServer.ControlEvent.Type.Connect,
                slot = slot
            });
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        if (slot < 0) return;
        try
        {
            var data = JObject.Parse(e.Data);
            if (data["type"]?.ToString() == "dual_joystick")
            {
                DirectControllerServer.EnqueueInput(slot,
                    (float)(data["left"]?["x"] ?? 0),
                    (float)(data["left"]?["y"] ?? 0),
                    (float)(data["right"]?["x"] ?? 0),
                    (float)(data["right"]?["y"] ?? 0));
            }
            else if (data["type"]?.ToString() == "button")
            {
                string action = data["action"]?.ToString();
                bool pressed = data["state"]?.ToString() == "pressed";
                if (!string.IsNullOrEmpty(action))
                    DirectControllerServer.EnqueueButton(slot, action, pressed);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DirectController] Parse error: {ex.Message}");
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        if (slot >= 0)
        {
            DirectControllerServer.ReleaseSlot(ID);
            DirectControllerServer.pendingEvents.Enqueue(
                new DirectControllerServer.ControlEvent
                {
                    type = DirectControllerServer.ControlEvent.Type.Disconnect,
                    slot = slot
                });
        }
    }
}

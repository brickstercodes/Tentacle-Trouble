# Octo - Frantic Co-op Physics Game

## Project Overview

**Game Concept:** An Octodad-style physics-based co-op game where 3 players control an octopus together using their smartphones as controllers via AirConsole.

**Purpose:** College fest game - meant to be chaotic, fun, and accessible to anyone with a phone.

**Tech Stack:**

- Unity 6 (6000.0.23f1)
- Universal Render Pipeline (URP)
- AirConsole Unity Plugin v2.5.7
- WebGL build target

---

## Player Control Scheme

Each player controls **2 tentacles** using **dual joysticks** on their phone:

- **Left Joystick:** Controls one tentacle
- **Right Joystick:** Controls another tentacle
- **3 players × 2 tentacles = 6 tentacles total**

Player-to-tentacle mapping:
| Player | Device ID | Left Joystick | Right Joystick |
|--------|-----------|---------------|----------------|
| 1 | 1 | Tentacle 0 | Tentacle 1 |
| 2 | 2 | Tentacle 2 | Tentacle 3 |
| 3 | 3 | Tentacle 4 | Tentacle 5 |

---

## Game Direction Plan (What the 3 Players Must Achieve)

### Core Fantasy

Three people are one clumsy octopus. They are not just "moving a character" — they are trying to coordinate chaos under time pressure.

### Primary Goal Loop

In each level, the team must complete **3 objective tasks** before time runs out:

1. **Navigate** to target zones (door, lever room, kitchen, dock, etc.)
2. **Manipulate** objects with tentacles (grab, drag, press, carry)
3. **Deliver/trigger** final objective (place item, activate machine, escape point)

If time ends, round fails. If they complete all objectives, they win stars/score.

### Why 3 Players Matters

- Movement requires agreement (dot-product based coordination)
- One player alone can wobble limbs but cannot efficiently move body
- Conflicting directions should visibly reduce movement efficiency
- Team communication becomes the core skill (fun chaos)

### Example Win Scenario

- Player 1 pushes right, Player 2 pushes right, Player 3 pushes right -> body translates quickly right
- Player 1 pushes right, Player 2 pushes left, Player 3 idle -> near-zero net progress, wobble/struggle
- Team re-coordinates -> regains speed -> reaches objective

### Suggested First Playable Mode

**"Kitchen Cleanup" (3-minute round):**
- Pick up 3 marked objects
- Carry/drop each into sink zone
- Hit final button to finish
- Score = completion time + number of drops/collisions penalty

---

## Completed Components

### 1. ProceduralTentacle System

**File:** `Assets/Scripts/Procedural/ProceduralTentacle.cs`

A multi-segment physics-driven tentacle using Configurable Joints:

- Supports both keyboard input (for testing) and AirConsole input
- `tentacleId` field identifies which joystick controls it
- Smooth movement with configurable speed and physics parameters
- Each segment connected via joints with angular limits

### 2. OctopusLocomotion System

**File:** `Assets/Scripts/Procedural/OctopusLocomotion.cs`

Manages overall octopus movement based on combined tentacle inputs:

- Aggregates input from all 6 tentacles
- Applies forces to the octopus body Rigidbody
- Configured for 3-player mode

### 3. HeadWobble System

**File:** `Assets/Scripts/Procedural/HeadWobble.cs`

Adds personality to the octopus head:

- Wobbles based on velocity
- Uses Perlin noise for organic movement

### 4. AirConsole Input Handler

**File:** `Assets/Scripts/Input/AirConsoleInputHandler.cs`

Bridge between controller messages and tentacle control:

- Supports direct controller input path used by `DirectControllerServer`
- Parses JSON with format: `{"type":"dual_joystick","left":{"x":0,"y":0},"right":{"x":0,"y":0}}`
- Maps player slots (0-2) to tentacle pairs (0-5)
- Keyboard fallback is disabled per slot when a real phone controller is active

### 5. Custom Dual-Joystick Controller

**File:** `Assets/AirConsole/controller.html` (461 lines)

HTML/JS controller with:

- Two touch joysticks (left and right)
- Landscape orientation enforced
- Sends input at 30fps via `airconsole.message()`
- Visual feedback with colored joystick knobs

**Simplified Version:** `Assets/AirConsole/controller_simple.html`

- Uses AirConsole's official Joystick library from CDN
- Cleaner implementation, same dual-joystick functionality

---

## File Structure

```
Assets/
├── AirConsole/
│   ├── controller.html              # Custom dual-joystick controller
│   ├── controller_simple.html       # Simplified version using AirConsole Joystick lib
│   ├── scripts/
│   │   ├── AirConsole.cs            # Main AirConsole Unity integration
│   │   ├── Settings.cs              # Port configuration (7842, 7843)
│   │   └── editor/
│   │       ├── Extentions.cs        # Opens browser on Play, manages webserver
│   │       └── Inspector.cs         # Custom inspector for AirConsole component
│   └── examples/
│       ├── basic/basic.unity        # Basic example scene
│       └── pong/                    # (DELETED - was interfering)
│
├── Scripts/
│   ├── Input/
│   │   └── AirConsoleInputHandler.cs
│   └── Procedural/
│       ├── ProceduralTentacle.cs
│       ├── OctopusLocomotion.cs
│       └── HeadWobble.cs
│
├── WebGLTemplates/
│   └── AirConsole-U6/
│       ├── controller.html          # Controller served by webserver (copy of above)
│       ├── index.html               # Game screen template
│       └── screen.html
│
└── Scenes/
    └── SampleScene.unity            # Main development scene
```

---

## AirConsole Configuration

### Ports

- **Web Server Port:** 7842 (serves controller.html)
- **WebSocket Port:** 7843 (Unity ↔ Browser communication)

### Browser Start Modes

| Mode                    | Description                                   | Status                         |
| ----------------------- | --------------------------------------------- | ------------------------------ |
| **Normal**              | Uses local webserver, loads custom controller | ✅ Fixed (PNA headers added)   |
| **Virtual Controllers** | Uses AirConsole's servers                     | ❌ Blocked by browser security |
| **Debug**               | Normal + debug overlay                        | Not tested                     |
| **No Browser Start**    | Manual browser opening                        | Not tested                     |

### Local Network

- Local IP: `192.168.0.114`
- Full URL opened: `http://http.airconsole.com/?http=1#http://192.168.0.114:7842/?unity-editor-websocket-port=7843&unity-plugin-version=2.5.7`

---

## Current Issues

### Status Summary

Most connection and input pipeline blockers are resolved.

#### Resolved

1. **Reload loop in Normal mode**  
   Fixed by adding CORS + PNA preflight headers in `WebListener.cs`.

2. **Phone redirect loop (`?http=1...`)**  
   Avoided by using direct local controller page + direct WebSocket path.

3. **`NullReferenceException` in `AirConsole.ProcessJS`**  
   Fixed by removing timing-sensitive dependency for direct joystick flow.

4. **Controller only worked after multiple reloads**  
   Fixed by:
   - stable lowest-free slot assignment (0,1,2)
   - preventing keyboard fallback from overriding active phone slots

#### Current Focus

- Tune movement feel (speed, acceleration, struggle wobble)
- Build objective gameplay loop and scoring
- Add UI feedback for coordination quality (agreement meter)

---

## Debugging Steps Taken

1. ✅ Added retry logic to file copy (didn't help)
2. ✅ Changed to delete-then-copy (caused 0-byte corruption)
3. ✅ Changed to skip-if-identical (still had issues)
4. ✅ **Final fix:** Skip copy entirely, manual copy required
5. ✅ Deleted Pong example folder (was loading instead of our controller)
6. ✅ Created simplified controller using AirConsole's Joystick library
7. ✅ Verified webserver serves correct files via curl
8. ✅ **Reload loop fixed:** Added CORS + PNA headers to WebListener.cs

---

## Controller Message Format

The controller sends JSON messages to Unity:

```json
{
  "type": "dual_joystick",
  "left": {
    "x": -1.0 to 1.0,
    "y": -1.0 to 1.0
  },
  "right": {
    "x": -1.0 to 1.0,
    "y": -1.0 to 1.0
  }
}
```

`DirectControllerServer` forwards this to `AirConsoleInputHandler`, which updates:
- Left joystick -> local limb 0 of that player's slot
- Right joystick -> local limb 1 of that player's slot

---

## Next Steps to Try

1. **Lock vertical slice goal**: one 3-minute objective-based level
2. **Add objective system**: task tracker + completion conditions
3. **Implement scoring**: time bonus, collision/drop penalties
4. **Add clear win/lose UI**: countdown, objective progress, result screen
5. **Playtest with 3 phones**: tune agreement threshold and speeds

---

## Commands for Debugging

```bash
# Check if webserver is running
lsof -i :7842

# Check if websocket is running (only during Play mode)
lsof -i :7843

# Test if controller.html is served correctly
curl -s "http://localhost:7842/controller.html" | head -20

# Check controller.html file size
ls -la Assets/WebGLTemplates/AirConsole-U6/controller.html

# Copy controller manually if needed
cp "Assets/AirConsole/controller.html" "Assets/WebGLTemplates/AirConsole-U6/controller.html"
```

---

## Key Files to Reference

| Purpose                   | File Path                                             |
| ------------------------- | ----------------------------------------------------- |
| Main AirConsole script    | `Assets/AirConsole/scripts/AirConsole.cs`             |
| Browser/webserver startup | `Assets/AirConsole/scripts/editor/Extentions.cs`      |
| Port settings             | `Assets/AirConsole/scripts/Settings.cs`               |
| Input handler             | `Assets/Scripts/Input/AirConsoleInputHandler.cs`      |
| Direct controller server  | `Assets/Scripts/Input/DirectControllerServer.cs`       |
| Tentacle animation        | `Assets/Scripts/Animation/ProceduralTentacle.cs`       |
| Body locomotion           | `Assets/Scripts/Movement/OctopusLocomotion.cs`         |
| Direct phone controller   | `Assets/WebGLTemplates/AirConsole-U6/controller_direct.html` |

---

## Contact/Resources

- [AirConsole Developer Docs](https://developers.airconsole.com/)
- [AirConsole Unity Plugin Docs](https://developers.airconsole.com/#!/guides/unity)
- [Joystick Library](https://github.com/AirConsole/airconsole-controls/tree/master/joystick)

---

_Last Updated: March 3, 2026_

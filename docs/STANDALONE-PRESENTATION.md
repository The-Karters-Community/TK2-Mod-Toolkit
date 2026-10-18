# Standalone input viewer and camera controls

`standalone/` implements **Bobby's Race Tools**, a native Windows x64 application
containing the Gamepad viewer and Camera setup. F8 opens a compact interactive
camera panel above the game; this is an external Windows overlay, not injected
game UI. It targets windowed/borderless play like the input HUD. Build with
`python standalone/build.py`; the output is `artifacts/BobbyRaceTools`.
The current implementation is approximately 586 KiB including its embedded bridge
and statically linked C++ runtime. No BepInEx, Python, .NET or WebView is required
on the player's PC. The main Toolkit remains separate and unchanged.

## Implementation

- Win32 controls, GDI+ click-through overlay, individual resets, portable INI
  settings and configurable F8/F9 shortcuts. Controls stack and scroll on narrow
  windows; display scaling recreates fonts as well as resizing controls.
- Automatic local-player detection is the default. Input/camera callbacks observe
  only local human slots in a bounded six-entry timestamp table. A saved inactive
  slot falls back to the first active local racer, with explicit status. The
  selector is available on both pages and lists current local players.
- Explicit Connect loads an authored DLL through a thread-scoped Windows message
  hook. The bridge is SHA-256 verified in a versioned temporary cache. A bounded,
  current-owner shared mapping carries settings and snapshots; no network server.
- Camera output, mapped Rewired input and replay-input callbacks use MinHook
  v1.3.4 (BSD 2-Clause plus its HDE decoder notices). Source and provenance are
  included in `standalone/vendor/minhook` and `standalone/THIRD-PARTY.txt`.
- Types, exact method signatures, field types, enum values and field offsets are
  resolved from the running game's exported IL2CPP API when connecting. There are
  no game RVAs, executable hashes or patch-version gates in production code.
  The current MethodInfo's first native method-pointer member is still an IL2CPP
  ABI dependency; executable-address and metadata checks do not guarantee every
  future engine or game implementation will remain compatible.
- Per-frame callbacks use cached offsets, methods and rooted action strings.
  Modern IL2CPP GC handles use pointer-sized integers. Runtime metadata walks,
  heap allocation, scene scans and synchronous requests are outside the hot path.
- Camera overrides start off. Only the selected local player's racing camera is
  adjusted; intro/spectator cameras remain native. Disabling the checkbox applies
  the native output immediately. Closing/disconnecting zeros the heartbeat; a
  missed heartbeat stops effects within 1.5 seconds. Installed detours become
  pass-through and the DLL remains pinned until the game exits, avoiding unload
  races with active callbacks.
- Live driving samples win over opponent ghosts. Replay ownership uses the game's
  `IsPlayerControlledByReplayInput(Ant_Player)`. Replay click pulses are sampled
  before the native method consumes/clears them, and cleared on source changes.
- Version 0.2.0 draws directly in the D3D11 back buffer for windowed, borderless
  and fullscreen play. Do not run equivalent Toolkit features simultaneously.

## Current evidence and validation (2026-10-18)

Game 0.1.4.24, Unity 6000.0.75f1, IL2CPP metadata 31. Installed GameAssembly
SHA-256: `55f5c88972fe1ccf4c9d5f3871b71bcfa261b3e42a379e18438e92623fc4d6a2`.
Fresh metadata is under ignored `local/input-source-evidence/dump`.

| Native method | Evidence RVA, not used by production | Observed behavior |
| --- | --- | --- |
| `PixelGameKartCamera.GetRacingCamPositionAndRotation(float, ref Vector3, ref Quaternion, ref float)` | `0x504C10` | Reads delta time and writes caller-owned position, rotation and FOV outputs. |
| `Ant_KartInput.ProcessRacingInput()` | `0x6B5180` | Reads the local player's mapped controls and updates kart input data. |
| `PTK_PlayerReplayInput_ReplaySerializer.ProcessAndSetPlayerInputFromReplay(PTK_KartInputBase)` | `0x62E290` | Copies recorded fields into kart input, then clears click pulses. |
| `PTK_ReplayManager.IsPlayerControlledByReplayInput(Ant_Player)` | `0x6C6D60` | Player-type/playback-mode branches select replay ownership. |

Ghidra 12.1.4 headless decompiled current extracted native bodies. This is focused
pseudocode evidence, not recovered C#; callees outside the extracts were not
analyzed. Generated game data stays in ignored `local/standalone-evidence` and
`local/input-source-evidence`, never in the distribution.

With user approval and the game closed, `winhttp.dll` was temporarily renamed.
The user launched a local race with BepInEx disabled. The executable connected,
resolved all three capabilities, sampled live inputs and rendered its input HUD.
A read-only 12-second run recorded 611 live input and 1,435 camera callbacks with
zero faults. Enabling camera overrides changed native FOV 75 to 65; disabling
restored 75, with zero faults. Captures and diagnostic reports are in ignored
`local/standalone-*`. The original BepInEx loader was restored after game exit and
its original SHA-256 verified:
`8c6cdbc38836dee87e3368f5de1994d7c0ccebf29e4ce7aba3c0981f9375412c`.

The first loading attempt exposed a cache-location issue on this machine; the
verified temporary-cache path is used instead. A window-directed bootstrap also
avoids Unity filtering thread-only messages during a race. These changes are
transport fixes, not game-version tables or build-specific offsets.

Native core assertions cover framing geometry, rotations, smoothing, bounded
settings, shared protocol and live/replay source precedence. Native GUI smoke
checks passed fresh defaults, individual reset, aim control enablement, camera
toggle visibility, real tab switching, narrow-window scrolling, shortcut/player
persistence, relocated-folder launch and clean exit. Python suite: 136 passed. The build receipt
records compilation and core tests only; live checks above are separate evidence.
Standalone ghost playback, split-screen, actual controller remapping and a timed
performance comparison have not yet been verified. No claim of zero frame cost
or universal future-build compatibility is made.


## 0.1.1 player-selection regression and camera overlay

The user reported responsive controls with no effect, persisting after restarting
the app. There was no corresponding application crash event. The saved setting
was local slot 2 (Player 3); the current race had only local slot 0 (Player 1).
The unmodified executable connected with all three capabilities but recorded zero
input and camera callbacks for that selection. Running the same bridge against
Player 1 immediately recorded 737 live samples and 1,441 camera callbacks.
This establishes a selection error, not a timed bridge/process failure.

The corrected executable was tested with the same saved Player 3 setting. It
reported `Player 3 is not active · using Player 1`, active mask 1, and resumed
input/camera snapshots without restarting the game. Automatic selection is the
new default; explicit active split-screen selections remain available. Regression
assertions cover missing slots, valid explicit selection, no local player and
expiry across scene/player changes.

F8 now restyles the existing controls into a dark tool window within the game
viewport, with independent resets, Back to game and Desktop actions. It hides
while another application has focus. Closing the panel does not disconnect or
exit the application. No extra game hook, renderer hook or BepInEx dependency is
introduced for this UI. Checkboxes and disabled labels have explicit contrasting
colors in the dark panel.

Live checks passed saved-slot fallback, switching to Automatic, FOV 75 -> 80 ->
75, three disconnect/reconnect cycles, panel close/reopen from a minimized
window, desktop/panel transitions and continued updates during a longer session,
all with zero reported bridge faults. The user also confirmed the F8 camera
panel appears over the game. The Toolkit's two equivalent modules were
already disabled in this session. Test diagnostics and captures remain in ignored
`local/standalone-*`. The user's FOV and other preferences were preserved; their
local-player selection was switched to Automatic. No game files were changed.


## 0.2.0 fullscreen controls and desktop lifetime

The 0.1.1 floating-window approach did not reliably survive fullscreen or repeated
F8 toggles. Its `WS_EX_TOOLWINDOW` conversion also removed the app from the
taskbar. That implementation is removed. The desktop HWND remains a normal
`WS_OVERLAPPEDWINDOW`; F8 only toggles a shared panel-open flag.

An authored renderer uses unchanged Dear ImGui 1.91.9b Win32/DX11 backends to draw
the Camera panel and input HUD into the game's back buffer before DXGI Present.
The stable IDXGISwapChain COM slots for Present and ResizeBuffers are discovered
with a temporary D3D11 device, not game-version offsets. The real swap chain is
accepted only for the game's HWND and a D3D11 device. Back-buffer references are
released before resize; replacement devices/swap chains recreate renderer resources.
Device identity is checked as well as the swap-chain pointer, since COM allocations
can reuse a previous chain address during a mode change.
No extra overlay HWND, game-window restyling or desktop focus cycling is used.

Windows events are queued to the render thread; Dear ImGui has a private context.
Keyboard navigation uses the upstream backend. XInput discovery is cached and
controller events are fed only in explicit editing mode; the upstream backend
is compiled with its always-on gamepad feeder disabled. Driving cannot move
focused UI sliders while the panel is passive. The camera UI
publishes changes through a revision/acknowledgement mailbox; the desktop process
remains the sole settings writer and saves changes automatically. The renderer owns
a separate mapping handle/view, so reconnect cannot invalidate render-thread reads.

While the panel is visible and the game is foreground, Unity's main thread unlocks
the cursor and saves/restores the game's requested lock and visibility state. The panel permits normal driving by default. Ctrl+Tab, controller View/Back or
the editing checkbox explicitly enables keyboard/gamepad navigation. Local
human driving input data is neutralized only in that mode; CPU and remote player input
is unchanged. Closing, losing focus or a missing desktop heartbeat releases capture.
Engine calls are never made from the render thread. The current Cursor getters and
setters are resolved by exact runtime signatures. Focused Ghidra extracts at RVAs
0x329BAC0, 0x329BAF0, 0x329BB20 and 0x329BB60 show cached engine-call dispatch,
with int32 lock mode and one-byte visibility arguments. Return types and static
signatures come from current IL2CPP declarations. These RVAs are evidence only.
Input field types and the ProcessRacingInput body are the same current evidence
listed above. No generated game code or game data is distributed.

Validation so far: the real D3D11 renderer harness passes Present, ResizeBuffers,
swap-chain/device replacement, mapping rebind and heartbeat shutdown. Mocked
XInput in the isolated test process verifies that controller editing changes
settings, then the same held D-pad/stick cannot alter settings after returning to
passive driving. The harness never sends real keys or changes foreground focus. The running
game connected with all four capabilities, renderer state 1, an XInput controller
present and zero bridge faults. The user confirmed repeated F8 toggles and mouse/controller interaction work
with the desktop app still available. Their follow-up requested normal driving
while the panel remains visible; the explicit editing mode addresses that request.
Exclusive DXGI fullscreen was not separately observed (the captured session
reported DXGI fullscreen=false). No claim about every display/driver is made.

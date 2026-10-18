# Standalone input viewer and camera controls

`standalone/` implements **Bobby's Race Tools**, a native Windows x64 application
containing the Gamepad viewer and Camera setup. Build with
`python standalone/build.py`; the output is `artifacts/BobbyRaceTools`.
The first implementation is approximately 580 KiB including its embedded bridge
and statically linked C++ runtime. No BepInEx, Python, .NET or WebView is required
on the player's PC. The main Toolkit remains separate and unchanged.

## Implementation

- Win32 controls, GDI+ click-through overlay, individual resets, portable INI
  settings and configurable F8/F9 shortcuts. Controls stack and scroll on narrow
  windows; display scaling recreates fonts as well as resizing controls.
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
- The overlay targets windowed/borderless play. Exclusive-fullscreen visibility
  is not supported. Do not run the equivalent Toolkit features simultaneously.

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

# Bobby's Race Tools

A small Windows app for **camera framing** and a **driving input overlay** in
The Karters 2 Turbo Charged. No BepInEx, .NET, Python, or browser runtime required.

[Quick user manual](USER-MANUAL.md) · [Feature list](FEATURES.md)

## Use

1. Run **BobbyRaceTools.exe** and start the game yourself.
2. Click **Connect**. **Automatic** finds your active local player.
3. Enable **Camera overrides** to adjust framing. Each value has its own reset.

**F8** shows or hides Camera inside the game, including fullscreen. The desktop
app stays available in the taskbar and Alt+Tab. You can keep driving with the panel
visible and adjust values with the mouse.

Press **Ctrl+Tab**, controller **View/Back**, or select **Keyboard / controller
editing** to navigate the panel instead of driving. Tab/arrows or D-pad/stick move
between controls; A selects a slider, the D-pad adjusts it, and Y resets it. **B**, **Esc**,
Ctrl+Tab or View returns control to the race while leaving the panel visible.
Driving inputs are held neutral only during this explicit editing mode.

**F9** toggles the input viewer. Change either shortcut in Input viewer.
Settings save automatically beside the executable; read-only installations use
`%LOCALAPPDATA%\BobbyRaceTools` instead.

The local player selector is visible on both settings pages. It lists active
local racers. If a saved player is no longer active, the app uses the first
available local racer and tells you which one.

Camera overrides start off in a fresh installation. Disabling them, disconnecting,
or closing the app returns control to the game. Inputs follow the game's mapped
keyboard/controller actions. Your inputs take priority when racing opponent ghosts;
recorded input viewing is optional.

The input viewer and Camera panel are drawn by the game's Direct3D 11 renderer.
They follow its window, resolution and fullscreen mode. The equivalent Toolkit
Camera and Gamepad viewer should be disabled when using Race Tools.

## Compatibility

Windows 10/11 x64, Direct3D 11. Connection, live inputs and camera changes were
verified on game **0.1.4.24** with BepInEx disabled. Fullscreen rendering uses
the same in-game back buffer as the race.
Replay source selection has regression coverage, but a standalone ghost session
has not yet been tested. The two programs should run with the same permissions.

The app discovers named game types, method signatures and field layouts when
connecting. It has no fixed game addresses or patch-version allowlist. Ordinary
address changes should not need an update; major changes to game or engine
internals can still require a fix. No promise of compatibility with every future
build is possible.

The executable contains an authored native bridge, extracted to a hashed cache
under `%TEMP%\BobbyRaceTools`. Connecting loads it into the game for the camera
and mapped inputs. No game files or saves are changed. A small inactive bridge
remains loaded until the game exits; it stops applying settings when disconnected,
and falls back after 1.5 seconds if the app stops responding.

## Build from source

Install Python and Visual Studio C++ x64 Build Tools, then run from the repository:

```powershell
python standalone/build.py
```

The script runs the native core tests and creates
`artifacts/BobbyRaceTools/BobbyRaceTools.exe`, with the C++ runtime and bridge
embedded. Only Windows system DLLs are required. Build records and intermediates
stay under ignored `artifacts/` and `local/`.

Implementation evidence is in `docs/STANDALONE-PRESENTATION.md` in the source
repository. Open-source notices are in [THIRD-PARTY.txt](THIRD-PARTY.txt),
[MinHook-LICENSE.txt](MinHook-LICENSE.txt) and [ImGui-LICENSE.txt](ImGui-LICENSE.txt).

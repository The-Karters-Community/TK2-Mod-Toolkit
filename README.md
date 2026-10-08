# TK2 Mod Studio

A local, readable modding workspace for The Karters 2: Turbo Charged. Initial target: **0.1.4.18 / beta_dev**, installed BepInEx **6.0.0-be.788**.

This repository contains an initial runnable Windows GUI, local analysis/export scripts, readable C# templates, and a compiled customization starter. Recovered game declarations and Ghidra pseudocode are local analysis artifacts, not original source code.

See [the roadmap](docs/ROADMAP.md), [investigation](docs/INVESTIGATION.md), and [mod possibilities](docs/CAPABILITIES.md).

## Start

Double-click **Launch Studio.cmd**, or run `python launch.py`. Python with tkinter and a .NET SDK are required; both are already installed here. No additional Python packages are needed.

The GUI includes installation diagnostics; searchable declarations, a full native function index and pseudocode; managed mod recovery; C# editing and new-mod creation; build/deploy; DLL toggles; config editing; supported starter settings; backups/restore; and logs.

The starter provides HUD scaling, Wwise volume multiplication, main-camera FOV, and experimental offline fast fall. All toggles default off. It builds successfully against the installed game, but **has not been tested in-game or deployed automatically**.

Read [usage and limitations](docs/USAGE.md), [source reconstruction](docs/RECONSTRUCTION.md), [legacy patch audit](docs/LEGACY-AUDIT.md), and [validation status](docs/VALIDATION.md).

## Workspace layout

```text
studio/                    Native tkinter GUI and filesystem/build services
plugins/TK2.Customization/  Readable starter plugin source
templates/HelloMod/        New-mod C# template
tools/ghidra/              Read-only existing-project export script and targets
tests/                     Core behavior/integrity tests
docs/                      Investigation, plan, possibilities and validation
local/                     Ignored declarations, native exports, recovered mods, backups
artifacts/                 Ignored staged DLLs and build receipts
```

Game binaries and recovered third-party sources are not committed. Existing BepInEx remains the runtime loader. Studio automates authoring and management above that loader; alternative loaders and an authenticated live bridge are later decisions in the roadmap.

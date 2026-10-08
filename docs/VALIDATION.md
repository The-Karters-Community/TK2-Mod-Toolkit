# Validation and remaining acceptance work

Verified in this workspace on 2026-10-08. Compilation and source/signature audits do not certify runtime compatibility.

| Check | Result |
|---|---|
| Pack build | .NET 6 library against current installed runtime/interop, zero warnings/errors |
| Pack installation | Exactly one active DLL in BepInEx/plugins: TK2-Mod-Studio/TK2.Customization.dll, 30,208 bytes |
| Installed integrity | Installed DLL matches latest staged artifact; SHA256 6d551bb916d2a64df6959d51f0801679697f60e4b5a8f32f6438b7764eff1f63 |
| Initial config | All ten built-in enable settings and FrameLimiter recipe enable setting false |
| Python core/API tests | 26 passed: containment, artifacts, rollback, settings, source conflicts, recipe creation, gameplay lock, host/origin/session checks |
| Reconstructed C# tests | 31 assertions passed: jump state/grounding/grace/trick branches, camera slots/threshold/NaN, velocity accumulation and camera flags |
| Offline frontend state | 16 assertions passed using an in-memory DOM stub: themes, lock display, preserved unsaved values/hash, readable editor loading and source conflict handling; no browser/network |
| Modern UI wiring smoke | 34 unique control IDs, three views, assets and theme/reduced-motion tokens present, JavaScript syntax passed |
| Reconstruction provenance | Seven local native evidence hashes and matching GameAssembly hash verified; camera threshold bytes verified directly |
| Python compilation | studio, tools and launch.py pass compileall |
| Visual browser inspection | Blocked: browser tool reported declined permission for http://127.0.0.1:8765; no alternate browser or workaround used |
| In-game test | Not run; game was not launched automatically |

Earlier analysis remains verified: 23,785 metadata types; 184,138 native function entries; 56 local pseudocode bodies; Ghidra executable hash matches installed GameAssembly; MKsKartersMods managed recovery has 37 local C# files. Legacy audit scanned 93 C# files and 81 named Harmony declarations: 79 present by name, one overload review, one absent method. Exact-address second Hit export at 0x18056dae0 succeeded. The old seven-tab analysis UI passed its earlier off-screen smoke check; that is not a visual check of the new Garage.

Installation created owned-file receipts under ignored `local/backups`; its report is `local/pack-installation.json`. The original binaries, Ghidra database and legacy source folders remain intact. Existing old config files were not deleted. Build receipts still have `runtimeTested=false`.

## Remaining acceptance

Inspect the new GUI in light/dark and narrow/desktop layouts. Start the game manually with toggles off; record pack load and injected component registration. Test HUD naming/scalers/CanvasGroups, Wwise capture/restoration, FOV transitions and split-screen limits, shadow pipeline behavior, FrameLimiter/VSync restoration, config reload and recipe lifetime. None is claimed working solely because it compiles.

Gameplay modules stay runtime-locked. Audit all upload routes and callbacks, test local/network/AI/ghost selection and online/offline transitions, verify every parameter restore path and scene cleanup, then review opening the gate. Partial semantic reconstruction is not full source recovery or native equivalence proof.

No-code recipe generation, completion, compiler-line navigation, conflict ownership, live scene inspection, package distribution and the remaining legacy ports are recorded in ROADMAP.md and MOD-PACK.md.

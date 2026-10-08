# One pack and legacy migration

The plugin folder was empty when inspected after the user's removal of legacy DLLs. It now contains exactly one installed DLL: `TK2-Mod-Studio/TK2.Customization.dll`, 30,208 bytes. Build and installation receipts remain local. All settings start off. No old binary/dependency was bundled into the pack.

The pack has **ten built-in modules plus one new recipe**. The retained assembly name and config GUID preserve compatibility with the earlier SDK starter; the displayed plugin is **TK2 Mod Garage Pack 0.2.0**.

| Module | Implementation | Runtime status |
|---|---|---|
| HUD scale | Capture/restore matching root non-world Canvas scale | Compiled; manual test needed |
| HUD opacity | Capture/restore existing matching CanvasGroup alpha | Compiled; manual test needed |
| Audio multiplier | Exact SetVolume(string,int) prefix; capture/restore Wwise levels | Compiled; manual test needed |
| Camera FOV | LateUpdate on Camera.main; restore prior camera/FOV | Compiled; manual test needed |
| Shadow distance | Capture/restore Unity QualitySettings.shadowDistance | Compiled; pipeline behavior untested |
| FrameLimiter recipe | Target frame rate + temporary VSync override, restored on disable | Compiled; manual test needed |
| Fast fall | Local human/offline/down-arrow conditions; reconstructed velocity accumulator | Gameplay locked |
| Custom laps | Exact GetGameModeRequiredLapCount() postfix | Gameplay locked |
| Driving challenge | Exact controller JumpInput(bool)/DriftInput(bool) prefixes | Gameplay locked |
| Automatic drift boost | Current boost-fill window in Ant_BoostManager.FixedUpdate() | Gameplay locked; partial legacy behavior |
| Kart tuning | Original-value speed/jump multipliers, not repeated compounding | Gameplay locked; two parameters ported |

Legacy ideas were inspected in the original source folders and ignored managed recovery. These are fresh scoped implementations against current signatures, rather than repackaging all recovered third-party source. The original sources/binaries remain intact. Their licenses and redistribution rights must be established before publishing third-party code; the supplied game artwork is retained for this local app, without claiming distribution rights.

## Remaining legacy features

Name presence alone does not prove compatibility. No remaining feature below is represented as implemented or working in this pack.

| Legacy feature | Needed before inclusion |
|---|---|
| BoostTrainer, CNKStyleBoostMeter | Current boost-state semantics and a Unity HUD adapter replacing GameOverlay/SharpDX dependencies |
| AlternateReservesMode, remaining boost/physics parameters | Native boost/reserve lifecycle reconstruction and parameter baseline/restore validation |
| FastRespawn | Validate current drone/respawn transitions, reset-physics side effects and speed restoration |
| MirrorMode, ReverseRaceMode | Track direction/checkpoint/timing/replay agreement and leaderboard protection |
| SaveStates | Versioned complete kart/boost/health/track state snapshots; reject incompatible scene handles |
| TeleportersForTricks, DashAndStash | Current trick/portal/input timing and safe teleport targets |
| ProximityVoiceLines | Current Wwise event routing, local players and scene cleanup |
| BobbyGang, SupraMayroKratt | Current model/material/asset ownership and restoration |
| NightmareAIs | Recover/port damage logic using the exact current Hit overload; old ambiguity is unresolved |
| Twitch command modules | Current player APIs, event cleanup, optional command integration; no Twitch connection is added |
| Standalone auto-reload/DisableLeaderboards | Pack already reloads its own cfg; full leaderboard protection needs all upload paths and callback validation |

## Unlock acceptance

`GameplayReady` is false even after exact upload and physics targets install. Only one upload path has static evidence. Before opening the offline lab, identify all race/time-trial/ghost/season upload paths, verify callback/error behavior when uploads are suppressed, and test enable/disable, finish/restart, local/network/AI/ghost selection, online transitions, and scene changes. Record observations and expand guards; then change the gate in a reviewed version. Source editing is available now, but setting a gameplay toggle is intentionally insufficient.

The next authoring milestone is typed visual recipe generation with compiler diagnostics and conflict ownership. The next reconstruction milestone is full jump motion and boost state transitions; see the roadmap and readable-source document.

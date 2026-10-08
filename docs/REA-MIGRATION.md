# REA evidence and native migration

The installed REA CLI 6.0.0 is used directly because its MCP tools are not exposed in this active tool session. `doctor --client codex --json` reported aligned Codex registration and skill version 33. No reinstall/reconfiguration was needed. Ghidra's process environment lacked GHIDRA_INSTALL_DIR; the SDK's read-only exporter uses the already installed Ghidra 12.1.4 and symbolized project directly.

REA `inspect-managed-artifact` and `inspect-managed-members` statically inspected the original MK DLL, NightmareAIs.dll, installed BepInEx.Core and Harmony assemblies. Evidence identifiers, subject SHA-256 and limitations are committed in rea-evidence.json; full results stay in ignored local/rea. These static operations did not execute the DLLs or game. CIL/decompiled managed mod implementations can be recovered; generated game interop wrappers are not original game C# bodies.

REA confirms ConfigFile.Reload calls SetSerializedValue and ConfigFile.OnSettingChanged checks SaveOnConfigSet before Save. The plugin now sets SaveOnConfigSet=false so main-thread reload cannot rewrite each GUI edit back to disk. GUI save baselines compare typed values, preserving comment rewrites and unrelated changes. The old first-poll skip was also removed: a change before the first scan is no longer swallowed. Three-way tests cover genuine same-value conflicts and benign rewrites.

Ghidra read-only exports match the installed GameAssembly SHA-256 `e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130`. The existing index contains 184,138 functions, 155,180 named. There are now 71 selected native C bodies locally. The committed migration-targets file makes the follow-up export reproducible without altering the project.

| Evidence | Migration consequence |
|---|---|
| Racing camera GetRacingCamPositionAndRotation 0x1804ee950 and UnityCustomUpdate 0x1804f1100 | Customize the native position/FOV pair; remove the global Camera.main LateUpdate overwrite. New tangent-based distance compensation preserves projected kart size. Intro, spectator and local-player checks remain. Native smoothing still requires a visual runtime check. |
| Camera InitOriginalProperties 0x1804eff80 | Readable adapter captures actual camera offsets, pitch/yaw/FOV, slope offsets and smoothing defaults. |
| Respawn HandleRespawnPhases 0x1805c6090, PlayerOutsideTrack_RespawnPlayer 0x1805c8560 | Current targetPositionPointForRespawn is Vector3. Preserve phases; speed up captured drone/attach timing instead of calling an obsolete transform path. |
| PlayerRaceLogic.UpdateAndCalculateLapCount 0x180613740 | Finish counting now also updates accepted crossing/respawn history. Reverse and full snapshot rewind cannot claim parity by changing one counter. |
| Boost reserve getter 0x180667ce0 | Current reserves decode directly from ObscuredFloat duration. Community input uses seconds; old empirical speed-percent conversion is not assumed valid. |
| HpBarController.GetCurrentHP at shared 0x18056d2b0 | Symbol is named PlayerGlobalStats.GetCurrentHP by Ghidra, but current dump records the HpBarController alias. The body reads synchronized visible player HP; the port follows the typed game getter. |
| Immunity ActivateImmunityNow 0x18056c150, RefreshImmunityState 0x18056ee90, SetImmunity 0x18056f230 | Current immunity is an independent flag mask, with delayed changes by source. Fast-fall dodge owns bit 0x10000, leaving shield/respawn/death sources intact. Reviewed C# translates flag/collider/visual updates; tests exercise the masks. |
| Nightmare AI damage overloads and installed Harmony metadata | Hook full 3-/4-parameter Hit signatures; avoid ambiguous lookup and obsolete 5-argument Harmony.Patch overload. |
| Current stringliteral.json | Master_Volume_RTPC, Music_Volume_RTPC, SFX_Volume_RTPC, VO_Volume_RTPC and UI_Volume_RTPC are present. Mixer has independent bus multipliers, instead of applying master five times. |

The workshop catalogs 32,350 readable Assembly-CSharp method signatures, with 12 method reconstructions linked to native hashes and editable source. This is useful semantic reconstruction, not complete recovery of the original project, stripped code, variable names or assets. Continue with exact overloads, full state transitions and native/runtime comparison. Runtime acceptance of the new DLL remains pending manual game observations.

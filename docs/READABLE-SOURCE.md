# Editable reconstruction, version 0.1.4.18

The workshop now opens **C# with executable method bodies**, not dump declarations. `src/Reconstructed/` contains semantic translations and pure logic models compiled into the same pack DLL; `ReadableGame.cs` adapts selected behavior to current game interop objects. Changing an adapter used by a recipe or module changes pack behavior. These files do not rewrite GameAssembly or automatically replace every original method.

| Native method | Virtual address | Readable implementation | Evidence / assumptions |
|---|---|---|---|
| PixelKartPhysics.AddVelocity | 0x1805dd8a0 | KartLogic.AddVelocity | Three component sums; used by fast fall used by the local offline fast-fall module |
| PixelKartPhysics.JumpInput(bool) | 0x1805e0270 | KartLogic.JumpInput and ReadableGame.JumpInput | False input, extra trick, history reset, replay grounding, airborne buffer, inclusive grace boundary; initialized objects and stable grounding assumed |
| PixelGameKartCamera.GetCamera | 0x1804ee5b0 | CameraLogic.GetCameraIndex and ReadableGame.GetCamera | Ghost returns null, normal/local-spectator player slot, nonlocal spectator slot zero |
| PixelGameKartCamera.IsCameraASpectatorCamera | 0x1804f05a0 | CameraLogic.IsSpectator | Boolean field read |
| PixelGameKartCamera.IsIntroCameraActiveAndRunning | 0x1804f05b0 | CameraLogic.IsIntroActive | Strict comparison against 0.95f; constant bytes `33 33 73 3f` at VA 0x183707e34 / file offset 0x3706634 |
| PixelGameKartCamera.EnableTribuneCamera | 0x1804ed620 | CameraLogic.EnableTribune | Boolean assignment |
| PixelGameKartCamera.ForceInstantTeleport | 0x1804ed730 | CameraLogic.ForceInstantTeleport | Set forced teleport, clear tribune camera |
| Ant_BoostManager.IsBoostingConstFromBoostpad | 0x180668080 | BoostLogic.IsBoostingFromBoostPad | Boost-pad timer is strictly greater than zero |
| Ant_BoostManager.IsBoostingConstFromReserves | 0x1806680a0 | BoostLogic.IsBoostingFromReserves | Decoded reserve timer is strictly greater than zero |
| Ant_BoostManager.IsBoostingFromPowerUp_Booster | 0x180668130 | BoostLogic.IsBoostingFromBooster | Booster timer is strictly greater than zero |
| Ant_BoostManager.IsBoostingFromPowerUp_Star | 0x180668150 | BoostLogic.IsBoostingFromStar | Star timer is strictly greater than zero |
| Ant_BoostManager.IsBoosting | 0x180668170 | BoostLogic.IsBoosting | Native aggregate checks reserves, just-triggered, boost-pad and Booster; it does not query the Star timer |
| Ant_BoostManager.GetManualBoostFillNormalized | 0x180667d70 | BoostLogic.GetManualBoostFillNormalized | Returns the indexed fill-time array value |
| Ant_BoostManager.IsManualBoostWithingFireRange | 0x1806681c0 | BoostLogic.IsManualBoostWithinFireRange | Slot zero; minimum and maximum are inclusive |
| Ant_BoostManager.IsPlayerAchivedMaximumBoostLevel | 0x180668280 | BoostLogic.HasMaximumManualBoostLevel | Booster count equals exactly three |
| Ant_BoostManager.IsPhysicalManualBoostEnabled_OnlyAICanDisable | 0x180668210 | BoostLogic.IsPhysicalManualBoostEnabled | AI restriction covers manual boost kinds 0–2 and leaves boost pad, jump-landed, Booster and Star kinds enabled |
| Ant_BoostManager.KartIsBreakingOnGround | 0x180668290 | BoostLogic.SetBreakingOnGround | Direct state assignment |
| Ant_BoostManager.ForceReservesMinVal | 0x180667900 | BoostLogic.ForceReservesMinimum | Raises the decoded reserve only if it is below the requested floor |
| Ant_BoostManager.WillWallCollisionReduceReserves | 0x180669a30 | BoostLogic.WillWallCollisionReduceReserves | Requires positive reserves and no tank weapon currently triggered/running |
| Ant_BoostManager.GetTriggeredBoostClickedOnBoostingRangeStrengthNormalized | 0x180667dc0 | BoostLogic.GetTriggeredBoostStrengthNormalized | Normalizes fill time and clamps to `[0,1]`; native upper-bound float verified at 0x1837079b4 |
| Ant_BoostManager.WallCollisionOccuredInTimeFromLastOne | 0x180669640 | BoostLogic.OnWallCollision | Clears loaded lateral velocity; multiplies positive reserves by native `0.7f` unless tank weapon is active; invokes event whenever subscribed |
| PixelGameKartCamera.InitOriginalProperties | 0x1804eff80 | ReadableGame.InitOriginalProperties | Captures original camera parameters; initialized nested objects assumed |
| Ant_BoostManager.GetCurrentBoostReservesTime | 0x180667ce0 | ReadableGame.GetCurrentBoostReservesTime | Reads the ObscuredFloat reserve value through its interop conversion |
| HpBarController.GetCurrentHP | 0x18056d2b0 | ReadableGame.GetCurrentHP | Shared native body with PlayerGlobalStats; reads synchronized visible HP |
| HpBarController.ActivateImmunityNow | 0x18056c150 | ReadableGame.ActivateImmunityNow | Updates one source flag and refreshes derived state |
| HpBarController.RefreshImmunityState | 0x18056ee90 | ReadableGame.RefreshImmunityState | Rebuilds immunity, death-immunity, collider, HP and VFX state from source flags |

`src/Reconstructed/provenance.json` records the game build hash, addresses, source files, and hashes of the local native evidence used for each translation. The C# console tests check branch combinations, boundary cases, state preservation, slot selection, NaN, boost timing, AI boost gates and velocity accumulation. These are semantic tests, not native runtime equivalence proof.

Native IL2CPP metadata initialization and error-helper assembly are omitted from the pure models. Null/error behavior is not fully reconstructed. The jump adapter snapshots grounding once; the native method queries it twice. A side effect between those native queries would require a more exact adapter. The adapter is opt-in and does not patch JumpInput automatically.

## Editing and authoring

1. Open Workshop. Select readable game logic or create a named recipe.
2. Edit the C# in the app, or use an external editor on the same files.
3. Save C#, then Build pack. Build & install also saves an edited file first.
4. The DLL always remains `TK2.Customization.dll`. Recipe classes are discovered inside that assembly.
5. Start the game manually once to create recipe-specific settings. Enable/configure those recipes under Your recipes. Settings reload without rebuilding; C# changes require a rebuild and game restart.

Recipes implement `IModRecipe`: Configure, Tick, Restore, and a gameplay declaration. The starter recipe demonstrates a configurable kart-hop shortcut using reconstructed velocity accumulation. Source edits have local backups and reject an external edit conflict. This interface is intentionally a small C# editor; it does not yet provide IntelliSense or a debugger.

The boost work is intentionally a first slice through `Ant_BoostManager`, not a translation of its large update/state-machine methods. It preserves a surprising native distinction: the Star timer has its own active predicate but is absent from `IsBoosting`'s aggregate conditions. It also captures how positive reserves are protected from wall-collision loss while the tank weapon is active and the native collision multiplier. `BoostLogic` accepts decoded values and normal valid enum inputs; IL2CPP's ObscuredFloat runtime plumbing, null/lifecycle failures and game-side effects are outside these pure methods.

## What remains to reconstruct

Twenty-six native methods have reviewed normal-state semantic translations. The function browser indexes 32,350 Assembly-CSharp method declarations; selected native bodies are exported separately. There are 184,138 native function entries in the local index, including engine and library code. Fully recovering the original C# source, comments, variable names, stripped code, and original Unity editor project from optimized IL2CPP binaries is not achievable by simply translating all Ghidra listings.

Continue subsystem by subsystem: boost state machine, complete jump motion, input dispatch, damage overloads, race rules, replay/save states, UI controller lifecycle, and native mod-content formats. For each method, record exact signature and aliases, resolve constants/call targets, reconstruct state transitions, test pure behavior, then compare against runtime observations. Do not substitute generated wrappers or fabricated stubs for a completed reconstruction.

## Additional reviewed native translations

NativeAdapters.cs adds Camera.InitOriginalProperties (0x1804eff80), BoostManager.GetCurrentBoostReservesTime (0x180667ce0), HpBarController.GetCurrentHP (0x18056d2b0, shared symbol with PlayerGlobalStats), ActivateImmunityNow (0x18056c150) and RefreshImmunityState (0x18056ee90). Camera settings capture the actual original offsets, pitch, yaw and FOV. Immunity preserves individual source flags and reconstructs native visual/collider synchronization; HealthLogic.cs tests the decision truth tables. These adapters assume normally initialized game objects; native exception/runtime initialization helpers are not recreated.

CameraFraming.cs is new mod geometry, not recovered original source. It is deliberately separated and tests preservation of projected kart size. Editing a reconstructed function affects callers in the mod DLL; it does not automatically replace every original game method.

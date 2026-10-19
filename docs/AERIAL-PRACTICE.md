# Air Shift, Practice Rewind and Ricochet Items

Added to the source Studio's **Community Mods** pack on 2026-10-19. All three
modules default off and are marked experimental. The plugin compiles against
the installed interop; **none of these new mechanics has been verified in game**.
No game launch, game-folder deployment, portable executable or release ZIP was
performed for this change.

## Controls and scope

| Module | Default control | Behavior |
| --- | --- | --- |
| Air Shift | `[` left, `]` right | One horizontal impulse per airborne period; grounded input does not queue a shift. |
| Rewind / Practice Scrubbing | Tap Backspace; hold to move farther backward | Initially returns three seconds, then scrubs backward at two history seconds per real second. Release to resume on a new timeline. |
| Practice snapshots (existing) | F5 save, F6 load | Retains manual snapshots and now shares the expanded driving-state restore. |
| Ricochet Items | Fire a supported straight missile normally | One bounce on a static, near-vertical wall; the second impact uses the game's normal collision path. |

Optional controller shortcuts use existing Rewired action names. They are blank
by default to avoid taking over Fast Fall, boost or trick controls. Choose distinct
actions in Studio. Air Shift changes only horizontal velocity, using the kart's
steering orientation for left/right; it does not write heading, vertical velocity,
reserves, trick or boost inputs. Its horizontal speed cap defaults to the speed
immediately before the shift and can be reduced to 90%. Fast Fall remains a
separate module and can be activated afterward.

Rewind records at most 20 samples/second, with a hard 401-sample cap and a
configurable 3–20 second window. The effective window is at least the selected
initial rewind interval. It requires one local kart without AI racers; playing
ghosts are excluded from the kart count. It clears history on checkpoint/lap
changes, scene/kart changes, race reset, finish, pause, teleport, respawn or disable.
It skips samples during unresolved jump/trick effects rather than trying to
serialize coroutines. Consequently, some intervals have no eligible sample;
rewind uses the last eligible sample at or before the requested time.

The shared snapshot restores KCC position, rotation, velocity and grounding;
steering/drift state; reserve, instantaneous, continuous, triple and boostpad
speed components; both boost-fill arrays; continuous-boost smoothing; air time;
normal route/respawn tracking values and copied route collections. Relevant
timestamps are rebased to preserve their age. Pending jump/boost input is cleared,
and Air Shift's used-per-jump flag is restored. Fast Fall's input and dodge latch
is cleared to avoid carrying an activation from the abandoned timeline.

This remains a **partial driving-state rewind**, not a world save. Rewind does
not restore the shared race clock, inventory, health, spawned projectiles,
temporary external item-effect coroutines, moving platforms, or battle-arena
world/history. Use ordinary solo track practice without active item effects.
It cannot rewind across a checkpoint or recover after the respawn system has
already taken control. The older manual snapshots retain their health/item and
experimental progress/clock options; those options do not make a full race rewind.
Manual loads also refuse checkpoint/lap changes unless their existing experimental
progress-restore option is enabled, and clear the rolling history after loading.

Ricochet accepts only locally owned `StraightMissileController` objects with
non-targeting visual logic, a static collider without a Rigidbody, and a wall
normal with `abs(y) <= 0.35`. It excludes kart, item-box and physics-trigger
objects. After a bounce it owns the projectile's horizontal movement and
suppresses passing the old target position, retaining native visual height,
damage and lifetime handling. Already-bounced missiles finish their path if the
switch is turned off. Homing missiles are never redirected. A wall which is not
marked static in Unity intentionally receives ordinary impact behavior.

Enabling any of these modules blocks leaderboard uploads and online rooms.
Once any gameplay mutation sets `SessionModified`, blocking persists until the
game restarts, even if every module is subsequently turned off.

## Evidence and compatibility

Current installed `GameAssembly.dll` SHA-256:
`55f5c88972fe1ccf4c9d5f3871b71bcfa261b3e42a379e18438e92623fc4d6a2`.
Current installed `BepInEx/interop/Assembly-CSharp.dll` SHA-256:
`0c45b8ffb5e8975f2114ac44dcf6356c7f3ea599461614bd1a22750b990cc215`.

The earlier full Ghidra export targets a different game hash and was treated as
historical reference. Current metadata was dumped from the installed game and
the current binary was imported into a separate ignored Ghidra project. Relevant
methods were disassembled and decompiled without changing the original project
or installed binary. The final inspection used the full PE image, not truncated
raw slices. Local evidence lives in `local/aerial-practice-evidence/` (ignored).
It is native pseudocode and metadata declarations, **not recovered original C#**.

| Current native method | RVA | Finding supporting the implementation |
| --- | --- | --- |
| `PixelKartPhysics.UpdateVelocity(ref Vector3, float)` | `0x5FC1A0` | Consumes pending velocity additions and writes the final velocity/ground-history state; Air Shift uses a postfix. |
| `PixelKartPhysics.AddVelocity(Vector3)` | `0x5F40B0` | Adds to the pending frame velocity vector, supporting the existing Fast Fall adapter on this build. |
| `PixelKartPhysics.FixedUpdate()` | `0x5F5CA0` | Current existing Fast Fall hook location. |
| `PixelKartPhysics.TriggerPhysicsJump(ref Vector3, float)` | `0x5F8F50` | Jump state has additional effects and buffers; unresolved effect samples are excluded. |
| `Ant_KartInput.ProcessRacingInput()` | `0x6B5180` | Current input path; new actions are read in postfixes without rewriting normal actions. |
| `Ant_BoostManager.GetCurrentBoostMaxSpeedIncrease()` | `0x67FB60` | Combines several independent reserve/instantaneous/continuous/power-up/boostpad values; reserve-only restores are insufficient. |
| `PlayerRaceLogic.OnRaceResetted()` | `0x6274F0` | Lifecycle boundary used to clear per-race history and latches. |
| `PTK_VehicleOutsideMapValidator.UpdateSequentialRoadpointTracking(...)` | `0x5E1C60` | Maintains confirmed/last grounded respawn points and sequential collections. |
| `PTK_VehicleOutsideMapValidator.UpdateRespawnRoute(...)` | `0x5E1410` | Maintains route/rejoin state separately from checkpoint flags. |
| `PTK_VehicleOutsideMapValidator.ResetRespawnHistory()` | `0x5DFB30` | Additional respawn history prevents claiming a full position-only rewind. |
| `RaycastCollisionDetector.Hit(GameObject, Vector3, Vector3)` | `0x4AABB0` | Receives a normal and dispatches the impact/explosion event; interception happens before it. |
| `AbstractMissileController.OnRaycastHit(GameObject, Vector3)` | `0x49D8D0` | Normal hit path starts explosion processing. |
| `StraightMissileController.Init(MissileInitializeParams)` | `0x4ABD90` | Dedicated straight-missile path. |
| `VisualLogicMissileController.RefreshBulletConstantMovementTowardsTarget(float)` | `0x4AE720` | Native movement uses target/muzzle-derived positions; changing rotation alone would not produce ricochet. |
| `VisualLogicMissileController.Update()` / `CheckIfPassedThrough()` | `0x4AF6B0` / `0x4ADEB0` | Old-target passage has its own explosion path, suppressed only after an accepted bounce. |

The current hash is now accepted by the existing physics build gate, alongside
its historical supported hash. Unknown hashes still disable the physics features.
Current interop signatures were additionally checked by a full plugin build.

## Validation and remaining game checks

Automated validation: full Python suite, UI tests/build, online-protection policy
checks, and deterministic Air Shift speed/impulse invariants plus history
expiration, branching, insufficient-history and memory-bound checks. These do
not establish native-hook execution or visible in-game behavior.

Before calling these mechanics ready, manually validate a solo offline race:

1. Shift once over a ramp; confirm unchanged heading, preserved vertical motion,
   no horizontal speed gain, no second shift until landing, and no grounded input
   carryover. Charge a mini-turbo, perform a trick, then Fast Fall on the same jump.
2. Rewind before/after a drift boost and boostpad; compare velocity, engine speed,
   reserve and fill values before capture and immediately after restore. Hold,
   release, then drive a new branch. Confirm checkpoint, respawn, pause and race
   reset boundaries clear history. Check the next natural respawn point afterward.
3. Fire a non-homing straight missile into a static wall: visibly confirm one
   bounce and normal second-impact damage/explosion. Test homing missiles, floors,
   kart hits, moving colliders and lifetime expiry separately.
4. Attempt a submission/online join with a new module enabled and again after
   disabling it following a mutation; both must remain blocked.

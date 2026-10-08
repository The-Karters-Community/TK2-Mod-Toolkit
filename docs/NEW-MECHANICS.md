# Seven new kart mechanics

These readable C# modules compile into the existing single `TK2.Customization.dll`.
Every module starts disabled. They use the current build's kart API and the
reconstructed `ReadableGame.AddVelocity` adapter, with no new Harmony targets.

Enable one at a time for initial testing. Configuration edits and toggles use
`BepInEx/config/local.tk2.customization.cfg` and hot reload. Editing C# requires
building and updating the one plugin with the game closed, then restarting it.

| Module | How to play | Default controls and feel |
|---|---|---|
| Slipstream Sling | Follow a moving kart inside its wake, then spend stored charge to overtake. Charge fades outside the wake. | **V**; 18-unit range, 25-degree half-cone, 2 seconds to charge, 12 extra velocity at full charge, 3-second cooldown. |
| Drift Capacitor | Store drift time separately from ordinary boost reserves. Release on a straight when it matters most. | **B**; 3-second full charge, 14 extra velocity. Automatic release when drifting ends is optional and off by default. |
| Air Glider | Hold the shortcut after leaving the ground for limited lift and sideways correction. Land to refill the fuel. | **G**, **Left/Right Arrow**; 2-second fuel supply, 0.2-second activation delay, lift acceleration 18. This is a physics mechanic; no wing model is attached. |
| Echo Rewind | Recover a recent motor position, rotation and velocity from a rolling history. | **R**; rewind 2 seconds, retain 6 seconds, cooldown 8 seconds. Grounded snapshots by default. The clock, checkpoints, lap count, health, items and other racers remain current. |
| Repulsor Pulse | Push nearby offline AI away from your kart with a radial impulse. | **P**; radius 9, push velocity 10, lift velocity 2, cooldown 6 seconds. Other local human players are unaffected by default. |
| Gravity Surf | Turn downhill travel into stored energy that assists the next climb. | Passive; downhill acceleration 3, uphill assistance 10, energy use 0.35 per second. Hill detection uses vertical movement while grounded. |
| Landing Combo | Tap near touchdown to earn a forward burst. Chain timed landings for stronger bursts; a miss breaks the chain. | **L** within 0.16 seconds before/after touchdown; minimum 0.4 seconds airborne, burst 5 plus 1.5 per extra combo, maximum combo 4. |

Every numeric setting has an explicit allowed range in its `Configure` method.
Keyboard shortcuts can be rebound. `AllLocalPlayers` defaults to `false`, so
shared keyboard shortcuts affect only the first local human kart. Enabling it
applies the mechanic to every local human player, using shared keyboard inputs.

## Read and extend the code

Each mechanic has its own file under `plugins/TK2.Customization/Recipes/`:

- `SlipstreamSling.cs`
- `DriftCapacitor.cs`
- `AirGlider.cs`
- `EchoRewind.cs`
- `RepulsorPulse.cs`
- `GravitySurf.cs`
- `LandingCombo.cs`

`MechanicsContext.cs` contains their shared local-player selection, main-thread
guards and racer cache. It scans active karts at most once per second, caches at
most 32 racers and prevents updates during online races, paused time, replays,
respawn recovery or kart death. A scene change clears histories; disabling a
module or leaving the running race clears its managed state. No module replaces
base physics parameters. Applied impulses end through ordinary game simulation;
disabling a module stops further impulses rather than undoing a previous action.

Echo Rewind stores at most 201 motor snapshots per selected player. Its history
also clears on a sudden position jump. A rewind clears the history again and
starts its cooldown. It does not rewind checkpoint or respawn-route bookkeeping;
use it for local practice and expect the next native recovery to use the game's
current route state. This is not a replay reconstruction.

## Current-build API evidence and limitations

The local shipped `BepInEx/interop/Assembly-CSharp.dll` metadata confirms:

- `PixelKartPhysics.kartController`, `bWasGrounded`, `bIsDrifting`, `fTimeInAir`,
  `vFrameVelocityAddOverride`, `SetSteeringPhysicsRotation(Quaternion, bool)`.
- `PixelEasyCharMoveKartController.parentPlayer`, `Motor`, `outsideMapValidator`,
  `GetKartPos()`, `GetKartVelocity()`, `GetKartRotation()`, `IsPlayingReplayData()`.
- `PTK_VehicleOutsideMapValidator.bIsPlayerRespawning`.
- `Ant_Player.ePlayerType`, `eAntLocalPlayerNr`, `hpBarController`.
- `KinematicCharacterController.KinematicCharacterMotor.GetState()` and
  `ApplyState(KinematicCharacterMotorState, bool)`; snapshot position and rotation.

These are interop API declarations, not recovered native method implementations.
The full REA metadata inventory exceeded the tool's result-size limit; a filtered,
read-only `System.Reflection.Metadata` inventory was used instead. Its report is
in ignored `local/mechanics-metadata/current-members.txt` on the development PC.
Compilation checks the complete API types and signatures against current game
references. The game was not launched automatically, so handling feel, native
physics timing and all seven mechanics still need in-game validation.

Movement mechanics can combine their impulses when enabled together. Air Glider
opposes Fast fall; Drift Capacitor stacks with native/automatic boosts; Gravity
Surf stacks with Kart tuning; Echo Rewind and Save States both alter motor state.
Use separate keys if you enable legacy shortcuts. Repulsor Pulse uses distance
without a terrain line-of-sight test, so a close racer separated by a wall can be
pushed. No leaderboard suppression or score-upload hooks are added.

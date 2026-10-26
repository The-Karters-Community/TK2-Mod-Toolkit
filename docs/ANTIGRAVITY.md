# Antigravity: road adhesion for offline driving

The Antigravity module is intended for banked roads and continuous loops. It is
opt-in under **Community Mods / Driving**. It does not make every collider
driveable, disable collision, or change the world's gravity. It is experimental
until checked in a user-driven race on suitable road geometry.

## Driving rules

- Acquire the surface frame from native stable road contact. An airborne kart
  cannot acquire a wall or ceiling by enabling the module.
- Keep the motor's original stable-ground layers and slope limit. Validate road
  support against wall/respawn masks, triggers, other players and dynamic bodies.
- Follow gradual surface changes with a transported local up/forward frame;
  reject abrupt surface changes. Steep/inverted driving requires speed.
- Jump also starts a drift. Redirect the native hop impulse and airborne gravity
  contribution into the acquired surface frame, preserving their magnitudes.
  A short hop can follow nearby continuous road, but its grace period cannot be
  extended by holding jump. A real gap, launch, recovery or expired grace releases
  adhesion.
- Preserve native acceleration, boost, jump/drift processing and collision.
  Frame transport must not create speed. Checkpoints, race time, respawn routes
  and replay data are not rewritten.
- Disable behavior for online sessions, other racers, replay playback, menus,
  recovery and unsupported special movement. Mandatory online protection treats
  this as a gameplay module. Once a race is changed, disabling the module does
  not remove the session's submission block.

These constraints reduce unintended shortcuts; they cannot certify every custom
track's geometry or make altered physics competitively equivalent to the game.
Native recovery volumes can still prevent a loop from being driveable.

The current conservative limits are 20 degrees of normal change per tick and
720 degrees per second, a 0.45-second hop window and 2.5 metres of maximum
road-probe separation. The separation limit measures distance from the road,
not forward travel. Steep means 70 degrees or more from world-up; its speed
thresholds use tangential road speed (12 m/s acquisition, 8 m/s retention),
excluding the hop's outward velocity. These are internal limits rather than
user-facing ways to relax the contact checks. **Show status** can hide the
small road-contact indicator.

## Current native evidence

Inspected installed Windows game **0.1.4.30 / beta_dev**.
`GameAssembly.dll` SHA-256:
`1104a3b50e15c4316c90f3f98c0441871140b8ca8eee53a6bd6380c744be6d39`.
Hashes identify the evidence, not compatibility gates.

Fresh Il2CppDumper declarations and method addresses were generated from this
binary and its metadata. Ghidra 12.1.4 inspected explicitly selected native
function byte ranges; private dumps, slices, logs and provenance are under
ignored `local/antigravity-evidence/`. This is partial native analysis, not
recovered original C# or a reconstructed Unity project. External calls remain
unresolved in the slices, and some decompiler paths end at bad-instruction or
slice boundaries. Only the following observed operations support the design:

| Native boundary | Current address | Observed behavior |
| --- | --- | --- |
| `KinematicCharacterMotor.IsStableOnNormal(Vector3,int)` | `0x180d5aa80` | Checks stable-ground layer membership and angle against the motor's character-up vector and configured slope limit. |
| Controller `ICharacterController.UpdateRotation(ref Quaternion,float)` | `0x1805f7c90` | Reads kart steering orientation and physics up/axis data for the motor rotation callback. |
| `PixelKartPhysics.CalculatePhysicsAxisDirections(ref Vector3,float)` | `0x1805fb090` | Builds surface-aligned axes using a world-up projection; changing only visual rotation is insufficient for vertical driving. |
| `PixelKartPhysics.UpdateSteeringCentiperalForceAndOrient` full overload | `0x180601ff0` | Updates the native steering quaternion after axis calculation, using local-Y quaternion deltas. Its wrapper at `0x180601ee0` delegates to this overload. |
| `PixelKartPhysics.UpdateGravityWhenNotGrounded(ref Vector3,ref Vector3,float)` | `0x180601950` | Adds a world-up-derived downward acceleration contribution when no ground is found. |
| `PixelKartPhysics.TriggerPhysicsJump(ref Vector3,float)` | `0x1805ffeb0` | Forces ungrounding and adds an impulse along world-up; the inspected impulse path supports redirecting only that delta. |
| `PixelKartPhysics.UpdateSlopeVelocity(ref Vector3,ref Vector3,ref float,float)` | `0x180601cc0` | Adds slope acceleration and extra maximum speed from its computed axes. |
| `PixelKartPhysics.LimitVerticalVelocity(ref Vector3)` | `0x1805fe240` | Applies a conditional world-Y upward-velocity cap. |
| `PixelKartPhysics.SetSteeringPhysicsRotation(Quaternion,bool)` | `0x1805ff6d0` | Normalizes steering to yaw; it cannot directly install an arbitrary loop orientation. |

The collision admission and ground-hit callbacks were also inspected; their
existing filtering and effects remain owned by the game. Fresh generated
declarations establish names and layouts, while the build and signature tests
must establish compatibility with the actual installed interop.

The adapter keeps native steering as an independent yaw source and applies its
changes around the transported road normal. It rebuilds the local acceleration
direction rather than re-projecting a native cache that can reverse past a
vertical section. Stable-contact velocity is aligned without rotating an
already-aligned velocity a second time; airborne hop transport preserves the
existing outward component. Native hop/gravity deltas retain their magnitudes.
The native slope-speed contribution and world-Y velocity cap are bypassed only
while attached to a steep surface. This does not establish identical handling
or lap times to the unmodified game.

## Validation boundary

### First live test and rotation-commit correction

The first tunnel test on Workshop item `3268524772` failed: the HUD reported
adhesion, but the kart tilted and stopped at its usual angle limit. This is
failed gameplay validation, regardless of the earlier automated test results.

Fresh native inspection found a concrete integration defect. The module wrote
`motor.Transform.rotation` from `AfterCharacterUpdate`. In
`KinematicCharacterSystem.Simulate` (`0x180d75780`), the game calls Phase2 and
then commits the motor's stored transient position/rotation to the transform,
overwriting a transform-only change. Phase2 (`0x180d5dd70`) ends with the
after-update callback. The native transient-rotation setter (`0x180d5f590`)
updates `CharacterUp`, forward and right together with the cached rotation.

The correction sets **`motor.TransientRotation`**, comparing against that
native cached rotation rather than the displayed transform. The normal system
commit applies it after the callback. It avoids resetting the initial/tick
interpolation baselines every step as `SetRotation(..., true)` would do.
The existing HUD now shows road/policy/motor angles and their disagreement at
most five times per second, rather than claiming success from policy state.

The revised adapter tests model transient state separately, derive native-style
support from the motor's actual up vector, and simulate the final commit. They
replace fixtures that previously labelled arbitrary steep contacts stable while
the motor still faced world-up. Current correction checks pass: **120 adapter
assertions, 393 policy assertions, 147 Python tests and 18 actual hook/signature
resolutions**, with a plugin build containing zero warnings/errors. A repeat
in-game tunnel test is still required.

Correction deployment: installed with the game stopped and checked against the
built artifact, SHA-256
`4caffc63f2525e1dc5710567fd3040eb4211564edc481315eb2297feb523d141`.
Rollback backup: `local/backups/1792991626496802500`. Existing user configuration
was preserved. The user can now retry the tunnel with road/motor angle feedback.

Native wall classification and helper inlining were also inspected. No blanket
wall-collision override or live world-Y restoration was added: the observed
end-of-velocity Y-zero branch is countdown-gated, so it does not establish a
live-race cause. Some gravity/vertical helper logic is inlined in the native
caller; resolving a helper hook alone does not prove the game invokes it during
a drift hop. Live hop handling remains part of the required test.

Automated policy, metadata and hook checks establish bounded state transitions,
frame math and integration. They cannot establish wheel contact, camera comfort,
drift feel or checkpoint behavior in a real loop. A user-driven offline test is
required before describing the module as working in game. Do not use a public
leaderboard attempt as the test.

Implemented and tested with the user-requested Luna agents, with design and
native-evidence review in the main task. First-build automated checks:

- Plugin build: 0 warnings and errors against current installed interop.
- Pure policy/frame harness: 393 assertions.
- Production adapter linked to headless game/Unity fakes: 59 assertions.
- Python suite: 147 tests; frontend suite: 23 tests.
- Actual installed-interop resolution: 18 hook targets and Harmony handler
  signatures, including 9 Antigravity targets. No game methods executed.
- Source HTML/assets/catalog smoke: Antigravity present and disabled by default.
  Visual browser automation could not start because its shared kernel assets
  path was missing; a rendered GUI test is not claimed.

The locally built DLL was installed while the game was stopped and its hash
verified against the artifact:
`f492c0e137aeb44722bea4925cc0751e0e169b7bb4feb8db46a10be8a59d394d`.
The previous DLL is backed up under ignored
`local/backups/1792990481967402300`. No gameplay module was automatically enabled
and the game was not launched. The requested offline test on Workshop item
`3268524772` remains pending; do not present automated checks as proof of loop
handling.

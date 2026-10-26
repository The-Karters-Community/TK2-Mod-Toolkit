# Antigravity: road adhesion for offline driving

## Temporary spline-path switch

`Disable spline teleports (testing)` is an opt-in, offline-only loop-test
switch. It blocks the local player's *new* `QuickDashMovement_Bezier` entries
while Antigravity is enabled. It does not change respawns, instant teleports,
AI players, or an already-running spline move. The shipped native entry sets
the player/kart and then initializes the Bezier path, so blocking it at entry
avoids partial path state. This is a testing control, not a general teleport
disable feature.

The direct-contact continuation probe is limited to 35-degree faceted seams.
This retains the observed 36-to-55-degree tunnel transition while preventing a
turbo-pad or obstacle face from being selected as a surface continuation.

Wall collision layers are never disabled by adhesion. A prior direct-surface
candidate did this while attempting to relax wall detection; user recordings
showed it could tunnel through loop geometry. The corrected candidate retains
the game's native KCC collision and penetration resolution, so unsupported
geometry should block or release the kart rather than permit a pass-through.

## Optional surface camera

`Camera follows surface` is default-off and local offline only. The camera
candidate transforms the position and rotation already returned by the game's
racing-camera routine around the kart focus, then smooths the gravity frame.
It does not write private camera fields, replace the game camera, or use Unity
physics casts/overlap queries. An exception disables only this option and keeps
the native camera output for that frame. Gameplay validation is still required.
It enters at eight degrees before the motor's native stable-slope limit (about
52 degrees with the observed 60-degree default) and exits eight degrees below
that, so normal road and small banking retain the game's ordinary camera.

Direct-surface contact normals are also rate-limited to 240 degrees per second
to remove abrupt orientation changes across joined inclined quads.

The surface frame is reapplied at `BeforeCharacterUpdate`, before KCC evaluates
its stable-slope limit. This is required for a continuous loop: applying it only
after the update lets the native 60-degree check stop the kart before the
inverted section.

Enable `Detailed status and logging` for diagnosis. The HUD then reports raw
contact tilt, applied frame tilt, motor tilt, frame error, native slope limit,
contact state, speed, and collider layer. BepInEx logs receive the same data at
most once every 350 ms, plus forced release reasons; it is local/offline only.

The direct loop mode allows a bounded 0.35-second/1.5-metre reacquisition across
faceted seams after KCC temporarily reports no ground. In all-surface mode the
probe uses the full collision mask, so a next quad on a wall layer can be
reacquired; eligibility still rejects triggers, respawn layers, dynamic bodies
and player colliders. Successful reacquisition resets the grace timer.

Direct mode also carries the last tangential forward speed down by a bounded
decay while the surface is steep or inverted. This only prevents the native
velocity pass from zeroing a loop; reverse input and ordinary flat-road
braking remain native. Accepted drift hops use the same bounded carry.

Normal jumps remain native on flat and banked direct surfaces. Only a jump
started while the kart is already on a steep/inverted surface is transported
into the surface frame as an adhesion hop.

The optional camera transports its previous surface frame onto the new normal,
preserving roll through a loop. It does not rebuild each frame from world-up,
which avoids the arbitrary-roll snap at the top/bottom of a loop.

## Current test candidate: ground and wall contacts

The user isolated a previous native crash to **Camera follows surface**. The
current camera path is limited to the native camera output and transported
surface frame; it performs no physics casts or private camera-field writes.
If the option throws another exception it disables itself for that session.
The saved crash dumps remain private. A real race test is still required.

The new **Drive on ground and walls** option is an explicit offline free-driving
mode. It follows actual native ground/movement contacts rather than the
curved-road arming gate. It accepts solid collision layers including walls,
temporarily extends the kart's stable-ground layers, and restores the original
mask on release. The kart's wall-response layer mask and wall-angle threshold
are temporarily overridden and restored with it. Native KCC collision
movement/resolution still executes. A single capsule-radius forward ray detects
a nearby static wall being driven into, without teleporting the kart.
Respawn layers, triggers, rigidbody objects and racers stay excluded. Accepted
native drift hops retain bounded nearby-support grace. Ordinary curved-road
mode remains available when the option is off.

This mode is intentionally capable of wall driving and shortcuts; mandatory
online/submission protection remains in force. The user explicitly requested
this broader behavior after the conservative mode failed.

Validation for this candidate is compilation against current installed interop
only. At the user's request, no further harness tests were written or run.
In-game behavior is awaiting the user's test; earlier assertion counts below
describe earlier builds and do not establish that this candidate works.

Previous crashing candidate SHA-256:
`1bcd7a7c8071db9d46c0395c32cba2a9627664208e39e4588d6576c74fb725b5`.
DLL rollback backup: `local/backups/1792999362763653900`. Test configuration
backup: `local/backups/1792999402849420800`. The local test profile has
Antigravity and wall surfaces enabled, surface camera disabled. Source defaults
remain opt-in. No game was launched automatically.

### Race-start crash correction

The preceding ground/wall candidate also crashed at race start. The newest
recorded dump (`TheKarters2.exe.40048.dmp`) exposes this managed/native boundary:

```
IL_STUB_ReversePInvoke(Int64, Int64, Vector3, Vector3, Int64, Int64)
Il2CppObjectPool.Get<T>(IntPtr)
IL2CPP.il2cpp_object_get_class(IntPtr)
```

That reverse-P/Invoke signature matches the newly patched `OnMovementHit`
callback. Its `ref HitStabilityReport` contains a collider reference and is not
a blittable struct; the installed interop bridge attempted to treat its native
data as an object pointer before entering the feature handler. Consequently,
handler-side local-player/enable guards cannot protect other racers' callbacks.
This explains why another racer's landing could trigger a crash. The fatal
`coreclr` report was secondary exception handling, not the original source.

Removed the **entire OnMovementHit patch**, including the report mutation. The
replacement uses local-kart settings and ordinary support/near-wall probes.
Camera following remains disconnected. Compilation only; the user will perform
in-game validation. No further harness tests were written or run.

Corrected DLL installed with the game stopped, SHA-256
`6dca98418e2890f0de89bf014bf284c6328f748d5924df6fcc0e699c01c393bc`.
Rollback backup: `local/backups/1793000248442952600`. Antigravity is left **off**;
the ground/wall option remains selected for the next explicit offline test.

The Antigravity module is intended for banked roads and continuous loops. It is
opt-in under **Community Mods / Driving**. It does not make every collider
driveable, disable collision, or change the world's gravity. It is experimental
until checked in a user-driven race on suitable road geometry.

## Driving rules

- Collect road history passively without changing physics. Engage near the
  engagement angle only after at least four stable samples, 0.5 m of forward
  travel and 2 degrees of gradual curve progression, with matching road ahead.
  An airborne kart cannot acquire a wall or ceiling by enabling the module.
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
- Return to passive driving below the engagement band, with hysteresis. The
  optional **Camera follows surface** control preserves native pitch and FOV,
  blends the gravity frame and retracts against near-plane volume obstructions.
- Disable behavior for online sessions, other racers, replay playback, menus,
  recovery and unsupported special movement. Mandatory online protection treats
  this as a gameplay module. Once a race is changed, disabling the module does
  not remove the session's submission block.

These constraints reduce unintended shortcuts; they cannot certify every custom
track's geometry or make altered physics competitively equivalent to the game.
Native recovery volumes can still prevent a loop from being driveable.

The engagement angle is the lesser of the native slope setting minus 8 degrees
and 47 degrees, with a minimum 25-degree current road incline. The 47-degree
ceiling is an empirical margin before the user's observed 55-degree driving
limit. Native slope stability is measured relative to motor-up, so its setting
alone cannot establish a world-angle driving limit. Low native limits fail closed.
The current conservative limits are 20 degrees between adjacent contact normals,
a 0.45-second hop window and 2.5 metres of maximum
road-probe separation. The separation limit measures distance from the road,
not forward travel. Steep means 70 degrees or more from world-up; its speed
thresholds use tangential road speed (12 m/s acquisition, 8 m/s retention),
excluding the hop's outward velocity. Engagement requires 12 m/s. Ordinary
contact-seam grace is separately bounded to 0.08 seconds and 0.25 m. Modest
facet steps are accepted independently of physics tick rate: connected quads do
not need to form a mathematically smooth surface. Abrupt 90-degree corners
remain ineligible.
These are internal limits rather than
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
The native slope-speed contribution is bypassed while active on steep surfaces;
the world-Y velocity cap is scoped around the main velocity call while active.
This does not establish identical handling
or lap times to the unmodified game.

## Validation boundary

### Follow-up: 55-degree passive state and history lifetime

The next screenshot showed road 55 degrees, target 47 degrees, native
motor-relative limit 60 degrees, but still passive. The user identified a
36-to-55-degree facet transition. Requiring current contact to cross the target
before engagement misses the opportunity to anticipate that connected 19-degree
step. Predictive engagement now permits a validated ahead normal crossing the
target, with the existing minimum incline, history, speed, geometry, layer and
normal-change checks. It does not permit a flat-to-vertical transition.

More decisively, the follow-up recording `20261026-0558-43.2301534.mp4`
already contained the diagnostic candidate built through the user's Studio.
Multiple frames reported `StableContacts`, history **1/4**, travel/curve/normal
progress **0**, and reset cause `none`, even with a valid same-road preview
(55-degree normal, zero normal disagreement, height approximately 0.54 m) and
speed 69.7. The history was not surviving the physics/render lifecycle.

The adapter stored `controller.gameObject.scene.handle`, then compared it to
`SceneManager.GetActiveScene().handle` in `Tick`. These are different identities
when a kart belongs to an additive or persistent scene. This can discard state
each rendered frame without a policy reset, matching the frozen history counter.
The correction stores **kart scene and active scene separately**, comparing each
only with its corresponding current identity. Scene transitions and destroyed
controllers still invalidate state. Tests must exercise
`AfterCharacterUpdate -> Tick -> AfterCharacterUpdate` with differing scene
handles; earlier fixtures put both in the same scene and missed this defect.

Read-only native observation captured stable 36-degree contact at speed 80 and
53-degree contact at speed 79.8, with motor-up still world-up. The observed
native upward cap was 60, so those measurements do not establish that cap as the
cause of the passive-state failure. Actual matching road ray calls also returned
valid hits. Their native-call capture is partial and cannot by itself identify
every managed policy gate. Private observations are in ignored
`local/antigravity-evidence/`. The new 5 Hz HUD names the failing gate and reports
preview geometry, history and speed instead of just saying passive.

Gameplay acceptance is still pending for the corrected history lifetime. The
previous failed tests remain failed; passing headless checks is not proof of
climbing or camera behavior.

### Second live test: failed; redesigned candidate

The user reported the rotation-commit correction still stopped at the usual
angle limit and activated on slight inclines. The supplied 14.63-second
recording, `20261026-0517-39.9974624.mp4`, shows the tunnel track
**CTR99 - Sewer Speedway (v1.21) - Cathret**. The indicator reports roughly
14 degrees of road/policy/motor angle, then repeatedly reports released support.
Neither of the first two builds established working wall or loop driving.
Historical test counts below describe those builds, not gameplay acceptance.

Two further integration defects were identified:

- The contact probe used `CharacterTransformToCapsuleBottom` as a world point.
  The current native getter returns the field at offset `0x174` unchanged;
  `SetCapsuleDimensions` constructs that field from the capsule's local center
  and local height. A world probe must start from
  `TransientPosition + TransientRotation * CharacterTransformToCapsuleBottom`.
  Origin-centred fake fixtures had concealed this error.
- The native main `PixelKartPhysics.UpdateVelocity(ref Vector3,float)` at
  `0x180603100` contains its own upward-Y cap and airborne gravity calculation.
  Hooks on the similarly named helper methods do not intercept those inlined
  operations. The controller velocity callback at `0x1805f7e40` calls the main
  physics update and then the actual `TriggerPhysicsJump` method before KCC
  movement. Corrective work must respect that ordering.

The redesign separates passive road-history collection from active adhesion.
Ordinary banked driving must remain unmodified. Engagement requires progressive
eligible road normals, actual forward travel, and approach to the calibrated
engagement angle; a sharp floor-to-wall corner cannot establish adhesion. Active
surface continuity uses world-space probes, with separate bounded allowances
for contact seams and accepted native drift hops. Camera adaptation is limited
to active adhesion and must retain native pitch and field of view, with a
near-plane volume obstruction check after all camera offsets.

Reference designs: [Custom Gravity](https://catlikecoding.com/unity/tutorials/movement/custom-gravity/)
for movement and camera frames, and [Orbit Camera](https://catlikecoding.com/unity/tutorials/movement/orbit-camera/)
for near-plane box obstruction testing. These explain techniques, not TK2's
native implementation. The new adapter still requires an offline tunnel test;
headless tests cannot establish camera comfort or traversable track geometry.

The new adapter scopes `bLimitVerticalUpVelocity` around the actual main
velocity method, restoring its exact original value in both postfix and
finalizer. It samples the unchanged native airborne-gravity helper with a zero
accumulator to replace only the inlined world-down delta during an accepted
hop; native friction adds a separate contribution rather than modifying that
gravity delta. Grounded tangent alignment occurs at the controller velocity
callback before KCC movement and skips native forced ungrounding.

Fresh camera inspection at `0x1805067a0` starts from the visual instance's
`vSteeringForward_Not_Aligned`, explicitly clears its Y component, and returns
native camera offsets, rotation and FOV. The camera adapter applies transported
gravity alignment to that frame's native pose, then performs bounded nonalloc
near-plane collision queries. It retains native pitch and FOV rather than
replacing the view with a surface-facing look rotation.

Redesigned-candidate validation:

- Plugin build against installed interop: zero warnings/errors.
- Policy harness: 271 assertions, including 15-degree facets at 60/120 Hz,
  passive 14-degree driving, calibrated engagement and abrupt-wall rejection.
- Runtime adapter: 89 assertions, including translated/rotated world probes,
  real-order inline cap handling, nested/exception restoration and native hop
  gravity-delta replacement before KCC movement.
- Camera harness: 59 checks; Python: 149 tests; frontend: 23 tests.
- Installed-interop hook resolution: 18 targets/all Harmony parameters,
  including exception finalizer validation. No native game bodies executed.
- Source HTML/assets/catalog smoke passed. Rendered GUI automation remains
  unavailable because its kernel assets path could not initialize.

Installed with the game stopped; the installed DLL matches the tested artifact:
`afee55a27101b59a80412a2cf77817c29f3591f393604cc60e3b03420c362d89`.
Rollback backup: `local/backups/1792993683201624800`. Existing configuration was
preserved. The game was not launched automatically. A user-driven tunnel test
of this candidate remains required; the reported 55-degree stop was from the
previously loaded plugin, before this candidate was installed.

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

# Antigravity: local offline surface driving

## Simplified AI, collision filtering, and camera exit candidate

The latest user test still fails. Current logs show native drift direction
opposing desired steering, including positive correction with negative drift.
The earlier input/yaw restoration chain has been replaced by one AI-only
`RefreshSteeringToPoint(float)` prefix: attached AI use bounded road-frame
heading changes directly, while native force calculations retain their vectors
but do not apply a second heading change. Native AI guidance resumes on release.
The AI adapter is 91 lines; obsolete smoothing state and finalizers are removed.

Nearby spline tangent/centre guidance remains. Targets more than 4 units off
the road plane or 12 units laterally away are rejected; the kart then retains
its transported surface heading. Drifts are allowed only near the route and
aligned, and canceled if opposing recovery. Native earned drift boost checks
remain, AI hop requests remain blocked, and human physics are unchanged.
This is conservative guidance, not proof that every Workshop path follows
the physical loop. No position snapping or extra adhesion is introduced.

Racer contact is rejected by the complete Collider signature on KCC collision
eligibility when either local racer is attached. The separate native bounce
update is also skipped/its pending impulse cleared for attached racers and
their immediate neighbors. The neighbor check can also suppress a bounce
between ordinary racers very close to an attached racer. No collision layers,
Unity collider flags or global configuration are mutated, so filtering ends
with the attachment guard. Native slices under ignored
`local/antigravity-evidence/collision` identify the proximity bounce update
(`0x1805f9a20`) and confirm `ForceStopDrifting` (`0x1805fcc90`) clears the native
drifting flag. No non-blittable collision-report references are hooked.

Camera entry/exit now takes 0.45/0.8 seconds with smooth endpoint weighting.
The complete safe surface pose is blended, including height and obstruction
distance. Previously these constraints remained fully applied after blending
position, causing a discontinuity when state was finally removed.

Build: zero warnings/errors; whitespace check passes. No new test suite or game
launch was performed, per the user's workflow. In-game AI driving, drift boosts,
collision filtering and camera comfort remain pending user validation.

## AI surface-relative guidance candidate

Fourth follow-up: the user reports the third candidate is worse. The installed
DLL matches that candidate (`fffa966b9386e5a4d262752c3765efd5d79d92ae83dc0f5cdc36493f8181b49a`).
Logs show many zero stored steering values despite large target errors, with
some centre errors over 30 units. These snapshots do not prove the route
matcher caused every death. The new continuity matcher is nevertheless removed
to reduce the regression surface. Guidance uses the earlier native matched
progress again. Smoothing advances using elapsed time at guidance updates,
and writes controller/physics steering fields as well as AI input data; the
input hook still prevents native assist from replacing that value when called.
Bounded logs now include actual input-hook counts and drift direction to check
whether native input consumption is bypassed. Hop suppression and native
earned-boost consumption remain; human behavior is unchanged. Build passes,
runtime behavior remains unverified.

Third follow-up: installed/artifact hashes confirmed that the user had deployed
the suppression build independently before reporting improved completion but
missing drift boosts. The revised candidate removes the two drift-input filters
and keeps AI jump-input/queued-hop suppression only. When attached AI are
drifting with `bUseBoosting` enabled, guidance also invokes native
`UpdateBoostUsingWhileDrifting`. Current native slice at `0x1804e4890` (ignored
`local/antigravity-evidence/ai-boost`) checks drifting and the earned drift-bar
progress against the existing AI threshold, then requests boost through normal
input. It does not require a jump; no free boost or modified threshold is added.

Attached AI retain the spline selected on entry and maintain a local 3D match
around their previous progress (nine samples, at most 10 matches/second), rather
than accepting a new global native match on every update. This reduces route
jumps to nearby loop exits/alternate paths; it cannot fix a spline that never
follows the physical loop. Human shortcut enforcement requires track geometry
or ordered checkpoints and is not added as a position/velocity clamp. Runtime
validation of this candidate is pending.

Second follow-up: the user reports visible AI jumps/deaths. Captured guidance
samples show most attached AI already drifting, plus large centre errors;
the two-second snapshots do not capture a live hop, so a hop cause is not
claimed from those snapshots alone. The prior smoother also advanced using
the guidance callback's delta, rather than on the actual input consumption
cadence. Smoothing now advances at `SteerInput(float)` using the fixed-step
delta, with desired steering stored separately from the current value.

Attached AI now receive false jump/drift requests at both controller and
physics input boundaries. Queued AI `TriggerPhysicsJump` impulses are skipped
while attached, and the existing jump postfix explicitly ignores that skip.
This creates a conservative drive-through AI mode for loops; it does not
disable human jump-drift, normal-road AI jumps, collision bounce or item forces.
Native `ProcessRacingInput` evidence identifies the controller jump and drift
calls consuming input fields `0x18` and `0x19`; current interop resolves the
full boolean signatures. Runtime validation is pending. Compilation passed
without new test suites, in accordance with the user's preference.

Follow-up: the user reports repeated AI deaths, although some eventually finish
the loop. Logs from the first candidate show mismatched steering signs (for
example a +8.5 degree target and -0.91 input), and distant targets up to 159
units away with 83 units of road-normal separation. The previous smoother read
the native input each call, allowing other AI producers to contaminate it.
Native `ProcessRacingInput` also adds `fSteerAssistSteerInputOverrideAdd` after
reading that input. This confirms two integration problems; the logs do not
prove the exact cause of every death.

The revised candidate keeps smoothing state per kart, intercepts final AI-only
`SteerInput(float)` consumption, and uses the local spline tangent plus lateral
centre correction instead of the long-range offset target. Current native
`BezierSolution.BezierSpline.GetPoint`/`GetTangent` slices (VAs `0x180d88a80` /
`0x180d88d80`, ignored `local/antigravity-evidence/ai-route`) establish full 3D
point/derivative calculations. This retains native route selection but removes
its lateral avoidance offset while attached in favor of centre guidance.
It does not teleport, constrain kart position, increase adhesion or change
human physics. Logs now include centre error, normal offset, drift and hop.
Build passes; live validation remains pending.

The all-racer runtime logs show AI surface attachment and blocked AI spline
travel, but the user reports that AI fall off the loop. Current native slices
in ignored `local/antigravity-evidence/ai-steering` identify a guidance mismatch:
`AILogicController.RefreshSteeringToPoint` (VA `0x1804e2de0`) calculates a full
3D offset target, then uses world-XZ direction and a world-yaw rotation.
`PixelKartAIInput.ProcessRacingInput` (VA `0x1804f70b0`) consumes its turning input.
These are native pseudocode observations, not recovered original C#.

An independently installed, AI-only hook now keeps native route selection and
target offsets, projects that target into the attached road frame, and feeds
bounded, rate-limited turning input to the existing surface steering adapter.
The native AI world-yaw write is restored after this call (also on exception),
so it cannot compete with the transported surface heading. Human steering,
camera, jumps, collision, and off-surface AI remain unchanged. Existing offline,
respawn, replay and player-type guards apply. Guidance logs are bounded to 12
samples per attachment state at two-second intervals, including target distance
and road-normal offset for diagnosing a route that shortcuts the loop.

Plugin compilation passes; in-game AI behavior is pending the user's test.
This does not generate a missing loop route or establish that this track's
native AI spline follows the physical loop. No additional tests were run,
per the user's requested build-and-play workflow.

## All-racer spline-blocking test

The user-confirmed working Antigravity checkpoint is commit `f6b69ee`.
The existing `DisableSplineTeleports` option now blocks new Bezier travel for
both local human and local AI racers during a running offline race. It remains
default-off and requires Antigravity enabled. Remote players, replay playback,
paused/menu states, respawning/tank karts, and already-teleporting controllers
are excluded. Instant teleports and respawns are separate native paths.

The Studio option describes this wider scope. A bounded per-racer log identifies
human/AI blocks; the log set clears on race reset. The installed game signature
is unchanged and plugin compilation passes. Live all-racer validation is pending.

The user clarified that all-racer scope includes Antigravity itself. Surface
attachment, scoped steering-force conversion, jump/landing handling, and visual
grounding now accept both local human and local AI types through one common
offline-race eligibility guard. Replays and remote types remain excluded.
The chase camera still applies only to the local human view. An AI respawn
releases that racer's state without resetting human/other AI attachment; pause
holds existing state. Detailed per-frame diagnostics and the status HUD remain
human-only, with one bounded AI attachment message per racer for validation.

Native player collisions remain active; side impulses can push any racer past
an edge or beyond the bounded contact-recovery range. Racer colliders are not
rideable surfaces. The all-racer build compiles, but live AI loop driving and
player/AI collision behavior remain unverified; AI route decisions are native.

## User-confirmed working checkpoint

On 2026-10-26, after installing the corrected character animator hook, the user
reported: "everything is working perfectly" and requested this checkpoint be
committed. This confirms their tested offline driving, loop, steering, hop,
camera and pose scenario. It does not establish every track or multiplayer
behavior. The installed checkpoint DLL hash is
`CA44762E53AC56C3BFD98C2929C561DA567F74AC04A804C734F75A09EEC285FF`.

## Character animator consumer correction

The visual-grounding logs confirm the bridge is active, but the user still
sees the character's airborne pose. Fresh native evidence under
`local/antigravity-evidence/character-pose` identifies the missing consumer:
`Ant_CharacterWorker.Update` (`0x180502410`) writes animator grounded from synced
visual-grounded offset `0x178` (including secondary/third character animators).
The previously hooked `Ant_CharacterControllerWorker.Update` instead calls an
aim-update path; the scoped flags were restored before the separate animator
update. The character hook now targets the actual worker update. The existing
contact/hop guards and restoration remain in force. Runtime validation pending.

## Visual-grounding candidate after the 12:58 recording

The user confirms smoother driving but reports an airborne character/kart pose
while attached. Logs confirm real hops and landings, sometimes with motor
`ground=False` at the close-contact landing boundary. This supports a visual
grounding mismatch; it does not by itself prove the exact remaining camera cause.

Fresh matching-build Ghidra slices under `local/antigravity-evidence/visual-pose`
show `Ant_KartAnimatorWorker.Update` reading visual-grounded at synced offset
`0x178` for its animator and air timer, and
`Ant_VisualKart.CalculatetargetVisualAngle` reading grounded at `0xBC` to select
its in-air steering pose. The character controller's update calls its worker's
visual update separately from the kart pose update.

The new bridge supplies close-contact grounded flags and the surface normal only
during these local visual-consumer calls. It restores the original synced fields
in postfix/finalizer and never changes motor grounding. Accepted hops, forced
ungrounding, and missing close contact bypass it. Contact checks are cached once
per rendered frame. Its hook installation is independent so a visual signature
failure cannot disable the playable physics module. Compilation passes; live
pose validation remains pending. The preceding smoother build is preserved in
`local/playable-backups/20261026-stabilized-before-visual-grounding`.

## Playable checkpoint and stabilization candidate

The user describes the steering candidate as almost perfect/playable, with
remaining kart and camera shake in `20261026-1244-15.2975772.mp4`. The installed
playable DLL has SHA-256
`0CF5DCD153582B21191DE33153430DAF6E604E6759679E7A986D03AD107A2225`.
Before editing it, the full plugin source, DLL, configuration, logs, and baseline
notes were copied to
`local/playable-backups/20261026-144626-antigravity-playable`, with a hash manifest
and runtime restore instructions. This checkpoint is separate from deployment
rollback receipts.

The stabilization candidate filters contact-normal noise over 25 ms while
retaining the existing quad-transition turn cap, adds a 10-degree attachment
exit hysteresis, and supplies a 1-unit/second inward contact velocity only after
the hop/forced-unground guards. It adds no ground queries. The camera filters
heading over 35 ms in the tangent plane; obstruction retraction remains instant,
but recovery outward is eased instead of snapping on alternating cast results.
The playable road-frame steering and hop adapters are retained. Build succeeds;
live smoothness and jump preservation need the next user run.

## 2026-10-26 steering candidate after the 14:26 recording

The user reports right input producing leftward movement while drifting on the
loop. The current log proves the bounded hop and landing path executes, but has
no steering observations and cannot establish the input/force relationship.
The recording shows the improved chase camera and a drift crossing the bank.

The retained matching-build native steering body (`0x180601ff0`) uses world-up
in its lateral/centripetal construction, then updates a yaw quaternion. The
adapter additionally flipped that quaternion delta when surface-up opposed
world-up. These mixed bases can reverse steering behavior on the inverted half.

This candidate runs only that native steering call in a temporary flat frame:
before-step velocity, current velocity, acceleration, cached axes, and cached
centripetal contribution are transformed consistently. Its outputs are mapped
back to the surface frame; cached axes are restored in postfix/finalizer. The
native yaw delta is applied once around surface-up with no tilt-dependent sign.
Existing native drift/countersteer scalar rules and speed calculations remain
in use. Camera and hop changes from the preceding candidate are retained.

Bounded `Antigravity steering` logs include input, drift direction, per-call yaw
delta, sideways velocity, and tilt. Opposite lateral motion alone is not proof
of reversed input because a native drift can continue sliding during
countersteering. Plugin build succeeds; live steering validation is pending.

## 2026-10-26 follow-up: inverted-hop separation and camera pose

The `1213-43` and `1214-26` recordings show the camera sliding alongside the kart
and an airborne loss of the loop. This time the installed build's log confirms
accepted input, triggered native jump, and surviving normal velocity: impulses
range from about 20 to 62 units/second. It also records successful landings at
0.365-0.440 seconds. Jump input is therefore no longer the observed blocker.

The previous adapter retained world-space hop velocity while the road frame
turned, and the hop probe still used the shorter 1.5-unit contact range despite
the separate 2.5-unit hop setting. It also expired a hop after 0.45 seconds even
if the underlying road remained detectable. This candidate bounds the outward
component to the kart's current base jump strength, transports airborne velocity
with the sampled road normal without changing its magnitude, uses the hop
probe range, and applies the 0.8-second timeout only while no road is detected.
Landing still requires close surface contact and descending normal velocity.

The camera is now a rear/above pose derived directly from road forward/up,
using the stock following distance captured on entry. It blends with the native
camera at the existing steep-section threshold, keeps one up-alignment smoothing
stage, and applies clearance before aiming. It no longer derives its lateral
position from the stock world-up camera offset. Runtime validation is pending.

## 2026-10-26 jump and camera candidate

The user's `1155-56` recording still shows camera lag and rejected repeat
jump-drifts. The corresponding loader log reports successful hook installation,
but the previous build emitted no per-jump observations; that log cannot prove
whether input, impulse, or landing failed.

The retained current-build Ghidra slice for
`CheckAndDecideIfAllowedToJumpInPhysics` (`0x1805fb8c0`) checks both the request
flag and `eJumpState`; its waiting state rearms through motor ground contact.
Its provenance hash matches the installed GameAssembly. A new REA full-binary
query timed out, so this diagnosis uses the retained matching slice.

This candidate supplies confirmed close surface contact to that native gate for
the duration of its call, restoring the original report in postfix/finalizer.
It retains accepted-hop state until actual landing rather than clearing it when
a continuity probe merely sees the road. Input and impulse hooks remain local
and offline; bounded `Antigravity jump` events record input, impulse, resulting
velocity, and landing for the next user run. Repeated air hops are not granted.

The camera uses one surface-up smoothing stage, aims after final placement, and
constrains its clearance against the actual road normal and a static-solid
sphere cast. The older fixed lift and stacked heading/view lag were removed.
Plugin compilation succeeds; live driving, hop, and camera validation is pending.

## Current implementation

There is one direct, contact-driven ride path. It attaches to eligible static
track colliders once the contact normal reaches 45 degrees, carries the local
surface frame over the loop, and returns to the game's normal handling below
45 degrees. Flat-road steering and jump-drift remain native.

The frame is installed before KCC checks its slope limit. A short 0.35-second
sphere-probe grace handles gaps between adjacent track quads. Native collision
resolution remains enabled; triggers, respawn volumes, moving bodies, and racer
colliders are excluded. The old arming/lookahead state machine and separate
"allow walls" mode were removed from the live path.

Steering evaluates the native force calculation in a temporary flat frame and
maps its results to the road. The native yaw delta is applied around surface-up
without a world-tilt sign flip.

`Camera follows surface` remains off by default. The user previously observed
crashes with it enabled; this implementation does not claim that camera issue
is fixed. `Disable spline teleports (testing)` remains an opt-in local test
switch for tracks that use spline teleports.

The latest user run reached inverted loop angles but steering felt reversed and
the ride was janky. This source revision simplifies contact handling and adds
local-frame steering correction; it still needs the user to validate in-game.

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

# Mid-air Jump Drift

Open `Launch Studio.cmd`, then find **Mid-air Jump Drift** in the Mod library.
It belongs to Community Mods and starts off in a fresh configuration. It works
only for local human racers in offline races. OnlineProtection stays mandatory;
modified races cannot upload leaderboard results or enter online rooms.

## Controls and behavior

- Release and freshly press a native jump shoulder/button, keep holding it, and
  steer left or right. Both native shoulder routes are supported, including
  controller remapping and the separate `Drift_left_right` action.
- A press up to **10/30 seconds (0.333 s)** before touching down can start the
  next hop and powerslide together. Releasing the button cancels the buffered
  request. A fresh press after leaving drivable ground has **1/6 second
  (0.167 s)** of grace if a previous hop is not still awaiting landing.
- A moving kart can also hop into a drift on flat ground. A normal drift that
  continues from an already-held shoulder has no fresh input and is unchanged.
- Charge and press the opposite boost shoulder for mini-turbos as usual. This
  module does not auto-fire the three mini-turbos or change their strength.
- The accepted hop arms its trick at the start. Eligibility stays armed for
  that flight; TK2 performs its normal trick once its height test passes and
  awards its normal landing boost. No extra jump/trick press is necessary.
  Releasing the drift later does not cancel an already armed flight.
- Low flat hops still need sufficient native trick height to earn a landing
  boost. This module does not manufacture a maximum landing boost.
- During the flight, the character's drift animation blend is suppressed only
  while its animation update runs. Native jump/trick presentation can play;
  kart drift tilt, boost sparks, boost meters and physics retain their drift
  state. No animator is frozen and no permanent pose is forced.

The optional status line reports an armed flight, a trick-qualified flight and
the native landing callback. Pause, menus, finish, recovery, teleport, tank form,
replay playback, disabling the module and practice snapshot restoration clear
the module's pending input. The Driving challenge's jump/drift restrictions take
precedence. Holding a button does not continuously create new jump requests.

## Implementation evidence

This is a new TK2 feature, not a port of CTR's vehicle physics. CTR reference:
`Github/ctr-native/include/namespace_Vehicle.h` names `jump_TenBuffer`,
`jump_CoyoteTimerMS` and `jump_CooldownMS`; `game/Vehicle/VehPhysProc.c` fills,
decrements and releases the buffer, and `VehPhysGeneral.c` consumes it only when
coyote time remains and the jump cooldown permits it.

Current TK2 Ghidra exports are ignored local evidence in
`local/midair-drift-evidence/`, including method metadata and binary provenance.
The analyzed GameAssembly SHA-256 is
`1104a3b50e15c4316c90f3f98c0441871140b8ca8eee53a6bd6380c744be6d39`.
This hash records the evidence; execution has no binary-hash allowlist.

| Native method | Address | Relevant behavior |
| --- | --- | --- |
| `Ant_KartInput.ProcessRacingInput` | `0x1806BCA90` | Shoulder swapping, per-flight jump latch, controller input and mini-turbo routing |
| `PixelEasyCharMoveKartController.JumpInput(bool)` | `0x1805F6790` | Inlines the jump request; patching only `PixelKartPhysics.JumpInput` would miss this path |
| `CheckAndDecideIfAllowedToJumpInPhysics(float)` | `0x1805FB8C0` | Landing callback and buffered hop acceptance; clears the drift bit on hop acceptance |
| `ForceDriftSteerDirection_Replay_OverrideDriftDir(float,bool)` | `0x1805FCC60` | Sets direction, resets drift time and starts the drift; no replay playback side effect |
| `DetectJumpTrick(float)` | `0x1805FC3E0` | Pending-input timeout, ground clearance test and native trick event |
| `OnKartLandedAfterPlayerTriggeredJump(bool)` | `0x1806BA9D0` | Native trick-qualified landing reward calculation |
| `Ant_CharacterWorker.Update()` | `0x180502410` | Reads visual drift state for the character animation blend |

The native hop applies its own impulse. The module restores the helper's
temporary forced-direction field immediately. Trick timeout and character
visual drift changes are scoped to their native calls and restored by Harmony
finalizers, including when the original method throws. The actual jump timestamp
and native grounding/landing cooldown remain intact.

## Validation, 2026-10-27

- Plugin build: zero warnings/errors against installed interop.
- Six feature hook signatures and Harmony argument types resolve against the
  installed game. Native method bodies above were inspected separately.
- Aerial practice harness: 30,032 assertions, including the new buffer timing,
  coyote boundary, release, duplicate callback, double-hop and reset cases.
- Python suite: 147/150 passed. The two failures and one error are unchanged
  Antigravity tests, reproduced with the original `HEAD` catalog. The full
  RuntimeHooks runner also encounters an existing Antigravity finalizer-return
  assertion; its new optional feature argument allows this module's six hooks
  to be verified independently.
- Studio: all 23 UI tests and production build passed; the actual Mod library
  displays the module, enabled state and status checkbox.
- During a user-started game session, installed DLL and build hashes matched
  `1ca92aea7490bd7b58bedc2e3b67516dbeae3aeac57f7b0d482416ce7f341542`.
  The enabled feature loaded successfully and its native landing callback
  reported six trick-qualified landings at the verification snapshot. No game
  was launched or driven automatically.

Live logs establish hook execution and trick-qualified landing callbacks. They
do not establish controller feel, sustained animation appearance or completion
of three manual airborne mini-turbos. Those remain visual/player checks: compare
a flat clean drift, a buffered landing hop, a ramp-edge hop with three turbos,
and an ordinary ramp-edge drift without a fresh jump. Check that pause/recovery
never replays an old buffered press.

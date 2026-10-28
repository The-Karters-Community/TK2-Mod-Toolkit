# CTR handling: initial evidence map

Initial status: research only; see the test-build update at the end of this document.

Sources: `Github/ctr-native/game/Vehicle/VehFire.c`, `VehPhysProc.c`, and `Github/CTR Guide 2023.md`.

- `VehFire_Increment` combines fire-level selection and reserve updates, including pad-specific logic. Its conditions at lines 298–370 must be ported together; simply summing TK2 speed bonuses changes priority and super-pad behavior.
- `VehPhysProc.c` decrements positive reserves using the game frame duration, and uses signed 16-bit reserve storage. Its drift-boost branch calculates reserve gain from meter charge. The guide describes about two seconds of reserves for a full drift boost and roughly one second for regular pads or eligible hangtime.
- The guide describes non-stacking fire levels, acceleration and friction relative to the current speed cap, different surface behavior, and reduced air friction. CTR fixed-point units cannot be assumed to be TK2 metres per second.
- Existing TK2 metadata exposes `Ant_BoostManager` reserve, boost meter, pad, landing and boost methods. The previously exported native landing callback confirms it calls native reserve and boost functions; this does not establish every boost caller or hook boundary on the installed build.
- Implementation still requires current native evidence for boost entry points, reserve consumption, speed calculation and ground/air friction, unit calibration and interactions with existing modules. Compiling a guessed implementation would not prove CTR-like handling. Runtime validation requires a user-driven offline race, per project instructions.

## Current native follow-up

On the installed GameAssembly SHA-256 `1104a3b50e15c4316c90f3f98c0441871140b8ca8eee53a6bd6380c744be6d39`, additional Ghidra exports were produced under ignored `local/midair-drift-evidence/` for FireSliderBoost, FireBoost, BoostPadTriggerEnter, AddReserves, FixedUpdateBoostTime, GetCurrentBoostMaxSpeedIncrease, UpdateDrivingFriction and UpdateAccelerationForwardBackward. Export metadata is in that directory's provenance file.

- Current native FireSliderBoost (`0x1806b8910`) calculates a normalized meter value, directly calls AddReserves (`0x1806b8130`), and calls FireBoost (`0x1806b8720`). Its input caller directly invokes FireSliderBoost. Different event hooks must not award the same boost twice.
- Native BoostPadTriggerEnter (`0x1806b8320`) calls FireBoost with boost type 3, and maintains TK2 boost-pad time. This timer is not evidence of physical pad contact; a contact/exit hook is still needed for CTR's pad rules.
- Native UpdateAccelerationForwardBackward (`0x180600630`) calls GetCurrentBoostMaxSpeedIncrease (`0x1806b9ed0`); that function adds multiple decoded contributions. Replacing only the reserve value does not establish CTR's fire priority or soft-cap physics.
- Native `EBoostType` distinguishes three drift boosts, boost pads, jump landing, booster and star aura. No dedicated super-pad type was established. Treating all TK2 pads as CTR super pads would invent behavior.

These are static native observations, not runtime validation. CTR-specific unit calibration, braking and wall-hit behavior, current pad contact/exit, and the collision and steering model remain unresolved. No CTR module has been built or deployed.

## Testable adaptation, 2026-10-28

A default-off offline CTR-inspired Handling module has now been implemented. It is an initial adaptation, not full or calibrated CTR physics. See `docs/CTR-HANDLING.md` for the exact supported and unsupported behavior and first test scenarios. Native exports are static evidence; player-run gameplay validation is still required.

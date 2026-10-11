using System;
using TK2.Reconstructed;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }

// Fixture sequences derived from the native branches, including state left over from an earlier input.
var released = new JumpState { RunJump = true, JumpedAt = 5, PendingTrick = true };
released.PressesBeforeGround.Add(3);
KartLogic.JumpInput(released, false, 8, false, false, false);
Check(released.RunJump && released.JumpedAt == 5 && released.PressesBeforeGround.Count == 1, "release must not clear prior state");
for (int ground = 0; ground <= 1; ground++)
for (int replay = 0; replay <= 1; replay++)
for (int replayGround = 0; replayGround <= 1; replayGround++)
{
    var state = new JumpState { TimeSinceNotGrounded = 10, GracePeriod = .2f };
    state.PressesBeforeGround.Add(-1);
    KartLogic.JumpInput(state, true, 42, ground == 1, replay == 1, replayGround == 1);
    bool expected = ground == 1 || (replay == 1 && replayGround == 1);
    Check(state.RunJump == expected, "replay grounding truth table");
    Check(state.PressesBeforeGround.Count == (expected ? 0 : 1), "old history must be replaced");
    if (!expected) Check(state.PressesBeforeGround[0] == 42, "airborne input must record current time");
}
foreach (var item in new[] { (.19f, true), (.2f, true), (.21f, false) })
{
    var state = new JumpState { TimeSinceNotGrounded = item.Item1, GracePeriod = .2f, ExtraJumpAllowsTrick = true };
    KartLogic.JumpInput(state, true, 12, false, false, false);
    Check(state.RunJump == item.Item2 && state.PendingTrick && state.JumpedAt == 12, "inclusive grace boundary and extra trick side effects");
}
var accumulated = KartLogic.AddVelocity(new(4, -2, 0), new(-4, 3, -7));
accumulated = KartLogic.AddVelocity(accumulated, new(1, -1, 7));
Check(accumulated.X == 1 && accumulated.Y == 0 && accumulated.Z == 0, "velocity calls must accumulate independently");
Check(CameraLogic.GetCameraIndex(4, 2, false) == null && CameraLogic.GetCameraIndex(4, 2, true) == null, "ghost has no camera");
Check(CameraLogic.GetCameraIndex(0, 2, true) == 2, "local split-screen spectator keeps original slot");
Check(CameraLogic.GetCameraIndex(1, 2, true) == 0, "network spectator uses first slot");
Check(CameraLogic.GetCameraIndex(1, 2, false) == 2, "normal camera keeps player slot");
Check(CameraLogic.IsIntroActive(.949f) && !CameraLogic.IsIntroActive(.95f) && !CameraLogic.IsIntroActive(float.NaN), "native strict threshold and NaN semantics");
bool forced = false, tribune = true;
CameraLogic.ForceInstantTeleport(ref forced, ref tribune);
Check(forced && !tribune, "teleport must clear tribune camera");
CameraLogic.EnableTribune(ref tribune, true);
Check(tribune && CameraLogic.IsSpectator(true) && !CameraLogic.IsSpectator(false), "camera field accessors");
Check(Math.Abs(CameraFraming.DistanceScale(75, 75, true, 1) - 1) < .00001, "equal FOV preserves distance");
foreach (float fov in new[] {35f, 65f, 90f, 110f}) {
    float distance = CameraFraming.DistanceScale(75, fov, true, 1);
    double before = 1 / Math.Tan(75 * Math.PI / 360);
    double after = 1 / (distance * Math.Tan(fov * Math.PI / 360));
    Check(Math.Abs(before - after) < .00001, "compensated distance preserves projected kart size");
}
Check(CameraFraming.DistanceScale(75, 110, true, 1) < 1 && CameraFraming.DistanceScale(75, 35, true, 1) > 1, "wide view moves closer, narrow view further");
Check(CameraFraming.DistanceScale(75, 35, false, 1.2f) == 1.2f, "manual framing uses chosen distance");
Check(CameraFraming.BlendFactor(.016f, 0) == 1, "zero smoothing is immediate");
Check(CameraFraming.BlendFactor(0, .12f) == 0, "zero elapsed time preserves value");
float half = CameraFraming.BlendFactor(.5f, .7f);
float full = CameraFraming.BlendFactor(1, .7f);
Check(Math.Abs((1 - half) * (1 - half) - (1 - full)) < .000001, "smoothing is independent of update subdivision");
Check(full > 0 && full < 1, "smoothing approaches without overshooting");
try { CameraFraming.BlendFactor(float.NaN, .12f); throw new Exception("NaN accepted"); }
catch (ArgumentOutOfRangeException) { Check(true, "invalid timing rejected"); }
uint modDodge = 0x10000;
foreach (uint mask in new uint[]{0,1,2,4,8,16,32,1|16}) {
    uint added = HealthLogic.ChangeSource(mask, modDodge, true);
    Check(HealthLogic.ChangeSource(added, modDodge, false) == mask, "mod dodge must not remove native protection sources");
    Check(HealthLogic.IsImmune(added, false), "a custom source protects against damage");
    Check(HealthLogic.IsDeathImmune(added) == ((mask & 16) != 0), "mod source must not grant death immunity");
    Check(HealthLogic.CollidersActive(mask) == (mask == 0 || (mask & 16) != 0), "native collider flag truth table");
}
Check(HealthLogic.IsImmune(0,true) && !HealthLogic.IsImmune(0,false), "ghost transformation grants immunity independently");
Check(BoostLogic.IsBoostingFromBoostPad(.001f) && !BoostLogic.IsBoostingFromBoostPad(0f), "boost-pad timer uses a strict positive threshold");
Check(BoostLogic.IsBoostingFromReserves(.001f) && !BoostLogic.IsBoostingFromReserves(0f), "reserve boost uses a strict positive threshold");
Check(BoostLogic.IsBoostingFromBooster(.001f) && !BoostLogic.IsBoostingFromBooster(0f), "booster power-up uses a strict positive threshold");
Check(BoostLogic.IsBoostingFromStar(.001f) && !BoostLogic.IsBoostingFromStar(0f), "star power-up has its own strict positive predicate");
Check(BoostLogic.IsBoosting(.1f, false, 0f, 0f), "reserve boost contributes to aggregate state");
Check(BoostLogic.IsBoosting(0f, true, 0f, 0f), "just-triggered flag contributes to aggregate state");
Check(BoostLogic.IsBoosting(0f, false, .1f, 0f), "boost-pad time contributes to aggregate state");
Check(BoostLogic.IsBoosting(0f, false, 0f, .1f), "booster power-up time contributes to aggregate state");
Check(!BoostLogic.IsBoosting(0f, false, 0f, 0f), "aggregate state is false without its four native conditions");
Check(BoostLogic.IsBoostingFromStar(.1f) && !BoostLogic.IsBoosting(0f, false, 0f, 0f), "star timer is omitted from the native aggregate predicate");
Check(BoostLogic.GetCurrentBoostMaxSpeedIncrease(1, 2, 3, 4, 1, 5, 1, 6, 1, 7) == 28,
    "max-speed getter sums accumulated bonuses and each active-source bonus");
Check(BoostLogic.GetCurrentBoostMaxSpeedIncrease(1, 2, 3, 4, 0, 5, 0, 6, 0, 7) == 10,
    "max-speed getter always includes accumulated bonuses but omits zero-timer sources");
Check(BoostLogic.GetCurrentBoostMaxSpeedIncrease(1, 2, 3, 4, -.1f, 5, -.1f, 6, -.1f, 7) == 10,
    "max-speed getter omits negative-timer sources");
Check(BoostLogic.GetCurrentBoostAccelMultiplier(0, 0, 1, 0, 0, 4, 1, 5, 1.8f) == 1.8f,
    "boost pad applies configured acceleration below the forward-speed threshold");
Check(BoostLogic.GetCurrentBoostAccelMultiplier(0, 0, 1, 0, 0, 5, 1, 5, 1.8f) == 1f,
    "boost pad does not apply the configured multiplier at the exact speed threshold");
Check(BoostLogic.GetCurrentBoostAccelMultiplier(0, 0, 1, 0, 0, 4, 0, 5, 1.8f) == 1f,
    "boost pad timer must be positive to apply the configured multiplier");
Check(BoostLogic.GetCurrentBoostAccelMultiplier(0, 0, 1, 4, 0, 0, 1, 5, 1.8f) == 1.8f,
    "native forward-speed magnitude ignores lateral velocity");
Check(BoostLogic.GetBoostZone(1, new[] { 1f, .1f, .05f }, new[] { .6f, .6f, .6f }, 0) == BoostZone.Perfect,
    "one full boost with two low slots can mark the active boost perfect");
Check(BoostLogic.GetBoostZone(1, new[] { 1f, float.NaN, float.NaN }, new[] { .6f, .6f, .6f }, 0) == BoostZone.Perfect,
    "native comparisons count NaN fill slots as low in the full-slot combo");
Check(BoostLogic.GetBoostZone(0, new[] { 1f, .05f, .05f }, new[] { .6f, .6f, .6f }, 3) == BoostZone.Great,
    "the full-slot combo is suppressed after all three boosters have been counted");
Check(BoostLogic.GetBoostZone(1, new[] { 1f, .4f, .5f }, new[] { .6f, .6f, .6f }, 1) == BoostZone.Great,
    "another completed boost slot raises the current result to great");
Check(BoostLogic.GetBoostZone(0, new[] { .2f, .3f, .4f }, new[] { .55f, .6f, .6f }, 1) == BoostZone.Early,
    "the early zone includes the exact 0.55 threshold");
Check(BoostLogic.GetBoostZone(0, new[] { .2f, .3f, .4f }, new[] { .5501f, .6f, .6f }, 1) == BoostZone.Great,
    "fill time above the early threshold falls back to great");
Check(BoostLogic.GetBoostZone(0, new[] { .2f, .3f, .4f }, new[] { .94f, .6f, .6f }, 1) == BoostZone.Great,
    "the perfect window excludes its 0.94 lower boundary");
Check(BoostLogic.GetBoostZone(0, new[] { .2f, .3f, .4f }, new[] { .9401f, .6f, .6f }, 1) == BoostZone.Perfect,
    "fill time just above 0.94 is perfect");
Check(BoostLogic.GetBoostZone(0, new[] { .2f, .3f, .4f }, new[] { 1.05f, .6f, .6f }, 1) == BoostZone.Perfect,
    "the perfect window includes its 1.05 upper boundary");
Check(BoostLogic.GetBoostZone(0, new[] { .2f, .3f, .4f }, new[] { 1.0501f, .6f, .6f }, 1) == BoostZone.Great,
    "fill time above 1.05 falls back to great");
var resetPad = BoostLogic.PlanBoostPadTrigger(true, 5f, 4f, 2f, .25f, 2f, 3f, 4f, 5f);
Check(resetPad.ShouldFire && resetPad.ResetInstantVelocity, "reset-mode boost pads bypass same-pad cooldown");
Check(resetPad.LastTriggerTime == 4f && resetPad.BoostPadTime == 12.25f,
    "reset-mode pad adds duration without updating the last-trigger timestamp");
Check(resetPad.FireLength == 12f && resetPad.FireInstantVelocity == 10f,
    "boost-pad strength and length use their configured multipliers");
var suppressedPad = BoostLogic.PlanBoostPadTrigger(false, 1.4f, 1f, .5f, .25f, 2f, 3f, 4f, 5f);
Check(!suppressedPad.ShouldFire && !suppressedPad.ResetInstantVelocity,
    "same-pad trigger is suppressed strictly inside the cooldown");
Check(suppressedPad.LastTriggerTime == 1.4f && suppressedPad.BoostPadTime == .25f,
    "suppressed pad still updates last-trigger time without adding pad duration");
var boundaryPad = BoostLogic.PlanBoostPadTrigger(false, 1.5f, 1f, .5f, .25f, 2f, 3f, 4f, 5f);
Check(boundaryPad.ShouldFire && boundaryPad.LastTriggerTime == 1.5f,
    "same-pad trigger is accepted at the exact cooldown boundary");
var stoppedBoost = BoostLogic.PlanStopBoost(new[] { .4f, .8f, 1.2f, 7f });
Check(stoppedBoost.BoostReserves == 0f && stoppedBoost.TripleBoostSpeedIncreaseReservesNormalized == 0f,
    "StopBoost clears reserves and the accumulated triple-boost reserve bonus");
Check(stoppedBoost.FillTimes.Length == 4 && stoppedBoost.FillTimes[0] == 0f
    && stoppedBoost.FillTimes[1] == 0f && stoppedBoost.FillTimes[2] == 0f && stoppedBoost.FillTimes[3] == 7f,
    "StopBoost clears the three boost slots and preserves any later array elements");
Check(stoppedBoost.CurrentAirTime == 0f && stoppedBoost.BoosterCount == 0,
    "StopBoost resets airborne time and the booster count");
float[] boostFills = { .35f, .8f, 1.2f };
Check(BoostLogic.GetManualBoostFillNormalized(boostFills, 1) == .8f, "manual boost getter returns the selected fill slot");
BoostLogic.SetWheelBurningBoostFillTime(boostFills, 1, .45f);
Check(boostFills[0] == .35f && boostFills[1] == .45f && boostFills[2] == 1.2f,
    "boost fill setter replaces only the selected slot");
try { BoostLogic.SetWheelBurningBoostFillTime(boostFills, -1, .2f); throw new Exception("negative boost slot accepted"); }
catch (IndexOutOfRangeException) { Check(true, "negative boost slot follows the native unsigned bounds check"); }
foreach (var item in new[] { (.2f, true), (.5f, true), (.5001f, false), (.1999f, false), (float.NaN, false) })
    Check(BoostLogic.IsManualBoostWithinFireRange(new[] { item.Item1 }, .2f, .5f) == item.Item2, "manual boost fire range uses inclusive bounds and rejects NaN");
foreach (var item in new[] { (.2f, 0f), (.5f, .5f), (.8f, 1f), (1.1f, 1f), (.1f, 0f) })
    Check(BoostLogic.GetTriggeredBoostStrengthNormalized(new[] { item.Item1 }, 0, .2f, .8f) == item.Item2, "triggered boost strength normalizes and clamps to zero through one");
Check(BoostLogic.HasMaximumManualBoostLevel(3) && !BoostLogic.HasMaximumManualBoostLevel(4), "maximum boost level is exactly three");
Check(BoostLogic.AddReserves(2, 3) == 5 && BoostLogic.AddReserves(2, -3) == -1,
    "reserve addition adds the amount directly without clamping");
Check(BoostLogic.KeepReservesAtMinimum(.2f, .5f, 0) == .2f
    && BoostLogic.KeepReservesAtMinimum(.2f, .5f, 1) == .5f
    && BoostLogic.KeepReservesAtMinimum(.8f, .5f, 1) == .8f,
    "conditional reserve floor is gated by a nonzero race-state value");
Check(float.IsNaN(BoostLogic.KeepReservesAtMinimum(float.NaN, .5f, 1)),
    "conditional reserve floor preserves NaN under the native comparison");
Check(BoostLogic.ForceReservesMinimum(.2f, .5f) == .5f && BoostLogic.ForceReservesMinimum(.8f, .5f) == .8f, "reserve floor raises only values below the target");
Check(float.IsNaN(BoostLogic.ForceReservesMinimum(float.NaN, .5f)), "reserve floor preserves a NaN current value like the native comparison");
foreach (var item in new[] { (0f, false, false), (0f, true, false), (.1f, false, true), (.1f, true, false) })
    Check(BoostLogic.WillWallCollisionReduceReserves(item.Item1, item.Item2) == item.Item3, "wall collision loses reserves only when reserves are positive and tank weapon is idle");
int wallEvents = 0;
float lateralVelocity = 8f, collisionReserves = 10f;
BoostLogic.OnWallCollision(ref lateralVelocity, ref collisionReserves, false, () => wallEvents++);
Check(lateralVelocity == 0f && Math.Abs(collisionReserves - 7f) < .00001f && wallEvents == 1, "wall collision resets lateral velocity, reduces reserves by native 0.7 and invokes event");
lateralVelocity = 3f; collisionReserves = 10f;
BoostLogic.OnWallCollision(ref lateralVelocity, ref collisionReserves, true, () => wallEvents++);
Check(lateralVelocity == 0f && collisionReserves == 10f && wallEvents == 2, "active tank weapon suppresses reserve loss but not the event");
lateralVelocity = 3f; collisionReserves = 0f;
BoostLogic.OnWallCollision(ref lateralVelocity, ref collisionReserves, false, null);
Check(lateralVelocity == 0f && collisionReserves == 0f && wallEvents == 2, "zero reserves still reset lateral velocity; absent event is skipped");
foreach (BoostKind kind in Enum.GetValues<BoostKind>())
{
    bool expectedForAi = kind is BoostKind.BoostPad or BoostKind.JumpLanding or BoostKind.BoosterPowerUp or BoostKind.StarAura;
    Check(BoostLogic.IsPhysicalManualBoostEnabled(2, false, true, kind) == expectedForAi, "AI physical-boost restriction follows native enum branches");
    Check(BoostLogic.IsPhysicalManualBoostEnabled(0, false, true, kind), "AI-only restriction leaves ordinary human boost enabled");
}
Check(!BoostLogic.IsPhysicalManualBoostEnabled(0, true, true, BoostKind.ManualOne), "debug-forced human enters AI restriction branch");
Check(BoostLogic.IsPhysicalManualBoostEnabled(0, false, false, BoostKind.ManualOne), "AI flag alone does not disable physical boost");
bool breaking = false;
BoostLogic.SetBreakingOnGround(ref breaking, true);
Check(breaking, "braking setter writes current state");
Console.WriteLine($"Readable behavior and camera geometry: {assertions} assertions passed.");

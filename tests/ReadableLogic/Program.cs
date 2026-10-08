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
uint modDodge = 0x10000;
foreach (uint mask in new uint[]{0,1,2,4,8,16,32,1|16}) {
    uint added = HealthLogic.ChangeSource(mask, modDodge, true);
    Check(HealthLogic.ChangeSource(added, modDodge, false) == mask, "mod dodge must not remove native protection sources");
    Check(HealthLogic.IsImmune(added, false), "a custom source protects against damage");
    Check(HealthLogic.IsDeathImmune(added) == ((mask & 16) != 0), "mod source must not grant death immunity");
    Check(HealthLogic.CollidersActive(mask) == (mask == 0 || (mask & 16) != 0), "native collider flag truth table");
}
Check(HealthLogic.IsImmune(0,true) && !HealthLogic.IsImmune(0,false), "ghost transformation grants immunity independently");
Console.WriteLine($"Readable behavior and camera geometry: {assertions} assertions passed.");

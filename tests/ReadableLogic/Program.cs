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
Console.WriteLine($"Readable native behavior: {assertions} assertions passed.");

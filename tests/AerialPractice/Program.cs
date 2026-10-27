using System;
using TK2.Customization;

int assertions = 0;
void Check(bool condition, string why) { assertions++; if (!condition) throw new Exception(why); }
var random = new Random(1942);
for (int i = 0; i < 10000; i++) {
    float x = (float)(random.NextDouble() * 160 - 80), z = (float)(random.NextDouble() * 160 - 80);
    float angle = (float)(random.NextDouble() * Math.PI * 2), distance = (float)(random.NextDouble() * 1000);
    var right = (X: MathF.Cos(angle), Z: MathF.Sin(angle));
    var impulse = distance;
    var target = AirDashMath.TargetLateral(x * right.X + z * right.Z, 1, impulse);
    float speed = MathF.Sqrt(x*x + z*z);
    var output = AirDashMath.Hold(x, z, right.X, right.Z, target, speed);
    Check(Math.Sqrt(output.X*output.X + output.Z*output.Z) <= Math.Sqrt(x*x + z*z) + .00003,
        "dash conserves the original horizontal speed");
    var leftTarget = AirDashMath.TargetLateral(x * right.X + z * right.Z, -1, impulse);
    var left = AirDashMath.Hold(x, z, right.X, right.Z, leftTarget, speed);
    Check(Math.Abs(AirDashMath.TargetLateral(0, 1, impulse) + AirDashMath.TargetLateral(0, -1, impulse)) < .0002,
        "left and right dash targets are symmetric");
    Check(Math.Sqrt(left.X*left.X + left.Z*left.Z) <= speed + .00003,
        "left dash also conserves horizontal speed");
}
Check(Math.Abs(AirDashMath.TargetLateral(5, 1, 65) - 70) < .0001, "dash adds the requested lateral speed");
Check(Math.Abs(AirDashMath.TargetLateral(5, 1, 1000) - 1005) < .01, "lateral impulse has no artificial ceiling");
try { AirDashMath.TargetLateral(0, 0, 2); Check(false, "zero direction is rejected"); }
catch (ArgumentOutOfRangeException) { Check(true, "zero direction is rejected"); }

var history = new PracticeHistory<string>();
Check(!history.TryAt(3, out _, out _), "empty history cannot rewind");
for (int i = 0; i <= 200; i++) history.Add(i * .05f, i.ToString(), 3);
Check(history.Oldest >= 7 && history.Newest == 10, "rolling window expires old samples");
Check(!history.TryAt(6, out _, out _), "insufficient history cannot silently restore a newer sample");
Check(history.TryAt(8.01f, out var value, out var time) && value == "160" && time == 8, "restore uses a real sample at or before the target");
history.Branch(8);
Check(history.Newest == 8, "resuming after scrubbing discards the abandoned future");
history.Add(8.05f, "new branch", 3);
Check(history.TryAt(9, out value, out time) && value == "new branch", "recording follows the new timeline");
history.Clear();
Check(history.Count == 0 && !history.TryAt(8, out _, out _), "reset makes previous-race state inaccessible");
for (int i = 0; i < 1000; i++) history.Add(i * .0001f, "bounded", 20);
Check(history.Count <= 401, "history has a hard memory bound even with unexpectedly frequent capture");
history.Trim(21, 20);
Check(history.Count == 0, "unsafe periods still expire stale samples even when no new capture is allowed");
var midair = new MidairDriftState();
Check(!midair.CanJump(1, true, true, false, 0), "holding a button without a fresh press cannot hop");
midair.Press(0, 1, "Jump", false);
Check(midair.Pending(MidairDriftState.BufferSeconds, true), "ten NTSC frames is an inclusive seconds-based buffer");
Check(!midair.Pending(MidairDriftState.BufferSeconds + .001f, true), "old presses expire");
Check(!midair.Pending(.1f, false) && !midair.Pending(-.01f, true), "release or time reversal cannot replay a press");
Check(midair.CanJump(.1f, true, false, false, MidairDriftState.CoyoteSeconds), "coyote edge is inclusive");
Check(!midair.CanJump(.1f, true, false, false, MidairDriftState.CoyoteSeconds + .001f), "no coyote hop after the grace interval");
Check(!midair.CanJump(.1f, true, false, true, .02f), "a launched jump cannot be jumped again in empty air");
Check(midair.CanJump(.1f, true, true, true, 0), "buffer survives the previous hop until native landing cooldown allows it");
Check(midair.Accept(.1f, true, -1, 30, 10) && midair.Active, "accepted buffered hop starts a left drift");
Check(!midair.Pending(.1f, true), "one press starts at most one hop");
midair.Press(.11f, 1, "Jump", false);
Check(!midair.Pending(.11f, true), "repeated input callbacks within one frame cannot recreate consumed input");
midair.Press(.12f, 2, "Boost", true);
Check(!midair.Pending(.12f, true) && midair.Active, "mini-turbo presses during a drift leave the flight intact");
midair.Press(.13f, 3, "Jump", false);
midair.Land();
Check(midair.Pending(.14f, true) && !midair.Active, "a released and re-pressed drift button can buffer the next hop across landing");
Check(midair.Accept(.14f, true, 1, 30, 10), "buffered double hop starts a right drift");
int midairFrame = 10;
foreach (var sample in new[] { (0f, 30f), (.1f, 30f), (1f, 9f), (float.NaN, 30f), (1f, float.NaN) }) {
    midair.Clear(); midair.Press(1, midairFrame++, "Jump", false);
    Check(midair.Pending(1.01f, true) && !midair.Accept(1.01f, true, sample.Item1, sample.Item2, 10), "neutral input, low speed and non-finite values cannot start drift");
}
midair.Clear(); midair.Press(2, 20, "Jump", false); midair.Clear();
Check(!midair.Active && !midair.Pending(2.01f, true), "disable, pause, rewind and recovery clear pending input");
Console.WriteLine($"Aerial practice: {assertions} assertions passed (math, history and mid-air input policy; not a runtime game test).");

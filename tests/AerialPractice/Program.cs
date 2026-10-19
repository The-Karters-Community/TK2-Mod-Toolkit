using System;
using TK2.Customization;

int assertions = 0;
void Check(bool condition, string why) { assertions++; if (!condition) throw new Exception(why); }
var random = new Random(1942);
for (int i = 0; i < 10000; i++) {
    float x = (float)(random.NextDouble() * 160 - 80), z = (float)(random.NextDouble() * 160 - 80);
    float angle = (float)(random.NextDouble() * Math.PI * 2), distance = (float)(random.NextDouble() * 1000);
    var right = (X: MathF.Cos(angle), Z: MathF.Sin(angle));
    var target = AirDashMath.TargetLateral(x * right.X + z * right.Z, 1, distance, .12f);
    float speed = MathF.Sqrt(x*x + z*z);
    var output = AirDashMath.Hold(x, z, right.X, right.Z, target, speed);
    Check(Math.Sqrt(output.X*output.X + output.Z*output.Z) <= Math.Sqrt(x*x + z*z) + .00003,
        "dash conserves the original horizontal speed");
    var leftTarget = AirDashMath.TargetLateral(x * right.X + z * right.Z, -1, distance, .12f);
    var left = AirDashMath.Hold(x, z, right.X, right.Z, leftTarget, speed);
    Check(Math.Abs(AirDashMath.TargetLateral(0, 1, distance, .12f) + AirDashMath.TargetLateral(0, -1, distance, .12f)) < .0002,
        "left and right dash targets are symmetric");
    Check(Math.Sqrt(left.X*left.X + left.Z*left.Z) <= speed + .00003,
        "left dash also conserves horizontal speed");
}
Check(Math.Abs(AirDashMath.TargetLateral(5, 1, 3, .12f) - 30) < .0001, "distance and duration define the lateral dash target");
Check(Math.Abs(AirDashMath.TargetLateral(5, 1, 1000, .12f) - 8338.333f) < .01, "dash distance has no artificial ceiling");
try { AirDashMath.TargetLateral(0, 0, 2, .12f); Check(false, "zero direction is rejected"); }
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
Console.WriteLine($"Aerial practice: {assertions} assertions passed (math and history; not a runtime game test).");

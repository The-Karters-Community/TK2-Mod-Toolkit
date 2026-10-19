using System;
using TK2.Customization;

int assertions = 0;
void Check(bool condition, string why) { assertions++; if (!condition) throw new Exception(why); }
var random = new Random(1942);
for (int i = 0; i < 10000; i++) {
    float x = (float)(random.NextDouble() * 160 - 80), z = (float)(random.NextDouble() * 160 - 80);
    float angle = (float)(random.NextDouble() * Math.PI * 2), impulse = (float)(random.NextDouble() * 30 - 15);
    var shifted = AirShiftMath.Adjust(x, z, MathF.Cos(angle), MathF.Sin(angle), impulse, 1);
    double speed = Math.Sqrt(x*x + z*z), after = Math.Sqrt(shifted.X*shifted.X + shifted.Z*shifted.Z);
    Check(after <= speed + .00003, "shift must never increase horizontal speed");
    double dx = shifted.X - x, dz = shifted.Z - z;
    Check(Math.Sqrt(dx*dx + dz*dz) <= Math.Abs(impulse) + .00003, "sideways correction is bounded by configured impulse");
    var reduced = AirShiftMath.Adjust(x, z, 1, 0, impulse, .9f);
    Check(Math.Sqrt(reduced.X*reduced.X + reduced.Z*reduced.Z) <= speed * .9 + .00003, "retention caps horizontal speed");
}
Check(AirShiftMath.Adjust(0, 0, 1, 0, 6, 1) == (0, 0), "stationary shift cannot create speed");
Check(AirShiftMath.Adjust(0, 30, 0, 0, 6, 1) == (0, 30), "vertical kart right vector cannot create a horizontal impulse");
var left = AirShiftMath.Adjust(0, 30, 1, 0, -6, 1);
var right = AirShiftMath.Adjust(0, 30, 1, 0, 6, 1);
Check(left.X < 0 && right.X > 0 && Math.Abs(left.Z-right.Z) < .00001, "left and right are symmetric");

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

using System;
using TK2.Customization;

int assertions = 0;
void Check(bool condition, string why) { assertions++; if (!condition) throw new Exception(why); }
var random = new Random(1942);
for (int i = 0; i < 10000; i++) {
    float x = (float)(random.NextDouble() * 160 - 80), z = (float)(random.NextDouble() * 160 - 80);
    float angle = (float)(random.NextDouble() * Math.PI * 2), target = (float)(random.NextDouble() * 300 - 150);
    float budget = MathF.Sqrt(x*x + z*z);
    var shifted = AirShiftMath.Hold(x, z, MathF.Cos(angle), MathF.Sin(angle), target, budget, true);
    Check(Math.Sqrt(shifted.X*shifted.X + shifted.Z*shifted.Z) <= budget + .00003, "enabled speed budget is respected");
    var unlimited = AirShiftMath.Hold(x, z, 1, 0, target, 0, false);
    Check(Math.Abs(unlimited.X-target) < .00003 && unlimited.Z == z, "unlimited shift preserves forward speed and has no lateral ceiling");
}
Check(AirShiftMath.Hold(0, 30, 1, 0, 1000, 30, false) == (1000, 30), "values far above the former 15 limit work");
Check(AirShiftMath.Hold(0, 0, 1, 0, 18, 0, false) == (18, 0), "unrestricted shifting works from rest");
Check(AirShiftMath.Hold(0, 30, 0, 0, 18, 30, false) == (0, 30), "a vertical right vector produces no horizontal displacement");
float lateral = 0, displacement = 0;
for (int i = 0; i < 25; i++) {
    lateral *= .1f; // Representative strong native air damping before our final-controller postfix.
    lateral = AirShiftMath.Hold(lateral, 30, 1, 0, 18, 30, false).X;
    displacement += lateral * .01f;
}
Check(Math.Abs(displacement-4.5f) < .0001f, "maintained velocity produces a visible 4.5m step over .25 seconds despite damping");

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

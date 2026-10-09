using System;
using TK2.Customization;

int assertions = 0;
void Check(bool ok, string name) { assertions++; if (!ok) throw new Exception(name); }

var live = InputOverlayModel.CreateLive(1.6f, -.4f, true, false, true, false, true, false, true, false, true, "KEYBOARD");
Check(live.Steering == 1 && live.Acceleration == -.4f, "live analog values are bounded and retained");
Check(live.Jump && live.Boost && live.Brake && live.TargetUp && live.Pickup, "live semantic buttons are preserved");
Check(!live.Drift && live.Boost, "Boost remains distinct from the game's remapped Drift gameplay field");
Check(!live.IsReplay && live.Device == "KEYBOARD", "live source and detected device are retained");
var replay = InputOverlayModel.CreateReplay(float.NaN, float.PositiveInfinity, false, true, false, true, false, true, false, true, false);
Check(replay.Steering == 0 && replay.Acceleration == 0, "non-finite axes are sanitized");
Check(replay.IsReplay && replay.Device == "RECORDED", "replay source is clearly distinguished from live hardware");
Check(replay.Drift && replay.Weapon && replay.LookBack && replay.TargetDown, "recorded ghost actions are exposed");
Check(InputOverlayModel.ClampAxis(-2) == -1 && InputOverlayModel.ClampAxis(.3f) == .3f, "axis boundaries remain normalized");
Console.WriteLine($"Gamepad overlay model: {assertions} assertions passed.");

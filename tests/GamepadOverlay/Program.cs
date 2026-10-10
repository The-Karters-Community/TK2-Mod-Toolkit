using System;
using TK2.Customization;

int assertions = 0;
void Check(bool ok, string name) { assertions++; if (!ok) throw new Exception(name); }

var live = InputOverlayModel.CreateLive(1.6f, .4f, true, true, true, true, true, true, false, true, true, true, "KEYBOARD");
Check(live.Steering == 1 && live.Acceleration == .4f, "live analog values are bounded and retained");
Check(live.JumpDrift && live.Boost && live.Weapon && live.Brake && live.TargetUp && live.Pickup, "live semantic buttons are preserved");
Check(live.Accelerate && !live.Reverse && live.Menu && live.PlayerList, "digital drive and menu actions are represented as held states");
Check(!live.IsReplay && live.Device == "KEYBOARD", "live source and detected device are retained");
var replay = InputOverlayModel.CreateReplay(float.NaN, float.PositiveInfinity, false, true, false, true, false, true, false, true, false);
Check(replay.Steering == 0 && replay.Acceleration == 0, "non-finite axes are sanitized");
Check(replay.IsReplay && replay.Device == "RECORDED", "replay source is clearly distinguished from live hardware");
Check(replay.JumpDrift && replay.Weapon && replay.LookBack && replay.TargetDown, "the recorded Jump/Drift combo and other ghost actions are exposed");
var reverse = InputOverlayModel.CreateLive(0, -.7f, false, false, false, false, false, false, false, false, false, false, "GAMEPAD");
Check(reverse.Reverse && !reverse.Accelerate, "reverse is shown as a held digital action instead of a saturated gauge");
Check(InputOverlayModel.ClampAxis(-2) == -1 && InputOverlayModel.ClampAxis(.3f) == .3f, "axis boundaries remain normalized");
Console.WriteLine($"Gamepad overlay model: {assertions} assertions passed.");

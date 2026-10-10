using System;
using TK2.Customization;

int assertions = 0;
void Check(bool ok, string name) { assertions++; if (!ok) throw new Exception(name); }

var live = InputOverlayModel.CreateLive(1.6f, .4f, true, true, true, true, "KEYBOARD");
Check(live.Steering == 1 && live.Acceleration == .4f, "live analog values are bounded and retained");
Check(live.JumpDrift && live.Boost && live.Weapon && live.Brake, "live viewer keeps only driving actions and Use Weapon");
Check(live.Accelerate && !live.Reverse, "digital drive actions are represented as held states");
Check(!live.IsReplay && live.Device == "KEYBOARD", "live source and detected device are retained");
var replay = InputOverlayModel.CreateReplay(float.NaN, float.PositiveInfinity, true, false, false);
Check(replay.Steering == 0 && replay.Acceleration == 0, "non-finite axes are sanitized");
Check(replay.IsReplay && replay.Device == "RECORDED", "replay source is clearly distinguished from live hardware");
Check(replay.JumpDrift && !replay.Weapon && replay.Boost == false, "ghost viewer omits live-only actions");
var braking = InputOverlayModel.CreateReplay(0, 1, false, true, true);
Check(braking.Accelerate && braking.Boost && braking.Brake && !braking.Weapon, "ghost viewer exposes only driving and braking actions");
var reverse = InputOverlayModel.CreateLive(0, -.7f, false, false, false, false, "GAMEPAD");
Check(reverse.Reverse && !reverse.Accelerate, "reverse is shown as a held digital action instead of a saturated gauge");
Check(InputOverlayModel.ClampAxis(-2) == -1 && InputOverlayModel.ClampAxis(.3f) == .3f, "axis boundaries remain normalized");
var topCenter = InputOverlayPlacement.Resolve("TopCenter", 1920, 1080, 510, 244, 18, 50, 50);
Check(topCenter.X == 705 && topCenter.Y == 18, "TopCenter is horizontally centered at the top margin");
var middleCenter = InputOverlayPlacement.Resolve("MiddleCenter", 1920, 1080, 510, 244, 18, 50, 50);
var middleRight = InputOverlayPlacement.Resolve("MiddleRight", 1920, 1080, 510, 244, 18, 50, 50);
Check(middleCenter.X == 705 && middleCenter.Y == 418 && middleRight.X == 1392 && middleRight.Y == 418, "middle presets center against screen and card dimensions");
var custom = InputOverlayPlacement.Resolve("Custom", 1920, 1080, 510, 244, 18, 100, 0);
Check(custom.X == 1392 && custom.Y == 18, "custom percentage sliders map to clamped screen edges");
Console.WriteLine($"Gamepad overlay model: {assertions} assertions passed.");

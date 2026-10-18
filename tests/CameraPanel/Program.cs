using System;
using TK2.Customization;
using UnityEngine;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Draw(Plugin plugin) { GUI.Labels.Clear(); CameraPanel.Draw(plugin); }
var p = new Plugin();
CameraFeature.RaceAvailable = true;
Input.Down = KeyCode.F8;
CameraPanel.Tick(p); Draw(p);
Check(GUI.Labels.Contains("Camera"), "editor opens while camera overrides are off");
StudioBehaviour.Clicked = "Camera overrides";
Draw(p); CameraPanel.Tick(p); Draw(p);
Check(p.CameraEnabled.Value && GUI.Labels.Contains("Camera"), "enabling overrides keeps editor open");
StudioBehaviour.Clicked = "Camera overrides";
Draw(p); CameraPanel.Tick(p); Draw(p);
Check(!p.CameraEnabled.Value && GUI.Labels.Contains("Camera"), "disabling overrides keeps editor open");
p.Fov.Value = 90; p.CameraHeight.Value = 4;
GUI.Clicked = "Reset Field of view"; Draw(p);
Check(p.Fov.Value == 65 && p.CameraHeight.Value == 4, "individual numeric reset preserves other adjustments");
p.AimAtKart.Value = true;
GUI.Clicked = "Reset Aim toward kart"; Draw(p);
Check(!p.AimAtKart.Value && p.CameraHeight.Value == 4, "individual toggle reset preserves geometry");
GUI.Clicked = "Aim, rotation & smoothing…"; Draw(p); Draw(p);
Check(GUI.Labels.Contains("Smoothing"), "advanced adjustments are reachable");
p.CameraYaw.Value = 42; p.CameraHeight.Value = 4;
GUI.Clicked = "Reset Yaw"; Draw(p);
Check(p.CameraYaw.Value == 0 && p.CameraHeight.Value == 4, "advanced reset changes only its own value");
Screen.height = 360; Draw(p);
Check(GUI.ScrollHeight > 0 && GUI.ContentHeight > GUI.ScrollHeight && GUI.Labels.Contains("Saved"), "small-screen body scrolls and keeps save status visible");
Input.Down = KeyCode.Escape; CameraPanel.Tick(p); Draw(p);
Check(!GUI.Labels.Contains("Camera") && LiveConfig.Flushes == 1, "Escape closes and flushes once");
Input.Down = KeyCode.F8; CameraPanel.Tick(p);
CameraFeature.RaceAvailable = false; CameraPanel.Tick(p); Draw(p);
Check(!GUI.Labels.Contains("Camera") && LiveConfig.Flushes == 2, "leaving the racing camera closes and flushes");
Console.WriteLine($"Camera panel: {assertions} behavior assertions passed (headless UI harness).");

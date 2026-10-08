using System;
using System.IO;
using BepInEx.Configuration;
using TK2.Customization;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
string folder = Path.Combine(Path.GetTempPath(), "tk2-live-config-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    string path = Path.Combine(folder, "local.tk2.customization.cfg");
    var p = new Plugin { Config = new ConfigFile(path, false) { SaveOnConfigSet = false } };
    var enabled = p.Config.Bind("Camera", "Enabled", false);
    var fov = p.Config.Bind("Camera", "FieldOfView", 65f);
    var audio = p.Config.Bind("Audio", "MasterVolume", 1f);
    p.Config.Save(); LiveConfig.Initialize(p);
    DateTime time = File.GetLastWriteTimeUtc(path);
    File.WriteAllText(path, File.ReadAllText(path).Replace("FieldOfView = 65", "FieldOfView = 82"));
    File.SetLastWriteTimeUtc(path, time);
    Check(LiveConfig.Tick(p, 1) && fov.Value == 82, "hot reload catches content changes even with identical file time");
    LiveConfig.Change(p, fov, 90f, 1.1f);
    Check(fov.Value == 90 && File.ReadAllText(path).Contains("FieldOfView = 82"), "immediate preview without writing on every slider event");
    Check(!LiveConfig.Tick(p, 1.2f), "save waits for adjustment debounce");
    File.WriteAllText(path, File.ReadAllText(path).Replace("MasterVolume = 1", "MasterVolume = 0.6"));
    Check(LiveConfig.Tick(p, 1.5f) && fov.Value == 90 && audio.Value == .6f, "panel save merges external audio change and reloads both");
    Check(!enabled.Value, "adjustments do not silently enable camera");
    LiveConfig.Change(p, fov, 100f, 2);
    File.WriteAllText(path, File.ReadAllText(path).Replace("FieldOfView = 90", "FieldOfView = 110"));
    Check(LiveConfig.Tick(p, 2.4f) && fov.Value == 110, "same-key external edit survives conflicting stale preview");
    Check(LiveConfig.Status.Contains("Camera/FieldOfView") && p.Log.Warnings == 1, "conflict is visible once with its key");
    LiveConfig.Change(p, fov, 95f, 3);
    Check(LiveConfig.Flush(p) && File.ReadAllText(path).Contains("FieldOfView = 95"), "closing panel flushes pending change");
    Check(!LiveConfig.Tick(p, 4), "own saved content does not cause another reload");
    Check(Directory.GetFiles(folder).Length == 1, "config transaction leaves no temp files or plugin DLLs");
}
finally { Directory.Delete(folder, true); }
Console.WriteLine($"Actual BepInEx live config: {assertions} assertions passed.");

namespace TK2.Customization
{
    // Exercise the real installed ConfigFile with a minimal host; no game/plugin execution.
    internal sealed class Plugin
    {
        internal ConfigFile Config = null!;
        internal TestLog Log = new();
    }
    internal sealed class TestLog { internal int Warnings; internal void LogWarning(object message) { Warnings++; } }
}

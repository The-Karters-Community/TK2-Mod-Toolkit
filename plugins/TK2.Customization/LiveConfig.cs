using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;

namespace TK2.Customization;

// All calls run on Unity's main thread. SHA detects edits even when file times coincide.
internal static class LiveConfig
{
    private static readonly Dictionary<string, (string? Baseline, string Value)> Pending = new();
    private static string _hash = "";
    private static float _nextPoll, _saveAt;
    internal static string Status = "Settings apply live. Changes save automatically.";

    internal static void Initialize(Plugin p)
    { _hash = Hash(File.ReadAllBytes(p.Config.ConfigFilePath)); Pending.Clear(); _nextPoll = _saveAt = 0; }
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    internal static void Change<T>(Plugin p, ConfigEntry<T> entry, T value, float now)
    {
        if (EqualityComparer<T>.Default.Equals(entry.Value, value)) return;
        string key = entry.Definition.Section + "/" + entry.Definition.Key;
        if (!Pending.TryGetValue(key, out var previous))
        {
            var current = ConfigDocument.Read(File.ReadAllText(p.Config.ConfigFilePath));
            current.TryGetValue(key, out string? baseline);
            previous = (baseline, "");
        }
        entry.Value = value;
        Pending[key] = (previous.Baseline, entry.GetSerializedValue());
        _saveAt = now + .35f;
        Status = "Preview live · saving when you finish adjusting…";
    }

    internal static bool Tick(Plugin p, float now)
    {
        if (Pending.Count > 0)
        {
            if (now < _saveAt) return false;
            _saveAt = now + 1; // Back off if the file is temporarily locked or denied.
            return Flush(p);
        }
        if (now < _nextPoll) return false;
        _nextPoll = now + .5f;
        byte[] data = File.ReadAllBytes(p.Config.ConfigFilePath);
        string hash = Hash(data);
        if (hash == _hash) return false;
        p.Config.Reload();
        _hash = hash;
        Status = "Toolkit settings reloaded live.";
        return true;
    }

    internal static bool Flush(Plugin p)
    {
        if (Pending.Count == 0) return false;
        string path = p.Config.ConfigFilePath;
        string temporary = path + ".toolkit-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                byte[] before = File.ReadAllBytes(path);
                string text = Encoding.UTF8.GetString(before).TrimStart('\uFEFF');
                byte[] after = new UTF8Encoding(false).GetBytes(ConfigDocument.Merge(text, Pending));
                File.WriteAllBytes(temporary, after);
                if (Hash(File.ReadAllBytes(path)) != Hash(before)) continue;
                File.Move(temporary, path, true);
                Pending.Clear();
                p.Config.Reload();
                _hash = Hash(after);
                Status = "Toolkit settings saved to config · applied live.";
                return true;
            }
            throw new IOException("Config changed during saving; finish editing in one window first.");
        }
        catch (InvalidOperationException ex)
        {
            // A competing edit wins over stale preview: reload, report the key, never overwrite it.
            Pending.Clear(); p.Config.Reload(); _hash = Hash(File.ReadAllBytes(path));
            Status = ex.Message + ". Current file values reloaded.";
            p.Log.LogWarning(Status);
            return true;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

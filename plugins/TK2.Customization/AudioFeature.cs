using System;
using System.Collections.Generic;

namespace TK2.Customization;

// Capture the game's requested Wwise levels, then apply a multiplier.
// Calls into Unity/game objects only occur in hooks or the main-thread behaviour.
internal static class AudioFeature
{
    private static readonly Dictionary<string, int> Levels = new();
    private static PTK_AudioListenerManager? _manager;
    private static bool _reapplying;
    private static bool _previousEnabled;
    private static float _previousMultiplier = 1f;

    internal static void BeforeSetVolume(PTK_AudioListenerManager manager, string rtpc, ref int value, bool enabled, float multiplier)
    {
        if (_reapplying) return;
        if (_manager != manager) { Levels.Clear(); _manager = manager; }
        Levels[rtpc] = value;
        if (enabled) value = (int)Math.Round(value * multiplier);
    }

    internal static void Tick(bool enabled, float multiplier)
    {
        if (_previousEnabled == enabled && Math.Abs(_previousMultiplier - multiplier) < 0.0001f) return;
        Reapply(enabled, multiplier);
        _previousEnabled = enabled;
        _previousMultiplier = multiplier;
    }

    private static void Reapply(bool enabled, float multiplier)
    {
        if (_manager == null) return;
        _reapplying = true;
        try
        {
            foreach (var pair in Levels)
                _manager.SetVolume(pair.Key, enabled ? (int)Math.Round(pair.Value * multiplier) : pair.Value);
        }
        finally { _reapplying = false; }
    }

    internal static void Restore() { Reapply(false, 1f); _previousEnabled = false; }
}

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
    private static (bool Enabled, float Master, float Music, float Effects, float Voice, float Interface) _previousMix;

    // These exact RTPC strings occur in the current build's stringliteral.json.
    internal static float Multiplier(Plugin p, string rtpc) => rtpc switch {
        "Master_Volume_RTPC" => p.Volume.Value,
        "Music_Volume_RTPC" => p.MusicVolume.Value,
        "SFX_Volume_RTPC" => p.SfxVolume.Value,
        "VO_Volume_RTPC" => p.VoiceVolume.Value,
        "UI_Volume_RTPC" => p.UiVolume.Value,
        _ => 1f
    };

    internal static void BeforeSetVolume(PTK_AudioListenerManager manager, string rtpc, ref int value, bool enabled, float multiplier)
    {
        if (_reapplying) return;
        if (_manager != manager) { Levels.Clear(); _manager = manager; }
        Levels[rtpc] = value;
        if (enabled) {
            if (!float.IsFinite(multiplier)) throw new ArgumentOutOfRangeException("Audio multiplier must be finite.");
            value = (int)Math.Round(value * multiplier);
        }
    }

    internal static void Tick(Plugin p)
    {
        var mix = (p.AudioEnabled.Value, p.Volume.Value, p.MusicVolume.Value, p.SfxVolume.Value, p.VoiceVolume.Value, p.UiVolume.Value);
        if (_previousMix.Equals(mix)) return;
        Reapply(p.AudioEnabled.Value);
        _previousMix = mix;
    }

    private static void Reapply(bool enabled)
    {
        if (_manager == null) return;
        _reapplying = true;
        try
        {
            foreach (var pair in Levels)
            {
                float multiplier = enabled ? Multiplier(Plugin.Instance!, pair.Key) : 1;
                if (!float.IsFinite(multiplier)) throw new ArgumentOutOfRangeException("Audio multiplier must be finite.");
                _manager.SetVolume(pair.Key, (int)Math.Round(pair.Value * multiplier));
            }
        }
        finally { _reapplying = false; }
    }

    internal static void Restore() { Reapply(false); _previousMix = default; }
}

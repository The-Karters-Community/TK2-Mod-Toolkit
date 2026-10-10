using System;
using System.Collections.Generic;
using System.Linq;

namespace TK2.Customization;

internal static class OnlineProtectionPolicy
{
    private const string ProtectionSection = "OnlineProtection";
    private const string PluginGuid = "local.tk2.customization";
    // These modules are explicitly approved for online use. The gamepad viewer
    // can expose recorded opponent controls but does not modify gameplay.
    // Keep this explicit: unknown module IDs remain protected.
    private static readonly HashSet<string> WhitelistedModules = new(StringComparer.Ordinal)
    {
        "UI",
        "HudOpacity",
        "DisableVignette",
        "Audio",
        "Camera",
        "BobbyGang",
        "GamepadOverlay",
        "PerformanceDiagnostics",
    };

    internal static bool IsWhitelistedModule(string section) => WhitelistedModules.Contains(section);

    internal static bool RequiresBlocking(
        IEnumerable<(string Section, string Key, bool Enabled)> settings,
        IEnumerable<string> pluginGuids)
    {
        var snapshot = settings.ToArray();
        foreach (var setting in snapshot)
        {
            if (!setting.Enabled || !string.Equals(setting.Key, "Enabled", StringComparison.Ordinal)) continue;
            if (string.Equals(setting.Section, ProtectionSection, StringComparison.Ordinal) || IsWhitelistedModule(setting.Section)) continue;
            return true;
        }

        foreach (string guid in pluginGuids)
            if (!string.Equals(guid, PluginGuid, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

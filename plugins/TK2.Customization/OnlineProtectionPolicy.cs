using System;
using System.Collections.Generic;

namespace TK2.Customization;

internal static class OnlineProtectionPolicy
{
    private const string ProtectionSection = "OnlineProtection";
    private const string PluginGuid = "local.tk2.customization";
    // These modules change only local presentation, sound mix, camera framing,
    // or measurement. Keep this explicit: module IDs with unknown behavior
    // must continue to require online protection.
    private static readonly HashSet<string> WhitelistedModules = new(StringComparer.Ordinal)
    {
        "UI",
        "HudOpacity",
        "DisableVignette",
        "Audio",
        "Camera",
        "BobbyGang",
        "PerformanceDiagnostics",
    };

    internal static bool IsWhitelistedModule(string section) => WhitelistedModules.Contains(section);

    internal static bool RequiresBlocking(
        IEnumerable<(string Section, string Key, bool Enabled)> settings,
        IEnumerable<string> pluginGuids)
    {
        foreach (var setting in settings)
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

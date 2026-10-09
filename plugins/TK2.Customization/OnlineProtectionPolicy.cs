using System;
using System.Collections.Generic;

namespace TK2.Customization;

internal static class OnlineProtectionPolicy
{
    private const string ProtectionSection = "OnlineProtection";
    private const string PluginGuid = "local.tk2.customization";

    internal static bool IsWhitelistedModule(string section) =>
        string.Equals(section, "UI", StringComparison.Ordinal) ||
        string.Equals(section, "HudOpacity", StringComparison.Ordinal);

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

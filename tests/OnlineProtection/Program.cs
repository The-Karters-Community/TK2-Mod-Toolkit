using System;
using TK2.Customization;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
var noPlugins = Array.Empty<string>();
Check(OnlineProtectionPolicy.IsWhitelistedModule("UI"), "HUD size is allowlisted");
Check(OnlineProtectionPolicy.IsWhitelistedModule("HudOpacity"), "HUD opacity is allowlisted");
Check(!OnlineProtectionPolicy.IsWhitelistedModule("Camera"), "camera changes are not allowlisted");
Check(!OnlineProtectionPolicy.IsWhitelistedModule("Physics"), "physics changes are not allowlisted");
Check(!OnlineProtectionPolicy.IsWhitelistedModule("DashAndStash"), "item-changing modules are not allowlisted");
Check(!OnlineProtectionPolicy.RequiresBlocking(new[] { ("UI", "Enabled", true), ("HudOpacity", "Enabled", true) }, noPlugins), "only allowlisted modules are active");
Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("UI", "Enabled", true), ("Camera", "Enabled", true) }, noPlugins), "camera module requires protection");
Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("Physics", "Enabled", true) }, noPlugins), "physics module requires protection");
Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("DashAndStash", "Enabled", true) }, noPlugins), "item-grant module requires protection");
Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("Recipe.MyMod", "Enabled", true) }, noPlugins), "enabled recipes are not implicitly allowlisted");
Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("MirrorRace", "Enabled", true) }, noPlugins), "gameplay-affecting camera/mirror modules are not allowlisted");
Check(!OnlineProtectionPolicy.RequiresBlocking(new[] { ("OnlineProtection", "Enabled", true), ("OnlineProtection", "BlockLeaderboardUploads", true) }, noPlugins), "protection settings do not count as an unapproved mod");
Check(!OnlineProtectionPolicy.RequiresBlocking(new[] { ("Physics", "Enabled", false) }, noPlugins), "disabled modules do not require blocking");
Check(!OnlineProtectionPolicy.RequiresBlocking(new[] { ("Physics", "SpeedMultiplier", true) }, noPlugins), "non-Enabled setting keys do not count as active modules");
Check(!OnlineProtectionPolicy.RequiresBlocking(Array.Empty<(string, string, bool)>(), new[] { "local.tk2.customization" }), "the toolkit plugin does not mark itself unapproved");
Check(OnlineProtectionPolicy.RequiresBlocking(Array.Empty<(string, string, bool)>(), new[] { "third.party.plugin" }), "unknown loaded plugins are blocked");
Console.WriteLine($"Online-protection policy: {assertions} assertions passed.");

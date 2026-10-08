using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TK2.Customization;

int checks = 0;
void Check(bool result, string name) { checks++; if (!result) throw new Exception(name); }
CommunityCommand Parse(string text)
{
    Check(CommunityCommandParser.TryParse(text, "#tk2", 999, 120, out var value), "Accept " + text);
    return value;
}
var hp = Parse("#tk2 hp gain");
Check(hp.Kind == CommunityCommandKind.HpGain && hp.Amount == 20, "Legacy default healing");
Check(Parse("#tk2 hp lose 20").Kind == CommunityCommandKind.HpLose, "HP loss");
Check(Parse("#tk2 hp gain 5000").Amount == 999, "Health amount limit");
Check(Parse("#TK2  RESERVE   gain 2.5").Kind == CommunityCommandKind.ReserveGain, "Whitespace and case");
Check(Parse("#tk2 reserve gain 2.5").Amount == 2.5f, "Reserve fractional seconds");
Check(Parse("#tk2 reserve lose 2").Kind == CommunityCommandKind.ReserveLose, "Reserve loss");
Check(Parse("#tk2 reserve set 999").Amount == 120, "Reserve duration limit");
Check(Parse("#tk2 kill humans").Kind == CommunityCommandKind.KillHumans, "All local humans");
Check(Parse("#tk2 kill ais").Kind == CommunityCommandKind.KillAi, "All local AIs");
Check(Parse("#tk2 kill pos 1").Amount == 1, "Position is one based");
Check(Parse("#tk2 kill karter Bob").Name == "Bob", "Named karter");
foreach (string bad in new[] { "", "#tk2", "#tk2 kill", "#tk2 kill pos", "#tk2 kill pos 0", "#tk2 kill pos -1", "#tk2 kill pos 2147483648", "#tk2 hp gain -1", "#tk2 hp gain NaN", "#tk2 hp gain Infinity", "#tk2 hp gain 1.5", "#tk2 hp gain 20 extra", "#tk2 reserve set", "#tk2 reserve set -20", "#tk2 reserve bad 20", "#other hp gain 20", "#tk2 hp set 20" })
    Check(!CommunityCommandParser.TryParse(bad, "#tk2", 999, 120, out _), "Reject " + bad);
Check(CommunityCommandParser.AllowedUser("broadcaster", "", "broadcaster"), "Empty allowlist permits broadcaster");
Check(!CommunityCommandParser.AllowedUser("viewer", "", "broadcaster"), "Empty allowlist excludes viewer");
Check(!CommunityCommandParser.AllowedUser("", "", ""), "Blank identity denied");
Check(CommunityCommandParser.AllowedUser("MOD", "owner, mod", "broadcaster"), "Case-insensitive allowlist");
Check(CommunityCommandParser.AllowedUser("viewer", " * ", "broadcaster"), "Explicit all-viewer choice");
using var catalogue = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "community_catalog.json")));
var settings = new Dictionary<string, JsonElement>();
foreach (var setting in catalogue.RootElement[0].GetProperty("settings").EnumerateArray()) settings.Add(setting[0].GetString()!, setting);
foreach (string text in new[] { "#tk2 hp gain 20", "#tk2 hp lose 20", "#tk2 reserve gain 20", "#tk2 reserve lose 20", "#tk2 reserve set 20", "#tk2 kill humans" })
{
    var command = Parse(text);
    string setting = CommunityCommandParser.EnabledSetting(command.Kind);
    Check(settings.ContainsKey(setting), "Local command maps to displayed setting " + setting);
    Check(settings[setting][2].GetString() == "bool" && !settings[setting][3].GetBoolean(), "Local command starts disabled " + setting);
}
Console.WriteLine($"Community commands: {checks} assertions passed.");

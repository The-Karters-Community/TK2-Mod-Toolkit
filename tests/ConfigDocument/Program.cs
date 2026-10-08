using System;
using System.Collections.Generic;
using TK2.Customization;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
var change = new Dictionary<string, (string? Baseline, string Value)> { ["Camera/FieldOfView"] = ("65", "80") };
string original = "# Keep metadata\r\n[Camera]\r\nFieldOfView = 65.0\r\nEnabled = true\r\n[Audio]\r\nMasterVolume = 0.4\r\nUnknown = untouched\r\n";
string external = original.Replace("MasterVolume = 0.4", "MasterVolume = 0.7");
string merged = ConfigDocument.Merge(external, change);
Check(merged.Contains("FieldOfView = 80"), "camera edit applies");
Check(merged.Contains("MasterVolume = 0.7"), "simultaneous Toolkit edit in another key survives");
Check(merged.Contains("# Keep metadata\r\n") && merged.Contains("Unknown = untouched\r\n"), "preserve comments, unknown values and CRLF");
Check(ConfigDocument.Read(merged)["Camera/Enabled"] == "true", "camera adjustment must not change module toggle");
Check(ConfigDocument.Merge(merged, change) == merged, "identical concurrent desired value is idempotent");
try { ConfigDocument.Merge(original.Replace("65.0", "90"), change); throw new Exception("conflict was swallowed"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("Camera/FieldOfView"), "same-key conflict reports key"); }
var missing = new Dictionary<string, (string? Baseline, string Value)> { ["Camera/HeightOffset"] = (null, "1.5"), ["Recipe.New/Enabled"] = (null, "false") };
string inserted = ConfigDocument.Merge(original, missing);
var values = ConfigDocument.Read(inserted);
Check(values["Camera/HeightOffset"] == "1.5" && !values.ContainsKey("Audio/HeightOffset"), "insert new camera key into correct section");
Check(values["Recipe.New/Enabled"] == "false", "append missing section");
Check(ConfigDocument.Read(";FieldOfView = 99\n[Camera]\n#FieldOfView = 100\nFieldOfView = 65\n")["Camera/FieldOfView"] == "65", "commented assignments are not values");
Check(ConfigDocument.Equivalent("True", "true") && ConfigDocument.Equivalent("1.000", "1"), "BepInEx formatting rewrites merge semantically");
Check(!ConfigDocument.Equivalent(null, "0") && !ConfigDocument.Equivalent("F8", "C"), "missing/hotkey changes remain real conflicts");
Check(ConfigDocument.Merge(original.Replace("\r\n", "\n"), change).Contains("FieldOfView = 80\n"), "preserve LF");
Console.WriteLine($"Live config transactions: {assertions} assertions passed.");

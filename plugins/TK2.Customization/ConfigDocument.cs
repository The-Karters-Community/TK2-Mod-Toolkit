using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TK2.Customization;

// Config-only transactions shared by the in-race editor. Never writes a plugin.
public static class ConfigDocument
{
    private static readonly Regex Entry = new(@"^(\s*([^=#;]+?)\s*=\s*)(.*)$");

    public static Dictionary<string, string> Read(string text)
    {
        var values = new Dictionary<string, string>();
        string section = "";
        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) section = trimmed[1..^1];
            else if (!trimmed.StartsWith("#") && !trimmed.StartsWith(";"))
            {
                var match = Entry.Match(line);
                if (match.Success) values[section + "/" + match.Groups[2].Value.Trim()] = match.Groups[3].Value.Trim();
            }
        }
        return values;
    }

    public static bool Equivalent(string? a, string? b)
    {
        if (a == null || b == null) return a == b;
        if (bool.TryParse(a, out bool ba) && bool.TryParse(b, out bool bb)) return ba == bb;
        if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double na) &&
            double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double nb)) return na == nb;
        return a.Trim() == b.Trim();
    }

    public static string Merge(string text, IReadOnlyDictionary<string, (string? Baseline, string Value)> edits)
    {
        var values = Read(text);
        foreach (var edit in edits)
        {
            values.TryGetValue(edit.Key, out string? current);
            if (!Equivalent(current, edit.Value.Baseline) && !Equivalent(current, edit.Value.Value))
                throw new InvalidOperationException("Setting also changed in Toolkit: " + edit.Key);
        }
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
        var remaining = new Dictionary<string, string>();
        foreach (var edit in edits) remaining[edit.Key] = edit.Value.Value;
        string section = "";
        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) { section = trimmed[1..^1]; continue; }
            if (trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
            var match = Entry.Match(lines[i]);
            string key = section + "/" + match.Groups[2].Value.Trim();
            if (match.Success && edits.TryGetValue(key, out var edit))
            { lines[i] = match.Groups[1].Value + edit.Value; remaining.Remove(key); }
        }
        // Insert missing keys inside their own section, never under the last section by accident.
        foreach (var edit in remaining)
        {
            int slash = edit.Key.IndexOf('/');
            string wanted = edit.Key[..slash], key = edit.Key[(slash + 1)..];
            int start = lines.FindIndex(line => line.Trim() == "[" + wanted + "]");
            if (start < 0) { lines.Add("[" + wanted + "]"); lines.Add(key + " = " + edit.Value); }
            else
            {
                int end = start + 1;
                while (end < lines.Count && !lines[end].TrimStart().StartsWith("[")) end++;
                lines.Insert(end, key + " = " + edit.Value);
            }
        }
        return string.Join(newline, lines);
    }
}

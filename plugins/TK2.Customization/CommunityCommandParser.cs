using System;
using System.Globalization;

namespace TK2.Customization;

// Readable replacement for TwitchCathretCommands' six command recognizers.
// Parsing is independent of Unity, networking, and credentials.
public enum CommunityCommandKind { HpGain, HpLose, ReserveGain, ReserveLose, ReserveSet, KillHumans, KillAi, KillPosition, KillName }
public readonly record struct CommunityCommand(CommunityCommandKind Kind, float Amount, string Name);

public static class CommunityCommandParser
{
    public static string EnabledSetting(CommunityCommandKind kind) => kind switch
    {
        CommunityCommandKind.HpGain => "HealthGain",
        CommunityCommandKind.HpLose => "HealthLose",
        CommunityCommandKind.ReserveGain => "ReserveGain",
        CommunityCommandKind.ReserveLose => "ReserveLose",
        CommunityCommandKind.ReserveSet => "ReserveSet",
        CommunityCommandKind.KillHumans or CommunityCommandKind.KillAi or CommunityCommandKind.KillPosition or CommunityCommandKind.KillName => "Kill",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool AllowedUser(string user, string allowedUsers, string channel)
    {
        if (allowedUsers.Trim() == "*") return true;
        string users = string.IsNullOrWhiteSpace(allowedUsers) ? channel : allowedUsers;
        foreach (string allowed in users.Split(','))
            if (!string.IsNullOrWhiteSpace(allowed) && allowed.Trim().Equals(user, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool TryParse(string message, string prefix, int healthLimit, float reserveLimit, out CommunityCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(prefix)) return false;
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts.Length > 4 || !parts[0].Equals(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string operation = parts[1].ToLowerInvariant(), action = parts[2].ToLowerInvariant();
        if (operation == "kill")
        {
            if (parts.Length == 3 && action is "humans" or "ais")
                command = new(action == "humans" ? CommunityCommandKind.KillHumans : CommunityCommandKind.KillAi, 0, "");
            else if (parts.Length == 4 && action == "pos" && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int position) && position > 0)
                command = new(CommunityCommandKind.KillPosition, position, "");
            else if (parts.Length == 4 && action == "karter" && !string.IsNullOrWhiteSpace(parts[3]))
                command = new(CommunityCommandKind.KillName, 0, parts[3]);
            else return false;
            return true;
        }
        if (operation != "hp" && operation != "reserve") return false;
        if (action != "gain" && action != "lose" && !(operation == "reserve" && action == "set")) return false;
        if (action == "set" && parts.Length != 4) return false;
        float amount = 20;
        if (parts.Length == 4 && (!float.TryParse(parts[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount) || !float.IsFinite(amount) || amount < 0)) return false;
        if (operation == "hp" && amount != MathF.Floor(amount)) return false;
        amount = Math.Clamp(amount, 0, Math.Max(0, operation == "hp" ? healthLimit : reserveLimit));
        CommunityCommandKind kind = operation == "hp"
            ? (action == "gain" ? CommunityCommandKind.HpGain : CommunityCommandKind.HpLose)
            : action == "gain" ? CommunityCommandKind.ReserveGain : action == "lose" ? CommunityCommandKind.ReserveLose : CommunityCommandKind.ReserveSet;
        command = new(kind, amount, "");
        return true;
    }
}

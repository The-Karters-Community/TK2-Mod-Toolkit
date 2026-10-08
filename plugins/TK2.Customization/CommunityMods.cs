using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Fresh port of TheKarters2Mods Twitch integration/basic commands/Cathret commands.
// Game calls are made only by Tick on the Unity thread. Network workers receive chat,
// and never send channel messages or log credentials.
public static class CommunityMods
{
    private static Plugin? _plugin;
    private static ConfigEntry<bool> _enabled = null!, _connect = null!, _hpGain = null!, _hpLose = null!, _kill = null!, _reserveGain = null!, _reserveLose = null!, _reserveSet = null!;
    private static ConfigEntry<string> _channel = null!, _username = null!, _tokenVariable = null!, _users = null!, _prefix = null!;
    private static ConfigEntry<float> _cooldown = null!, _reserveLimit = null!;
    private static ConfigEntry<int> _healthLimit = null!;
    private static readonly ConcurrentQueue<(string User, string Message)> Messages = new();
    private static readonly ConcurrentQueue<string> Status = new();
    private static CancellationTokenSource? _cancel;
    private static TcpClient? _client;
    private static string _connectionKey = "";
    private static float _lastInteraction = float.NegativeInfinity;
    private static Task? _worker;

    internal static void Install(Plugin plugin)
    {
        Disconnect();
        _connectionKey = "";
        _lastInteraction = float.NegativeInfinity;
        _plugin = plugin;
        var cfg = plugin.Config;
        _enabled = cfg.Bind("CommunityCommands", "Enabled", false, "Offline chat commands for health, reserves, and elimination. Default off.");
        _connect = cfg.Bind("CommunityCommands", "ConnectToTwitch", false, "Explicitly connect to Twitch over TLS and receive chat. No chat replies are sent.");
        _channel = cfg.Bind("CommunityCommands", "Channel", "", "Twitch channel login, without #.");
        _username = cfg.Bind("CommunityCommands", "UserName", "", "Login that owns the Twitch chat-read access token.");
        _tokenVariable = cfg.Bind("CommunityCommands", "TokenEnvironmentVariable", "TK2_TWITCH_TOKEN", "Read OAuth token from this environment variable. Tokens are never written to config or logs.");
        _users = cfg.Bind("CommunityCommands", "AllowedUsers", "", "Comma-separated permitted Twitch logins. Empty: channel owner only. *: all viewers.");
        _prefix = cfg.Bind("CommunityCommands", "CommandPrefix", "#tk2", "Command prefix. Example: #tk2 hp gain 20.");
        _cooldown = cfg.Bind("CommunityCommands", "CooldownSeconds", 5f, new ConfigDescription("Minimum seconds between successful commands.", new AcceptableValueRange<float>(0, 300)));
        _healthLimit = cfg.Bind("CommunityCommands", "HealthAmountLimit", 999, new ConfigDescription("Maximum HP amount per command.", new AcceptableValueRange<int>(0, 10000)));
        _reserveLimit = cfg.Bind("CommunityCommands", "ReserveSecondsLimit", 120f, new ConfigDescription("Maximum reserve duration per command. Uses current-game reserve seconds, not Prologue speed percentages.", new AcceptableValueRange<float>(0, 1000)));
        _hpGain = cfg.Bind("CommunityCommands", "HealthGain", false, "Allow: hp gain [amount]. Default 20 HP.");
        _hpLose = cfg.Bind("CommunityCommands", "HealthLose", false, "Allow: hp lose [amount]. Default 20 HP.");
        _reserveGain = cfg.Bind("CommunityCommands", "ReserveGain", false, "Allow: reserve gain [seconds]. Default 20 seconds.");
        _reserveLose = cfg.Bind("CommunityCommands", "ReserveLose", false, "Allow: reserve lose [seconds]. Default 20 seconds.");
        _reserveSet = cfg.Bind("CommunityCommands", "ReserveSet", false, "Allow: reserve set seconds.");
        _kill = cfg.Bind("CommunityCommands", "Kill", false, "Allow: kill humans / kill ais / kill pos 1 / kill karter NAME. Local race only.");
    }

    // Public module bridge for custom recipes or a future authenticated local test console.
    // Queueing never bypasses enable switches, user permissions, race state, or cooldown.
    public static void Receive(string user, string message)
    {
        if (user != null && message != null && Messages.Count < 128 && user.Length <= 128 && message.Length <= 2048) Messages.Enqueue((user, message));
    }

    internal static void Tick()
    {
        if (_plugin == null) return;
        RefreshConnection();
        while (Status.TryDequeue(out string? status)) _plugin.Log.LogInfo(status);
        for (int count = 0; count < 16 && Messages.TryDequeue(out var message); count++)
        {
            if (!_enabled.Value || !Plugin.OfflineLabAllowed || Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING || !CommunityCommandParser.AllowedUser(message.User, _users.Value, _channel.Value)) continue;
            if (Time.unscaledTime - _lastInteraction < _cooldown.Value) continue;
            if (!CommunityCommandParser.TryParse(message.Message, _prefix.Value, _healthLimit.Value, _reserveLimit.Value, out var command)) continue;
            try
            {
                if (Execute(command)) { _lastInteraction = Time.unscaledTime; _plugin.Log.LogInfo($"Community command applied: {command.Kind}"); }
            }
            catch (Exception ex) { _plugin.Log.LogWarning($"Community command failed: {ex.GetType().Name}"); }
        }
    }

    private static Ant_Player? MainPlayer()
    {
        foreach (var player in UnityEngine.Object.FindObjectsOfType<Ant_Player>())
            if (player.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && player.eAntLocalPlayerNr == Ant_Player.EAntLocalPlayerNumber.PL_LOC_0) return player;
        return null;
    }

    private static bool Execute(CommunityCommand command)
    {
        bool commandEnabled = CommunityCommandParser.EnabledSetting(command.Kind) switch
        {
            "HealthGain" => _hpGain.Value, "HealthLose" => _hpLose.Value,
            "ReserveGain" => _reserveGain.Value, "ReserveLose" => _reserveLose.Value,
            "ReserveSet" => _reserveSet.Value, "Kill" => _kill.Value, _ => false
        };
        if (!commandEnabled) return false;
        bool isKill = command.Kind >= CommunityCommandKind.KillHumans;
        if (isKill)
        {
            bool applied = false;
            foreach (var player in UnityEngine.Object.FindObjectsOfType<Ant_Player>())
            {
                bool local = player.ePlayerType is Ant_Player.EPlayerType.E_HUMAN_LOCAL or Ant_Player.EPlayerType.E_AI_LOCAL;
                if (!local || player.hpBarController == null) continue;
                bool match = command.Kind switch
                {
                    CommunityCommandKind.KillHumans => player.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL,
                    CommunityCommandKind.KillAi => player.ePlayerType == Ant_Player.EPlayerType.E_AI_LOCAL,
                    CommunityCommandKind.KillPosition => player.playerRaceLogic != null && player.playerRaceLogic.GetCurrentPlayerRacePositionIndex() + 1 == (int)command.Amount,
                    CommunityCommandKind.KillName => player.GetPlayerName().Equals(command.Name, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
                if (match) { player.hpBarController.Death(); applied = true; }
            }
            return applied;
        }
        var main = MainPlayer();
        if (main == null) return false;
        if (command.Kind is CommunityCommandKind.HpGain or CommunityCommandKind.HpLose)
        {
            if (main.hpBarController == null) return false;
            // Preserve the old SDK's SetCurrentHealth behavior: update the obscured
            // value, then let the game refresh its HUD and apply death conditions.
            int current = main.hpBarController.GetCurrentHP();
            long hpTarget = (long)current + (command.Kind == CommunityCommandKind.HpGain ? (int)command.Amount : -(int)command.Amount);
            main.hpBarController.currentHp = (int)Math.Clamp(hpTarget, 0, int.MaxValue);
            main.hpBarController.CheckHp();
            return true;
        }
        if (main.aiController == null || main.aiController.aiDistController == null || main.aiController.aiDistController.boostManager == null) return false;
        var boost = main.aiController.aiDistController.boostManager;
        float currentReserve = boost.GetCurrentBoostReservesTime();
        float target = command.Kind switch { CommunityCommandKind.ReserveGain => currentReserve + command.Amount, CommunityCommandKind.ReserveLose => currentReserve - command.Amount, _ => command.Amount };
        boost.fBoostingReserves = Math.Clamp(target, 0, _reserveLimit.Value);
        return true;
    }

    private static bool Login(string value)
    {
        if (value.Length == 0 || value.Length > 25) return false;
        foreach (char character in value) if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')) return false;
        return true;
    }

    private static void RefreshConnection()
    {
        string channel = _channel.Value.Trim().TrimStart('#').ToLowerInvariant(), username = _username.Value.Trim().ToLowerInvariant();
        string key = _enabled.Value && _connect.Value ? channel + "|" + username + "|" + _tokenVariable.Value : "";
        if (key == _connectionKey) return;
        Disconnect();
        _connectionKey = key;
        if (key.Length == 0) return;
        string? token;
        try { token = Environment.GetEnvironmentVariable(_tokenVariable.Value); }
        catch (ArgumentException)
        { Status.Enqueue("Twitch not connected: invalid token environment variable name. Correct it, then toggle Connect to Twitch."); return; }
        if (!Login(channel) || !Login(username) || string.IsNullOrWhiteSpace(token) || token.Contains('\r') || token.Contains('\n'))
        { Status.Enqueue("Twitch not connected: configure channel/login and token environment variable, then toggle Connect to Twitch."); return; }
        _cancel = new CancellationTokenSource();
        var client = _client = new TcpClient();
        CancellationToken cancellation = _cancel.Token;
        _worker = Task.Run(() => ReadChat(client, channel, username, token, cancellation));
    }

    private static async Task ReadChat(TcpClient client, string channel, string username, string token, CancellationToken cancellation)
    {
        try
        {
            await client.ConnectAsync("irc.chat.twitch.tv", 6697, cancellation);
            using var tls = new SslStream(client.GetStream(), false);
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "irc.chat.twitch.tv" }, cancellation);
            using var reader = new StreamReader(tls);
            using var writer = new StreamWriter(tls) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("PASS " + (token.StartsWith("oauth:", StringComparison.Ordinal) ? token : "oauth:" + token));
            await writer.WriteLineAsync("NICK " + username);
            await writer.WriteLineAsync("JOIN #" + channel);
            while (!cancellation.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null) break;
                // A disabled or reconfigured connection must not queue a late read.
                if (cancellation.IsCancellationRequested) break;
                if (line.StartsWith("PING ", StringComparison.Ordinal)) { await writer.WriteLineAsync("PONG " + line.Substring(5)); continue; }
                if (line.Contains(" 001 ", StringComparison.Ordinal)) { Status.Enqueue("Twitch authenticated; receiving chat commands."); continue; }
                if (line.Contains("Login authentication failed", StringComparison.Ordinal)) { Status.Enqueue("Twitch authentication failed. Update the token, then toggle Connect to Twitch."); break; }
                int bang = line.IndexOf('!'), marker = line.IndexOf(" PRIVMSG #" + channel + " :", StringComparison.OrdinalIgnoreCase);
                if (!line.StartsWith(":", StringComparison.Ordinal) || bang <= 1 || marker < 0) continue;
                int messageStart = marker + (" PRIVMSG #" + channel + " :").Length;
                Receive(line.Substring(1, bang - 1), line.Substring(messageStart));
            }
        }
        catch (Exception ex) { if (!cancellation.IsCancellationRequested) Status.Enqueue($"Twitch disconnected ({ex.GetType().Name}). Toggle Connect to Twitch to retry."); }
        finally { client.Dispose(); }
    }

    private static void Disconnect()
    {
        _cancel?.Cancel();
        _client?.Dispose();
        _cancel?.Dispose();
        _cancel = null; _client = null; _worker = null;
        while (Messages.TryDequeue(out _)) { }
    }

    internal static void Restore() { Disconnect(); _connectionKey = ""; _plugin = null; }
}

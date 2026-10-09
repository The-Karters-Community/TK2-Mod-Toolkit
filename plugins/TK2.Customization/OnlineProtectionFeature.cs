using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Photon.Pun;

namespace TK2.Customization;

public sealed partial class Plugin
{
    private ConfigEntry<bool> OnlineProtectionEnabled = null!, BlockLeaderboardUploads = null!, BlockOnlineLobbyJoins = null!;
    private bool _warnedLeaderboardBlock, _warnedLobbyBlock;

    private void BindOnlineProtection()
    {
        OnlineProtectionEnabled = Config.Bind("OnlineProtection", "Enabled", true,
            "Protect leaderboard and online-room actions when unapproved mods/plugins are active.");
        BlockLeaderboardUploads = Config.Bind("OnlineProtection", "BlockLeaderboardUploads", true,
            "Block record uploads while an unapproved mod or plugin is active.");
        BlockOnlineLobbyJoins = Config.Bind("OnlineProtection", "BlockOnlineLobbyJoins", true,
            "Block joining or creating online rooms while an unapproved mod or plugin is active.");
    }

    private void InstallOnlineProtection()
    {
        MethodInfo leaderboard = AccessTools.DeclaredMethod(typeof(KartersLeaderboardsManager), "RaceFinished_UploadLeaderboard",
            new[] { typeof(float), typeof(float[]), typeof(System.Collections.Generic.List<PTK_LeaderboardFacet.CCheckpointTimes>),
                typeof(Action), typeof(Action) })
            ?? throw new MissingMethodException(typeof(KartersLeaderboardsManager).FullName, "RaceFinished_UploadLeaderboard(float, float[], List<CCheckpointTimes>, Action, Action)");

        string[] roomOperations = { "JoinLobby", "JoinRoom", "JoinRandomRoom", "JoinOrCreateRoom", "JoinRandomOrCreateRoom", "CreateRoom", "ReconnectAndRejoin" };
        MethodInfo[] roomTargets = AccessTools.GetDeclaredMethods(typeof(PhotonNetwork))
            .Where(method => method.IsStatic && method.ReturnType == typeof(bool) && roomOperations.Contains(method.Name))
            .ToArray();
        string[] missingOperations = roomOperations.Where(name => !roomTargets.Any(method => method.Name == name)).ToArray();
        if (missingOperations.Length != 0)
            throw new MissingMethodException("PhotonNetwork is missing online room operations: " + string.Join(", ", missingOperations));

        Harmony.Patch(leaderboard, prefix: new HarmonyMethod(typeof(Plugin), nameof(LeaderboardUploadProtectionPrefix)));
        var prefix = new HarmonyMethod(typeof(Plugin), nameof(OnlineRoomProtectionPrefix));
        foreach (MethodInfo method in roomTargets) Harmony.Patch(method, prefix: prefix);
        Log.LogInfo($"Online protection ready: leaderboard upload hook and {roomTargets.Length} exact Photon room-operation overloads.");
    }

    private bool ShouldBlockOnlineAction(bool leaderboardUpload)
    {
        try
        {
            var activeSettings = Config.Keys.Select(definition =>
                (definition.Section, definition.Key, Config[definition] is ConfigEntry<bool> entry && entry.Value));
            string[] pluginGuids = IL2CPPChainloader.Instance.Plugins
                .Select(plugin => plugin.Key)
                .ToArray();
            if (!OnlineProtectionPolicy.RequiresBlocking(activeSettings, pluginGuids)) return false;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Online protection failed closed while checking loaded mods ({ex.GetType().Name}).");
            return true;
        }

        if (leaderboardUpload && !_warnedLeaderboardBlock)
        {
            _warnedLeaderboardBlock = true;
            Log.LogWarning("Online protection blocked a leaderboard upload because an unapproved mod/plugin is active.");
        }
        else if (!leaderboardUpload && !_warnedLobbyBlock)
        {
            _warnedLobbyBlock = true;
            Log.LogWarning("Online protection blocked an online room action because an unapproved mod/plugin is active.");
        }
        return true;
    }

    private static bool LeaderboardUploadProtectionPrefix() => Instance?.ShouldBlockOnlineAction(leaderboardUpload: true) != true;

    private static bool OnlineRoomProtectionPrefix(ref bool __result, MethodBase __originalMethod)
    {
        if (Instance?.ShouldBlockOnlineAction(leaderboardUpload: false) != true) return true;
        __result = false;
        return false;
    }
}

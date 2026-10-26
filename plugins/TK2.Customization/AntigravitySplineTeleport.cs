using System;
using System.Collections.Generic;
using HarmonyLib;

namespace TK2.Customization;

// The shipped game represents spline travel as QuickDashMovement_Bezier. This
// deliberately does not touch the separate instant-teleport or respawn paths.
internal static class AntigravitySplineTeleport
{
    private static readonly HashSet<int> LoggedRacers = new();
    internal static void Install(Plugin plugin)
    {
        FeatureHook.Install(plugin.Harmony, typeof(AntigravitySplineTeleport), new[]
        {
            new FeatureHook(typeof(PTK_PlayerLogicModCommand_QuickDashMovement),
                nameof(PTK_PlayerLogicModCommand_QuickDashMovement.ExecuteEffect_QuickDashMovement_Bezier),
                new[]
                {
                    typeof(PTK_BezierSpline), typeof(bool),
                    typeof(PTK_Command_05_PlayerLogicEffects.CPlayerEffect_E6_QuickDashMovementBase.EMoveSpeed),
                    typeof(Ant_Player), typeof(PixelEasyCharMoveKartController),
                    typeof(int), typeof(bool), typeof(bool)
                }, Prefix: nameof(BezierPrefix)),
            new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted),
                Type.EmptyTypes, Postfix: nameof(ResetLog))
        });
    }

    // Complete native signature: retain all arguments even though only the
    // player/controller identity decides whether this offline race test may apply.
    private static bool BezierPrefix(PTK_PlayerLogicModCommand_QuickDashMovement __instance,
        PTK_BezierSpline __0, bool __1,
        PTK_Command_05_PlayerLogicEffects.CPlayerEffect_E6_QuickDashMovementBase.EMoveSpeed __2,
        Ant_Player __3, PixelEasyCharMoveKartController __4, int __5, bool __6, bool __7)
    {
        var plugin = Plugin.Instance;
        if (plugin?.AntigravityEnabled.Value != true ||
            plugin.AntigravityDisableSplineTeleports.Value != true ||
            !Plugin.OfflineLabAllowed || __3 == null || __4 == null ||
            __4.parentPlayer != __3 || !AntigravityFeature.RacerRunning(__4))
            return true;

        plugin.SessionModified = true;
        int id = __4.GetInstanceID();
        if (LoggedRacers.Count >= 64) LoggedRacers.Clear();
        if (LoggedRacers.Add(id))
            plugin.Log.LogInfo($"Antigravity test: blocked Bezier spline movement for {__3.ePlayerType} kart {id}.");
        return false;
    }

    private static void ResetLog() => LoggedRacers.Clear();
}

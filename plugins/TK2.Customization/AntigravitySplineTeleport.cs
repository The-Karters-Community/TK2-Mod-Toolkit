using HarmonyLib;

namespace TK2.Customization;

// The shipped game represents spline travel as QuickDashMovement_Bezier. This
// deliberately does not touch the separate instant-teleport or respawn paths.
internal static class AntigravitySplineTeleport
{
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
                }, Prefix: nameof(BezierPrefix))
        });
    }

    // Complete native signature: retain all arguments even though only the
    // player/controller identity decides whether this local test may apply.
    private static bool BezierPrefix(PTK_PlayerLogicModCommand_QuickDashMovement __instance,
        PTK_BezierSpline __0, bool __1,
        PTK_Command_05_PlayerLogicEffects.CPlayerEffect_E6_QuickDashMovementBase.EMoveSpeed __2,
        Ant_Player __3, PixelEasyCharMoveKartController __4, int __5, bool __6, bool __7)
    {
        var plugin = Plugin.Instance;
        if (plugin?.AntigravityEnabled.Value != true ||
            plugin.AntigravityDisableSplineTeleports.Value != true ||
            !Plugin.OfflineLabAllowed || __3 == null || __4 == null ||
            __3.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL ||
            !AirShiftFeature.Running(__4))
            return true;

        plugin.SessionModified = true;
        plugin.Log.LogInfo("Antigravity test: blocked one local Bezier spline movement.");
        return false;
    }
}

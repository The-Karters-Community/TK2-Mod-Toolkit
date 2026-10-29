using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace TK2.Customization;

// Adapts the game's own exhaust variants to the currently held CTR fire.
// SetBoostParticlesActive's four arrays and its stop/play behavior were checked
// against the installed native implementation, not inferred from wrapper bodies.
internal static class CtrFireVisualFeature
{
    private static ConfigEntry<bool> Enabled = null!;
    private static readonly Dictionary<int, int> LastTier = new();
    private static MethodInfo? SetParticles;

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("CtrHandling", "FireVisuals", true,
            "Use the kart's exhaust variants to show the current CTR fire level.");
        SetParticles = AccessTools.Method(typeof(Ant_KartParticlesWorker), "SetBoostParticlesActive", new[] { typeof(bool), typeof(int) });
        FeatureHook.Install(p.Harmony, typeof(CtrFireVisualFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
            new FeatureHook(typeof(Ant_KartParticlesWorker), "WorkerUpdate", new[] { typeof(float) }, Prefix: nameof(UpdatePrefix), Postfix: nameof(UpdatePostfix)),
            new FeatureHook(typeof(Ant_KartParticlesWorker), "SetBoostParticlesActive", new[] { typeof(bool), typeof(int) }, Prefix: nameof(ParticlesPrefix))
        };

    private static int Level(Ant_KartParticlesWorker worker, float fire)
    {
        // All four native arrays are indexed without bounds checks before playing.
        int count = Math.Min(Math.Min(worker.leftExhaustBoostLoopParents?.Length ?? 0, worker.rightExhaustBoostLoopParents?.Length ?? 0),
            Math.Min(worker.leftExhaustBoostSuccessParents?.Length ?? 0, worker.rightExhaustBoostSuccessParents?.Length ?? 0));
        if (count <= 0) return -1;
        int tier = fire < 10f ? 0 : fire < 12f ? 1 : fire < 16f ? 2 : 3;
        return Math.Min(tier, count - 1);
    }

    private static bool TryLevel(Ant_KartParticlesWorker worker, out int tier)
    {
        tier = -1;
        if (Enabled?.Value != true || worker == null || !Plugin.OfflineLabAllowed) return false;
        var synced = worker.visualSyncedParams;
        if (synced == null || synced.parentPlayer == null ||
            !CtrHandlingFeature.TryGetFire(synced.parentPlayer, out float fire, out _)) return false;
        tier = Level(worker, fire);
        return tier >= 0;
    }

    // Native boost events still play normally; select the currently held fire tier
    // instead of blindly using the most recent boost's own tier.
    private static void ParticlesPrefix(Ant_KartParticlesWorker __instance, bool __0, ref int __1)
    {
        try { if (__0 && TryLevel(__instance, out int tier)) __1 = tier; }
        catch (Exception ex) { Stop(ex); }
    }

    private static void UpdatePrefix(Ant_KartParticlesWorker __instance, out bool __state) =>
        __state = __instance != null && __instance.isKartBoosting;

    private static void UpdatePostfix(Ant_KartParticlesWorker __instance, bool __state)
    {
        try {
            if (__instance == null || SetParticles == null) return;
            int id = __instance.GetInstanceID();
            if (TryLevel(__instance, out int tier)) {
                // Native can stop the loop when its own boost timer ends even while
                // CTR reserves remain. Restart once at that transition.
                bool stopped = __state && !__instance.isKartBoosting;
                if (!LastTier.TryGetValue(id, out int previous) || previous != tier || stopped) {
                    LastTier[id] = tier;
                    SetParticles.Invoke(__instance, new object[] { true, tier });
                }
            } else if (LastTier.Remove(id)) {
                // Don't alter other players, replays, or online presentation.
                if (Plugin.OfflineLabAllowed && __instance.visualSyncedParams?.parentPlayer?.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL)
                    SetParticles.Invoke(__instance, new object[] { __instance.isKartBoosting, 0 });
            }
        } catch (Exception ex) { Stop(ex); }
    }

    private static void Stop(Exception ex)
    {
        if (Enabled != null) Enabled.Value = false;
        LastTier.Clear();
        Plugin.Instance?.Log.LogWarning($"CTR fire visuals stopped after an error: {ex.Message}");
    }
}

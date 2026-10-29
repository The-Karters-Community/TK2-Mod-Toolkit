using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TK2.Customization;

// Adapts the game's own exhaust variants to the currently held CTR fire.
// SetBoostParticlesActive's four arrays and its stop/play behavior were checked
// against the installed native implementation, not inferred from wrapper bodies.
internal static class CtrFireVisualFeature
{
    private static ConfigEntry<bool> Enabled = null!;
    private static ConfigEntry<float> Size = null!;
    private sealed class VisualState { internal float Scale; internal readonly Dictionary<int, (Transform Transform, Vector3 Original)> Transforms = new(); }
    private static readonly Dictionary<int, VisualState> Scales = new();
    private static readonly Dictionary<int, int> LastTier = new();
    private static MethodInfo? SetParticles;

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("CtrHandling", "FireVisuals", true,
            "Use the kart's exhaust variants to show the current CTR fire level.");
        Size = p.Config.Bind("CtrHandling", "FireSize", 4f, "Maximum exhaust size multiplier at turbo-pad fire; lower fire levels scale proportionally.");
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

    private static bool TryLevel(Ant_KartParticlesWorker worker, out int tier, out float fire)
    {
        tier = -1; fire = 0;
        if (Enabled?.Value != true || worker == null || !Plugin.OfflineLabAllowed) return false;
        var synced = worker.visualSyncedParams;
        if (synced == null || synced.parentPlayer == null ||
            !CtrHandlingFeature.TryGetFire(synced.parentPlayer, out fire, out _)) return false;
        tier = Level(worker, fire);
        return tier >= 0;
    }

    // Native boost events still play normally; select the currently held fire tier
    // instead of blindly using the most recent boost's own tier.
    private static void ParticlesPrefix(Ant_KartParticlesWorker __instance, bool __0, ref int __1)
    {
        try { if (__0 && TryLevel(__instance, out int tier, out _)) __1 = tier; }
        catch (Exception ex) { Stop(ex); }
    }

    private static void UpdatePrefix(Ant_KartParticlesWorker __instance, out bool __state) =>
        __state = __instance != null && __instance.isKartBoosting;

    private static void UpdatePostfix(Ant_KartParticlesWorker __instance, bool __state)
    {
        try {
            if (__instance == null || SetParticles == null) return;
            int id = __instance.GetInstanceID();
            if (TryLevel(__instance, out int tier, out float fire)) {
                ApplyScale(__instance, fire);
                // Native can stop the loop when its own boost timer ends even while
                // CTR reserves remain. Restart once at that transition.
                bool stopped = __state && !__instance.isKartBoosting;
                if (!LastTier.TryGetValue(id, out int previous) || previous != tier || stopped) {
                    LastTier[id] = tier;
                    SetParticles.Invoke(__instance, new object[] { true, tier });
                }
            } else {
                RestoreScale(id);
                if (!LastTier.Remove(id)) return;
                // Don't alter other players, replays, or online presentation.
                if (Plugin.OfflineLabAllowed && __instance.visualSyncedParams?.parentPlayer?.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL)
                    SetParticles.Invoke(__instance, new object[] { __instance.isKartBoosting, 0 });
            }
        } catch (Exception ex) { Stop(ex); }
    }

    private static void ApplyScale(Ant_KartParticlesWorker worker, float fire)
    {
        float max = Size.Value;
        if (!float.IsFinite(max)) max = 1f;
        max = Math.Clamp(max, 1f, 10f);
        float scale = 1f + (max - 1f) * Math.Clamp(fire / 16f, 0f, 1f);
        int id = worker.GetInstanceID();
        if (!Scales.TryGetValue(id, out var state)) Scales[id] = state = new VisualState();
        if (Math.Abs(state.Scale - scale) < .001f && state.Transforms.Count != 0) return;
        CaptureAndScale(worker.leftExhaustBoostLoopParents, state, scale);
        CaptureAndScale(worker.rightExhaustBoostLoopParents, state, scale);
        state.Scale = scale;
    }

    private static void CaptureAndScale(Il2CppReferenceArray<GameObject> objects, VisualState state, float scale)
    {
        if (objects == null) return;
        foreach (var obj in objects) {
            if (obj == null || obj.transform == null) continue;
            Transform transform = obj.transform;
            int id = transform.GetInstanceID();
            if (!state.Transforms.TryGetValue(id, out var entry)) {
                entry = (transform, transform.localScale);
                state.Transforms[id] = entry;
            }
            transform.localScale = entry.Original * scale;
        }
    }

    private static void RestoreScale(int id)
    {
        if (!Scales.TryGetValue(id, out var state)) return;
        foreach (var entry in state.Transforms.Values)
            if (entry.Transform != null) entry.Transform.localScale = entry.Original;
        Scales.Remove(id);
    }

    internal static void Reset()
    {
        foreach (var id in new List<int>(Scales.Keys)) RestoreScale(id);
        LastTier.Clear();
    }

    private static void Stop(Exception ex)
    {
        if (Enabled != null) Enabled.Value = false;
        Reset();
        Plugin.Instance?.Log.LogWarning($"CTR fire visuals stopped after an error: {ex.Message}");
    }
}

using System;
using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private struct VisualGroundState
    {
        internal VisualInstanceSyncedParams? Visual;
        internal bool Grounded, VisualGrounded;
        internal Vector3 Normal;
        internal float Distance;
    }

    internal static void InstallVisualGrounding(Plugin plugin) => FeatureHook.Install(plugin.Harmony,
        typeof(AntigravityFeature), new[] {
            new FeatureHook(typeof(Ant_VisualKart), "UnityCustomUpdate", new[] { typeof(float) },
                Prefix: nameof(KartVisualGroundPrefix), Postfix: nameof(RestoreVisualGround), Finalizer: nameof(RestoreVisualGround)),
            new FeatureHook(typeof(Ant_KartAnimatorWorker), "Update", Type.EmptyTypes,
                Prefix: nameof(KartAnimationGroundPrefix), Postfix: nameof(RestoreVisualGround), Finalizer: nameof(RestoreVisualGround)),
            new FeatureHook(typeof(Ant_CharacterWorker), "Update", Type.EmptyTypes,
                Prefix: nameof(CharacterVisualGroundPrefix), Postfix: nameof(RestoreVisualGround), Finalizer: nameof(RestoreVisualGround))
        });

    private static void KartVisualGroundPrefix(Ant_VisualKart __instance, out VisualGroundState __state) =>
        BeginVisualGround(__instance.visualInstanceSyncedParams, out __state);

    private static void KartAnimationGroundPrefix(Ant_KartAnimatorWorker __instance, out VisualGroundState __state) =>
        BeginVisualGround(__instance.visualSyncedParams, out __state);

    private static void CharacterVisualGroundPrefix(Ant_CharacterWorker __instance, out VisualGroundState __state) =>
        BeginVisualGround(__instance.visualInstanceParams, out __state);

    private static void BeginVisualGround(VisualInstanceSyncedParams visual, out VisualGroundState state)
    {
        state = default;
        if (visual == null || Plugin.Instance?.AntigravityEnabled.Value != true || !Plugin.OfflineLabAllowed ||
            !IsLocalRacer(visual.parentPlayer)) return;
        foreach (var entry in States.Values)
        {
            if (!entry.Attached || entry.Controller == null || entry.Controller.parentPlayer != visual.parentPlayer) continue;
            if (entry.AcceptedHop || entry.HopElapsed > 0f || entry.Motor.MustUnground()) return;
            if (entry.VisualContactFrame != Time.frameCount)
            {
                entry.VisualContactFrame = Time.frameCount;
                entry.VisualContact = HasJumpContact(entry);
            }
            if (!entry.VisualContact) return;
            state = new VisualGroundState { Visual = visual, Grounded = visual.bIsPlayerGrouned,
                VisualGrounded = visual.bIsPlayerVisualGrouned, Normal = visual.vPlayerVisualGrounedNormal,
                Distance = visual.fPlayerDistanceToGround };
            visual.bIsPlayerGrouned = visual.bIsPlayerVisualGrouned = true;
            visual.vPlayerVisualGrounedNormal = entry.Up;
            visual.fPlayerDistanceToGround = 0f;
            if (IsLocalHuman(entry.Controller) && (!state.Grounded || !state.VisualGrounded) && entry.VisualLogCount < 4 &&
                Time.unscaledTime >= entry.NextVisualLog)
            {
                entry.VisualLogCount++;
                entry.NextVisualLog = Time.unscaledTime + .5f;
                Plugin.Instance?.Log.LogInfo($"Antigravity visual grounding: contact=True " +
                    $"native={state.Grounded} visual={state.VisualGrounded} tilt={Vector3.Angle(Vector3.up, entry.Up):0}");
            }
            return;
        }
    }

    private static void RestoreVisualGround(ref VisualGroundState __state)
    {
        var visual = __state.Visual;
        if (visual == null) return;
        visual.bIsPlayerGrouned = __state.Grounded;
        visual.bIsPlayerVisualGrouned = __state.VisualGrounded;
        visual.vPlayerVisualGrounedNormal = __state.Normal;
        visual.fPlayerDistanceToGround = __state.Distance;
        __state.Visual = null;
    }
}

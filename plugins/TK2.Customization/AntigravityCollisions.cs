using System;
using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    internal static void InstallCollisions(Plugin plugin) => FeatureHook.Install(plugin.Harmony,
        typeof(AntigravityFeature), new[] {
            new FeatureHook(typeof(PixelEasyCharMoveKartController), "UpdatePlayerPlayerCollision", Type.EmptyTypes,
                Prefix: nameof(RacerBouncePrefix)),
            new FeatureHook(typeof(PixelEasyCharMoveKartController),
                "KinematicCharacterController_ICharacterController_IsColliderValidForCollisions", new[] { typeof(Collider) },
                Prefix: nameof(RacerContactPrefix))
        });

    private static bool SurfaceRacer(PixelEasyCharMoveKartController controller) =>
        controller != null && controller.kartPhysics != null && TryGetAttached(controller.kartPhysics, out _);

    private static bool RacerBouncePrefix(PixelEasyCharMoveKartController __instance)
    {
        if (!Plugin.OfflineLabAllowed || Plugin.Instance?.AntigravityEnabled.Value != true ||
            !IsLocalRacer(__instance) || __instance.Motor == null) return true;
        bool ignore = SurfaceRacer(__instance);
        // Bounce is a separate proximity calculation, independent of KCC contact.
        foreach (var entry in States.Values)
        {
            if (!entry.Attached || entry.Motor == null || !RacerRunning(entry.Controller)) continue;
            float reach = entry.Motor.Capsule.radius + __instance.Motor.Capsule.radius + 2f;
            if ((__instance.Motor.TransientPosition - entry.Motor.TransientPosition).sqrMagnitude < reach * reach)
            { ignore = true; break; }
        }
        if (!ignore) return true;
        __instance.bPerformPlayerPlayerCollisionBounceEffect = false;
        __instance.vBouncePlayerPlayerImpulse = Vector3.zero;
        return false;
    }

    private static bool RacerContactPrefix(PixelEasyCharMoveKartController __instance, Collider __0, ref bool __result)
    {
        if (States.Count == 0 || !Plugin.OfflineLabAllowed || Plugin.Instance?.AntigravityEnabled.Value != true || __0 == null ||
            !IsLocalRacer(__instance)) return true;
        var other = __0.GetComponentInParent<PixelEasyCharMoveKartController>();
        if (other == null || other == __instance || !IsLocalRacer(other) ||
            (!SurfaceRacer(__instance) && !SurfaceRacer(other))) return true;
        __result = false;
        return false;
    }
}

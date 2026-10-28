using System;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Approximation: CTR's fixed-point coefficients have not been calibrated to TK2 units.
internal static class CtrPhysicsFeature
{
    private static ConfigEntry<bool> Enabled = null!;
    private struct Scope { internal bool Applied; internal float Acceleration, Friction; }

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("CtrHandling", "Enabled", false, "Experimental offline CTR-inspired handling.");
        FeatureHook.Install(p.Harmony, typeof(CtrPhysicsFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(PixelKartPhysics), nameof(PixelKartPhysics.UpdateVelocity),
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(BeforeVelocity),
            Postfix: nameof(AfterVelocity), Finalizer: nameof(FinalizeVelocity))
    };

    private static void BeforeVelocity(PixelKartPhysics __instance, out Scope __state)
    {
        __state = default;
        try {
            if (Enabled?.Value != true || !AirShiftFeature.Running(__instance.kartController) ||
                __instance.kartController.parentPlayer.playerRaceLogic?.bPlayerEndRaceEventTriggered != false) return;
            __state = new Scope { Applied = true, Acceleration = __instance.acceleration_factor,
                Friction = __instance.ground_friction_factor };
            // Preserve native surface, slope, gravity, collisions and over-cap momentum.
            __instance.acceleration_factor = __state.Acceleration * 1.1f;
            __instance.ground_friction_factor = __state.Friction * .85f;
            Plugin.Instance!.SessionModified = true;
        } catch (Exception ex) {
            Restore(__instance, ref __state);
            Enabled.Value = false;
            Plugin.Instance?.Log.LogError($"CTR physics disabled: {ex.Message}");
        }
    }

    private static void AfterVelocity(PixelKartPhysics __instance, ref Scope __state) => Restore(__instance, ref __state);
    private static Exception? FinalizeVelocity(PixelKartPhysics __instance, ref Scope __state, Exception? __exception)
    { Restore(__instance, ref __state); return __exception; }
    private static void Restore(PixelKartPhysics physics, ref Scope state)
    {
        if (!state.Applied) return;
        physics.acceleration_factor = state.Acceleration;
        physics.ground_friction_factor = state.Friction;
        state.Applied = false;
    }
    internal static void Reset() { }
}

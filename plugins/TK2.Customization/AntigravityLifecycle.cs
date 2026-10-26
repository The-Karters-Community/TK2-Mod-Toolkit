using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    internal static void Tick()
    {
        if (States.Count == 0 && PendingReleaseRotation.Count == 0) return;
        var plugin = Plugin.Instance;
        int scene = SceneManager.GetActiveScene().handle;
        foreach (var pending in PendingReleaseRotation.Values)
            if (pending.Controller == null || pending.ActiveSceneHandle != scene ||
                pending.KartSceneHandle != pending.Controller.gameObject.scene.handle)
            { PendingReleaseRotation.Clear(); break; }
        if (States.Count == 0) return;
        foreach (var entry in States.Values)
            if (entry.Controller == null || entry.ActiveSceneHandle != scene ||
                entry.KartSceneHandle != entry.Controller.gameObject.scene.handle)
            { ReleaseAll(); PendingReleaseRotation.Clear(); return; }
        if (plugin != null && plugin.AntigravityEnabled.Value && Plugin.OfflineLabAllowed)
        {
            foreach (var entry in States.Values)
                if (!AirShiftFeature.Running(entry.Controller))
                { ReleaseAll(); SetStatus("Antigravity: waiting for a local offline race"); return; }
            return;
        }
        ReleaseAll();
        SetStatus("Antigravity: disabled or unavailable");
    }

    private static void AfterCharacterUpdate(PixelEasyCharMoveKartController __instance, float __0)
    {
        if (__instance?.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
        int id = __instance.GetInstanceID();
        var plugin = Plugin.Instance;
        if (plugin?.AntigravityEnabled.Value != true || !Plugin.OfflineLabAllowed ||
            !AirShiftFeature.Running(__instance))
        {
            Release(id, true);
            return;
        }

        try
        {
            var physics = __instance.kartPhysics;
            var motor = __instance.Motor;
            if (physics == null || motor == null) { Release(id, true); return; }
            States.TryGetValue(id, out var entry);
            UpdateSolidSurface(__instance, physics, motor, entry);
        }
        catch (Exception exception)
        {
            Fail(id, plugin, exception);
        }
    }

    // KCC evaluates its stable-slope limit before AfterCharacterUpdate. Feed the
    // already-established surface frame back at this boundary so a loop remains
    // a stable surface when its world tilt crosses the native ~60-degree limit.
    private static void BeforeCharacterUpdatePrefix(PixelEasyCharMoveKartController __instance, float __0)
    {
        var plugin = Plugin.Instance;
        if (plugin?.AntigravityEnabled.Value != true ||
            !AirShiftFeature.Running(__instance) || !States.TryGetValue(__instance.GetInstanceID(), out var entry) ||
            !entry.Attached || entry.Motor == null)
            return;
        Vector3 forward = Vector3.ProjectOnPlane(entry.Forward, entry.Up).normalized;
        if (forward.sqrMagnitude < .5f || entry.Up.sqrMagnitude < .5f) return;
        entry.Motor.TransientRotation = Quaternion.LookRotation(forward, entry.Up.normalized);
    }

}

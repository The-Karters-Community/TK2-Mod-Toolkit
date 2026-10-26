using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static void RaceResetPostfix() { ReleaseAll(); PendingReleaseRotation.Clear(); }

    internal static void ReleaseAll()
    {
        foreach (var pair in States)
        {
            try { ReleaseKart(pair.Value); }
            catch (Exception ex) { Plugin.Instance?.Log.LogWarning($"Antigravity cleanup failed for one kart: {ex.Message}"); }
        }
        States.Clear();
    }

    internal static void ReleaseKartBeforeRestore(PixelKartPhysics physics)
    {
        if (physics?.kartController == null) return;
        int id = physics.kartController.GetInstanceID();
        if (!States.TryGetValue(id, out var entry)) return;
        ReleaseKart(entry, true);
        States.Remove(id);
        PendingReleaseRotation.Remove(id);
    }

    internal static void RestoreAll()
    {
        ReleaseAll();
        PendingReleaseRotation.Clear();
    }

    private static void Release(int id, bool safeCharacterBoundary = false)
    {
        if (!States.TryGetValue(id, out var entry)) return;
        ReleaseKart(entry, safeCharacterBoundary);
        States.Remove(id);
        if (safeCharacterBoundary) PendingReleaseRotation.Remove(id);
    }

    // Stop applying direct-surface state for a native shallow-road jump. Unlike
    // a normal detach, this deliberately leaves transient rotation and velocity
    // untouched so the game's jump-drift can execute its own pose and impulse.
    private static void ReleaseForNativeJump(int id)
    {
        if (!States.TryGetValue(id, out var entry)) return;
        if (entry.LayersOverridden && entry.Motor != null)
        {
            entry.Motor.StableGroundLayers = entry.OriginalStableLayers;
            entry.LayersOverridden = false;
        }
        States.Remove(id);
        PendingReleaseRotation.Remove(id);
    }

    private static void ReleaseKart(KartState entry, bool safeCharacterBoundary = false)
    {
        if (entry.LayersOverridden && entry.Motor != null)
        {
            entry.Motor.StableGroundLayers = entry.OriginalStableLayers;
            entry.LayersOverridden = false;
        }
        if (!entry.Applied) return;
        if (entry.Controller == null || entry.Motor == null || entry.Controller.kartPhysics == null) return;
        var release = AntigravityPolicy.ReleaseAtCurrentHeading(entry.Controller.transform.forward, entry.Motor.Velocity);
        ApplyAxes(entry.Controller.kartPhysics, release.Forward, release.Up, release.Velocity);
        entry.Motor.BaseVelocity = release.Velocity;
        if (safeCharacterBoundary) entry.Motor.TransientRotation = release.Rotation;
        else PendingReleaseRotation[entry.Controller.GetInstanceID()] = entry;
    }

    private static void Fail(int id, Plugin plugin, Exception exception)
    {
        ReleaseAll();
        plugin.AntigravityEnabled.Value = false;
        plugin.Log.LogError($"Antigravity stopped after runtime failure: {exception.Message}");
    }
}

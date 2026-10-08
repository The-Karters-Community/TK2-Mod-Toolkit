using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TK2.Reconstructed;

namespace TK2.Customization;

// The native camera returns position, rotation and FOV together. Customize that output,
// rather than overriding Camera.main after it was framed using the old FOV.
internal static class CameraFeature
{
    private sealed class Frame
    {
        internal float Fov, Distance, Height, Lateral, Target, Pitch, Yaw, Roll;
        internal int Number = -1;
    }
    private static readonly Dictionary<int, Frame> Frames = new();
    private static float _lastRaceTime = -1000;
    internal static bool RaceAvailable => Time.unscaledTime - _lastRaceTime < 1;
    internal static void Install(Plugin plugin)
    {
        var target = AccessTools.DeclaredMethod(typeof(PixelGameKartCamera), "GetRacingCamPositionAndRotation",
            new[] { typeof(float), typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType(), typeof(float).MakeByRefType() })
            ?? throw new MissingMethodException("Racing camera output signature changed");
        plugin.Harmony.Patch(target, postfix: new HarmonyMethod(typeof(CameraFeature), nameof(AfterFraming)));
    }

    private static Frame Current(Plugin p) => new() { Fov = p.Fov.Value, Distance = p.CameraDistance.Value,
        Height = p.CameraHeight.Value, Lateral = p.CameraLateral.Value, Target = p.CameraTargetHeight.Value,
        Pitch = p.CameraPitch.Value, Yaw = p.CameraYaw.Value, Roll = p.CameraRoll.Value };

    private static void AfterFraming(PixelGameKartCamera __instance, ref Vector3 __1, ref Quaternion __2, ref float __3)
    {
        var plugin = Plugin.Instance;
        if (plugin == null || __instance.bIsSpectatorCamera ||
            __instance.fRacingCameraStrength < 0.95f) return;
        try
        {
            var player = __instance.antPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL ||
                player.visualInstanceSyncedParams == null || __3 < 1 || __3 > 175) return;
            _lastRaceTime = Time.unscaledTime;
            if (!plugin.CameraEnabled.Value) { Frames.Clear(); return; }
            int id = __instance.GetInstanceID();
            if (!Frames.TryGetValue(id, out var f))
            {
                if (Frames.Count > 16) Frames.Clear(); // Bound stale scene-camera state.
                f = Current(plugin); Frames[id] = f;
            }
            if (f.Number != Time.frameCount)
            {
                float blend = CameraFraming.BlendFactor(Time.unscaledDeltaTime, plugin.CameraSmoothing.Value);
                f.Fov = Mathf.Lerp(f.Fov, plugin.Fov.Value, blend);
                f.Distance = Mathf.Lerp(f.Distance, plugin.CameraDistance.Value, blend);
                f.Height = Mathf.Lerp(f.Height, plugin.CameraHeight.Value, blend);
                f.Lateral = Mathf.Lerp(f.Lateral, plugin.CameraLateral.Value, blend);
                f.Target = Mathf.Lerp(f.Target, plugin.CameraTargetHeight.Value, blend);
                f.Pitch = Mathf.Lerp(f.Pitch, plugin.CameraPitch.Value, blend);
                f.Yaw = Mathf.Lerp(f.Yaw, plugin.CameraYaw.Value, blend);
                f.Roll = Mathf.Lerp(f.Roll, plugin.CameraRoll.Value, blend);
                f.Number = Time.frameCount;
            }
            Vector3 kartPosition = player.visualInstanceSyncedParams.vCurPos;
            float scale = CameraFraming.DistanceScale(__3, f.Fov, plugin.PreserveKartFraming.Value, f.Distance);
            if (!float.IsFinite(f.Height) || !float.IsFinite(f.Lateral)) throw new ArgumentOutOfRangeException("Camera offsets must be finite.");
            __1 = kartPosition + (__1 - kartPosition) * scale + Vector3.up * f.Height + (__2 * Vector3.right) * f.Lateral;
            if (plugin.AimAtKart.Value)
            {
                Vector3 direction = kartPosition + Vector3.up * f.Target - __1;
                if (direction.sqrMagnitude > .0001f) __2 = Quaternion.LookRotation(direction, Vector3.up);
            }
            __2 *= Quaternion.Euler(f.Pitch, f.Yaw, f.Roll);
            __3 = f.Fov;
        }
        catch (Exception ex)
        {
            plugin.CameraEnabled.Value = false;
            Frames.Clear();
            plugin.Log.LogError($"Camera customization stopped: {ex.Message}");
        }
    }
}

using System;
using HarmonyLib;
using UnityEngine;
using TK2.Reconstructed;

namespace TK2.Customization;

// The native camera returns position, rotation and FOV together. Customize that output,
// rather than overriding Camera.main after it was framed using the old FOV.
internal static class CameraFeature
{
    internal static void Install(Plugin plugin)
    {
        var target = AccessTools.DeclaredMethod(typeof(PixelGameKartCamera), "GetRacingCamPositionAndRotation",
            new[] { typeof(float), typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType(), typeof(float).MakeByRefType() })
            ?? throw new MissingMethodException("Racing camera output signature changed");
        plugin.Harmony.Patch(target, postfix: new HarmonyMethod(typeof(CameraFeature), nameof(AfterFraming)));
    }

    private static void AfterFraming(PixelGameKartCamera __instance, ref Vector3 __1, ref float __3)
    {
        var plugin = Plugin.Instance;
        if (plugin == null || !plugin.CameraEnabled.Value || __instance.bIsSpectatorCamera ||
            __instance.fRacingCameraStrength < 0.95f) return;
        try
        {
            var player = __instance.antPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL ||
                player.visualInstanceSyncedParams == null || __3 < 1 || __3 > 175) return;
            Vector3 kartPosition = player.visualInstanceSyncedParams.vCurPos;
            float scale = CameraFraming.DistanceScale(__3, plugin.Fov.Value, plugin.PreserveKartFraming.Value, plugin.CameraDistance.Value);
            if (!float.IsFinite(plugin.CameraHeight.Value)) throw new ArgumentOutOfRangeException("Camera height must be finite.");
            __1 = kartPosition + (__1 - kartPosition) * scale + Vector3.up * plugin.CameraHeight.Value;
            __3 = plugin.Fov.Value;
        }
        catch (Exception ex)
        {
            plugin.CameraEnabled.Value = false;
            plugin.Log.LogError($"Camera customization stopped: {ex.Message}");
        }
    }
}

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

public sealed partial class Plugin
{
    internal ConfigEntry<bool> LapsEnabled = null!, SimpleDriving = null!, AutoBoost = null!, TuningEnabled = null!;
    internal ConfigEntry<int> LapCount = null!;
    internal ConfigEntry<float> SpeedMultiplier = null!, JumpMultiplier = null!;
    internal ConfigEntry<bool> HudOpacityEnabled = null!, RenderEnabled = null!;
    internal ConfigEntry<float> HudOpacity = null!, ShadowDistance = null!;

    private void BindPack()
    {
        LapsEnabled = Config.Bind("Laps", "Enabled", false, "Offline custom laps. Locked pending runtime validation.");
        LapCount = Config.Bind("Laps", "Count", 3, new ConfigDescription("Laps per race.", new AcceptableValueRange<int>(1, 99)));
        SimpleDriving = Config.Bind("SimpleDriving", "Enabled", false, "Disable jumping and drifting for a driving challenge. Offline lab.");
        AutoBoost = Config.Bind("AutoBoost", "Enabled", false, "Trigger a filled drift boost just before the perfect window ends. Offline lab.");
        TuningEnabled = Config.Bind("Tuning", "Enabled", false, "Scale local kart speed and jump strength from captured originals. Offline lab.");
        SpeedMultiplier = Config.Bind("Tuning", "SpeedMultiplier", 1f, new ConfigDescription("Forward speed multiplier.", new AcceptableValueRange<float>(0.25f, 3f)));
        JumpMultiplier = Config.Bind("Tuning", "JumpMultiplier", 1f, new ConfigDescription("Jump strength multiplier.", new AcceptableValueRange<float>(0.25f, 3f)));
        HudOpacityEnabled = Config.Bind("HudOpacity", "Enabled", false, "Change opacity on matching HUD canvases that already have a CanvasGroup.");
        HudOpacity = Config.Bind("HudOpacity", "Opacity", 1f, new ConfigDescription("HUD alpha.", new AcceptableValueRange<float>(0.1f, 1f)));
        RenderEnabled = Config.Bind("Rendering", "Enabled", false, "Override Unity shadow distance.");
        ShadowDistance = Config.Bind("Rendering", "ShadowDistance", 100f, new ConfigDescription("Shadow draw distance.", new AcceptableValueRange<float>(0f, 500f)));
    }

    private void InstallPack()
    {
        TryFeature("custom laps", () => PatchExact(typeof(Ant_MainGame), "GetGameModeRequiredLapCount", Type.EmptyTypes, nameof(LapsPostfix), false));
        TryFeature("simple driving", () => {
            PatchExact(typeof(PixelEasyCharMoveKartController), "JumpInput", new[] { typeof(bool) }, nameof(DrivingPrefix), true);
            PatchExact(typeof(PixelEasyCharMoveKartController), "DriftInput", new[] { typeof(bool) }, nameof(DrivingPrefix), true);
        });
        TryFeature("auto boost", () => PatchExact(typeof(Ant_BoostManager), "FixedUpdate", Type.EmptyTypes, nameof(BoostPostfix), false));
        TryFeature("kart tuning", () => PatchExact(typeof(PixelKartPhysics), "FixedUpdate", Type.EmptyTypes, nameof(TuningPostfix), false));
        RecipeHost.Install(this);
    }

    internal static bool OfflineLabAllowed => Instance != null && Instance.GameplayReady &&
        !Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet;

    private static bool LocalPlayer(Ant_Player player) => player != null && player.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL;

    private static void LapsPostfix(ref int __result)
    {
        var p = Instance!;
        if (!OfflineLabAllowed || !p.LapsEnabled.Value) return;
        p.SessionModified = true;
        __result = p.LapCount.Value;
    }

    private static bool DrivingPrefix(PixelEasyCharMoveKartController __instance)
    {
        if (!OfflineLabAllowed || !Instance!.SimpleDriving.Value || !LocalPlayer(__instance.parentPlayer)) return true;
        Instance.SessionModified = true;
        return false;
    }

    private static void BoostPostfix(Ant_BoostManager __instance)
    {
        if (!OfflineLabAllowed || !Instance!.AutoBoost.Value || !LocalPlayer(__instance.kartController.parentPlayer)) return;
        try
        {
            foreach (float fill in __instance.fCurrentBoostFillTime)
                if (fill >= __instance.fMaximumTimeForBestBoost - Time.fixedDeltaTime)
                {
                    Instance.SessionModified = true;
                    __instance.BoostInput(true);
                    break;
                }
        }
        catch (Exception ex) { Instance!.AutoBoost.Value = false; Instance.Log.LogError(ex); }
    }

    private static readonly Dictionary<int, (PixelKartPhysics Kart, float Speed, float Jump)> TuningOriginals = new();
    private static void TuningPostfix(PixelKartPhysics __instance)
    {
        try
        {
            int id = __instance.GetInstanceID();
            var p = Instance!;
            bool apply = OfflineLabAllowed && p.TuningEnabled.Value && LocalPlayer(__instance.kartController.parentPlayer);
            if (!apply)
            {
                if (TuningOriginals.TryGetValue(id, out var saved))
                {
                    __instance.max_speed_accel_forward = saved.Speed;
                    __instance.fJumpStrength = saved.Jump;
                    TuningOriginals.Remove(id);
                }
                return;
            }
            if (!TuningOriginals.TryGetValue(id, out var original))
            {
                original = (__instance, __instance.max_speed_accel_forward, __instance.fJumpStrength);
                TuningOriginals[id] = original;
            }
            p.SessionModified = true;
            __instance.max_speed_accel_forward = original.Speed * p.SpeedMultiplier.Value;
            __instance.fJumpStrength = original.Jump * p.JumpMultiplier.Value;
        }
        catch (Exception ex) { Instance!.TuningEnabled.Value = false; Instance.Log.LogError(ex); }
    }
}

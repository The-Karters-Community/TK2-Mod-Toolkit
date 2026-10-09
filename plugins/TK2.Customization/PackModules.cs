using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Reflection;

namespace TK2.Customization;

public sealed partial class Plugin
{
    internal ConfigEntry<bool> LapsEnabled = null!, SimpleDriving = null!, AutoBoost = null!, TuningEnabled = null!;
    internal ConfigEntry<int> LapCount = null!;
    internal ConfigEntry<float> SpeedMultiplier = null!, JumpMultiplier = null!;
    internal ConfigEntry<bool> HudOpacityEnabled = null!, RenderEnabled = null!;
    internal ConfigEntry<float> HudOpacity = null!, ShadowDistance = null!;
    internal ConfigEntry<bool> DisableJump = null!, DisableDrift = null!, BoostOnDriftStop = null!, SuppressEarlyBoost = null!;
    internal ConfigEntry<float> AutoBoostThreshold = null!;

    private void BindPack()
    {
        LapsEnabled = Config.Bind("Laps", "Enabled", false, "Override lap count in offline test races.");
        LapCount = Config.Bind("Laps", "Count", 3, new ConfigDescription("Laps per race.", new AcceptableValueRange<int>(1, 99)));
        SimpleDriving = Config.Bind("SimpleDriving", "Enabled", false, "Disable jumping and drifting for a driving challenge. Offline lab.");
        DisableJump = Config.Bind("SimpleDriving", "DisableJump", true, "Disable local jump input while this module is on.");
        DisableDrift = Config.Bind("SimpleDriving", "DisableDrift", true, "Disable local drift input while this module is on.");
        AutoBoost = Config.Bind("AutoBoost", "Enabled", false, "Trigger a filled drift boost just before the perfect window ends. Offline lab.");
        AutoBoostThreshold = Config.Bind("AutoBoost", "ThresholdPercent", 100f, new ConfigDescription("Percent of the best-boost fill time; one physics step is allowed for timing.", new AcceptableValueRange<float>(1,100)));
        BoostOnDriftStop = Config.Bind("AutoBoost", "BoostOnDriftStop", true, "Release a valid accumulated boost when the drift ends.");
        SuppressEarlyBoost = Config.Bind("AutoBoost", "SuppressEarlyBoost", true, "Ignore boost presses before any bar reaches the minimum valid fill time.");
        TuningEnabled = Config.Bind("Tuning", "Enabled", false, "Scale local kart speed and jump strength from captured originals. Offline lab.");
        SpeedMultiplier = Config.Bind("Tuning", "SpeedMultiplier", 1f, new ConfigDescription("Forward speed multiplier.", new AcceptableValueRange<float>(0.25f, 3f)));
        JumpMultiplier = Config.Bind("Tuning", "JumpMultiplier", 1f, new ConfigDescription("Jump strength multiplier.", new AcceptableValueRange<float>(0.25f, 3f)));
        HudOpacityEnabled = Config.Bind("HudOpacity", "Enabled", false, "Change opacity on matching HUD canvases that already have a CanvasGroup.");
        HudOpacity = Config.Bind("HudOpacity", "Opacity", 1f, new ConfigDescription("HUD alpha.", new AcceptableValueRange<float>(0.1f, 1f)));
        VignetteDisabled = Config.Bind("DisableVignette", "Enabled", false, "Disable the vignette post-processing effect without changing its profile settings.");
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
        TryFeature("auto boost", () => {
            PatchExact(typeof(Ant_BoostManager), "FixedUpdate", Type.EmptyTypes, nameof(BoostPostfix), false);
            PatchExact(typeof(PixelKartPhysics), "StopDrifting", Type.EmptyTypes, nameof(DriftStopPrefix), true);
            PatchExact(typeof(Ant_BoostManager), "BoostInput", new[] {typeof(bool)}, nameof(BoostInputPrefix), true);
        });
        TryFeature("kart tuning", () => PatchExact(typeof(PixelKartPhysics), "FixedUpdate", Type.EmptyTypes, nameof(TuningPostfix), false));
        TryFeature("disable vignette", () => PatchExact(typeof(Vignette), "IsEnabledAndSupported",
            new[] { typeof(PostProcessRenderContext) }, nameof(VignettePrefix), prefix: true));
        RecipeHost.Install(this);
        LegacyMK.BeforeAdvancedTuning = ReleaseSimpleTuning;
    }

    internal static bool OfflineLabAllowed => Instance != null && Instance.GameplayReady &&
        !Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet;

    private static bool VignettePrefix(ref bool __result)
    {
        if (Instance?.VignetteDisabled.Value != true) return true;
        __result = false;
        return false;
    }

    private static bool LocalPlayer(Ant_Player player) => player != null && player.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL;

    private static void LapsPostfix(ref int __result)
    {
        var p = Instance!;
        if (!OfflineLabAllowed || !p.LapsEnabled.Value) return;
        p.SessionModified = true;
        __result = p.LapCount.Value;
    }

    private static bool DrivingPrefix(PixelEasyCharMoveKartController __instance, MethodBase __originalMethod)
    {
        if (!OfflineLabAllowed || !Instance!.SimpleDriving.Value || !LocalPlayer(__instance.parentPlayer)) return true;
        if (__originalMethod.Name == "JumpInput" ? !Instance.DisableJump.Value : !Instance.DisableDrift.Value) return true;
        Instance.SessionModified = true;
        return false;
    }

    private static void BoostPostfix(Ant_BoostManager __instance)
    {
        if (!OfflineLabAllowed || !Instance!.AutoBoost.Value || !LocalPlayer(__instance.kartController.parentPlayer)) return;
        try
        {
            foreach (float fill in __instance.fCurrentBoostFillTime)
                if (fill >= Math.Max(__instance.fMinimumTimeForBoost, __instance.fMaximumTimeForBestBoost * Instance.AutoBoostThreshold.Value / 100 - Time.fixedDeltaTime))
                {
                    Instance.SessionModified = true;
                    __instance.BoostInput(true);
                    break;
                }
        }
        catch (Exception ex) { Instance!.AutoBoost.Value = false; Instance.Log.LogError(ex); }
    }

    private static bool ValidBoost(Ant_BoostManager boost) {
        foreach (float fill in boost.fCurrentBoostFillTime) if (fill >= boost.fMinimumTimeForBoost) return true;
        return false;
    }
    private static void DriftStopPrefix(PixelKartPhysics __instance) {
        if (!OfflineLabAllowed || !Instance!.AutoBoost.Value || !Instance.BoostOnDriftStop.Value || !LocalPlayer(__instance.kartController?.parentPlayer!)) return;
        try { if (__instance.boostManager != null && ValidBoost(__instance.boostManager)) { Instance.SessionModified = true; __instance.boostManager.BoostInput(true); } }
        catch (Exception ex) { Instance.AutoBoost.Value = false; Instance.Log.LogError(ex); }
    }
    private static bool BoostInputPrefix(Ant_BoostManager __instance, bool __0) {
        if (!__0 || !OfflineLabAllowed || !Instance!.AutoBoost.Value || !Instance.SuppressEarlyBoost.Value || !LocalPlayer(__instance.kartController?.parentPlayer!)) return true;
        try { return ValidBoost(__instance); }
        catch (Exception ex) { Instance.AutoBoost.Value = false; Instance.Log.LogError(ex); return true; }
    }

    private static readonly Dictionary<int, (PixelKartPhysics Kart, float? Speed, float? Jump)> TuningOriginals = new();
    private static void ReleaseSimpleTuning(PixelKartPhysics kart, bool speed, bool jump)
    {
        if (speed) NightmareAI.ReleaseSpeed(kart);
        int id = kart.GetInstanceID();
        if (!TuningOriginals.TryGetValue(id, out var saved)) return;
        if (speed && saved.Speed.HasValue) { kart.max_speed_accel_forward = saved.Speed.Value; saved.Speed = null; }
        if (jump && saved.Jump.HasValue) { kart.fJumpStrength = saved.Jump.Value; saved.Jump = null; }
        if (!saved.Speed.HasValue && !saved.Jump.HasValue) TuningOriginals.Remove(id);
        else TuningOriginals[id] = saved;
    }
    private static void RestoreTuning()
    {
        foreach (var saved in TuningOriginals.Values)
            if (saved.Kart != null) {
                if (saved.Speed.HasValue) saved.Kart.max_speed_accel_forward = saved.Speed.Value;
                if (saved.Jump.HasValue) saved.Kart.fJumpStrength = saved.Jump.Value;
            }
        TuningOriginals.Clear();
    }
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
                    if (saved.Speed.HasValue) __instance.max_speed_accel_forward = saved.Speed.Value;
                    if (saved.Jump.HasValue) __instance.fJumpStrength = saved.Jump.Value;
                    TuningOriginals.Remove(id);
                }
                return;
            }
            if (!TuningOriginals.TryGetValue(id, out var original))
            {
                NightmareAI.ReleaseSpeed(__instance);
                original = (__instance, null, null);
            }
            p.SessionModified = true;
            if (!LegacyMK.OverridesSpeed) {
                original.Speed ??= __instance.max_speed_accel_forward;
                __instance.max_speed_accel_forward = original.Speed.Value * p.SpeedMultiplier.Value;
            }
            if (!LegacyMK.OverridesJump) {
                original.Jump ??= __instance.fJumpStrength;
                __instance.fJumpStrength = original.Jump.Value * p.JumpMultiplier.Value;
            }
            TuningOriginals[id] = original;
        }
        catch (Exception ex) { Instance!.TuningEnabled.Value = false; Instance.Log.LogError(ex); }
    }
}

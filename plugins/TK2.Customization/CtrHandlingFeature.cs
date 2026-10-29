using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Runtime adapter for CtrFireModel. The installed native bodies and the intentional
// approximations are described in docs/CTR-HANDLING-EVIDENCE.md.
internal static class CtrHandlingFeature
{
    private static ConfigEntry<bool> Enabled = null!, ShowStatus = null!, DriftReserves = null!, PadReserves = null!, LandingReserves = null!, BrakeClears = null!;
    private static ConfigEntry<float> FireSpeedScale = null!, ReserveGain = null!, ReserveDecay = null!;
    private sealed class Entry
    {
        internal readonly CtrFireModel Fire = new();
        internal Ant_BoostManager Boost = null!;
        internal float LastWritten;
        internal bool WroteReserve;
    }
    private static readonly Dictionary<int, Entry> States = new();
    private static string Status = "CTR handling: waiting for a local kart";

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("CtrHandling", "Enabled", false, "Offline CTR-inspired reserves and fire speed.");
        ShowStatus = p.Config.Bind("CtrHandling", "ShowStatus", true, "Show fire and reserve status in offline races.");
        FireSpeedScale = p.Config.Bind("CtrHandling", "FireSpeedScale", 1f, "Scale the CTR fire contribution to TK2's speed; 1 uses the CTR Speed-class ratio.");
        DriftReserves = p.Config.Bind("CtrHandling", "DriftReserves", true, "Add reserves from manual drift boosts.");
        PadReserves = p.Config.Bind("CtrHandling", "PadReserves", true, "Add reserves from boost pads and boosters.");
        LandingReserves = p.Config.Bind("CtrHandling", "LandingReserves", true, "Add reserves from qualified jump landings.");
        BrakeClears = p.Config.Bind("CtrHandling", "BrakeClears", true, "Braking clears fire and reserves.");
        ReserveGain = p.Config.Bind("CtrHandling", "ReserveGain", 1f, "Multiplier for reserve gained from boosts.");
        ReserveDecay = p.Config.Bind("CtrHandling", "ReserveDecay", 1f, "Multiplier for reserve consumption over time.");
        FeatureHook.Install(p.Harmony, typeof(CtrHandlingFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(Ant_BoostManager), "FireSliderBoost", new[] { typeof(int) },
            Prefix: nameof(SliderPrefix), Postfix: nameof(SliderPostfix)),
        new FeatureHook(typeof(Ant_BoostManager), "FireBoost",
            new[] { typeof(Ant_BoostManager.EBoostType), typeof(float), typeof(float), typeof(float) }, Postfix: nameof(BoostPostfix)),
        new FeatureHook(typeof(Ant_BoostManager), "OnKartLandedAfterPlayerTriggeredJump", new[] { typeof(bool) },
            Prefix: nameof(LandingPrefix), Postfix: nameof(LandingPostfix)),
        new FeatureHook(typeof(Ant_BoostManager), "FixedUpdate", Type.EmptyTypes, Postfix: nameof(FixedPostfix)),
        new FeatureHook(typeof(Ant_BoostManager), "GetCurrentBoostMaxSpeedIncrease", Type.EmptyTypes, Postfix: nameof(SpeedPostfix)),
        new FeatureHook(typeof(PlayerRaceLogic), "OnRaceResetted", Type.EmptyTypes, Postfix: nameof(Reset))
    };

    private static bool Allowed(Ant_BoostManager boost) => Enabled?.Value == true &&
        boost != null && AirShiftFeature.Running(boost.kartController) &&
        boost.kartController.parentPlayer.playerRaceLogic?.bPlayerEndRaceEventTriggered == false;

    private static Entry State(Ant_BoostManager boost)
    {
        int id = boost.GetInstanceID();
        if (!States.TryGetValue(id, out var state)) States[id] = state = new Entry { Boost = boost };
        return state;
    }

    private static void SliderPrefix(Ant_BoostManager __instance, int __0, out float __state)
    {
        __state = -1f;
        try {
            if (!Allowed(__instance) || __0 < 0 || __0 > 2 || __instance.fCurrentBoostFillTime == null ||
                __0 >= __instance.fCurrentBoostFillTime.Length) return;
            __state = CtrFireModel.DriftGain(__instance.fCurrentBoostFillTime[__0],
                __instance.fMinimumTimeForBoost, __instance.fMaximumTimeForBestBoost);
        } catch (Exception ex) { Fail(ex); }
    }

    private static void SliderPostfix(Ant_BoostManager __instance, int __0, float __state)
    {
        try {
            if (__state < 0 || !Allowed(__instance) || !DriftReserves.Value) return;
            var state = State(__instance);
            state.Fire.Add(__state * Scale(ReserveGain), 8f + 2f * __0, false);
            Sync(__instance, state);
            Plugin.Instance!.Log.LogInfo($"CTR drift boost {__0 + 1}: +{__state:0.00}s, reserves {state.Fire.Reserves:0.00}s, fire {state.Fire.FireCap:0}");
        } catch (Exception ex) { Fail(ex); }
    }

    private static void BoostPostfix(Ant_BoostManager __instance, Ant_BoostManager.EBoostType __0, float __3)
    {
        try {
            if (!Allowed(__instance) || !PadReserves.Value) return;
            // FireSliderBoost and the landing callback own their events. A nested
            // FireBoost must not award their reserves a second time.
            if (__0 != Ant_BoostManager.EBoostType.EBOOST_BOOSTPAD &&
                __0 != Ant_BoostManager.EBoostType.EBOOST_BOOSTER) return;
            var state = State(__instance);
            if (__0 == Ant_BoostManager.EBoostType.EBOOST_BOOSTPAD)
                state.Fire.Add(Scale(ReserveGain), 16f, false, freezePad: true);
            else
                state.Fire.Add(Math.Max(0f, __3) * Scale(ReserveGain), 12f, false, freezePad: true);
            Sync(__instance, state);
            Plugin.Instance!.Log.LogInfo($"CTR {__0}: reserves {state.Fire.Reserves:0.00}s, fire {state.Fire.FireCap:0}");
        } catch (Exception ex) { Fail(ex); }
    }

    private static void LandingPrefix(Ant_BoostManager __instance, out float __state)
    {
        __state = 0;
        try { if (Allowed(__instance)) __state = __instance.kartController.kartPhysics.fTimeInAir; }
        catch (Exception ex) { Fail(ex); }
    }

    private static void LandingPostfix(Ant_BoostManager __instance, float __state)
    {
        try {
            if (!Allowed(__instance) || !LandingReserves.Value || __state < 2f / 3f) return;
            float fire = __state >= 1.5f ? 16f : __state >= 1f ? 12f : 10f;
            var state = State(__instance);
            state.Fire.Add(Scale(ReserveGain), fire, false);
            Sync(__instance, state);
            Plugin.Instance!.Log.LogInfo($"CTR landing: {__state:0.00}s airtime, +1.00s reserves, fire {state.Fire.FireCap:0}");
        } catch (Exception ex) { Fail(ex); }
    }

    private static void FixedPostfix(Ant_BoostManager __instance)
    {
        try {
            if (Enabled?.Value != true || Time.timeScale <= 0 || MenuManager.Instance != null) return;
            if (!Allowed(__instance)) { if (__instance != null) Drop(__instance.GetInstanceID()); return; }
            var state = State(__instance);
            var physics = __instance.kartController.kartPhysics;
            if (BrakeClears.Value && physics.bIsBreakingInput) state.Fire.Clear();
            else state.Fire.Tick(Time.fixedDeltaTime * Scale(ReserveDecay), false);
            Sync(__instance, state);
        } catch (Exception ex) { Fail(ex); }
    }

    private static void SpeedPostfix(Ant_BoostManager __instance, ref float __result)
    {
        try {
            if (!Allowed(__instance) || !States.TryGetValue(__instance.GetInstanceID(), out var state)) return;
            float scale = FireSpeedScale.Value;
            if (!float.IsFinite(scale) || scale < 0) throw new ArgumentException("Fire speed scale must be finite and non-negative.");
            float baseSpeed = Math.Max(0, __instance.kartController.kartPhysics.max_speed_accel_forward);
            float fireSpeed = state.Fire.Reserves > 0 ? baseSpeed * state.Fire.FireCap / 54f * scale : 0f;
            if (!float.IsFinite(baseSpeed) || !float.IsFinite(fireSpeed))
                throw new ArgumentException("CTR fire speed must remain finite.");
            // Preserve native item power-up speed without adding it to CTR fire.
            float booster = __instance.fCurrentConstBoostingTimeLeft_PowerUpBooster > 0 ? __instance.fPowerUp_Booster_MaxSpeedIncrease : 0f;
            float star = __instance.fCurrentConstBoostingTimeLeft_PowerUpStar > 0 ? __instance.fPowerUp_Star_MaxSpeedIncrease : 0f;
            __result = Math.Max(fireSpeed, Math.Max(booster, star));
        } catch (Exception ex) { Fail(ex); }
    }

    internal static bool TryGetFire(Ant_Player player, out float fire, out bool super)
    {
        fire = 0; super = false;
        foreach (var state in States.Values) {
            var boost = state.Boost;
            if (boost != null && Allowed(boost) && boost.kartController.parentPlayer == player && state.Fire.Reserves > 0) {
                fire = state.Fire.FireCap;
                return true;
            }
        }
        return false;
    }

    private static float Scale(ConfigEntry<float> setting) =>
        float.IsFinite(setting.Value) ? Math.Clamp(setting.Value, 0f, 10f) : 1f;

    private static void Sync(Ant_BoostManager boost, Entry state)
    {
        boost.fBoostingReserves = state.Fire.Reserves;
        state.LastWritten = state.Fire.Reserves;
        state.WroteReserve = true;
        Plugin.Instance!.SessionModified = true;
        Status = $"CTR fire {state.Fire.FireCap:0}   |   reserves {state.Fire.Reserves:0.0}s";
    }

    internal static void Tick()
    {
        if (Enabled?.Value != true || !Plugin.OfflineLabAllowed)
            Reset();
    }
    private static void ClearOwned(Entry state)
    {
        // The game's reserve can change between callbacks. Do not overwrite a
        // newer native or third-party value, or write to an online/replay kart.
        try {
            var boost = state.Boost;
            if (!Plugin.OfflineLabAllowed || !state.WroteReserve || boost == null || boost.kartController == null ||
                boost.kartController.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL ||
                boost.kartController.IsPlayingReplayData()) return;
            float current = boost.fBoostingReserves;
            if (Math.Abs(current - state.LastWritten) <= .0001f) boost.fBoostingReserves = 0f;
        } catch (Exception ex) { Plugin.Instance?.Log.LogWarning($"CTR reserve cleanup skipped: {ex.Message}"); }
    }
    private static void Drop(int id)
    {
        if (!States.TryGetValue(id, out var state)) return;
        ClearOwned(state); States.Remove(id);
    }
    internal static void Forget(Ant_BoostManager boost)
    {
        if (boost != null) Drop(boost.GetInstanceID());
    }
    internal static void Reset()
    {
        foreach (var state in States.Values) ClearOwned(state);
        States.Clear(); Status = "CTR handling: waiting for a local kart";
    }
    internal static void DrawStatus()
    {
        if (Enabled?.Value == true && ShowStatus.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 178, 1000, 28), Status + "   |   " + CtrPhysicsFeature.SlopeStatus);
    }
    private static void Fail(Exception ex)
    {
        if (Enabled != null) Enabled.Value = false;
        Reset();
        Plugin.Instance?.Log.LogError($"CTR handling stopped after an actual error: {ex.Message}");
    }
}

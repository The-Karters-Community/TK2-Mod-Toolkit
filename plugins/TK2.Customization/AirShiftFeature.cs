using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

internal static class AirShiftFeature
{
    private static ConfigEntry<bool> Enabled = null!, OncePerJump = null!, LimitSpeed = null!, GroundTesting = null!, ShowStatus = null!;
    private static ConfigEntry<KeyCode> Left = null!, Right = null!;
    private static ConfigEntry<string> LeftAction = null!, RightAction = null!;
    private static ConfigEntry<float> Strength = null!, Retention = null!, Duration = null!;
    private sealed class Shift {
        internal bool Used, Active, WasGrounded;
        internal int Pending;
        internal float Expires, TargetLateral, Budget;
        internal double Until;
        internal Vector3 Right;
    }
    private static readonly Dictionary<int, Shift> States = new();
    private static string Status = "Air Shift: velocity hook unavailable; check the plugin log";

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("AirShift", "Enabled", false, "Offline lateral shift for airborne driving and optional ground testing.");
        Left = p.Config.Bind("AirShift", "LeftKey", KeyCode.LeftBracket, "Shift left while airborne or ground testing is enabled.");
        Right = p.Config.Bind("AirShift", "RightKey", KeyCode.RightBracket, "Shift right while airborne or ground testing is enabled.");
        LeftAction = p.Config.Bind("AirShift", "LeftAction", "", "Selected physical gamepad button or existing Rewired action.");
        RightAction = p.Config.Bind("AirShift", "RightAction", "", "Selected physical gamepad button or existing Rewired action.");
        Strength = p.Config.Bind("AirShift", "SidewaysImpulse", 35f, "Lateral velocity change (m/s), maintained during the shift. Suggested 25-45; no upper limit.");
        Duration = p.Config.Bind("AirShift", "ShiftDuration", .4f, "Seconds to maintain the lateral correction. Suggested 0.3-0.6; no upper limit. Zero is an impulse.");
        Retention = p.Config.Bind("AirShift", "SpeedRetention", 1f, "Horizontal speed budget multiplier. 1 preserves initial speed, 0.9 slows slightly, above 1 permits more speed. No upper limit.");
        OncePerJump = p.Config.Bind("AirShift", "OncePerJump", true, "Disable to experiment with repeated shifts during the same jump.");
        LimitSpeed = p.Config.Bind("AirShift", "LimitHorizontalSpeed", false, "Apply the initial horizontal speed budget. Disable for unrestricted lateral additions.");
        GroundTesting = p.Config.Bind("AirShift", "AllowGroundTesting", true, "Also allow lateral shifts while grounded for practice and tuning.");
        ShowStatus = p.Config.Bind("AirShift", "ShowStatus", true, "Show ready/spent/active state and applied lateral speed while testing.");
        FeatureHook.Install(p.Harmony, typeof(AirShiftFeature), Hooks());
        Status = "Air Shift: waiting for a local kart";
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(Ant_KartInput), nameof(Ant_KartInput.ProcessRacingInput), Type.EmptyTypes, Postfix: nameof(InputPostfix)),
        // Final KCC controller boundary, after native damping.
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_UpdateVelocity),
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Postfix: nameof(VelocityPostfix)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(Reset))
    };

    internal static bool Running(PixelEasyCharMoveKartController? kart) => BlockReason(kart) == null;
    private static string? BlockReason(PixelEasyCharMoveKartController? kart)
    {
        if (!Plugin.OfflineLabAllowed) return "offline mode required";
        if (kart?.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return "waiting for a local kart";
        if (Time.timeScale <= 0) return "paused";
        if (MenuManager.Instance != null) return "menu open";
        if (Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) return "race not running";
        if (kart.IsPlayingReplayData()) return "replay playback";
        if (kart.outsideMapValidator == null || kart.outsideMapValidator.bIsPlayerRespawning) return "recovery in progress";
        if (kart.kartPhysics.IsTeleportingFromAtoB()) return "teleport in progress";
        if (kart.bIsInTankForm) return "tank form active";
        return null;
    }

    private static Shift State(PixelKartPhysics physics)
    {
        int id = physics.GetInstanceID();
        if (!States.TryGetValue(id, out var state)) States[id] = state = new Shift();
        return state;
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Enabled.Value) { if (States.Count > 0) Reset(); return; }
        try {
            var kart = __instance.boostManager?.kartController;
            string? blocked = BlockReason(kart);
            if (blocked != null) { Status = "Air Shift: " + blocked; return; }
            var state = State(kart!.kartPhysics);
            bool grounded = kart.Motor.GroundingStatus.IsStableOnGround;
            state.WasGrounded = grounded;
            if (grounded && !GroundTesting.Value) { Status = "Air Shift: grounded (ground testing off)"; return; }
            bool left = Input.GetKeyDown(Left.Value) || ShortcutInput.Read(__instance, LeftAction.Value);
            bool right = Input.GetKeyDown(Right.Value) || ShortcutInput.Read(__instance, RightAction.Value);
            if ((OncePerJump.Value && state.Used) || left == right) return;
            state.Pending = left ? -1 : 1; state.Expires = Time.unscaledTime + .2f;
        } catch (Exception ex) { Stop(ex); }
    }

    private static void VelocityPostfix(PixelEasyCharMoveKartController __instance, ref Vector3 __0)
    {
        if (!Enabled.Value) return;
        try {
            var kart = __instance;
            if (kart.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            var physics = kart.kartPhysics; var state = State(physics);
            bool grounded = kart.Motor.GroundingStatus.IsStableOnGround;
            if (state.WasGrounded && !grounded) state.Used = false;
            if (!state.WasGrounded && grounded) { state.Used = false; state.Active = false; state.Pending = 0; }
            state.WasGrounded = grounded;
            if (grounded && !GroundTesting.Value) { state.Active = false; state.Pending = 0; Status = "Air Shift: grounded (ground testing off)"; return; }
            string? blocked = BlockReason(kart);
            if (blocked != null) { state.Active = false; state.Pending = 0; Status = "Air Shift: " + blocked; return; }
            if (state.Pending != 0 && Time.unscaledTime <= state.Expires && (!OncePerJump.Value || !state.Used)) {
                if (!float.IsFinite(Strength.Value) || Strength.Value < 0 || !float.IsFinite(Duration.Value) || Duration.Value < 0 ||
                    !float.IsFinite(Retention.Value) || Retention.Value < 0) throw new ArgumentException("Air Shift settings must be finite, non-negative numbers.");
                state.Right = physics.GetMainMotorSteeringOrientation() * Vector3.right;
                state.Right.y = 0; state.Right.Normalize();
                state.TargetLateral = Vector3.Dot(__0, state.Right) + state.Pending * Strength.Value;
                state.Budget = new Vector2(__0.x, __0.z).magnitude * Retention.Value;
                state.Until = (double)Time.time + Duration.Value;
                state.Active = true;
                if (!grounded) state.Used = true;
                state.Pending = 0;
                Plugin.Instance!.Log.LogInfo($"Air Shift accepted: lateral target {state.TargetLateral:0.0} m/s, duration {Duration.Value:0.00}s, speed budget {(LimitSpeed.Value ? state.Budget.ToString("0.0") : "unlimited")}.");
            } else if (Time.unscaledTime > state.Expires) state.Pending = 0;
            if (!state.Active) { Status = grounded ? "Air Shift: ground test ready" : OncePerJump.Value && state.Used ? "Air Shift: spent until landing" : "Air Shift: ready"; return; }
            var adjusted = AirShiftMath.Hold(__0.x, __0.z, state.Right.x, state.Right.z, state.TargetLateral, state.Budget, LimitSpeed.Value);
            Plugin.Instance!.SessionModified = true;
            __0 = new Vector3(adjusted.X, __0.y, adjusted.Z);
            physics.vLastVelocity = __0;
            Status = $"Air Shift: {(grounded ? "ground test" : "active")} — lateral {Vector3.Dot(__0, state.Right):0.0} m/s";
            if (Time.time >= state.Until) state.Active = false;
        } catch (Exception ex) { Stop(ex); }
    }

    internal static bool Used(PixelKartPhysics physics) => State(physics).Used;
    internal sealed record SavedShift(bool Used, bool Active, double Remaining, Vector3 Right, float Target, float Budget);
    internal static SavedShift Capture(PixelKartPhysics physics) {
        var state = State(physics);
        return new(state.Used, state.Active, Math.Max(0, state.Until - Time.time), state.Right, state.TargetLateral, state.Budget);
    }
    internal static void Restore(PixelKartPhysics physics, SavedShift saved) {
        var state = State(physics); state.Used = saved.Used; state.Active = saved.Active && Enabled.Value;
        state.Until = Time.time + saved.Remaining; state.Right = saved.Right;
        state.TargetLateral = saved.Target; state.Budget = saved.Budget; state.Pending = 0;
    }
    internal static void RestoreUsed(PixelKartPhysics physics, bool used) { var state = State(physics); state.Used = used; state.Active = false; state.Pending = 0; }
    internal static void DrawStatus() {
        if (Enabled != null && Enabled.Value && ShowStatus.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 72, 850, 28), Status);
    }
    private static void Stop(Exception ex) { Enabled.Value = false; Reset(); Plugin.Instance?.Log.LogError($"Air Shift stopped after an actual error: {ex.Message}"); }
    internal static void Reset() => States.Clear();
}

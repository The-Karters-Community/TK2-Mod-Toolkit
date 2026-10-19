using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

internal static class AirShiftFeature
{
    private static ConfigEntry<bool> Enabled = null!, OncePerJump = null!, ShowStatus = null!;
    private static ConfigEntry<KeyCode> Left = null!, Right = null!;
    private static ConfigEntry<string> LeftAction = null!, RightAction = null!;
    private static ConfigEntry<float> Impulse = null!, Duration = null!;
    private sealed class Dash {
        internal bool Used, Active;
        internal double Until;
        internal float TargetLateral, SpeedBudget;
        internal Vector3 Right;
    }
    private static readonly Dictionary<int, Dash> States = new();
    private static string Status = "Air Dash: waiting for a local kart";

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("AirShift", "Enabled", false, "Offline lateral burst while airborne.");
        Left = p.Config.Bind("AirShift", "LeftKey", KeyCode.LeftBracket, "Dash left while airborne.");
        Right = p.Config.Bind("AirShift", "RightKey", KeyCode.RightBracket, "Dash right while airborne.");
        LeftAction = p.Config.Bind("AirShift", "LeftAction", "", "Selected physical gamepad button or existing Rewired action.");
        RightAction = p.Config.Bind("AirShift", "RightAction", "", "Selected physical gamepad button or existing Rewired action.");
        Impulse = p.Config.Bind("AirShift", "SidewaysImpulse", 65f, "Added lateral velocity in metres per second before total horizontal speed is conserved. Suggested 65; no upper limit.");
        Duration = p.Config.Bind("AirShift", "ShiftDuration", .25f, "How long the lateral burst is held. Suggested 0.25 seconds; no upper limit.");
        OncePerJump = p.Config.Bind("AirShift", "OncePerJump", true, "Disable to allow repeated dashes before landing.");
        ShowStatus = p.Config.Bind("AirShift", "ShowStatus", true, "Show grounded/ready/spent/dash status while testing.");
        FeatureHook.Install(p.Harmony, typeof(AirShiftFeature), Hooks());
        Status = "Air Dash: waiting for a local kart";
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(Ant_KartInput), nameof(Ant_KartInput.ProcessRacingInput), Type.EmptyTypes, Postfix: nameof(InputPostfix)),
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

    private static Dash State(PixelKartPhysics physics)
    {
        int id = physics.GetInstanceID();
        if (!States.TryGetValue(id, out var state)) States[id] = state = new Dash();
        return state;
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Enabled.Value) return;
        try {
            var kart = __instance.boostManager?.kartController;
            // Bot callbacks must not overwrite the local player's status line.
            if (kart?.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            var state = State(kart.kartPhysics);
            if (kart.Motor.GroundingStatus.IsStableOnGround) {
                state.Used = false; state.Active = false; Status = "Air Dash: grounded — ready on next jump"; return;
            }
            string? blocked = BlockReason(kart);
            if (blocked != null) { state.Active = false; Status = "Air Dash: " + blocked; return; }
            bool left = Input.GetKeyDown(Left.Value) || ShortcutInput.Read(__instance, LeftAction.Value);
            bool right = Input.GetKeyDown(Right.Value) || ShortcutInput.Read(__instance, RightAction.Value);
            if (left == right) return;
            if (OncePerJump.Value && state.Used) { Status = "Air Dash: spent until landing"; return; }
            if (!float.IsFinite(Impulse.Value) || Impulse.Value <= 0 ||
                !float.IsFinite(Duration.Value) || Duration.Value <= 0)
                throw new ArgumentException("Air Dash lateral impulse and duration must be finite and positive.");

            Vector3 axis = kart.kartPhysics.GetMainMotorSteeringOrientation() * Vector3.right;
            axis.y = 0;
            if (axis.sqrMagnitude < .001f) throw new InvalidOperationException("Kart lateral direction is unavailable.");
            axis.Normalize();
            Vector3 velocity = kart.Motor.Velocity;
            state.Right = axis;
            state.TargetLateral = AirDashMath.TargetLateral(Vector3.Dot(velocity, axis), left ? -1 : 1, Impulse.Value);
            state.SpeedBudget = new Vector2(velocity.x, velocity.z).magnitude;
            state.Until = Time.time + Duration.Value;
            state.Active = true; state.Used = true;

            Plugin.Instance!.SessionModified = true;
            Status = $"Air Dash: {(left ? "left" : "right")} — {Impulse.Value:0.0} m/s for {Duration.Value:0.00}s";
            Plugin.Instance.Log.LogInfo(Status + "; heading unchanged and horizontal speed capped at its pre-dash value.");
        } catch (Exception ex) { Stop(ex); }
    }

    // The native KCC controller has already applied kart damping at this boundary.
    private static void VelocityPostfix(PixelEasyCharMoveKartController __instance, ref Vector3 __0)
    {
        if (!Enabled.Value || __instance.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
        try {
            var kart = __instance; var physics = kart.kartPhysics; var state = State(physics);
            if (kart.Motor.GroundingStatus.IsStableOnGround) {
                state.Used = false; state.Active = false; Status = "Air Dash: grounded — ready on next jump"; return;
            }
            string? blocked = BlockReason(kart);
            if (blocked != null) { state.Active = false; Status = "Air Dash: " + blocked; return; }
            if (!state.Active) {
                if (OncePerJump.Value && state.Used) Status = "Air Dash: spent until landing";
                return;
            }
            if (Time.time >= state.Until) { state.Active = false; Status = OncePerJump.Value ? "Air Dash: spent until landing" : "Air Dash: ready"; return; }
            var adjusted = AirDashMath.Hold(__0.x, __0.z, state.Right.x, state.Right.z, state.TargetLateral, state.SpeedBudget);
            __0 = new Vector3(adjusted.X, __0.y, adjusted.Z);
            physics.vLastVelocity = __0;
            Plugin.Instance!.SessionModified = true;
        } catch (Exception ex) { Stop(ex); }
    }

    internal static bool Used(PixelKartPhysics physics) => State(physics).Used;
    internal sealed record SavedShift(bool Used, bool Active, double Remaining, Vector3 Right, float Target, float Budget);
    internal static SavedShift Capture(PixelKartPhysics physics) {
        var state = State(physics); return new(state.Used, state.Active, Math.Max(0, state.Until - Time.time), state.Right, state.TargetLateral, state.SpeedBudget);
    }
    internal static void Restore(PixelKartPhysics physics, SavedShift saved) {
        var state = State(physics); state.Used = saved.Used; state.Active = saved.Active && Enabled.Value;
        state.Until = Time.time + saved.Remaining; state.Right = saved.Right; state.TargetLateral = saved.Target; state.SpeedBudget = saved.Budget;
    }
    internal static void RestoreUsed(PixelKartPhysics physics, bool used) { var state = State(physics); state.Used = used; state.Active = false; }
    internal static void DrawStatus()
    {
        if (Enabled != null && Enabled.Value && ShowStatus.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 72, 850, 28), Status);
    }
    private static void Stop(Exception ex)
    {
        Enabled.Value = false; Reset();
        Plugin.Instance?.Log.LogError($"Air Dash stopped after an actual error: {ex.Message}");
    }
    internal static void Reset() => States.Clear();
}

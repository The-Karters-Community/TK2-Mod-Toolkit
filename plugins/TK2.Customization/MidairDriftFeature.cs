using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

internal static class MidairDriftFeature
{
    private static ConfigEntry<bool> Enabled = null!, ShowStatus = null!;
    private sealed class Entry
    {
        internal readonly MidairDriftState Input = new();
        internal readonly PixelKartPhysics Physics;
        internal Ant_KartInput? Controls;
        internal Entry(PixelKartPhysics physics) { Physics = physics; }
    }
    private static readonly Dictionary<int, Entry> States = new();
    private static string Status = "Mid-air jump drift: ready";

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("MidairDrift", "Enabled", false, "Offline buffered jump drift with automatic native trick eligibility and landing boost.");
        ShowStatus = p.Config.Bind("MidairDrift", "ShowStatus", true, "Show when a mid-air drift or its landing trick is ready.");
        FeatureHook.Install(p.Harmony, typeof(MidairDriftFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(Ant_KartInput), nameof(Ant_KartInput.ProcessRacingInput), Type.EmptyTypes, Postfix: nameof(InputPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), nameof(PixelKartPhysics.CheckAndDecideIfAllowedToJumpInPhysics),
            new[] { typeof(float) }, Prefix: nameof(JumpPrefix), Postfix: nameof(JumpPostfix), Finalizer: nameof(JumpFinalizer)),
        new FeatureHook(typeof(PixelKartPhysics), nameof(PixelKartPhysics.DetectJumpTrick), new[] { typeof(float) },
            Prefix: nameof(TrickPrefix), Finalizer: nameof(TrickFinalizer)),
        new FeatureHook(typeof(Ant_BoostManager), nameof(Ant_BoostManager.OnKartLandedAfterPlayerTriggeredJump),
            new[] { typeof(bool) }, Postfix: nameof(LandedPostfix)),
        new FeatureHook(typeof(Ant_CharacterWorker), nameof(Ant_CharacterWorker.Update), Type.EmptyTypes,
            Prefix: nameof(PosePrefix), Finalizer: nameof(PoseFinalizer)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(Reset))
    };

    private static bool Allowed(PixelKartPhysics physics) => Enabled.Value && AirShiftFeature.Running(physics.kartController) &&
        physics.kartController.parentPlayer.playerRaceLogic?.bPlayerEndRaceEventTriggered == false &&
        !(Plugin.Instance!.SimpleDriving.Value && (Plugin.Instance.DisableJump.Value || Plugin.Instance.DisableDrift.Value));

    private static int Id(PixelKartPhysics physics) => physics.kartController.parentPlayer.GetInstanceID();
    private static bool Held(Entry state) => state.Controls?.player != null && state.Input.Button.Length > 0 &&
        state.Controls.player.GetButton(state.Input.Button);

    private static void InputPostfix(Ant_KartInput __instance)
    {
        var physics = __instance.boostManager?.kartController?.kartPhysics;
        if (physics == null || physics.kartController.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
        try {
            if (!Allowed(physics)) { Forget(physics); return; }
            int id = Id(physics);
            if (!States.TryGetValue(id, out var state)) States[id] = state = new Entry(physics);
            state.Controls = __instance;
            if (state.Input.Active) {
                Status = physics.bAlreadyMadeTrick || physics.bIsPlayerMadeTrickBeforeLanding
                    ? "Mid-air jump drift: landing boost ready" : "Mid-air jump drift: trick armed";
            }
            var player = __instance.player;
            if (player == null) return;
            // Both native shoulder routes can initiate a hop; the opposite shoulder during a drift is a mini-turbo.
            foreach (string button in new[] { "Jump", "Boost", "Drift_left_right" }) {
                if (!player.GetButtonDown(button)) continue;
                state.Input.Press(Time.time, Time.frameCount, button, physics.IsDrifting());
                break;
            }
        } catch (Exception ex) { Fail(ex); }
    }

    private static void JumpPrefix(PixelKartPhysics __instance, out bool __state)
    {
        __state = false;
        try {
            if (!Allowed(__instance) || !States.TryGetValue(Id(__instance), out var state)) return;
            var p = __instance;
            if (p.bIsMovingBackward || !MidairDriftState.CanDrift(p.GetSteerInput(), p.kartController.Motor.Velocity.magnitude, p.fMinSpeedForDrifting) ||
                !state.Input.CanJump(Time.time, Held(state), p.kartController.IsGrounded_Now(),
                    p.eJumpState == PixelKartPhysics.EJumpState.E_JUMPED_WAITING_FOR_GROUNDED, p.fTimeSinceNotGrounded)) return;
            __state = !p.bPlayerJumpedWhenGrounedOrJustAfterMissedGround_RunJump;
            p.bPlayerJumpedWhenGrounedOrJustAfterMissedGround_RunJump = true;
        } catch (Exception ex) { Fail(ex); }
    }

    private static void JumpPostfix(PixelKartPhysics __instance)
    {
        try {
            if (!Allowed(__instance) || !__instance.bTriggerJumpInPhysicsStep ||
                !States.TryGetValue(Id(__instance), out var state)) return;
            var p = __instance;
            if (p.bIsMovingBackward || !state.Input.Accept(Time.time, Held(state), p.GetSteerInput(), p.kartController.Motor.Velocity.magnitude, p.fMinSpeedForDrifting)) return;
            // Current native helper sets drift direction, resets its timer and starts drifting; it does not start replay playback.
            float previousForcedDirection = p.fForcedDriftDirection;
            try { p.ForceDriftSteerDirection_Replay_OverrideDriftDir(Math.Sign(p.GetSteerInput()), true); }
            finally { p.fForcedDriftDirection = previousForcedDirection; }
            p.bIsDriftInput = true;
            Plugin.Instance!.SessionModified = true;
            Status = "Mid-air jump drift: sliding — trick armed";
            Plugin.Instance.Log.LogInfo(Status);
        } catch (Exception ex) { Fail(ex); }
    }

    private static Exception? JumpFinalizer(PixelKartPhysics __instance, bool __state, Exception? __exception)
    {
        // Never leave an unconsumed request behind when native landing cooldown rejects it.
        if (__state) __instance.bPlayerJumpedWhenGrounedOrJustAfterMissedGround_RunJump = false;
        return __exception;
    }

    private static void TrickPrefix(PixelKartPhysics __instance, out float? __state)
    {
        __state = null;
        try {
            if (!Allowed(__instance) || !States.TryGetValue(Id(__instance), out var state) || !state.Input.Active ||
                __instance.kartController.IsGrounded_Now() || __instance.bAlreadyMadeTrick) return;
            // Keep the accepted hop armed throughout the flight, retaining native height tests and its real jump timestamp.
            __state = __instance.fAllowTrickAfterJumpClicked_ForTime;
            __instance.fAllowTrickAfterJumpClicked_ForTime = float.MaxValue;
            __instance.bPendingTrickFromAcceptedJump = true;
        } catch (Exception ex) { Fail(ex); }
    }

    private static Exception? TrickFinalizer(PixelKartPhysics __instance, float? __state, Exception? __exception)
    {
        if (__state.HasValue) __instance.fAllowTrickAfterJumpClicked_ForTime = __state.Value;
        return __exception;
    }

    private static void LandedPostfix(Ant_BoostManager __instance, bool __0)
    {
        if (Enabled?.Value != true || __instance.kartController?.parentPlayer == null ||
            !States.TryGetValue(__instance.kartController.parentPlayer.GetInstanceID(), out var state) || !state.Input.Active) return;
        state.Input.Land();
        Status = __0 ? "Mid-air jump drift: native landing boost" : "Mid-air jump drift: landed below trick height";
        Plugin.Instance?.Log.LogInfo(Status);
    }

    private static void PosePrefix(Ant_CharacterWorker __instance, out bool? __state)
    {
        __state = null;
        try {
            var visual = __instance.visualInstanceParams;
            if (Enabled?.Value != true || visual?.parentPlayer == null || visual.bIsPlayerGrouned ||
                !States.TryGetValue(visual.parentPlayer.GetInstanceID(), out var state) || !state.Input.Active || !Allowed(state.Physics)) return;
            // Only the character animation reads this temporary value. Kart tilt, boost sparks and HUD retain the actual drift.
            __state = visual.bIsDrifting;
            visual.bIsDrifting = false;
        } catch (Exception ex) { Fail(ex); }
    }

    private static Exception? PoseFinalizer(Ant_CharacterWorker __instance, bool? __state, Exception? __exception)
    {
        if (__state.HasValue && __instance.visualInstanceParams != null) __instance.visualInstanceParams.bIsDrifting = __state.Value;
        return __exception;
    }

    internal static void Tick()
    {
        if (Enabled == null) return;
        try {
            foreach (var state in States.Values)
                if (state.Physics == null || !Allowed(state.Physics)) state.Input.Clear();
        } catch (Exception ex) { Fail(ex); }
    }

    internal static void Forget(PixelKartPhysics physics)
    {
        if (physics.kartController?.parentPlayer != null) States.Remove(Id(physics));
    }
    internal static void Reset() { States.Clear(); Status = "Mid-air jump drift: ready"; }
    internal static void DrawStatus()
    {
        if (Enabled?.Value == true && ShowStatus.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 148, 850, 28), Status);
    }
    private static void Fail(Exception ex)
    {
        Enabled.Value = false; Reset();
        Plugin.Instance?.Log.LogError($"Mid-air jump drift disabled after an actual error: {ex.Message}");
    }
}

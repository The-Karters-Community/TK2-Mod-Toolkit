using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

internal static class AirShiftFeature
{
    private static ConfigEntry<bool> Enabled = null!;
    private static ConfigEntry<KeyCode> Left = null!, Right = null!;
    private static ConfigEntry<string> LeftAction = null!, RightAction = null!;
    private static ConfigEntry<float> Strength = null!, Retention = null!;
    private sealed class Shift { internal bool Used; internal int Direction; internal float Expires; }
    private static readonly Dictionary<int, Shift> States = new();

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("AirShift", "Enabled", false, "Experimental offline lateral air adjustment, once per jump. Runtime validation pending.");
        Left = p.Config.Bind("AirShift", "LeftKey", KeyCode.LeftBracket, "Shift left while airborne.");
        Right = p.Config.Bind("AirShift", "RightKey", KeyCode.RightBracket, "Shift right while airborne.");
        LeftAction = p.Config.Bind("AirShift", "LeftAction", "", "Optional existing Rewired action; blank uses keyboard only.");
        RightAction = p.Config.Bind("AirShift", "RightAction", "", "Optional existing Rewired action; blank uses keyboard only.");
        Strength = p.Config.Bind("AirShift", "SidewaysImpulse", 6f, new ConfigDescription("Maximum lateral velocity impulse (m/s).", new AcceptableValueRange<float>(1, 15)));
        Retention = p.Config.Bind("AirShift", "SpeedRetention", 1f, new ConfigDescription("Horizontal speed cap relative to speed before shifting.", new AcceptableValueRange<float>(.9f, 1)));
        Patch(p, typeof(Ant_KartInput), "ProcessRacingInput", Type.EmptyTypes, nameof(InputPostfix));
        Patch(p, typeof(PixelKartPhysics), "UpdateVelocity", new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, nameof(VelocityPostfix));
        Patch(p, typeof(PlayerRaceLogic), "OnRaceResetted", Type.EmptyTypes, nameof(Reset));
    }

    private static void Patch(Plugin p, Type type, string name, Type[] args, string hook) => p.Harmony.Patch(
        AccessTools.DeclaredMethod(type, name, args) ?? throw new MissingMethodException(type.Name, name),
        postfix: new HarmonyMethod(typeof(AirShiftFeature), hook));

    internal static bool Running(PixelEasyCharMoveKartController? kart) => Plugin.Instance?.GameplayReady == true &&
        Plugin.OfflineLabAllowed && Time.timeScale > 0 && MenuManager.Instance == null &&
        Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING &&
        kart?.parentPlayer?.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && !kart.IsPlayingReplayData() &&
        kart.outsideMapValidator != null && !kart.outsideMapValidator.bIsPlayerRespawning &&
        !kart.kartPhysics.IsTeleportingFromAtoB() && !kart.bIsInTankForm;

    private static Shift State(PixelKartPhysics physics)
    {
        int id = physics.GetInstanceID();
        if (!States.TryGetValue(id, out var state)) States[id] = state = new Shift();
        return state;
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Enabled.Value) return;
        try {
            var kart = __instance.boostManager?.kartController;
            if (!Running(kart) || kart!.Motor.GroundingStatus.FoundAnyGround) return;
            var state = State(kart.kartPhysics);
            bool left = Input.GetKeyDown(Left.Value) || ActionDown(__instance, LeftAction.Value);
            bool right = Input.GetKeyDown(Right.Value) || ActionDown(__instance, RightAction.Value);
            if (state.Used || left == right) return;
            state.Direction = left ? -1 : 1; state.Expires = Time.unscaledTime + .15f;
        } catch (Exception ex) { Stop(ex); }
    }

    private static bool ActionDown(Ant_KartInput input, string action) => action.Length > 0 && input.player != null && input.player.GetButtonDown(action);

    private static void VelocityPostfix(PixelKartPhysics __instance, ref Vector3 __0)
    {
        try {
            var kart = __instance.kartController;
            if (kart?.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            var state = State(__instance);
            if (kart.Motor.GroundingStatus.FoundAnyGround) { state.Used = false; state.Direction = 0; return; }
            if (!Enabled.Value || !Running(kart) || Time.unscaledTime > state.Expires) { state.Direction = 0; return; }
            if (state.Used || state.Direction == 0) return;
            var right = __instance.GetMainMotorSteeringOrientation() * Vector3.right;
            var adjusted = AirShiftMath.Adjust(__0.x, __0.z, right.x, right.z, state.Direction * Strength.Value, Retention.Value);
            Plugin.Instance!.SessionModified = true;
            __0 = new Vector3(adjusted.X, __0.y, adjusted.Z);
            __instance.vLastVelocity = __0;
            state.Used = true; state.Direction = 0;
        } catch (Exception ex) { Stop(ex); }
    }

    internal static bool Used(PixelKartPhysics physics) => State(physics).Used;
    internal static void RestoreUsed(PixelKartPhysics physics, bool used) { var state = State(physics); state.Used = used; state.Direction = 0; }
    private static void Stop(Exception ex) { Enabled.Value = false; Reset(); Plugin.Instance?.Log.LogError($"Air Shift stopped: {ex.Message}"); }
    internal static void Reset() => States.Clear();
}

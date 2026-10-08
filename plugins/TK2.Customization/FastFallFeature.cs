using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

// Fresh current-build implementation of both legacy AerialFastFall input modes.
internal static class FastFallFeature
{
    private static ConfigEntry<bool> SinglePress = null!, Dodge = null!;
    private static ConfigEntry<float> DodgeDuration = null!, Deadzone = null!;
    private static ConfigEntry<string> Button = null!;
    private static readonly Dictionary<int, bool> Latched = new();
    private static readonly Dictionary<int, HpBarController> Immunity = new();
    // The current API combines independent flag sources. Reserve a mod-owned flag
    // rather than clearing Shield/Teleport/Respawn protection when our timer expires.
    private const EImmunitySource ModDodge = (EImmunitySource)0x10000;

    internal static void Install(Plugin p)
    {
        SinglePress = p.Config.Bind("Physics", "UseSinglePressInput", true, "Press the fast-fall button once per jump; otherwise hold Down Arrow or the Vertical axis.");
        Dodge = p.Config.Bind("Physics", "ShouldDodgeOnPress", false, "Grant temporary damage immunity once per fast-fall jump.");
        DodgeDuration = p.Config.Bind("Physics", "DodgeDurationAfterPress", .5f, new ConfigDescription("Dodge duration in seconds.", new AcceptableValueRange<float>(.1f, 1f)));
        Deadzone = p.Config.Bind("Physics", "MinimumJoystickInputBeforeFastFall", .1f, new ConfigDescription("Directional fast-fall deadzone.", new AcceptableValueRange<float>(0, 1)));
        Button = p.Config.Bind("Physics", "ControllerAction", "MenuTriangle", "Rewired controller action; Down Arrow is the keyboard shortcut.");
        var target = AccessTools.DeclaredMethod(typeof(Ant_KartInput), "ProcessRacingInput", Type.EmptyTypes)
            ?? throw new MissingMethodException("Ant_KartInput.ProcessRacingInput()");
        p.Harmony.Patch(target, postfix: new HarmonyMethod(typeof(FastFallFeature), nameof(InputPostfix)));
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        var p = Plugin.Instance;
        if (p == null) return;
        try {
            var player = __instance.antPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            int id = player.GetInstanceID();
            if (!p.PhysicsEnabled.Value || !Plugin.OfflineLabAllowed || !SinglePress.Value) { Latched.Remove(id); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow) || (__instance.player != null && __instance.player.GetButtonDown(Button.Value)))
                Latched[id] = true;
        }
        catch (Exception ex) { p.PhysicsFaulted = true; p.Log.LogWarning($"Fast-fall input failed: {ex.Message}"); }
    }

    internal static void Tick(PixelKartPhysics physics, Plugin p)
    {
        var player = physics.kartController?.parentPlayer;
        if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
        int id = player.GetInstanceID();
        bool allowed = Plugin.OfflineLabAllowed && p.PhysicsEnabled.Value && !p.PhysicsFaulted && Time.timeScale > 0 &&
            Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
        if (!allowed) { Latched.Remove(id); ClearImmunity(id); return; }
        if (physics.bWasGrounded || physics.bIsDrifting || physics.fTimeInAir < p.AirTime.Value) { Latched.Remove(id); ClearImmunity(id); return; }
        float rate = SinglePress.Value ? (Latched.TryGetValue(id, out bool pressed) && pressed ? 1 : 0)
            : (Input.GetKey(KeyCode.DownArrow) ? 1 : Mathf.Clamp01(-Input.GetAxis("Vertical")));
        if (rate <= (SinglePress.Value ? 0 : Deadzone.Value)) return;
        p.SessionModified = true;
        ReadableGame.AddVelocity(physics, Vector3.down * Time.fixedDeltaTime * p.FallAcceleration.Value * rate);
        if (SinglePress.Value && Dodge.Value && !Immunity.ContainsKey(id) && player.hpBarController != null) {
            var hp = player.hpBarController;
            hp.SetImmunity(ModDodge, true, 0);
            hp.SetImmunity(ModDodge, false, DodgeDuration.Value);
            Immunity[id] = hp;
        }
    }

    private static void ClearImmunity(int id) {
        if (Immunity.Remove(id, out var hp) && hp != null) hp.SetImmunity(ModDodge, false, 0);
    }
    internal static void Restore() {
        foreach (var hp in Immunity.Values) if (hp != null) hp.SetImmunity(ModDodge, false, 0);
        Immunity.Clear(); Latched.Clear();
    }
}

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using ItemEffects;
using TMPro;
using UnityEngine;

namespace TK2.Customization;

// Fresh implementations of legacy MK module ideas using the 0.1.4.18 interop API.
// No legacy DLL, overlay dependency, or leaderboard patch is loaded by this pack.
internal static partial class LegacyMK
{
    private static Plugin _plugin = null!;
    private static ConfigEntry<bool> Respawn = null!, Trainer = null!, Reserves = null!, Dash = null!,
        TeleportTricks = null!, Bobby = null!, Voice = null!, AirBrake = null!;
    private static ConfigEntry<float> RespawnSpeed = null!, TrainerThreshold = null!, TrainerStrength = null!,
        ReserveRate = null!, ReservesToSet = null!, VoiceDistance = null!;
    private static ConfigEntry<int> Ammo = null!;
    private static ConfigEntry<PixelWeaponObject.EWeaponType> Weapon = null!;
    private static ConfigEntry<KeyCode> ReserveKey = null!, WeaponKey = null!;
    private static ConfigEntry<string> NameFrom = null!, NameTo = null!;

    internal static void Install(Plugin plugin)
    {
        _plugin = plugin;
        Respawn = Toggle("FastRespawn", "Speed up Otto recovery while preserving the current respawn phases.");
        RespawnSpeed = Number("FastRespawn", "SpeedMultiplier", 10, 1, 30, "Multiplier of captured drone movement speed; live changes are reversible.");
        Trainer = Toggle("BoostTrainer", "Vibrate the local controller when a boost reaches the selected threshold.");
        TrainerThreshold = Number("BoostTrainer", "ThresholdPercent", 85, 1, 100, "Percent of the current best-boost fill time.");
        TrainerStrength = Number("BoostTrainer", "VibrationStrength", 0.7f, 0, 1, "Controller rumble strength.");
        Reserves = Toggle("AlternateReserves", "Build reserves while accelerating instead of from drift boosts.");
        ReserveRate = Number("AlternateReserves", "RateMultiplier", 1, 0, 10, "Multiplier of the legacy reserve growth curve.");
        Dash = Toggle("DashAndStash", "Practice with configurable reserves and items, awarded using keyboard shortcuts.");
        ReservesToSet = Number("DashAndStash", "Reserves", 10, 0, 300, "Raw game reserve units; not the Prologue speed-display approximation.");
        ReserveKey = plugin.Config.Bind("DashAndStash", "SetReservesKey", KeyCode.F7, "Press to assign reserves to each local player.");
        WeaponKey = plugin.Config.Bind("DashAndStash", "GiveItemKey", KeyCode.F8, "Press to award the chosen item to each local player.");
        Weapon = plugin.Config.Bind("DashAndStash", "Item", PixelWeaponObject.EWeaponType.TELEPORT_DASH_12, "Item to award. Choose an actual weapon, not __WEAPONS_COUNT.");
        Ammo = plugin.Config.Bind("DashAndStash", "Ammo", 1, new ConfigDescription("Item quantity.", new AcceptableValueRange<int>(1, 99)));
        TeleportTricks = Toggle("TeleportersForTricks", "Award a teleport item after landing a trick; portal use blocks the next award.");
        Bobby = Toggle("BobbyGang", "Replace character-name text in the visible UI.");
        NameFrom = plugin.Config.Bind("BobbyGang", "OriginalName", "Bubble", "Case-sensitive text to replace; blank does nothing.");
        NameTo = plugin.Config.Bind("BobbyGang", "ReplacementName", "Bobby", "Replacement text.");
        Voice = Toggle("ProximityVoiceLines", "Allow nearby offline AI racers to play their normal character voice lines.");
        VoiceDistance = Number("ProximityVoiceLines", "MaximumDistance", 200, 1, 1000, "Maximum world-space distance to the nearest local player.");
        AirBrake = Toggle("AirBrake", "Cancel local kart velocity while braking in the air, based on the hidden SupraMayroKratt experiment.");
        BindParameters();
        BindPractice();
        BindMeter();
        Capability("fast respawn", () => Hook(typeof(PTK_VehicleOutsideMapValidator), "FixedUpdate", Type.EmptyTypes, nameof(RespawnPrefix), true));
        Capability("boost trainer / practice inputs", () => Hook(typeof(Ant_KartInput), "ProcessRacingInput", Type.EmptyTypes, nameof(InputPostfix), false));
        Capability("reserve growth / air brake", () => Hook(typeof(PixelEasyCharMoveKartController), "FixedUpdate", Type.EmptyTypes, nameof(ControllerPrefix), true));
        Capability("teleport trick reward", () => {
            Hook(typeof(Ant_BoostManager), "OnKartLandedAfterPlayerTriggeredJump", new[] { typeof(bool) }, nameof(LandingPostfix), false);
            Hook(typeof(PortalDash), "StartEffect", Type.EmptyTypes, nameof(PortalPostfix), false);
        });
        Capability("nearby voices", () => {
            Hook(typeof(PTK_PlayerVoiceOverManager), "CanPlayVOForPlayer", Type.EmptyTypes, nameof(VoiceAllowedPostfix), false);
            Hook(typeof(PTK_PlayerVoiceOverManager), "PlayVoiceOver", new[] { typeof(bool) }, nameof(VoicePlayPrefix), true);
        });
        Capability("UI name replacement", () => Hook(typeof(Ant_CurrentGameConfiguration), "Update", Type.EmptyTypes, nameof(TextPostfix), false));
        InstallParameters();
        InstallPractice();
    }

    private static ConfigEntry<bool> Toggle(string section, string description) => _plugin.Config.Bind(section, "Enabled", false, description + " Current-build port; runtime testing required.");
    private static ConfigEntry<float> Number(string section, string key, float value, float min, float max, string description) =>
        _plugin.Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
    private static void Capability(string name, Action action)
    {
        try { action(); _plugin.Log.LogInfo($"MK capability installed: {name}"); }
        catch (Exception ex) { _plugin.Log.LogWarning($"MK capability unavailable: {name}: {ex.Message}"); }
    }
    private static void Hook(Type type, string method, Type[] args, string hook, bool prefix)
    {
        var target = AccessTools.DeclaredMethod(type, method, args) ?? throw new MissingMethodException(type.FullName, method);
        var patch = new HarmonyMethod(typeof(LegacyMK), hook);
        if (prefix) _plugin.Harmony.Patch(target, prefix: patch); else _plugin.Harmony.Patch(target, postfix: patch);
    }
    private static bool Local(Ant_Player? player) => player != null && player.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL;
    private static bool Allowed(Ant_Player? player) => Plugin.OfflineLabAllowed && Local(player);
    private static void Fault(ConfigEntry<bool> feature, Exception error)
    {
        // Changing an entry must not rewrite the config behind the editor's back.
        feature.Value = false;
        _plugin.Log.LogError($"{feature.Definition.Section} stopped after runtime error: {error.Message}");
    }

    private static readonly Dictionary<int, (PTK_VehicleOutsideMapValidator Target, float Speed, float AttachTime)> RespawnOriginals = new();
    private static void RespawnPrefix(PTK_VehicleOutsideMapValidator __instance)
    {
        try
        {
            int id = __instance.GetInstanceID();
            bool active = Respawn.Value && Allowed(__instance.parentPlayer);
            if (!active)
            {
                if (RespawnOriginals.Remove(id, out var old) && old.Target != null)
                { old.Target.fMoveTowardTargetRespawnPointSpeed = old.Speed; old.Target.fAttachVehicleToDroneHookInTime = old.AttachTime; }
                return;
            }
            if (!RespawnOriginals.TryGetValue(id, out var saved))
                RespawnOriginals[id] = saved = (__instance, __instance.fMoveTowardTargetRespawnPointSpeed, __instance.fAttachVehicleToDroneHookInTime);
            __instance.fMoveTowardTargetRespawnPointSpeed = saved.Speed * RespawnSpeed.Value;
            __instance.fAttachVehicleToDroneHookInTime = saved.AttachTime / RespawnSpeed.Value;
        }
        catch (Exception ex) { Fault(Respawn, ex); }
    }

    private static readonly Dictionary<int, Rewired.Player> RumblePlayers = new();
    private static readonly Dictionary<int, bool> TrainerWasReady = new();
    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Local(__instance.antPlayer)) return;
        var boost = __instance.boostManager;
        if (boost == null) return;
        var input = __instance.player;
        int id = __instance.antPlayer.GetInstanceID();
        try
        {
            if (Trainer.Value && input != null)
            {
                bool ready = false;
                foreach (float fill in boost.fCurrentBoostFillTime)
                    ready |= fill >= boost.fMaximumTimeForBestBoost * TrainerThreshold.Value / 100f;
                bool wasReady = TrainerWasReady.TryGetValue(id, out bool previous) && previous;
                if (ready && !wasReady) { input.SetVibration(0, TrainerStrength.Value, 0.15f); RumblePlayers[id] = input; }
                TrainerWasReady[id] = ready;
            }
            else { TrainerWasReady.Remove(id); }
        }
        catch (Exception ex) { Fault(Trainer, ex); }
        try
        {
            if (Dash.Value && Allowed(__instance.antPlayer))
            {
                if (Input.GetKeyDown(ReserveKey.Value)) boost.fBoostingReserves = ReservesToSet.Value;
                if (Input.GetKeyDown(WeaponKey.Value) && Weapon.Value != PixelWeaponObject.EWeaponType.__WEAPONS_COUNT)
                    __instance.weaponsController.WeaponBoxReward_AddWeapon(Weapon.Value, Ammo.Value, PixelWeaponObject.EWeaponType.__WEAPONS_COUNT, 0, true);
            }
        }
        catch (Exception ex) { Fault(Dash, ex); }
        PracticeInput(__instance);
        CaptureMeter(boost);
    }

    private static readonly Dictionary<int, (Ant_BoostManager Target, float One, float Two, float Three)> ReserveOriginals = new();
    private static void ControllerPrefix(PixelEasyCharMoveKartController __instance)
    {
        try
        {
            var boost = __instance.kartPhysics.boostManager;
            int id = boost.GetInstanceID();
            bool active = Reserves.Value && Allowed(__instance.parentPlayer);
            if (!active)
            {
                if (ReserveOriginals.Remove(id, out var old) && old.Target != null)
                { old.Target.fBoostLengthFor1 = old.One; old.Target.fBoostLengthFor2 = old.Two; old.Target.fBoostLengthFor3 = old.Three; }
            }
            else
            {
                if (!ReserveOriginals.ContainsKey(id)) ReserveOriginals[id] = (boost, boost.fBoostLengthFor1, boost.fBoostLengthFor2, boost.fBoostLengthFor3);
                boost.fBoostLengthFor1 = boost.fBoostLengthFor2 = boost.fBoostLengthFor3 = 0;
                if (__instance.GetAccelInput() > 0.1f && Time.timeScale > 0 &&
                    Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING)
                {
                    float current = boost.fBoostingReserves;
                    float growth = (10f / (95f - Mathf.Clamp(current, 0.1f, 90f)) + 0.85f) * 2f;
                    boost.fBoostingReserves = current + growth * Time.fixedDeltaTime * ReserveRate.Value;
                }
            }
            if (AirBrake.Value && Allowed(__instance.parentPlayer) && !__instance.kartPhysics.bWasGrounded && __instance.IsBreaking())
                ReadableGame.AddVelocity(__instance.kartPhysics, -__instance.GetKartVelocity());
        }
        catch (Exception ex) { Fault(Reserves, ex); Fault(AirBrake, ex); }
    }

    private static readonly Dictionary<int, bool> PortalBlocksAward = new();
    private static void PortalPostfix(PortalDash __instance)
    {
        if (!TeleportTricks.Value || !Plugin.OfflineLabAllowed) return;
        try
        {
            if (__instance.targetPlayers == null) return;
            foreach (var player in __instance.targetPlayers)
                if (Local(player)) PortalBlocksAward[player.GetInstanceID()] = true;
        }
        catch (Exception ex) { Fault(TeleportTricks, ex); }
    }
    private static void LandingPostfix(Ant_BoostManager __instance, bool __0)
    {
        var player = __instance.kartController?.parentPlayer;
        if (!TeleportTricks.Value || !Allowed(player)) return;
        try
        {
            int id = player!.GetInstanceID();
            bool blocked = PortalBlocksAward.TryGetValue(id, out bool value) && value;
            if (__0 && !blocked) player.weaponsController.WeaponBoxReward_AddWeapon(PixelWeaponObject.EWeaponType.TELEPORT_DASH_12, 1, PixelWeaponObject.EWeaponType.__WEAPONS_COUNT, 0, true);
            PortalBlocksAward[id] = false;
        }
        catch (Exception ex) { Fault(TeleportTricks, ex); }
    }

    private static float NearestLocalDistance(PTK_PlayerVoiceOverManager manager)
    {
        var source = manager.visualInstanceParent?.parentPlayer;
        if (source == null) return float.PositiveInfinity;
        var position = source.playerDirectionManager.GetPlayerKartTransform().position;
        float closest = float.PositiveInfinity;
        foreach (var player in Ant_MainGame.Instance.antMainGamePlayers.currentlyPlayingPlayers)
            if (Local(player)) closest = Mathf.Min(closest, Vector3.Distance(position, player.playerDirectionManager.GetPlayerKartTransform().position));
        return closest;
    }
    private static void VoiceAllowedPostfix(PTK_PlayerVoiceOverManager __instance, ref bool __result)
    {
        if (!Voice.Value || !Plugin.OfflineLabAllowed || __result) return;
        try
        {
            var player = __instance.visualInstanceParent?.parentPlayer;
            if (player != null && player.ePlayerType == Ant_Player.EPlayerType.E_AI_LOCAL && NearestLocalDistance(__instance) <= VoiceDistance.Value)
                __result = true;
        }
        catch (Exception ex) { Fault(Voice, ex); }
    }
    private static bool VoicePlayPrefix(PTK_PlayerVoiceOverManager __instance)
    {
        if (!Voice.Value || !Plugin.OfflineLabAllowed) return true;
        try
        {
            if (Local(__instance.visualInstanceParent?.parentPlayer)) return true;
            float distance = NearestLocalDistance(__instance);
            if (distance > VoiceDistance.Value) return false;
            AkSoundEngine.SetRTPCValue("VO_Volume_RTPC", 100f / Mathf.Pow(1 + distance / VoiceDistance.Value, 2), __instance.gameObject);
            AttenuatedVoices[__instance.GetInstanceID()] = __instance;
        }
        catch (Exception ex) { Fault(Voice, ex); }
        return true;
    }

    private static float _nextTextScan;
    private static readonly Dictionary<int, PTK_PlayerVoiceOverManager> AttenuatedVoices = new();
    private static readonly Dictionary<int, (TextMeshProUGUI Text, string Before, string After)> ChangedText = new();
    private static void TextPostfix()
    {
        if (Time.unscaledTime < _nextTextScan) return;
        _nextTextScan = Time.unscaledTime + 0.5f;
        foreach (var saved in ChangedText.Values) if (saved.Text != null && saved.Text.text == saved.After) saved.Text.text = saved.Before;
        ChangedText.Clear();
        if (!Bobby.Value || string.IsNullOrEmpty(NameFrom.Value)) return;
        foreach (var label in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            string before = label.text;
            if (string.IsNullOrEmpty(before) || !before.Contains(NameFrom.Value, StringComparison.OrdinalIgnoreCase)) continue;
            string after = before.Replace(NameFrom.Value, NameTo.Value).Replace(NameFrom.Value.ToUpperInvariant(), NameTo.Value.ToUpperInvariant());
            if (after == before) continue;
            ChangedText[label.GetInstanceID()] = (label, before, after); label.text = after;
        }
    }

    internal static void Restore()
    {
        foreach (var old in RespawnOriginals.Values) if (old.Target != null)
        { old.Target.fMoveTowardTargetRespawnPointSpeed = old.Speed; old.Target.fAttachVehicleToDroneHookInTime = old.AttachTime; }
        RespawnOriginals.Clear();
        foreach (var old in ReserveOriginals.Values) if (old.Target != null)
        { old.Target.fBoostLengthFor1 = old.One; old.Target.fBoostLengthFor2 = old.Two; old.Target.fBoostLengthFor3 = old.Three; }
        ReserveOriginals.Clear();
        foreach (var input in RumblePlayers.Values) input.StopVibration();
        RumblePlayers.Clear(); TrainerWasReady.Clear(); PortalBlocksAward.Clear();
        foreach (var old in ChangedText.Values) if (old.Text != null && old.Text.text == old.After) old.Text.text = old.Before;
        ChangedText.Clear();
        RestoreVoiceAttenuation();
        RestoreParameters(); ClearPractice(); RestoreMeter();
    }

    private static void RestoreVoiceAttenuation()
    {
        foreach (var voice in AttenuatedVoices.Values) if (voice != null) AkSoundEngine.ResetRTPCValue("VO_Volume_RTPC", voice.gameObject);
        AttenuatedVoices.Clear();
    }
}

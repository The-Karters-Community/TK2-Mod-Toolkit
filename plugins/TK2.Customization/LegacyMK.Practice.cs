using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using KinematicCharacterController;
using UnityEngine;

namespace TK2.Customization;

internal static partial class LegacyMK
{
    private static ConfigEntry<bool> SaveStates = null!, ControllerShortcuts = null!, HoldLoad = null!,
        RestoreProgress = null!, RestoreClock = null!, ReverseRace = null!;
    private static ConfigEntry<KeyCode> SaveKey = null!, LoadKey = null!;
    private static ConfigEntry<string> SaveAction = null!, LoadAction = null!;
    private static readonly Dictionary<int, PracticeState> PracticeStates = new();
    private static float _savedClock;
    private static int _lastSaveFrame = -1, _lastLoadFrame = -1;
    private static string _savedScene = "";

    private sealed class PracticeState
    {
        internal KinematicCharacterMotorState Motor = null!;
        internal float Reserves, AirTime;
        internal int BoostIndex, Laps, Health, Ammo;
        internal PixelWeaponObject.EWeaponType Weapon;
        internal float[] Fill = Array.Empty<float>(), PartialTimes = Array.Empty<float>();
        internal bool[] Checkpoints = Array.Empty<bool>();

        internal static PracticeState Capture(Ant_BoostManager boost)
        {
            var kart = boost.kartController;
            var player = kart.parentPlayer;
            var race = player.playerRaceLogic;
            var state = new PracticeState {
                Motor = kart.Motor.GetState(), Reserves = boost.fBoostingReserves,
                AirTime = boost.fCurrentAirTime, BoostIndex = boost.iCurrentBoosterNumberCount,
                Laps = race.iPlayerMovedThroughFinishLineCount_LocalOnly, Health = player.hpBarController.currentHp,
                Weapon = player.weaponsController.currentWeaponType,
                Fill = new float[boost.fCurrentBoostFillTime.Length],
                PartialTimes = new float[race.partialTimes.Count], Checkpoints = new bool[race.bCheckpoints.Length]
            };
            state.Ammo = player.weaponsController.weaponsAndAmmoManager.GetCurrentAmmoCount(state.Weapon);
            for (int i = 0; i < state.Fill.Length; i++) state.Fill[i] = boost.fCurrentBoostFillTime[i];
            for (int i = 0; i < state.PartialTimes.Length; i++) state.PartialTimes[i] = race.partialTimes[i];
            for (int i = 0; i < state.Checkpoints.Length; i++) state.Checkpoints[i] = race.bCheckpoints[i];
            return state;
        }

        internal void Apply(Ant_BoostManager boost)
        {
            var kart = boost.kartController;
            var player = kart.parentPlayer;
            // Restore complete KCC motor state, including velocity and grounded status.
            kart.Motor.ApplyState(Motor, true);
            kart.kartPhysics.SetSteeringPhysicsRotation(Motor.Rotation, false);
            boost.fBoostingReserves = Reserves;
            boost.fCurrentAirTime = AirTime;
            boost.iCurrentBoosterNumberCount = BoostIndex;
            for (int i = 0; i < Math.Min(Fill.Length, boost.fCurrentBoostFillTime.Length); i++) boost.fCurrentBoostFillTime[i] = Fill[i];
            player.hpBarController.currentHp = Health;
            var weapons = player.weaponsController;
            // Remove current weapon first, so a captured empty slot can also be restored.
            weapons.ImmediateResetWeaponState();
            if (Ammo > 0 && Weapon != PixelWeaponObject.EWeaponType.__WEAPONS_COUNT)
                weapons.WeaponBoxReward_AddWeapon(Weapon, Ammo, PixelWeaponObject.EWeaponType.__WEAPONS_COUNT, 0, true);
            if (!RestoreProgress.Value) return;
            var race = player.playerRaceLogic;
            race.iPlayerMovedThroughFinishLineCount_LocalOnly = Laps;
            race.partialTimes.Clear();
            foreach (float time in PartialTimes) race.partialTimes.Add(time);
            for (int i = 0; i < Math.Min(Checkpoints.Length, race.bCheckpoints.Length); i++) race.bCheckpoints[i] = Checkpoints[i];
        }
    }

    private static void BindPractice()
    {
        SaveStates = Toggle("SaveStates", "Save and reload local kart practice snapshots within the same scene. Does not rewind AI racers or running item-effect coroutines.");
        SaveKey = _plugin.Config.Bind("SaveStates", "SaveKey", KeyCode.F5, "Keyboard shortcut to capture local player state.");
        LoadKey = _plugin.Config.Bind("SaveStates", "LoadKey", KeyCode.F6, "Keyboard shortcut to restore the last snapshot.");
        ControllerShortcuts = _plugin.Config.Bind("SaveStates", "ControllerShortcuts", false, "Also use named Rewired controller actions.");
        SaveAction = _plugin.Config.Bind("SaveStates", "SaveAction", "InGamePlayersList", "Rewired action name for saving.");
        LoadAction = _plugin.Config.Bind("SaveStates", "LoadAction", "MenuTriangle", "Rewired action name for loading.");
        HoldLoad = _plugin.Config.Bind("SaveStates", "HoldToLoad", false, "Continuously restore while the load shortcut is held.");
        RestoreProgress = _plugin.Config.Bind("SaveStates", "RestoreRaceProgress", false, "Experimental: also restore lap count, partial times and checkpoint flags. Respawn route history is not rewound.");
        RestoreClock = _plugin.Config.Bind("SaveStates", "RestoreRaceTime", false, "Experimental: rewind the shared race clock; other racers are not rewound.");
        ReverseRace = Toggle("ReverseRace", "Experimental reverse finish-line counting and reversed local starting direction. Current checkpoint/respawn route behavior needs in-game validation.");
    }

    private static void PracticeInput(Ant_KartInput input)
    {
        if (!SaveStates.Value || !Allowed(input.antPlayer)) return;
        try
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (_savedScene.Length > 0 && _savedScene != scene) ClearPractice();
            bool save = Input.GetKeyDown(SaveKey.Value);
            bool load = HoldLoad.Value ? Input.GetKey(LoadKey.Value) : Input.GetKeyDown(LoadKey.Value);
            if (ControllerShortcuts.Value && input.player != null)
            {
                save |= input.player.GetButtonDown(SaveAction.Value);
                load |= HoldLoad.Value ? input.player.GetButton(LoadAction.Value) : input.player.GetButtonDown(LoadAction.Value);
            }
            if (save && _lastSaveFrame != Time.frameCount)
            {
                PracticeStates.Clear(); _savedScene = scene; _savedClock = Ant_CurrentGameConfiguration.fSynchronizedTime;
                foreach (var boost in UnityEngine.Object.FindObjectsByType<Ant_BoostManager>(FindObjectsSortMode.None))
                {
                    var kart = boost.kartController;
                    if (kart != null && Local(kart.parentPlayer)) PracticeStates[kart.parentPlayer.GetInstanceID()] = PracticeState.Capture(boost);
                }
                _lastSaveFrame = Time.frameCount;
                _plugin.Log.LogInfo($"Saved {PracticeStates.Count} local kart practice snapshots.");
            }
            if (!load || _lastLoadFrame == Time.frameCount || _savedScene != scene || PracticeStates.Count == 0) return;
            foreach (var boost in UnityEngine.Object.FindObjectsByType<Ant_BoostManager>(FindObjectsSortMode.None))
            {
                var kart = boost.kartController;
                if (kart != null && Local(kart.parentPlayer) && PracticeStates.TryGetValue(kart.parentPlayer.GetInstanceID(), out var state)) state.Apply(boost);
            }
            if (RestoreClock.Value) Ant_MainGame.Instance.ant_SyncrhonizedTime.ResetGameTime(Time.time - _savedClock);
            _lastLoadFrame = Time.frameCount;
        }
        catch (Exception ex) { Fault(SaveStates, ex); }
    }

    private static void ClearPractice()
    {
        PracticeStates.Clear(); _savedScene = ""; _lastLoadFrame = _lastSaveFrame = -1;
        ReverseStates.Clear();
    }

    private sealed class ReverseState
    {
        internal int Before;
        internal bool InitialCrossing;
        internal readonly List<float> LapEndTimes = new();
    }
    private static readonly Dictionary<int, ReverseState> ReverseStates = new();
    private static void InstallPractice()
    {
        Capability("practice snapshot lifecycle", () => Hook(typeof(PlayerRaceLogic), "OnRaceResetted", Type.EmptyTypes, nameof(RaceResetPrefix), true));
        Capability("reverse race", () => {
            Hook(typeof(PlayerRaceLogic), "PrepareToStartRace", Type.EmptyTypes, nameof(ReverseStartPostfix), false);
            Hook(typeof(PlayerRaceLogic), "UpdateAndCalculateLapCount", Type.EmptyTypes, nameof(ReverseLapPrefix), true);
            Hook(typeof(PlayerRaceLogic), "UpdateAndCalculateLapCount", Type.EmptyTypes, nameof(ReverseLapPostfix), false);
        });
    }
    private static void RaceResetPrefix()
    { ClearPractice(); PortalBlocksAward.Clear(); TrainerWasReady.Clear(); }
    private static void ReverseStartPostfix(PlayerRaceLogic __instance)
    {
        ReverseStates.Remove(__instance.GetInstanceID());
        if (!ReverseRace.Value || !Allowed(__instance.parentPlayer)) return;
        try
        {
            __instance.iPlayerMovedThroughFinishLineCount_LocalOnly = 1;
            ReverseStates[__instance.GetInstanceID()] = new ReverseState();
            foreach (var boost in UnityEngine.Object.FindObjectsByType<Ant_BoostManager>(FindObjectsSortMode.None))
                if (boost.kartController?.parentPlayer == __instance.parentPlayer)
                {
                    var kart = boost.kartController;
                    kart.ResetKartPhysics(kart.GetKartPos(), kart.GetKartRotation() * Quaternion.Euler(0, 180, 0));
                }
        }
        catch (Exception ex) { Fault(ReverseRace, ex); }
    }
    private static void ReverseLapPrefix(PlayerRaceLogic __instance)
    {
        if (!ReverseRace.Value || !Allowed(__instance.parentPlayer)) return;
        if (ReverseStates.TryGetValue(__instance.GetInstanceID(), out var state)) state.Before = __instance.iPlayerMovedThroughFinishLineCount_LocalOnly;
    }
    private static void ReverseLapPostfix(PlayerRaceLogic __instance)
    {
        if (!ReverseRace.Value || !Allowed(__instance.parentPlayer) || !ReverseStates.TryGetValue(__instance.GetInstanceID(), out var state)) return;
        try
        {
            int actual = __instance.iPlayerMovedThroughFinishLineCount_LocalOnly;
            int delta = actual - state.Before;
            if (delta == 0) return;
            int corrected = state.Before - delta;
            if (corrected == 0 && actual == 2) state.InitialCrossing = true;
            corrected = Math.Max(state.InitialCrossing ? 0 : 1, corrected);
            __instance.iPlayerMovedThroughFinishLineCount_LocalOnly = corrected;
            if (corrected < 2) return;
            while (state.LapEndTimes.Count < corrected - 1) state.LapEndTimes.Add(0);
            state.LapEndTimes[corrected - 2] = Ant_MainGame.Instance.GetCurrentRaceTime();
            __instance.partialTimes.Clear();
            for (int i = 0; i < state.LapEndTimes.Count; i++)
                __instance.partialTimes.Add(state.LapEndTimes[i] - (i == 0 ? 0 : state.LapEndTimes[i - 1]));
        }
        catch (Exception ex) { Fault(ReverseRace, ex); }
    }
}

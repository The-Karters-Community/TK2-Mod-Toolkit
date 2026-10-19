using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static class PracticeRewindFeature
{
    private static ConfigEntry<bool> Enabled = null!, ShowStatus = null!;
    private static ConfigEntry<KeyCode> Key = null!;
    private static ConfigEntry<string> Action = null!;
    private static ConfigEntry<float> Seconds = null!, HistorySeconds = null!;
    private static readonly PracticeHistory<DrivingSnapshot> History = new();
    private static int PlayerId, SceneHandle, Lap;
    private static bool[] Checkpoints = Array.Empty<bool>();
    private static float Cursor, LastUpdate, NextSample, ScrubTarget;
    private static bool Scrubbing, ControllerHeld;
    private static string Status = "Waiting for a solo practice race";

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("PracticeRewind", "Enabled", false, "Experimental solo practice scrubbing within one checkpoint segment. Does not rewind the world or race clock. Runtime validation pending.");
        Key = p.Config.Bind("PracticeRewind", "RewindKey", KeyCode.Backspace, "Tap to rewind; hold to scrub farther backward. Release to resume.");
        Action = p.Config.Bind("PracticeRewind", "ControllerAction", "", "Optional existing Rewired action, blank for keyboard only.");
        Seconds = p.Config.Bind("PracticeRewind", "RewindSeconds", 3f, new ConfigDescription("Initial rewind interval; requires this much history.", new AcceptableValueRange<float>(.5f, 10)));
        HistorySeconds = p.Config.Bind("PracticeRewind", "HistorySeconds", 10f, new ConfigDescription("Rolling history length at 20 samples per second.", new AcceptableValueRange<float>(3, 20)));
        ShowStatus = p.Config.Bind("PracticeRewind", "ShowStatus", true, "Show available history and scrub status during practice.");
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(Ant_KartInput), "ProcessRacingInput", Type.EmptyTypes)!,
            postfix: new HarmonyMethod(typeof(PracticeRewindFeature), nameof(InputPostfix)));
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(PlayerRaceLogic), "OnRaceResetted", Type.EmptyTypes)!,
            postfix: new HarmonyMethod(typeof(PracticeRewindFeature), nameof(Reset)));
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Enabled.Value || __instance.antPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
        try { ControllerHeld = Action.Value.Length > 0 && __instance.player != null && __instance.player.GetButton(Action.Value); }
        catch (Exception ex) { Fail(ex); }
    }

    internal static void Tick()
    {
        if (!Enabled.Value || !Plugin.OfflineLabAllowed) { Reset(); return; }
        if (Time.timeScale <= 0 || MenuManager.Instance != null ||
            Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) {
            Reset(); Status = "Waiting for an unpaused solo race"; return;
        }
        try {
            float now = Time.unscaledTime;
            bool held = Input.GetKey(Key.Value) || ControllerHeld;
            if (now < NextSample && !Scrubbing && !held) return;
            NextSample = now + .05f;
            // AI and split-screen worlds cannot be restored consistently by a kart-only history.
            var boosts = UnityEngine.Object.FindObjectsByType<Ant_BoostManager>(FindObjectsSortMode.None);
            Ant_BoostManager? boost = null; int racers = 0;
            foreach (var candidate in boosts) {
                var kart = candidate.kartController;
                if (kart == null || kart.IsPlayingReplayData()) continue;
                racers++;
                if (kart.parentPlayer?.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL) boost = candidate;
            }
            if (racers != 1 || boost == null) { Reset(); Status = "Requires one local kart, no AI racers"; return; }
            var c = boost.kartController; var race = c.parentPlayer.playerRaceLogic;
            if (!AirShiftFeature.Running(c) || race.bPlayerEndRaceEventTriggered) {
                Reset(); Status = "History cleared during teleport, recovery or finish"; return;
            }
            int scene = SceneManager.GetActiveScene().handle, id = c.GetInstanceID();
            if (SceneHandle != scene || PlayerId != id || !SameSegment(race)) {
                Reset(); SceneHandle = scene; PlayerId = id; Lap = race.iPlayerMovedThroughFinishLineCount_LocalOnly;
                Checkpoints = new bool[race.bCheckpoints.Length];
                for (int i = 0; i < Checkpoints.Length; i++) Checkpoints[i] = race.bCheckpoints[i];
                LastUpdate = now;
            }
            float dt = Mathf.Max(0, now - LastUpdate); LastUpdate = now;
            if (held) {
                if (!Scrubbing) {
                    ScrubTarget = Cursor - Seconds.Value;
                    if (!History.TryAt(ScrubTarget, out _, out _)) { Status = $"Release rewind to record more history ({History.Newest - History.Oldest:0.0}s / {Seconds.Value:0.0}s)"; return; }
                    Scrubbing = true;
                } else ScrubTarget = Mathf.Max(History.Oldest, ScrubTarget - dt * 2);
                if (History.TryAt(ScrubTarget, out var sample, out var time)) {
                    sample.Apply(boost); Cursor = time; Status = "Scrubbing backward — release to resume";
                }
                return;
            }
            if (Scrubbing) { History.Branch(Cursor); Scrubbing = false; }
            Cursor += dt;
            History.Trim(Cursor, Mathf.Max(HistorySeconds.Value, Seconds.Value));
            if (DrivingSnapshot.Stable(c)) History.Add(Cursor, DrivingSnapshot.Capture(boost), Mathf.Max(HistorySeconds.Value, Seconds.Value));
            Status = $"Practice rewind: {History.Newest - History.Oldest:0.0}s ready — {Key.Value}";
        } catch (Exception ex) { Fail(ex); }
    }

    private static bool SameSegment(PlayerRaceLogic race)
    {
        if (Lap != (int)race.iPlayerMovedThroughFinishLineCount_LocalOnly || Checkpoints.Length != race.bCheckpoints.Length) return false;
        for (int i = 0; i < Checkpoints.Length; i++) if (Checkpoints[i] != (bool)race.bCheckpoints[i]) return false;
        return true;
    }

    internal static void DrawStatus()
    {
        if (Enabled == null || !Enabled.Value || !ShowStatus.Value || !Plugin.OfflineLabAllowed || MenuManager.Instance != null) return;
        GUI.Label(new Rect(18, Screen.height - 42, 850, 30), Status);
    }

    private static void Fail(Exception ex) { Enabled.Value = false; Reset(); Plugin.Instance?.Log.LogError($"Practice rewind stopped: {ex.Message}"); }
    internal static void Reset() { History.Clear(); Scrubbing = ControllerHeld = false; Cursor = LastUpdate = NextSample = 0; PlayerId = SceneHandle = 0; Checkpoints = Array.Empty<bool>(); }
}

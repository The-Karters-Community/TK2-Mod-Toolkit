using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// The native boostometer's percentage is based on engine power and reserves,
// not a measurement of the kart's current velocity. Keep both readouts distinct.
internal static class CtrSpeedDisplay
{
    private static ConfigEntry<bool> Enabled = null!, ShowMeasuredSpeed = null!, HideNativeSpeedPercent = null!;
    private static readonly Dictionary<int, (GameObject Object, bool WasActive)> Hidden = new();
    private static PixelEasyCharMoveKartController? kart;
    private static float nextScan;

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("CtrHandling", "Enabled", false, "Offline CTR-inspired reserves and fire speed.");
        ShowMeasuredSpeed = p.Config.Bind("CtrHandling", "ShowMeasuredSpeed", true, "Show actual kart velocity in game units per second.");
        HideNativeSpeedPercent = p.Config.Bind("CtrHandling", "HideNativeSpeedPercent", true, "Hide the game's reserve-based speed percentage while CTR handling is enabled.");
    }

    internal static void Tick()
    {
        try {
            if (Enabled?.Value != true || !Plugin.OfflineLabAllowed || MenuManager.Instance != null || Time.timeScale <= 0 ||
                Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) {
                Restore(); return;
            }
            if (Time.unscaledTime >= nextScan) {
                nextScan = Time.unscaledTime + 2f;
                if (kart == null || !AirShiftFeature.Running(kart)) {
                    kart = null;
                    foreach (var candidate in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
                        if (AirShiftFeature.Running(candidate)) { kart = candidate; break; }
                }
                if (kart != null && HideNativeSpeedPercent.Value)
                    foreach (var meter in UnityEngine.Object.FindObjectsOfType<VariantUIRightBoostometr>()) {
                        Hide(meter.rightBoostometrSpeedText);
                        Hide(meter.rightBoostometrSpeedTextPercent);
                    }
            }
            if (kart == null || !AirShiftFeature.Running(kart) || !HideNativeSpeedPercent.Value)
                RestoreHidden();
            else
                foreach (var entry in Hidden.Values)
                    if (entry.Object != null && entry.Object.activeSelf) entry.Object.SetActive(false);
        } catch (Exception ex) {
            Restore();
            Plugin.Instance?.Log.LogWarning($"CTR speed display stopped: {ex.Message}");
            if (ShowMeasuredSpeed != null) ShowMeasuredSpeed.Value = false;
            if (HideNativeSpeedPercent != null) HideNativeSpeedPercent.Value = false;
        }
    }

    private static void Hide(GameObject? item)
    {
        if (item == null) return;
        int id = item.GetInstanceID();
        if (!Hidden.ContainsKey(id)) Hidden[id] = (item, item.activeSelf);
        if (item.activeSelf) item.SetActive(false);
    }

    private static void RestoreHidden()
    {
        foreach (var entry in Hidden.Values)
            if (entry.Object != null && entry.WasActive && !entry.Object.activeSelf) entry.Object.SetActive(true);
        Hidden.Clear();
    }

    internal static void Restore() { RestoreHidden(); kart = null; nextScan = 0; }

    internal static void Draw()
    {
        if (Enabled?.Value != true || ShowMeasuredSpeed?.Value != true || kart == null || !AirShiftFeature.Running(kart)) return;
        float speed = kart.Motor.Velocity.magnitude;
        if (float.IsFinite(speed)) GUI.Label(new Rect(18, Screen.height - 205, 500, 28), $"Speed {speed:0.0} game units/s");
    }
}

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace TK2.Customization;

internal static partial class LegacyMK
{
    private static ConfigEntry<bool> MeterEnabled = null!;
    private static ConfigEntry<float> MeterScale = null!, MeterX = null!, MeterY = null!,
        MeterThreshold3 = null!, MeterThreshold4 = null!;
    private static ConfigEntry<string> MeterColor1 = null!, MeterColor2 = null!, MeterColor3 = null!, MeterColor4 = null!;
    private static GameObject? _meterRoot;
    private static readonly Dictionary<int, MeterView> Meters = new();
    private sealed class MeterView
    {
        internal GameObject Root = null!;
        internal RectTransform Rect = null!;
        internal readonly Image[] Bars = new Image[3];
        internal float LastUpdate;
    }

    private static void BindMeter()
    {
        MeterEnabled = Toggle("CNKBoostMeter", "Show three boost-fill bars beside each local kart, inspired by the legacy CNK meter. Uses in-game Unity UI rather than an external overlay.");
        MeterScale = Number("CNKBoostMeter", "Scale", 1, 0.25f, 3, "Meter size multiplier at 1080p.");
        MeterX = Number("CNKBoostMeter", "HorizontalOffset", 100, -600, 600, "Screen-pixel offset to the right of the projected kart.");
        MeterY = Number("CNKBoostMeter", "VerticalOffset", 100, -600, 600, "Screen-pixel offset above the projected kart.");
        MeterThreshold3 = Number("CNKBoostMeter", "OrangeThreshold", 0.8f, 0, 1, "Fill fraction for the third color.");
        MeterThreshold4 = Number("CNKBoostMeter", "RedThreshold", 0.95f, 0, 1, "Fill fraction for the fourth color.");
        MeterColor1 = _plugin.Config.Bind("CNKBoostMeter", "BelowMinimumColor", "00FF00", "RGB hex color before minimum boost fill.");
        MeterColor2 = _plugin.Config.Bind("CNKBoostMeter", "ValidColor", "FFD800", "RGB hex color inside the valid boost window.");
        MeterColor3 = _plugin.Config.Bind("CNKBoostMeter", "HighColor", "FF6A00", "RGB hex color after OrangeThreshold.");
        MeterColor4 = _plugin.Config.Bind("CNKBoostMeter", "PerfectColor", "FF0000", "RGB hex color after RedThreshold.");
    }

    private static Image Rectangle(Transform parent, string name, Vector2 size, Vector2 offset, Color color)
    {
        var node = new GameObject(name);
        var rect = node.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        var image = node.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    private static MeterView CreateMeter(int id)
    {
        if (_meterRoot == null)
        {
            _meterRoot = new GameObject("TK2 Mod Garage Boost Meter");
            _meterRoot.AddComponent<RectTransform>();
            var canvas = _meterRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            UnityEngine.Object.DontDestroyOnLoad(_meterRoot);
        }
        var root = new GameObject("Local Boost Meter " + id);
        var rect = root.AddComponent<RectTransform>(); rect.SetParent(_meterRoot.transform, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
        var view = new MeterView { Root = root, Rect = rect };
        for (int i = 0; i < 3; i++)
        {
            Rectangle(rect, "Background " + i, new Vector2(14, 70), new Vector2(i * 20, 0), new Color(0.12f, 0.15f, 0.16f, 0.85f));
            view.Bars[i] = Rectangle(rect, "Boost " + i, new Vector2(10, 0), new Vector2(i * 20 + 2, 2), Color.green);
        }
        return view;
    }

    private static Color MeterColor(float fill, float minimum, float maximum)
    {
        string value = MeterColor1.Value;
        if (fill >= minimum) value = MeterColor2.Value;
        if (fill >= maximum * MeterThreshold3.Value) value = MeterColor3.Value;
        if (fill >= maximum * MeterThreshold4.Value) value = MeterColor4.Value;
        if (ColorUtility.TryParseHtmlString("#" + value.TrimStart('#'), out var color)) return color;
        return Color.green;
    }

    private static void CaptureMeter(Ant_BoostManager boost)
    {
        if (!MeterEnabled.Value) return;
        var kart = boost.kartController;
        if (kart == null || !Local(kart.parentPlayer)) return;
        try
        {
            var camera = kart.parentPlayer.gameplayCamera?.GetCamera()?.unityCamera;
            if (camera == null) return;
            int id = kart.parentPlayer.GetInstanceID();
            if (!Meters.TryGetValue(id, out var view)) Meters[id] = view = CreateMeter(id);
            Vector3 projected = camera.WorldToScreenPoint(kart.GetKartPos());
            bool visible = projected.z > 0 && Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
            view.Root.SetActive(visible);
            view.LastUpdate = Time.unscaledTime;
            float scale = Screen.height / 1080f * MeterScale.Value;
            view.Rect.localScale = Vector3.one * scale;
            view.Rect.anchoredPosition = new Vector2(projected.x + MeterX.Value * scale, projected.y + MeterY.Value * scale);
            for (int i = 0; i < 3; i++)
            {
                float fill = i < boost.fCurrentBoostFillTime.Length ? boost.fCurrentBoostFillTime[i] : 0;
                float fraction = boost.fMaximumTimeForBestBoost <= 0 ? 0 : Mathf.Clamp01(fill / boost.fMaximumTimeForBestBoost);
                view.Bars[i].rectTransform.sizeDelta = new Vector2(10, 66 * fraction);
                view.Bars[i].color = MeterColor(fill, boost.fMinimumTimeForBoost, boost.fMaximumTimeForBestBoost);
            }
        }
        catch (Exception ex) { Fault(MeterEnabled, ex); RestoreMeter(); }
    }

    internal static void Tick()
    {
        if (!Voice.Value || !Plugin.OfflineLabAllowed) RestoreVoiceAttenuation();
        if (!Trainer.Value)
        {
            foreach (var input in RumblePlayers.Values) input.StopVibration();
            RumblePlayers.Clear(); TrainerWasReady.Clear();
        }
        if (!SaveStates.Value || !Plugin.OfflineLabAllowed) { PracticeStates.Clear(); _savedScene = ""; }
        if (!KartParameters.Value || !Plugin.OfflineLabAllowed) foreach (var parameter in KartValues) parameter.Restore();
        if (!BoostParameters.Value || !Plugin.OfflineLabAllowed) foreach (var parameter in BoostValues) parameter.Restore();
        if (!MeterEnabled.Value) RestoreMeter();
        else foreach (var view in Meters.Values)
                if (view.Root != null && Time.unscaledTime - view.LastUpdate > 0.5f) view.Root.SetActive(false);
    }
    private static void RestoreMeter()
    {
        if (_meterRoot != null) UnityEngine.Object.Destroy(_meterRoot);
        _meterRoot = null; Meters.Clear();
    }
}

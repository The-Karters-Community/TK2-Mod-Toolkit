using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime.Attributes;

namespace TK2.Customization;

public sealed class StudioBehaviour : MonoBehaviour
{
    public StudioBehaviour(IntPtr pointer) : base(pointer) { }
    private readonly Dictionary<int, (Canvas Item, float Original)> _canvases = new();
    private readonly Dictionary<int, (CanvasGroup Item, float Original)> _opacity = new();
    private float? _originalShadowDistance;
    private float _nextScan;
    private string? _configError;
    private bool _uiFaulted, _audioFaulted;
    private bool _mkFaulted, _communityFaulted, _nightmareFaulted;

    [HideFromIl2Cpp]
    internal static void DrawSolidPanel(Rect rect)
    {
        var old = GUI.color;
        GUI.color = new Color(.035f, .045f, .065f, .98f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(.33f, .43f, .58f, 1f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    [HideFromIl2Cpp]
    internal static bool DrawCheckBox(Rect rect, bool value, string label)
    {
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        var old = GUI.color;
        var box = new Rect(rect.x + 2, rect.y + 3, 17, 17);
        GUI.color = new Color(.025f, .035f, .05f, 1f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(.65f, .72f, .82f, 1f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.x, box.yMax - 1, box.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.x, box.y, 1, box.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(box.xMax - 1, box.y, 1, box.height), Texture2D.whiteTexture);
        if (value)
        {
            GUI.color = new Color(.08f, .72f, .32f, 1f);
            GUI.DrawTexture(new Rect(box.x + 2, box.y + 2, box.width - 4, box.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 1, box.y - 2, box.width + 2, box.height + 2), "✓");
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 27, rect.y, rect.width - 29, rect.height), label);
        GUI.color = old;
        return clicked ? !value : value;
    }

    public void Update()
    {
        var p = Plugin.Instance;
        if (p == null) return;
        // Config saves/reloads and camera UI updates stay on Unity's main thread.
        try
        {
            if (LiveConfig.Tick(p, Time.unscaledTime))
            {
                _uiFaulted = _audioFaulted = false;
                _mkFaulted = _communityFaulted = _nightmareFaulted = false;
                p.PhysicsFaulted = false;
            }
            _configError = null;
        }
        catch (Exception ex)
        {
            LiveConfig.Status = $"Config reload/save failed: {ex.Message}";
            if (_configError != ex.Message) p.Log.LogWarning(LiveConfig.Status);
            _configError = ex.Message;
        }
        CameraPanel.Tick(p);
        RunFeature(ref _audioFaulted, "audio", () => AudioFeature.Tick(p));
        RecipeHost.Tick();
        PerformanceFeature.Tick();
        PerformanceDiagnostics.Tick();
        TrackBoundaries.Tick();
        RunFeature(ref _mkFaulted, "MK modules", LegacyMK.Tick);
        RunFeature(ref _communityFaulted, "community commands", CommunityMods.Tick);
        RunFeature(ref _nightmareFaulted, "Nightmare AI", NightmareAI.Tick);
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 1f;
            RunFeature(ref _uiFaulted, "UI", () => UpdateHud(p));
            try { UpdateVisualPack(p); }
            catch (Exception ex) { p.HudOpacityEnabled.Value = false; p.RenderEnabled.Value = false; p.Log.LogError(ex); RestoreVisualPack(); }
        }
    }

    [HideFromIl2Cpp]
    private void RunFeature(ref bool faulted, string name, Action work)
    {
        if (faulted) return;
        try { work(); }
        catch (Exception ex)
        {
            faulted = true;
            Plugin.Instance?.Log.LogError($"{name} disabled after runtime failure: {ex.Message}");
            // Best-effort restore after failure; isolate restoration from other modules.
            try {
                if (name == "UI") RestoreHud();
                else if (name == "MK modules") LegacyMK.Restore();
                else if (name == "Nightmare AI") NightmareAI.Restore();
                else if (name == "community commands") CommunityMods.Restore();
                else AudioFeature.Restore();
            }
            catch (Exception restoreEx) { Plugin.Instance?.Log.LogWarning(restoreEx.Message); }
        }
    }

    [HideFromIl2Cpp]
    private void UpdateHud(Plugin p)
    {
        if (!p.UiEnabled.Value) { RestoreHud(); return; }
        var stale = new List<int>();
        foreach (var pair in _canvases)
        {
            var canvas = pair.Value.Item;
            if (canvas == null) { stale.Add(pair.Key); continue; }
            if (!Matches(canvas, p.CanvasFilter.Value)) { canvas.scaleFactor = pair.Value.Original; stale.Add(pair.Key); }
        }
        foreach (int id in stale) _canvases.Remove(id);
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!Matches(canvas, p.CanvasFilter.Value)) continue;
            int id = canvas.GetInstanceID();
            if (!_canvases.TryGetValue(id, out var data)) { data = (canvas, canvas.scaleFactor); _canvases[id] = data; }
            canvas.scaleFactor = data.Original * p.HudScale.Value;
        }
    }

    private static bool Matches(Canvas canvas, string filter) => canvas != null && canvas.isRootCanvas &&
        canvas.renderMode != RenderMode.WorldSpace && !string.IsNullOrWhiteSpace(filter) &&
        canvas.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

    private void RestoreHud()
    {
        foreach (var pair in _canvases) if (pair.Value.Item != null) pair.Value.Item.scaleFactor = pair.Value.Original;
        _canvases.Clear();
    }

    [HideFromIl2Cpp]
    private void UpdateVisualPack(Plugin p)
    {
        if (p.RenderEnabled.Value)
        {
            _originalShadowDistance ??= QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = p.ShadowDistance.Value;
        }
        else if (_originalShadowDistance.HasValue)
        {
            QualitySettings.shadowDistance = _originalShadowDistance.Value;
            _originalShadowDistance = null;
        }
        var stale = new List<int>();
        foreach (var pair in _opacity)
        {
            var group = pair.Value.Item;
            if (group == null) stale.Add(pair.Key);
            else if (!p.HudOpacityEnabled.Value || !Matches(group.GetComponent<Canvas>(), p.CanvasFilter.Value))
            { group.alpha = pair.Value.Original; stale.Add(pair.Key); }
        }
        foreach (int id in stale) _opacity.Remove(id);
        if (!p.HudOpacityEnabled.Value) return;
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!Matches(canvas, p.CanvasFilter.Value)) continue;
            var group = canvas.GetComponent<CanvasGroup>();
            if (group == null) continue;
            int id = group.GetInstanceID();
            if (!_opacity.ContainsKey(id)) _opacity[id] = (group, group.alpha);
            group.alpha = p.HudOpacity.Value;
        }
    }

    private void RestoreVisualPack()
    {
        foreach (var pair in _opacity) if (pair.Value.Item != null) pair.Value.Item.alpha = pair.Value.Original;
        _opacity.Clear();
        if (_originalShadowDistance.HasValue) QualitySettings.shadowDistance = _originalShadowDistance.Value;
        _originalShadowDistance = null;
    }

    public void OnRenderObject() => TrackBoundaries.Render();

    public void OnGUI()
    {
        var p = Plugin.Instance;
        if (p != null) CameraPanel.Draw(p);
        TrackBoundaries.Draw();
    }

    public void RestoreAll() { RestoreHud(); RestoreVisualPack(); TrackBoundaries.Restore(); }
    public void OnDestroy() { if (Plugin.Instance != null) CameraPanel.Close(Plugin.Instance); RestoreAll(); }
}

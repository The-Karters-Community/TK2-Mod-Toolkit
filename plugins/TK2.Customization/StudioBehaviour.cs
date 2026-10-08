using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace TK2.Customization;

public sealed class StudioBehaviour : MonoBehaviour
{
    public StudioBehaviour(IntPtr pointer) : base(pointer) { }
    private readonly Dictionary<int, (Canvas Item, float Original)> _canvases = new();
    private readonly Dictionary<int, (Camera Item, float Original)> _cameras = new();
    private readonly Dictionary<int, (CanvasGroup Item, float Original)> _opacity = new();
    private float? _originalShadowDistance;
    private DateTime _configTime;
    private float _nextScan, _nextConfig;
    private bool _uiFaulted, _cameraFaulted, _audioFaulted;

    public void Update()
    {
        var p = Plugin.Instance;
        if (p == null) return;
        // File polling and reload are performed on the Unity main thread, never FileSystemWatcher callbacks.
        if (Time.unscaledTime >= _nextConfig)
        {
            _nextConfig = Time.unscaledTime + 1f;
            try
            {
                var modified = File.GetLastWriteTimeUtc(p.Config.ConfigFilePath);
                if (_configTime != default && modified != _configTime) p.Config.Reload();
                _configTime = modified;
            }
            catch (Exception ex) { p.Log.LogWarning($"Config reload failed: {ex.Message}"); }
        }
        RunFeature(ref _audioFaulted, "audio", () => AudioFeature.Tick(p.AudioEnabled.Value, p.Volume.Value));
        RecipeHost.Tick();
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 1f;
            RunFeature(ref _uiFaulted, "UI", () => UpdateHud(p));
            try { UpdateVisualPack(p); }
            catch (Exception ex) { p.HudOpacityEnabled.Value = false; p.RenderEnabled.Value = false; p.Log.LogError(ex); RestoreVisualPack(); }
        }
    }

    // Follow cameras often update after normal Update; apply FOV in LateUpdate.
    public void LateUpdate()
    {
        var p = Plugin.Instance;
        if (p != null) RunFeature(ref _cameraFaulted, "camera", () => UpdateCamera(p));
    }

    private void RunFeature(ref bool faulted, string name, Action work)
    {
        if (faulted) return;
        try { work(); }
        catch (Exception ex)
        {
            faulted = true;
            Plugin.Instance?.Log.LogError($"{name} disabled after runtime failure: {ex.Message}");
            // Best-effort restore after failure; isolate restoration from other modules.
            try { if (name == "UI") RestoreHud(); else if (name == "camera") RestoreCamera(); else AudioFeature.Restore(); }
            catch (Exception restoreEx) { Plugin.Instance?.Log.LogWarning(restoreEx.Message); }
        }
    }

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

    private static bool Matches(Canvas canvas, string filter) => canvas.isRootCanvas &&
        canvas.renderMode != RenderMode.WorldSpace && !string.IsNullOrWhiteSpace(filter) &&
        canvas.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

    private void UpdateCamera(Plugin p)
    {
        if (!p.CameraEnabled.Value) { RestoreCamera(); return; }
        Camera camera = Camera.main;
        var stale = new List<int>();
        foreach (var pair in _cameras)
        {
            if (pair.Value.Item == null) stale.Add(pair.Key);
            else if (pair.Value.Item != camera) { pair.Value.Item.fieldOfView = pair.Value.Original; stale.Add(pair.Key); }
        }
        foreach (int id in stale) _cameras.Remove(id);
        if (camera == null || camera.orthographic) return;
        int current = camera.GetInstanceID();
        if (!_cameras.TryGetValue(current, out var data)) { data = (camera, camera.fieldOfView); _cameras[current] = data; }
        camera.fieldOfView = p.Fov.Value;
    }

    private void RestoreHud()
    {
        foreach (var pair in _canvases) if (pair.Value.Item != null) pair.Value.Item.scaleFactor = pair.Value.Original;
        _canvases.Clear();
    }

    private void RestoreCamera()
    {
        foreach (var pair in _cameras) if (pair.Value.Item != null) pair.Value.Item.fieldOfView = pair.Value.Original;
        _cameras.Clear();
    }

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

    public void RestoreAll() { RestoreHud(); RestoreCamera(); RestoreVisualPack(); }
    public void OnDestroy() { RestoreAll(); }
}

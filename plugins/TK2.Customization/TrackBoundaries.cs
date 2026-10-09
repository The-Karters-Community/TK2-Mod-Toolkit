using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK2.Customization;

// Reveal authored debug meshes, not generated approximations. Native evidence: docs/TRACK-BOUNDARIES.md.
internal static class TrackBoundaries
{
    private static ConfigEntry<bool> _enabled = null!, _walls = null!, _respawn = null!;
    private static ConfigEntry<float> _distance = null!;
    private static ConfigEntry<string> _key = null!;
    private static bool _wasEnabled, _visible, _faulted;
    private static float _nextScan;
    private static int _mapId;
    private static string _status = "Waiting for track";
    private static readonly Dictionary<int, Snapshot> Saved = new();
    private static readonly HashSet<int> Owned = new();
    private static readonly HashSet<int> Selected = new();
    private static readonly List<int> Stale = new();
    private static readonly Dictionary<(int Shader, BoundaryKind Kind), Material> Palette = new();
    private sealed class Snapshot
    {
        internal MeshRenderer Renderer = null!;
        internal GameObject ViewObject = null!;
        internal MeshRenderer ViewRenderer = null!;
        internal int ViewId;
        internal Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material> Materials = null!;
        internal Material Tint = null!;
    }

    internal static bool Visible => _visible && Saved.Count != 0;
    internal static void Install(Plugin p)
    {
        _enabled = p.Config.Bind("TrackBoundaries", "Enabled", false, "Show only authored invisible-wall and respawn debug meshes in offline races.");
        _walls = p.Config.Bind("TrackBoundaries", "ShowWalls", true, "Cyan invisible walls.");
        _respawn = p.Config.Bind("TrackBoundaries", "ShowRespawn", true, "Orange respawn boundaries, including conditional flat boundaries.");
        _distance = p.Config.Bind("TrackBoundaries", "DrawDistance", 200f,
            new ConfigDescription("Show boundary meshes within this many metres of a local kart.", new AcceptableValueRange<float>(25, 1000)));
        _key = p.Config.Bind("TrackBoundaries", "ToggleKey", "F10", "Toggle visibility during an offline race while this module is enabled.");
    }
    internal static void Tick()
    {
        if (_enabled == null) return;
        if (!_enabled.Value) { Restore(); _wasEnabled = _faulted = false; return; }
        if (_faulted) return;
        try
        {
            if (!_wasEnabled) { _visible = true; _wasEnabled = true; _nextScan = 0; }
            bool allowed = Plugin.Instance?.GameplayReady == true && Plugin.OfflineLabAllowed && MenuManager.Instance == null &&
                Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
            if (!allowed) { RestoreMeshes(); _mapId = 0; _nextScan = 0; return; }
            if (Application.isFocused && Enum.TryParse<KeyCode>(_key.Value, true, out var key) && key != KeyCode.None && Input.GetKeyDown(key))
            { _visible = !_visible; _nextScan = 0; }
            if (!_visible) { RestoreMeshes(); return; }
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1;
            Scan();
        }
        catch (Exception ex)
        {
            _faulted = true; RestoreMeshes(); DestroyPalette();
            Plugin.Instance?.Log.LogWarning("Track boundaries unavailable: " + ex.Message);
        }
    }
    private static void Scan()
    {
        var map = Ant_MapData.instance;
        if (map == null) { RestoreMeshes(); _status = "Waiting for track"; return; }
        int mapId = map.GetInstanceID();
        if (_mapId != mapId) { RestoreMeshes(); DestroyPalette(); _mapId = mapId; }
        var karts = UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>();
        var local = new List<PixelEasyCharMoveKartController>();
        foreach (var kart in karts)
            if (kart != null && kart.parentPlayer != null && kart.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && kart.gameObject.activeInHierarchy)
                local.Add(kart);
        if (local.Count == 0) { RestoreMeshes(); _status = "Waiting for local kart"; return; }
        Selected.Clear(); int walls = 0, respawn = 0, unsupported = 0;
        foreach (var hidden in UnityEngine.Object.FindObjectsOfType<PTK_HideMeshRendererInPlayMode>())
        {
            if (hidden == null || !hidden.bEnabled || !hidden.gameObject.activeInHierarchy || hidden.GetComponentInParent<Ant_Player>() != null) continue;
            foreach (var renderer in hidden.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer == null || Owned.Contains(renderer.GetInstanceID()) ||
                    (!hidden.bIncludeChildrenMeshRenderers && renderer.transform != hidden.transform)) continue;
                // A collider must belong to this hide component's subtree. Do not classify unrelated siblings.
                var collider = renderer.GetComponent<Collider>();
                var parent = renderer.transform.parent;
                while (collider == null && parent != null && renderer.transform != hidden.transform)
                {
                    collider = parent.GetComponent<Collider>();
                    if (parent == hidden.transform) break;
                    parent = parent.parent;
                }
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.GetComponentInParent<Ant_Player>() != null) continue;
                if (collider.gameObject.scene.handle != map.gameObject.scene.handle &&
                    collider.gameObject.scene.handle != UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle) continue;
                BoundaryKind kind = BoundaryKind.None;
                foreach (var kart in local)
                {
                    if (collider.bounds.SqrDistance(kart.transform.position) > _distance.Value * _distance.Value) continue;
                    var candidate = BoundarySelection.Classify(collider.gameObject.layer, kart.wallLayerToCollDetect.value,
                        kart.wallRespawn_Flat_ColliderLayer.value, kart.wallRespawn_Always_ColliderLayer.value);
                    if (candidate == BoundaryKind.Respawn) { kind = candidate; break; }
                    if (candidate != BoundaryKind.None) kind = candidate;
                }
                if (kind == BoundaryKind.None || (kind == BoundaryKind.Wall ? !_walls.Value : !_respawn.Value)) continue;
                int id = renderer.GetInstanceID();
                if (!Selected.Add(id)) continue;
                if (!Saved.TryGetValue(id, out var saved) || saved.Renderer != renderer)
                {
                    RestoreOne(id);
                    var tint = MaterialFor(renderer, kind);
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (tint == null || mesh == null) { unsupported++; continue; }
                    // Copy only authored geometry into a collider-free visual child. Never
                    // expand camera masks or enable the original renderer's entire layer.
                    var view = new GameObject("TK2 boundary view") { hideFlags = HideFlags.DontSave, layer = 0 };
                    view.transform.SetParent(renderer.transform, false);
                    view.transform.localPosition = Vector3.zero;
                    view.transform.localRotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one;
                    try
                    {
                        view.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var visual = view.AddComponent<MeshRenderer>();
                        visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
                        saved = new Snapshot { Renderer = renderer, Materials = renderer.sharedMaterials, Tint = tint,
                            ViewObject = view, ViewRenderer = visual, ViewId = visual.GetInstanceID() };
                        Owned.Add(saved.ViewId);
                    }
                    catch { UnityEngine.Object.Destroy(view); throw; }
                    Saved[id] = saved;
                }
                else saved.Tint = MaterialFor(renderer, kind) ?? saved.Tint;
                int materialCount = saved.Materials.Length;
                var tinted = new Material[Math.Max(1, materialCount)];
                for (int i = 0; i < tinted.Length; i++) tinted[i] = saved.Tint;
                saved.ViewRenderer.sharedMaterials = tinted;
                saved.ViewRenderer.enabled = true;
                if (kind == BoundaryKind.Wall) walls++; else respawn++;
            }
        }
        Stale.Clear(); foreach (var pair in Saved) if (!Selected.Contains(pair.Key)) Stale.Add(pair.Key);
        foreach (int id in Stale) RestoreOne(id);
        string status = $"{walls} walls · {respawn} respawn meshes" + (unsupported == 0 ? "" : $" · {unsupported} unsupported meshes/materials");
        if (walls + respawn == 0) status += " (no matching authored meshes nearby)";
        if (_status != status) Plugin.Instance?.Log.LogInfo("Track boundaries: " + status);
        _status = status;
    }
    private static Material? MaterialFor(MeshRenderer renderer, BoundaryKind kind)
    {
        // Prefer a shader shipped with this actual debug mesh. No stripped shader assumptions.
        var source = Saved.TryGetValue(renderer.GetInstanceID(), out var saved) ? saved.Materials : renderer.sharedMaterials;
        foreach (var original in source)
        {
            if (original == null || original.shader == null) continue;
            var key = (original.shader.GetInstanceID(), kind);
            if (Palette.TryGetValue(key, out var cached)) return cached;
            var shader = Shader.Find("Hidden/Internal-Colored");
            var material = shader != null ? new Material(shader) : new Material(original);
            material.hideFlags = HideFlags.DontSave;
            var color = kind == BoundaryKind.Wall ? new Color(.08f, .82f, 1f, .35f) : new Color(1f, .35f, .08f, .4f);
            bool colored = false;
            if (material.HasProperty("_Color")) { material.SetColor("_Color", color); colored = true; }
            if (material.HasProperty("_BaseColor")) { material.SetColor("_BaseColor", color); colored = true; }
            if (!colored) { UnityEngine.Object.Destroy(material); continue; }
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            material.renderQueue = 3100;
            Palette[key] = material; return material;
        }
        return null;
    }
    internal static void Draw()
    {
        if (_enabled == null || !_enabled.Value || !Plugin.OfflineLabAllowed || MenuManager.Instance != null) return;
        var color = GUI.color;
        try
        {
            GUI.Box(new Rect(16, Screen.height - 88, 420, 72), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(28, Screen.height - 83, 396, 22), $"Track boundaries · {_key.Value} · {(_visible ? "ON" : "OFF")}");
            GUI.color = new Color(.08f, .82f, 1f, 1); GUI.Label(new Rect(28, Screen.height - 61, 180, 22), "Cyan: invisible walls");
            GUI.color = new Color(1f, .45f, .15f, 1); GUI.Label(new Rect(210, Screen.height - 61, 210, 22), "Orange: respawn boundaries");
            GUI.color = Color.white; GUI.Label(new Rect(28, Screen.height - 40, 396, 22), _faulted ? "Unavailable; see BepInEx log" : _status);
        }
        finally { GUI.color = color; }
    }
    private static void RestoreOne(int id)
    {
        if (!Saved.Remove(id, out var saved)) return;
        Owned.Remove(saved.ViewId);
        try
        {
            if (saved.ViewRenderer != null) saved.ViewRenderer.enabled = false;
            if (saved.ViewObject != null) { saved.ViewObject.SetActive(false); UnityEngine.Object.Destroy(saved.ViewObject); }
        }
        catch (Exception ex) { Plugin.Instance?.Log.LogWarning("Boundary restore: " + ex.Message); }
    }
    private static void RestoreMeshes()
    {
        Stale.Clear(); Stale.AddRange(Saved.Keys); foreach (int id in Stale) RestoreOne(id);
        Owned.Clear();
    }
    private static void DestroyPalette()
    {
        foreach (var material in Palette.Values) if (material != null) UnityEngine.Object.Destroy(material);
        Palette.Clear();
    }
    internal static void Restore() { RestoreMeshes(); DestroyPalette(); _visible = false; _mapId = 0; _nextScan = 0; }
}

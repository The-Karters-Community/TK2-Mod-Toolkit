using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK2.Customization;

// Native hit masks select the collider; visualization never changes physics.
internal static class TrackBoundaries
{
    private static ConfigEntry<bool> _enabled = null!, _walls = null!, _respawn = null!;
    private static ConfigEntry<float> _distance = null!;
    private static ConfigEntry<string> _key = null!;
    private static bool _wasEnabled, _visible, _faulted;
    private static float _nextScan, _nextInventory;
    private static int _mapId;
    private static string _status = "Waiting for track";
    private static Collider[] _inventory = Array.Empty<Collider>();
    private static readonly Dictionary<int, Snapshot> Saved = new();
    private static readonly HashSet<int> Selected = new();
    private static readonly List<int> Stale = new();
    private static readonly Dictionary<BoundaryKind, Material> Palette = new();
    private sealed class Snapshot
    {
        internal Collider Collider = null!;
        internal BoundaryGeometry Geometry = null!;
        internal GameObject ViewObject = null!;
        internal MeshRenderer ViewRenderer = null!;
        internal BoundaryKind Kind;
    }

    internal static bool Visible => _visible && Saved.Count != 0;
    internal static void Install(Plugin p)
    {
        _enabled = p.Config.Bind("TrackBoundaries", "Enabled", false, "Show invisible-wall and respawn collider geometry in offline races.");
        _walls = p.Config.Bind("TrackBoundaries", "ShowWalls", true, "Cyan invisible walls.");
        _respawn = p.Config.Bind("TrackBoundaries", "ShowRespawn", true, "Orange respawn boundaries, including conditional flat boundaries.");
        _distance = p.Config.Bind("TrackBoundaries", "DrawDistance", 200f,
            new ConfigDescription("Show boundary colliders within this many metres of a local kart.", new AcceptableValueRange<float>(25, 1000)));
        _key = p.Config.Bind("TrackBoundaries", "ToggleKey", "F10", "Toggle visibility during an offline race while this module is enabled.");
    }
    private static bool Allowed => Plugin.Instance?.GameplayReady == true && Plugin.OfflineLabAllowed && MenuManager.Instance == null &&
        Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
    internal static void Tick()
    {
        if (_enabled == null) return;
        if (!_enabled.Value) { Restore(); _wasEnabled = _faulted = false; return; }
        if (_faulted) return;
        try
        {
            if (!_wasEnabled) { _visible = true; _wasEnabled = true; _nextScan = 0; }
            if (!Allowed) { ClearTrack(); return; }
            if (Application.isFocused && Enum.TryParse<KeyCode>(_key.Value, true, out var key) && key != KeyCode.None && Input.GetKeyDown(key))
            { _visible = !_visible; _nextScan = 0; }
            if (!_visible) { ClearTrack(); return; }
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1;
                Scan();
            }
            Stale.Clear();
            foreach (var pair in Saved)
            {
                var saved = pair.Value;
                if (saved.Collider == null || !saved.Collider.enabled || !saved.Collider.gameObject.activeInHierarchy || saved.ViewObject == null)
                    Stale.Add(pair.Key);
                else saved.Geometry.Apply(saved.ViewObject.transform);
            }
            foreach (int id in Stale) Remove(id);
        }
        catch (Exception ex)
        {
            _faulted = true; ClearTrack();
            Plugin.Instance?.Log.LogWarning("Track boundaries unavailable: " + ex.Message);
        }
    }
    private static void Scan()
    {
        var map = Ant_MapData.instance;
        if (map == null) { ClearTrack(); _status = "Waiting for track"; return; }
        int mapId = map.GetInstanceID();
        if (_mapId != mapId) { ClearTrack(); _mapId = mapId; }
        var local = new List<PixelEasyCharMoveKartController>();
        foreach (var kart in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
            if (kart != null && kart.parentPlayer != null && kart.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && kart.gameObject.activeInHierarchy)
                local.Add(kart);
        if (local.Count == 0) { ClearTrack(); _status = "Waiting for local kart"; return; }
        // Track physics can live in additive scenes. Native collision masks, not the
        // map object's scene or a debug renderer component, identify boundaries.
        if (_inventory.Length == 0 || Time.unscaledTime >= _nextInventory)
        {
            var found = UnityEngine.Object.FindObjectsOfType<Collider>();
            _inventory = new Collider[found.Length];
            for (int i = 0; i < found.Length; i++) _inventory[i] = found[i];
            _nextInventory = Time.unscaledTime + 5;
        }
        Selected.Clear();
        int walls = 0, respawn = 0, unsupported = 0, matched = 0, distant = 0, visibleWalls = 0;
        foreach (var collider in _inventory)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.GetComponentInParent<Ant_Player>() != null) continue;
            BoundaryKind kind = BoundaryKind.None;
            bool near = false;
            foreach (var kart in local)
            {
                var candidate = BoundarySelection.Classify(collider.gameObject.layer, kart.wallLayerToCollDetect.value,
                    kart.wallRespawn_Flat_ColliderLayer.value, kart.wallRespawn_Always_ColliderLayer.value);
                if (candidate == BoundaryKind.None) continue;
                if (candidate == BoundaryKind.Respawn || kind == BoundaryKind.None) kind = candidate;
                if (collider.bounds.SqrDistance(kart.transform.position) <= _distance.Value * _distance.Value) near = true;
            }
            if (kind == BoundaryKind.None) continue;
            matched++;
            if (!near) { distant++; continue; }
            if (kind == BoundaryKind.Wall && IsAlreadyVisible(collider)) { visibleWalls++; continue; }
            if (kind == BoundaryKind.Wall ? !_walls.Value : !_respawn.Value) continue;
            int id = collider.GetInstanceID();
            if (!Selected.Add(id)) continue;
            if (!Saved.TryGetValue(id, out var saved) || saved.Collider != collider)
            {
                Remove(id);
                var geometry = BoundaryGeometry.Create(collider);
                if (geometry == null) { unsupported++; continue; }
                var tint = MaterialFor(kind, collider);
                if (tint == null) { geometry.Dispose(); unsupported++; continue; }
                var view = new GameObject("TK2 boundary view") { hideFlags = HideFlags.DontSave, layer = 0 };
                try
                {
                    geometry.Attach(view.transform);
                    view.AddComponent<MeshFilter>().sharedMesh = geometry.Mesh;
                    var visual = view.AddComponent<MeshRenderer>();
                    SetMaterials(visual, geometry.Mesh, tint);
                    // Automatic MeshRenderer drawing would fill the entire volume.
                    // Render() draws this mesh explicitly with scoped wireframe state.
                    visual.enabled = false;
                    visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
                    saved = new Snapshot { Collider = collider, Geometry = geometry, ViewObject = view, ViewRenderer = visual, Kind = kind };
                    Saved[id] = saved;
                }
                catch { UnityEngine.Object.Destroy(view); geometry.Dispose(); throw; }
            }
            else
            {
                if (!saved.Geometry.Refresh()) { Remove(id); unsupported++; continue; }
                saved.ViewObject.GetComponent<MeshFilter>().sharedMesh = saved.Geometry.Mesh;
                if (saved.Kind != kind)
                {
                    var tint = MaterialFor(kind, collider);
                    if (tint == null) { Remove(id); unsupported++; continue; }
                    saved.Kind = kind;
                }
                SetMaterials(saved.ViewRenderer, saved.Geometry.Mesh, Palette[kind]);
            }
            saved.ViewRenderer.enabled = false;
            if (kind == BoundaryKind.Wall) walls++; else respawn++;
        }
        Stale.Clear(); foreach (var pair in Saved) if (!Selected.Contains(pair.Key)) Stale.Add(pair.Key);
        foreach (int id in Stale) Remove(id);
        string status = $"{walls} walls · {respawn} respawn shapes" + (unsupported == 0 ? "" : $" · {unsupported} unsupported");
        if (walls + respawn == 0) status += $" (mask matches {matched}, far {distant}, visible {visibleWalls})";
        if (_status != status)
        {
            var kart = local[0];
            Plugin.Instance?.Log.LogInfo($"Track boundaries: {status}; scanned {_inventory.Length} colliders; masks wall=0x{kart.wallLayerToCollDetect.value:X8}, flat=0x{kart.wallRespawn_Flat_ColliderLayer.value:X8}, always=0x{kart.wallRespawn_Always_ColliderLayer.value:X8}");
        }
        _status = status;
    }
    private static void SetMaterials(MeshRenderer renderer, Mesh mesh, Material tint)
    {
        int count = Math.Max(1, mesh.subMeshCount);
        var current = renderer.sharedMaterials;
        if (current.Length == count && current[0] == tint) return;
        var materials = new Material[count];
        for (int i = 0; i < count; i++) materials[i] = tint;
        renderer.sharedMaterials = materials;
    }
    private static bool IsAlreadyVisible(Collider collider)
    {
        var renderer = collider.GetComponent<MeshRenderer>();
        if (renderer == null || !renderer.enabled || renderer.forceRenderingOff) return false;
        var mesh = collider.TryCast<MeshCollider>();
        return mesh != null && mesh.sharedMesh != null && collider.GetComponent<MeshFilter>()?.sharedMesh == mesh.sharedMesh;
    }
    private static Material? MaterialFor(BoundaryKind kind, Collider collider)
    {
        if (Palette.TryGetValue(kind, out var cached)) return cached;
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        Material? material = shader != null ? new Material(shader) : null;
        if (material == null)
        {
            var renderer = collider.GetComponent<MeshRenderer>();
            if (renderer != null)
                foreach (var original in renderer.sharedMaterials)
                    if (original != null && original.shader != null && (original.HasProperty("_Color") || original.HasProperty("_BaseColor")))
                    { material = new Material(original); break; }
        }
        if (material == null) return null;
        material.hideFlags = HideFlags.DontSave;
        var color = kind == BoundaryKind.Wall ? new Color(.08f, .82f, 1f, .85f) : new Color(1f, .35f, .08f, .85f);
        bool colored = false;
        if (material.HasProperty("_Color")) { material.SetColor("_Color", color); colored = true; }
        if (material.HasProperty("_BaseColor")) { material.SetColor("_BaseColor", color); colored = true; }
        if (!colored) { UnityEngine.Object.Destroy(material); return null; }
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
        material.renderQueue = 3100;
        Palette[kind] = material; return material;
    }
    internal static void Render()
    {
        if (_enabled == null || !_enabled.Value || !_visible || _faulted || !Allowed || Saved.Count == 0) return;
        var camera = Camera.current;
        if (camera == null || camera.cameraType != CameraType.Game || (camera.cullingMask & 1) == 0) return;
        bool wireframe = GL.wireframe;
        try
        {
            // Never leave wireframe enabled for the native scene or HUD, including
            // when material binding/drawing throws. Native GPU meshes stay shared.
            GL.wireframe = true;
            foreach (var saved in Saved.Values)
            {
                if (saved.Collider == null || !saved.Collider.enabled || !saved.Collider.gameObject.activeInHierarchy ||
                    saved.ViewObject == null || !saved.ViewObject.activeInHierarchy || saved.Geometry.Mesh == null) continue;
                if (!Palette[saved.Kind].SetPass(0)) continue;
                for (int i = 0; i < saved.Geometry.Mesh.subMeshCount; i++)
                    Graphics.DrawMeshNow(saved.Geometry.Mesh, saved.ViewObject.transform.localToWorldMatrix, i);
            }
        }
        catch (Exception ex)
        {
            _faulted = true;
            Plugin.Instance?.Log.LogWarning("Boundary outline rendering unavailable: " + ex.Message);
        }
        finally { GL.wireframe = wireframe; }
        if (_faulted) ClearTrack();
    }
    internal static void Draw()
    {
        if (_enabled == null || !_enabled.Value || !Allowed) return;
        var color = GUI.color;
        try
        {
            GUI.Box(new Rect(16, Screen.height - 88, 500, 72), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(28, Screen.height - 83, 476, 22), $"Track boundaries · {_key.Value} · {(_visible ? "ON" : "OFF")}");
            GUI.color = new Color(.08f, .82f, 1f, 1); GUI.Label(new Rect(28, Screen.height - 61, 180, 22), "Cyan: invisible walls");
            GUI.color = new Color(1f, .45f, .15f, 1); GUI.Label(new Rect(210, Screen.height - 61, 250, 22), "Orange: respawn boundaries");
            GUI.color = Color.white; GUI.Label(new Rect(28, Screen.height - 40, 476, 22), _faulted ? "Unavailable; see BepInEx log" : _status);
        }
        finally { GUI.color = color; }
    }
    private static void Remove(int id)
    {
        if (!Saved.Remove(id, out var saved)) return;
        try
        {
            if (saved.ViewRenderer != null) saved.ViewRenderer.enabled = false;
            if (saved.ViewObject != null) { saved.ViewObject.SetActive(false); UnityEngine.Object.Destroy(saved.ViewObject); }
        }
        catch (Exception ex) { Plugin.Instance?.Log.LogWarning("Boundary cleanup: " + ex.Message); }
        finally { saved.Geometry.Dispose(); }
    }
    private static void ClearTrack()
    {
        Stale.Clear(); Stale.AddRange(Saved.Keys); foreach (int id in Stale) Remove(id);
        foreach (var material in Palette.Values) if (material != null) UnityEngine.Object.Destroy(material);
        Palette.Clear(); _inventory = Array.Empty<Collider>(); _mapId = 0; _nextInventory = _nextScan = 0;
    }
    internal static void Restore() { ClearTrack(); _visible = false; }
}

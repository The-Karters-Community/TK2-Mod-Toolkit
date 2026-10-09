using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK2.Customization;

// Native hit masks select the collider; visualization never changes physics.
internal static class TrackBoundaries
{
    private static ConfigEntry<bool> _enabled = null!, _walls = null!, _respawn = null!, _xray = null!, _fills = null!;
    private static ConfigEntry<float> _distance = null!;
    private static ConfigEntry<float> _fillOpacity = null!;
    private static ConfigEntry<int> _maxVisible = null!;
    private static ConfigEntry<string> _key = null!, _xrayKey = null!, _inspectKey = null!, _optionsKey = null!;
    private static bool _wasEnabled, _visible, _faulted, _optionsOpen, _cursorOwned;
    private static bool _previousCursorVisible;
    private static CursorLockMode _previousCursorLock;
    private static float _nextScan, _nextInventory;
    private static int _mapId;
    private static string _status = "Waiting for track";
    private static bool _lastXray;
    private static float _lastFillOpacity = -1f;
    private static Rect _optionsRect = new(0, 40, 440, 365);
    private static int _pinnedId = -1, _candidateCount, _renderedThisFrame;
    private static Camera? _focusCamera;
    private static Collider[] _inventory = Array.Empty<Collider>();
    private static readonly Dictionary<int, Snapshot> Saved = new();
    private static readonly HashSet<int> Selected = new();
    private static readonly List<int> Stale = new();
    private static readonly List<Candidate> Candidates = new();
    private static readonly List<Snapshot> DrawOrder = new();
    private static readonly Dictionary<BoundaryKind, Material> Palette = new();
    private static readonly Dictionary<BoundaryKind, Material> FillPalette = new();
    private static Material? _pinnedMaterial;
    private struct Candidate
    {
        internal Collider Collider;
        internal BoundaryKind Kind;
        internal float KartDistance;
        internal Candidate(Collider collider, BoundaryKind kind, float distance)
        { Collider = collider; Kind = kind; KartDistance = distance; }
    }
    private sealed class Snapshot
    {
        internal Collider Collider = null!;
        internal BoundaryGeometry Geometry = null!;
        internal GameObject ViewObject = null!;
        internal MeshRenderer ViewRenderer = null!;
        internal BoundaryKind Kind;
        internal float KartDistance;
    }

    internal static bool Visible => _visible && Saved.Count != 0;
    internal static void Install(Plugin p)
    {
        _enabled = p.Config.Bind("TrackBoundaries", "Enabled", false, "Inspect invisible-wall and respawn colliders in offline races.");
        _walls = p.Config.Bind("TrackBoundaries", "ShowWalls", true, "Cyan invisible walls.");
        _respawn = p.Config.Bind("TrackBoundaries", "ShowRespawn", true, "Orange respawn boundaries, including conditional flat boundaries.");
        _distance = p.Config.Bind("TrackBoundaries", "DrawDistance", 200f,
            new ConfigDescription("Show boundary colliders within this many metres of a local kart.", new AcceptableValueRange<float>(25, 1000)));
        _fills = p.Config.Bind("TrackBoundaries", "ShowSurfaces", false, "Draw translucent collider surfaces behind the outlines.");
        _fillOpacity = p.Config.Bind("TrackBoundaries", "SurfaceOpacity", .12f,
            new ConfigDescription("Opacity of translucent boundary surfaces.", new AcceptableValueRange<float>(.03f, .35f)));
        _maxVisible = p.Config.Bind("TrackBoundaries", "MaxVisibleColliders", 64,
            new ConfigDescription("Maximum number of nearest boundaries to outline per track.", new AcceptableValueRange<int>(16, 192)));
        _key = p.Config.Bind("TrackBoundaries", "ToggleKey", "F10", "Toggle visibility during an offline race while this module is enabled.");
        _xray = p.Config.Bind("TrackBoundaries", "XRay", false, "Draw outlines through scenery. F9 toggles this during a race.");
        _xrayKey = p.Config.Bind("TrackBoundaries", "XRayKey", "F9", "Toggle outlines through scenery while the inspector is visible.");
        _inspectKey = p.Config.Bind("TrackBoundaries", "InspectKey", "F8", "Pin the boundary under the screen center and show its details; press again to clear.");
        _optionsKey = p.Config.Bind("TrackBoundaries", "OptionsKey", "F7", "Open the in-game track inspector controls.");
    }
    private static bool Allowed => Plugin.Instance?.GameplayReady == true && Plugin.OfflineLabAllowed && MenuManager.Instance == null &&
        Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
    internal static void Tick()
    {
        if (_enabled == null) return;
        if (!_enabled.Value) { Restore(); _wasEnabled = _faulted = false; return; }
        if (_faulted) { CloseOptions(Plugin.Instance); return; }
        try
        {
            if (!_wasEnabled) { _visible = true; _wasEnabled = true; _nextScan = 0; }
            if (!Allowed) { CloseOptions(Plugin.Instance); ClearTrack(); return; }
            if (Application.isFocused)
            {
                if (Pressed(_optionsKey.Value)) ToggleOptions(Plugin.Instance);
                if (_optionsOpen && Pressed("Escape")) CloseOptions(Plugin.Instance);
                if (_optionsOpen)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    if (Pressed(_key.Value)) { _visible = !_visible; _nextScan = 0; }
                    if (_visible && Pressed(_xrayKey.Value)) Set(Plugin.Instance, _xray, !_xray.Value);
                    if (_visible && Pressed(_inspectKey.Value)) TogglePinnedBoundary();
                }
            }
            if (!_visible) { ClearTrack(); return; }
            if (_lastXray != _xray.Value) { ApplyDepthMode(); _lastXray = _xray.Value; }
            if (Math.Abs(_lastFillOpacity - _fillOpacity.Value) > .0001f) ApplyFillOpacity();
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
            _faulted = true; CloseOptions(Plugin.Instance); ClearTrack();
            Plugin.Instance?.Log.LogWarning("Track boundaries unavailable: " + ex.Message);
        }
    }
    private static bool Pressed(string binding) =>
        Enum.TryParse<KeyCode>(binding, true, out var key) && key != KeyCode.None && Input.GetKeyDown(key);

    private static void Set<T>(Plugin? plugin, ConfigEntry<T> entry, T value)
    {
        if (plugin == null) entry.Value = value;
        else LiveConfig.Change(plugin, entry, value, Time.unscaledTime);
    }

    private static void ToggleOptions(Plugin? plugin)
    {
        if (_optionsOpen) { CloseOptions(plugin); return; }
        if (plugin == null) return;
        _optionsOpen = true;
        _previousCursorVisible = Cursor.visible;
        _previousCursorLock = Cursor.lockState;
        _cursorOwned = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void CloseOptions(Plugin? plugin)
    {
        _optionsOpen = false;
        if (_cursorOwned)
        {
            if (Cursor.lockState == CursorLockMode.None)
            { Cursor.lockState = _previousCursorLock; Cursor.visible = _previousCursorVisible; }
            _cursorOwned = false;
        }
        if (plugin == null) return;
        try { LiveConfig.Flush(plugin); }
        catch (Exception ex) { plugin.Log.LogWarning($"Track inspector settings save will retry: {ex.Message}"); }
    }

    private static void Scan()
    {
        var map = Ant_MapData.instance;
        if (map == null) { ClearTrack(); _status = "Waiting for track"; return; }
        int mapId = map.GetInstanceID();
        if (_mapId != mapId) { ClearTrack(); _mapId = mapId; }

        var local = new List<PixelEasyCharMoveKartController>();
        foreach (var kart in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
            if (kart != null && kart.parentPlayer != null &&
                kart.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && kart.gameObject.activeInHierarchy)
                local.Add(kart);
        if (local.Count == 0) { ClearTrack(); _status = "Waiting for local kart"; return; }

        _focusCamera = null;
        foreach (var kart in local)
        {
            var camera = kart.parentPlayer?.gameplayCamera?.GetCamera()?.unityCamera;
            if (camera != null && camera.isActiveAndEnabled) { _focusCamera = camera; break; }
        }

        // Track physics can live in additive scenes. Native collision masks, not the
        // map object's scene or a debug renderer component, identify boundaries.
        if (_inventory.Length == 0 || Time.unscaledTime >= _nextInventory)
        {
            var found = UnityEngine.Object.FindObjectsOfType<Collider>();
            _inventory = new Collider[found.Length];
            for (int i = 0; i < found.Length; i++) _inventory[i] = found[i];
            _nextInventory = Time.unscaledTime + 5;
        }

        Candidates.Clear();
        int matched = 0, distant = 0, visibleWalls = 0;
        float maxDistanceSquared = _distance.Value * _distance.Value;
        foreach (var collider in _inventory)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                collider.GetComponentInParent<Ant_Player>() != null) continue;
            BoundaryKind kind = BoundaryKind.None;
            float nearestKartDistance = float.PositiveInfinity;
            bool near = false;
            foreach (var kart in local)
            {
                var candidate = BoundarySelection.Classify(collider.gameObject.layer, kart.wallLayerToCollDetect.value,
                    kart.wallRespawn_Flat_ColliderLayer.value, kart.wallRespawn_Always_ColliderLayer.value);
                if (candidate == BoundaryKind.None) continue;
                if (candidate == BoundaryKind.Respawn || kind == BoundaryKind.None) kind = candidate;
                float distance = collider.bounds.SqrDistance(kart.transform.position);
                if (distance < nearestKartDistance) nearestKartDistance = distance;
                if (distance <= maxDistanceSquared) near = true;
            }
            if (kind == BoundaryKind.None) continue;
            matched++;
            if (!near) { distant++; continue; }
            if (kind == BoundaryKind.Wall && IsAlreadyVisible(collider)) { visibleWalls++; continue; }
            if (kind == BoundaryKind.Wall ? !_walls.Value : !_respawn.Value) continue;
            Candidates.Add(new Candidate(collider, kind, nearestKartDistance));
        }

        // Create visuals only for the nearest configured budget. Avoid creating then
        // immediately destroying a large batch of distant volumes on every rescan.
        Candidates.Sort(static (a, b) => a.KartDistance.CompareTo(b.KartDistance));
        _candidateCount = Candidates.Count;
        int budget = Math.Clamp(_maxVisible.Value, 16, 192);
        int drawCount = Math.Min(_candidateCount, budget);
        Selected.Clear();
        int walls = 0, respawn = 0, unsupported = 0;
        for (int i = 0; i < drawCount; i++)
        {
            var candidate = Candidates[i];
            var collider = candidate.Collider;
            int id = collider.GetInstanceID();
            Selected.Add(id);
            if (!Saved.TryGetValue(id, out var saved) || saved.Collider != collider)
            {
                Remove(id);
                var geometry = BoundaryGeometry.Create(collider);
                if (geometry == null) { unsupported++; continue; }
                var tint = MaterialFor(candidate.Kind, collider);
                if (tint == null) { geometry.Dispose(); unsupported++; continue; }
                var view = new GameObject("TK2 boundary view") { hideFlags = HideFlags.DontSave, layer = 0 };
                try
                {
                    geometry.Attach(view.transform);
                    view.AddComponent<MeshFilter>().sharedMesh = geometry.Mesh;
                    var visual = view.AddComponent<MeshRenderer>();
                    SetMaterials(visual, geometry.Mesh, tint);
                    // Automatic renderer drawing fills the volume. Render() draws
                    // the mesh explicitly with scoped wireframe state.
                    visual.enabled = false;
                    visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
                    saved = new Snapshot { Collider = collider, Geometry = geometry, ViewObject = view,
                        ViewRenderer = visual, Kind = candidate.Kind, KartDistance = candidate.KartDistance };
                    Saved[id] = saved;
                }
                catch { UnityEngine.Object.Destroy(view); geometry.Dispose(); throw; }
            }
            else
            {
                if (!saved.Geometry.Refresh()) { Remove(id); unsupported++; continue; }
                saved.ViewObject.GetComponent<MeshFilter>().sharedMesh = saved.Geometry.Mesh;
                if (saved.Kind != candidate.Kind)
                {
                    var tint = MaterialFor(candidate.Kind, collider);
                    if (tint == null) { Remove(id); unsupported++; continue; }
                    saved.Kind = candidate.Kind;
                }
                SetMaterials(saved.ViewRenderer, saved.Geometry.Mesh, Palette[candidate.Kind]);
                saved.KartDistance = candidate.KartDistance;
            }
            saved.ViewRenderer.enabled = false;
            if (candidate.Kind == BoundaryKind.Wall) walls++; else respawn++;
        }

        Stale.Clear();
        foreach (var pair in Saved) if (!Selected.Contains(pair.Key)) Stale.Add(pair.Key);
        foreach (int id in Stale) Remove(id);
        if (_pinnedId >= 0 && !Saved.ContainsKey(_pinnedId)) _pinnedId = -1;

        DrawOrder.Clear();
        foreach (var saved in Saved.Values) DrawOrder.Add(saved);
        DrawOrder.Sort(static (a, b) => a.KartDistance.CompareTo(b.KartDistance));
        string status = $"{walls} walls · {respawn} respawn shapes · showing {Saved.Count}/{_candidateCount}";
        if (_candidateCount > drawCount) status += $" · cap {budget}";
        if (unsupported != 0) status += $" · {unsupported} unsupported";
        if (Saved.Count == 0) status += $" (mask matches {matched}, far {distant}, visible {visibleWalls})";
        if (_status != status)
        {
            var kart = local[0];
            Plugin.Instance?.Log.LogInfo($"Track inspector: {status}; scanned {_inventory.Length} colliders; " +
                $"masks wall=0x{kart.wallLayerToCollDetect.value:X8}, flat=0x{kart.wallRespawn_Flat_ColliderLayer.value:X8}, " +
                $"always=0x{kart.wallRespawn_Always_ColliderLayer.value:X8}");
        }
        _status = status;
    }

    private static void TogglePinnedBoundary()
    {
        if (_pinnedId >= 0) { _pinnedId = -1; return; }
        var camera = _focusCamera;
        if (camera == null || !camera.isActiveAndEnabled) return;
        var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        float nearest = float.PositiveInfinity;
        int selected = -1;
        foreach (var saved in DrawOrder)
            if (saved.Collider != null && saved.Collider.bounds.IntersectRay(ray, out float distance) && distance < nearest)
            { nearest = distance; selected = saved.Collider.GetInstanceID(); }
        _pinnedId = selected;
    }

    private static void ApplyDepthMode()
    {
        int test = (int)(_xray.Value ? CompareFunction.Always : CompareFunction.LessEqual);
        foreach (var material in Palette.Values)
            if (material != null && material.HasProperty("_ZTest")) material.SetInt("_ZTest", test);
        foreach (var material in FillPalette.Values)
            if (material != null && material.HasProperty("_ZTest")) material.SetInt("_ZTest", test);
        if (_pinnedMaterial != null && _pinnedMaterial.HasProperty("_ZTest")) _pinnedMaterial.SetInt("_ZTest", test);
    }
    private static void ApplyFillOpacity()
    {
        _lastFillOpacity = _fillOpacity.Value;
        foreach (var pair in FillPalette)
        {
            var color = pair.Key == BoundaryKind.Wall ? new Color(.08f, .82f, 1f, _lastFillOpacity) :
                new Color(1f, .35f, .08f, _lastFillOpacity);
            if (pair.Value.HasProperty("_Color")) pair.Value.SetColor("_Color", color);
            if (pair.Value.HasProperty("_BaseColor")) pair.Value.SetColor("_BaseColor", color);
        }
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
    private static Material? MaterialFor(BoundaryKind kind, Collider collider, bool fill = false)
    {
        var palette = fill ? FillPalette : Palette;
        if (palette.TryGetValue(kind, out var cached)) return cached;
        Material? material = null;
        if (fill && Palette.TryGetValue(kind, out var outline)) material = new Material(outline);
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        if (material == null && shader != null) material = new Material(shader);
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
        var alpha = fill ? _fillOpacity.Value : .85f;
        var color = kind == BoundaryKind.Wall ? new Color(.08f, .82f, 1f, alpha) : new Color(1f, .35f, .08f, alpha);
        bool colored = false;
        if (material.HasProperty("_Color")) { material.SetColor("_Color", color); colored = true; }
        if (material.HasProperty("_BaseColor")) { material.SetColor("_BaseColor", color); colored = true; }
        if (!colored) { UnityEngine.Object.Destroy(material); return null; }
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)(_xray.Value ? CompareFunction.Always : CompareFunction.LessEqual));
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)(fill ? CullMode.Back : CullMode.Off));
        material.renderQueue = fill ? 3000 : 3100;
        palette[kind] = material;
        if (fill) _lastFillOpacity = _fillOpacity.Value;
        return material;
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
            _renderedThisFrame = 0;
            int budget = Math.Clamp(_maxVisible.Value, 16, 192);
            int normalBudget = _pinnedId >= 0 ? budget - 1 : budget;
            foreach (var saved in DrawOrder)
            {
                if (_pinnedId >= 0 && saved.Collider != null && saved.Collider.GetInstanceID() == _pinnedId) continue;
                if (saved.Collider == null || !saved.Collider.enabled || !saved.Collider.gameObject.activeInHierarchy ||
                    saved.ViewObject == null || !saved.ViewObject.activeInHierarchy || saved.Geometry.Mesh == null) continue;
                if (!VisibleInCamera(camera, saved.Collider.bounds)) continue;
                if (_fills.Value)
                {
                    var fill = MaterialFor(saved.Kind, saved.Collider, fill: true);
                    GL.wireframe = false;
                    if (fill != null && fill.SetPass(0))
                        for (int i = 0; i < saved.Geometry.Mesh.subMeshCount; i++)
                            Graphics.DrawMeshNow(saved.Geometry.Mesh, saved.ViewObject.transform.localToWorldMatrix, i);
                }
                GL.wireframe = true;
                if (!Palette[saved.Kind].SetPass(0)) continue;
                for (int i = 0; i < saved.Geometry.Mesh.subMeshCount; i++)
                    Graphics.DrawMeshNow(saved.Geometry.Mesh, saved.ViewObject.transform.localToWorldMatrix, i);
                _renderedThisFrame++;
                if (_renderedThisFrame >= normalBudget) break;
            }
            if (_pinnedId >= 0 && Saved.TryGetValue(_pinnedId, out var pinned) &&
                pinned.Collider != null && pinned.Collider.enabled && pinned.Collider.gameObject.activeInHierarchy &&
                pinned.ViewObject != null && pinned.Geometry.Mesh != null && VisibleInCamera(camera, pinned.Collider.bounds))
            {
                if (_fills.Value)
                {
                    var fill = MaterialFor(pinned.Kind, pinned.Collider, fill: true);
                    GL.wireframe = false;
                    if (fill != null && fill.SetPass(0))
                        for (int i = 0; i < pinned.Geometry.Mesh.subMeshCount; i++)
                            Graphics.DrawMeshNow(pinned.Geometry.Mesh, pinned.ViewObject.transform.localToWorldMatrix, i);
                }
                GL.wireframe = true;
                _pinnedMaterial ??= CreatePinnedMaterial();
                if (_pinnedMaterial != null && _pinnedMaterial.SetPass(0))
                {
                    for (int i = 0; i < pinned.Geometry.Mesh.subMeshCount; i++)
                        Graphics.DrawMeshNow(pinned.Geometry.Mesh, pinned.ViewObject.transform.localToWorldMatrix, i);
                    _renderedThisFrame++;
                }
            }
        }
        catch (Exception ex)
        {
            _faulted = true;
            Plugin.Instance?.Log.LogWarning("Boundary overlay rendering unavailable: " + ex.Message);
        }
        finally { GL.wireframe = wireframe; }
        if (_faulted) ClearTrack();
    }
    private static bool VisibleInCamera(Camera camera, Bounds bounds)
    {
        var viewport = camera.WorldToViewportPoint(bounds.center);
        float radius = bounds.extents.magnitude;
        if (viewport.z + radius < camera.nearClipPlane || viewport.z - radius > camera.farClipPlane) return false;
        float projectedRadius;
        if (camera.orthographic) projectedRadius = radius / Math.Max(.001f, camera.orthographicSize * 2f);
        else
        {
            float depth = Math.Max(camera.nearClipPlane, viewport.z - radius);
            projectedRadius = radius / (2f * depth * (float)Math.Tan(camera.fieldOfView * Math.PI / 360.0));
        }
        return viewport.x + projectedRadius >= 0f && viewport.x - projectedRadius <= 1f &&
            viewport.y + projectedRadius >= 0f && viewport.y - projectedRadius <= 1f;
    }
    private static Material? CreatePinnedMaterial()
    {
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        if (shader == null) return null;
        var material = new Material(shader) { hideFlags = HideFlags.DontSave };
        var color = new Color(1f, 1f, .24f, 1f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)(_xray.Value ? CompareFunction.Always : CompareFunction.LessEqual));
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
        material.renderQueue = 3100;
        return material;
    }
    internal static void Draw()
    {
        if (_enabled == null || !_enabled.Value || !Allowed) return;
        var matrix = GUI.matrix;
        var color = GUI.color;
        var background = GUI.backgroundColor;
        var content = GUI.contentColor;
        int depth = GUI.depth;
        bool enabled = GUI.enabled;
        try
        {
            float height = _pinnedId >= 0 && Saved.TryGetValue(_pinnedId, out _) ? 126f : 94f;
            float top = Screen.height - height - 16f;
            GUI.Box(new Rect(16, top, 640, height), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(28, top + 7, 470, 20), $"TRACK INSPECTOR  ·  {_key.Value} view  ·  {_xrayKey.Value} X-ray  ·  {_inspectKey.Value} pin  ·  {(_visible ? "ON" : "OFF")}");
            if (GUI.Button(new Rect(516, top + 5, 126, 24), _optionsOpen ? "Close options" : $"{_optionsKey.Value} Options"))
            {
                if (_optionsOpen) CloseOptions(Plugin.Instance); else ToggleOptions(Plugin.Instance);
            }
            GUI.color = new Color(.08f, .82f, 1f, 1); GUI.Label(new Rect(28, top + 29, 190, 20), "●  Invisible walls");
            GUI.color = new Color(1f, .45f, .15f, 1); GUI.Label(new Rect(218, top + 29, 220, 20), "●  Respawn boundaries");
            GUI.color = Color.white; GUI.Label(new Rect(28, top + 51, 616, 20), _faulted ? "Unavailable; see BepInEx log" : _status + $" · drawing {_renderedThisFrame}");
            if (_pinnedId >= 0 && Saved.TryGetValue(_pinnedId, out var saved) && saved.Collider != null)
            {
                var collider = saved.Collider;
                string layer = LayerMask.LayerToName(collider.gameObject.layer);
                var size = collider.bounds.size;
                GUI.color = new Color(1f, 1f, .35f, 1f);
                GUI.Label(new Rect(28, top + 73, 616, 20), $"PINNED  {saved.Kind} {saved.Geometry.ShapeName}  ·  {collider.gameObject.name}");
                GUI.color = Color.white;
                GUI.Label(new Rect(28, top + 95, 616, 20), $"Layer {collider.gameObject.layer}{(string.IsNullOrEmpty(layer) ? "" : " / " + layer)}  ·  {saved.KartDistance:0.0} m  ·  bounds {size.x:0.0} × {size.y:0.0} × {size.z:0.0} m");
            }
            if (_optionsOpen)
            {
                _optionsRect.width = Math.Min(460f, Screen.width - 32f);
                _optionsRect.height = 365f;
                _optionsRect.x = Math.Max(16f, Screen.width - _optionsRect.width - 24f);
                _optionsRect.y = Math.Max(16f, Math.Min(60f, Screen.height - _optionsRect.height - 16f));
                DrawOptionsPanel(_optionsRect.x, _optionsRect.y, _optionsRect.width);
            }
        }
        finally
        { GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content; GUI.depth = depth; GUI.enabled = enabled; }
    }
    private static void DrawOptionsPanel(float x, float y, float width)
    {
        var plugin = Plugin.Instance;
        if (plugin == null) return;
        var color = GUI.color;
        var background = GUI.backgroundColor;
        var content = GUI.contentColor;
        bool enabled = GUI.enabled;
        try
        {
            GUI.color = new Color(.94f, .97f, 1f, 1f);
            GUI.backgroundColor = new Color(.16f, .20f, .27f, .97f);
            GUI.contentColor = new Color(.94f, .97f, 1f, 1f);
            GUI.Box(new Rect(x, y, width, 365), "");
            GUI.Label(new Rect(x + 16, y + 5, width - 66, 24), "TRACK INSPECTOR OPTIONS");
            if (GUI.Button(new Rect(x + width - 43, y + 3, 30, 24), "×")) { CloseOptions(plugin); return; }
            ToggleControl(plugin, _walls, "Invisible walls", x + 16, y + 34, width * .5f - 20);
            ToggleControl(plugin, _respawn, "Respawn boundaries", x + width * .5f, y + 34, width * .5f - 14);
            ToggleControl(plugin, _fills, "Translucent surfaces", x + 16, y + 61, width * .5f - 20);
            ToggleControl(plugin, _xray, "X-ray through scenery", x + width * .5f, y + 61, width * .5f - 14);
            SliderControl(plugin, _fillOpacity, "Surface opacity", .03f, .35f, 2, 0, y + 96, width, x);
            SliderControl(plugin, _distance, "View distance", 25, 1000, 0, 1, y + 148, width, x);
            int maxVisible = _maxVisible.Value;
            GUI.Label(new Rect(x + 16, y + 200, width - 32, 20), $"Maximum outlines: {maxVisible}");
            float maxSlider = GUI.HorizontalSlider(new Rect(x + 16, y + 222, width - 32, 18), maxVisible, 16, 192);
            int nextMax = Math.Clamp((int)Math.Round(maxSlider), 16, 192);
            if (nextMax != maxVisible) Set(plugin, _maxVisible, nextMax);
            if (GUI.Button(new Rect(x + 16, y + 250, 110, 27), "Reset defaults")) ResetOptions(plugin);
            if (GUI.Button(new Rect(x + width - 142, y + 250, 126, 27), "Save now")) LiveConfig.Flush(plugin);
            GUI.Label(new Rect(x + 16, y + 283, width - 32, 28), LiveConfig.Status);
            GUI.Label(new Rect(x + 16, y + 314, width - 32, 30), $"F10 show · F9 X-ray · F8 pin · {_optionsKey.Value} options · Esc closes · settings apply live");
        }
        finally { GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content; GUI.enabled = enabled; }
    }
    private static void ToggleControl(Plugin plugin, ConfigEntry<bool> entry, string label, float x, float y, float width)
    {
        bool next = GUI.Toggle(new Rect(x, y, width, 23), entry.Value, label);
        if (next != entry.Value) Set(plugin, entry, next);
    }
    private static void SliderControl(Plugin plugin, ConfigEntry<float> entry, string label, float low, float high, int decimals, int sliderId, float y, float width, float x)
    {
        string format = decimals == 0 ? "0" : "0.00";
        GUI.Label(new Rect(x + 16, y, width - 32, 20), $"{label}: {entry.Value.ToString(format)}  [{low.ToString(format)}–{high.ToString(format)}]");
        float slider = GUI.HorizontalSlider(new Rect(x + 16, y + 22, width - 32, 18), entry.Value, low, high);
        if (sliderId == 0 && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            slider = (float)Math.Round(slider, 3);
        else if (decimals == 0) slider = (float)Math.Round(slider);
        else slider = (float)Math.Round(slider, decimals);
        slider = Mathf.Clamp(slider, low, high);
        if (Math.Abs(slider - entry.Value) > .0001f) Set(plugin, entry, slider);
    }
    private static void ResetOptions(Plugin plugin)
    {
        Set(plugin, _walls, true); Set(plugin, _respawn, true); Set(plugin, _fills, false); Set(plugin, _xray, false);
        Set(plugin, _fillOpacity, .12f); Set(plugin, _distance, 200f); Set(plugin, _maxVisible, 64);
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
        foreach (var material in FillPalette.Values) if (material != null) UnityEngine.Object.Destroy(material);
        if (_pinnedMaterial != null) UnityEngine.Object.Destroy(_pinnedMaterial);
        _pinnedMaterial = null; _pinnedId = -1; _candidateCount = _renderedThisFrame = 0; _lastFillOpacity = -1f;
        DrawOrder.Clear(); Candidates.Clear(); Selected.Clear(); Palette.Clear(); FillPalette.Clear(); _inventory = Array.Empty<Collider>();
        _focusCamera = null; _mapId = 0; _nextInventory = _nextScan = 0;
    }
    internal static void Restore() { CloseOptions(Plugin.Instance); ClearTrack(); _visible = false; }
}

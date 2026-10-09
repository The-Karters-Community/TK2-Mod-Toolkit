using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

/// <summary>Shows native trigger meshes and nearby colliders on the kart's track-wall layers.</summary>
public sealed class TrackInspector : IModRecipe
{
    private enum WallKind { Wall, Respawn, AlwaysRespawn }
    private sealed class Overlay
    {
        internal Collider Collider = null!;
        internal GameObject Object = null!;
        internal Mesh Mesh = null!;
        internal MeshRenderer Renderer = null!;
        internal WallKind Kind;
    }

    public string Name => "TrackInspector";
    public bool ChangesGameplay => false;

    private const int MaxShown = 48;
    private const float RefreshInterval = 2f;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _enabled = null!;
    private static TrackInspector? _instance;
    private readonly Dictionary<int, (Ant_MapData Map, bool OriginalFlag)> _mapDefaults = new();
    private readonly Dictionary<int, (MeshRenderer Renderer, bool OriginalEnabled)> _rendererDefaults = new();
    private readonly Dictionary<int, Overlay> _overlays = new();
    private readonly Dictionary<WallKind, Material> _materials = new();
    private readonly Dictionary<PrimitiveType, Mesh> _primitiveMeshes = new();
    private Ant_MapData? _map;
    private Mesh? _boxMesh;
    private bool _mapDefault;
    private bool _visible;
    private bool _nativeVisible;
    private int _nativeMapId;
    private float _nextScan;
    private string _status = "Press F10 to inspect nearby track walls";

    public void Configure(ConfigFile config)
    {
        _instance = this;
        _enabled = config.Bind("Recipe." + Name, "Enabled", false,
            "Show nearby track collision walls in a local offline race. Press the configured key to toggle visibility.");
        _key = config.Bind("Recipe." + Name, "ToggleKey", KeyCode.F10,
            "Show or hide nearby wall colliders in the current race.");

        var harmony = Plugin.Instance!.Harmony;
        PatchMapSetup(harmony);
        PatchTriggerSetup(harmony, "Init");
        PatchTriggerSetup(harmony, "Start");

        foreach (string key in new[] { "ShowWalls", "ShowRespawn", "ShowKillTriggers", "ShowOtherTriggers", "DrawDistance",
                     "Opacity", "ShowThroughTrack", "ShowLabels", "ColliderSampleResolution", "LineWidth", "ShowBoundsFallback" })
            config.Remove(new ConfigDefinition("Recipe.TrackInspector", key));
    }

    private static void PatchMapSetup(Harmony harmony)
    {
        var target = AccessTools.DeclaredMethod(typeof(Ant_MapData), "Start", Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(Ant_MapData).FullName, "Start()");
        harmony.Patch(target,
            prefix: new HarmonyMethod(typeof(TrackInspector), nameof(BeforeMapSetup)),
            postfix: new HarmonyMethod(typeof(TrackInspector), nameof(AfterMapSetup)));
    }

    private static void PatchTriggerSetup(Harmony harmony, string method)
    {
        var target = AccessTools.DeclaredMethod(typeof(PTK_ModTKLogic_PhysicsTrigger), method, Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(PTK_ModTKLogic_PhysicsTrigger).FullName, method + "()");
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(TrackInspector), nameof(BeforeTriggerSetup)));
    }

    private static void BeforeMapSetup(Ant_MapData __instance)
    {
        var module = _instance;
        if (module == null || !module.ShowRequested || __instance == null) return;
        module.PrepareMap(__instance);
        Plugin.Instance?.Log.LogInfo("Track Inspector: native map setup hook reached.");
    }

    private static void AfterMapSetup(Ant_MapData __instance)
    {
        var module = _instance;
        if (module == null || !module.ShowRequested || __instance == null) return;
        module.PrepareMap(__instance);
        Plugin.Instance?.Log.LogInfo("Track Inspector: native map setup finished.");
    }

    private static void BeforeTriggerSetup(PTK_ModTKLogic_PhysicsTrigger __instance)
    {
        var module = _instance;
        if (module == null || !module.ShowRequested || __instance == null) return;
        var map = Ant_MapData.instance;
        if (map != null) module.PrepareMap(map);
        module.CaptureTriggerDefaults(__instance);
    }

    private bool ShowRequested => _enabled.Value && _visible && Plugin.OfflineLabAllowed;

    public void Tick()
    {
        if (!_enabled.Value || !Plugin.OfflineLabAllowed)
        {
            Restore();
            return;
        }

        if (Input.GetKeyDown(_key.Value))
        {
            _visible = !_visible;
            _nextScan = 0f;
            Plugin.Instance?.Log.LogInfo($"Track Inspector: {_key.Value} pressed; wall view {(_visible ? "on" : "off")}.");
            if (!_visible) HideOverlays();
            var currentMap = Ant_MapData.instance;
            if (currentMap != null) ApplyNativeMeshes(currentMap, _visible);
        }

        if (!_visible || Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING)
        {
            HideOverlays();
            return;
        }

        var map = Ant_MapData.instance;
        if (map == null || map.gameObject == null) { HideOverlays(); return; }
        if (_map != map) PrepareMap(map);
        if (!_nativeVisible || _nativeMapId != map.GetInstanceID()) ApplyNativeMeshes(map, true);
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + RefreshInterval;
            RefreshWallOverlays(map);
        }
    }

    private void PrepareMap(Ant_MapData map)
    {
        if (_map != null && _map != map) RestoreMapSnapshot(_map);
        _map = map;
        int id = map.GetInstanceID();
        if (!_mapDefaults.TryGetValue(id, out var saved))
        {
            saved = (map, map.bForceDebugShowTriggerCollisionMeshes);
            _mapDefaults[id] = saved;
        }
        _mapDefault = saved.OriginalFlag;
        map.bForceDebugShowTriggerCollisionMeshes = _mapDefault || ShowRequested;
    }

    private void CaptureTriggerDefaults(PTK_ModTKLogic_PhysicsTrigger trigger)
    {
        foreach (var collider in trigger.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null) continue;
            var renderer = collider.GetComponent<MeshRenderer>();
            if (renderer != null && !_rendererDefaults.ContainsKey(renderer.GetInstanceID()))
                _rendererDefaults[renderer.GetInstanceID()] = (renderer, renderer.enabled);
        }
    }

    private void ApplyNativeMeshes(Ant_MapData map, bool visible)
    {
        PrepareMap(map);
        map.bForceDebugShowTriggerCollisionMeshes = _mapDefault || visible;
        int count = 0;
        foreach (var trigger in UnityEngine.Object.FindObjectsOfType<PTK_ModTKLogic_PhysicsTrigger>(true))
        {
            if (trigger == null) continue;
            CaptureTriggerDefaults(trigger);
            foreach (var collider in trigger.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null) continue;
                var renderer = collider.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                var saved = _rendererDefaults[renderer.GetInstanceID()];
                renderer.enabled = visible || saved.OriginalEnabled;
                count++;
            }
        }
        _nativeVisible = visible;
        _nativeMapId = map.GetInstanceID();
        Plugin.Instance?.Log.LogInfo($"Track Inspector: native trigger meshes {(visible ? "shown" : "hidden")} ({count} renderers).");
    }

    private void RefreshWallOverlays(Ant_MapData map)
    {
        var localKart = FindLocalKart();
        if (localKart == null)
        {
            _status = "No local kart found";
            HideOverlays();
            return;
        }

        int wallLayer = localKart.wallLayerToCollDetect.value;
        int flatRespawnLayer = localKart.wallRespawn_Flat_ColliderLayer.value;
        int alwaysRespawnLayer = localKart.wallRespawn_Always_ColliderLayer.value;
        var origin = localKart.transform.position;
        var candidates = new List<(Collider Collider, WallKind Kind, float Distance)>();
        var activeIds = new HashSet<int>();

        // Use the same collision-layer masks as the live kart controller. This targets
        // actual track walls and respawn barriers instead of relying on mod-only triggers.
        foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if (collider.GetComponentInParent<Ant_Player>() != null) continue;
            int layerBit = 1 << collider.gameObject.layer;
            WallKind kind;
            if ((wallLayer & layerBit) != 0) kind = WallKind.Wall;
            else if ((alwaysRespawnLayer & layerBit) != 0) kind = WallKind.AlwaysRespawn;
            else if ((flatRespawnLayer & layerBit) != 0) kind = WallKind.Respawn;
            else continue;

            // Keep the track map's scene and any scenes containing its registered triggers.
            int scene = collider.gameObject.scene.handle;
            if (!BelongsToTrackScene(map, scene)) continue;
            float distance = collider.bounds.SqrDistance(origin);
            if (distance > 22500f) continue; // 150 m from the kart
            candidates.Add((collider, kind, distance));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        int count = Math.Min(MaxShown, candidates.Count);
        int visualized = 0;
        for (int i = 0; i < count; i++)
        {
            var candidate = candidates[i];
            int id = candidate.Collider.GetInstanceID();
            activeIds.Add(id);
            if (!_overlays.TryGetValue(id, out var overlay) || overlay.Collider != candidate.Collider || overlay.Kind != candidate.Kind)
            {
                RemoveOverlay(id);
                overlay = CreateOverlay(candidate.Collider, candidate.Kind);
                if (overlay != null) _overlays[id] = overlay;
            }
            if (overlay != null)
            {
                overlay.Object.SetActive(true);
                visualized++;
            }
        }

        foreach (var pair in _overlays)
            if (!activeIds.Contains(pair.Key) && pair.Value.Object != null) pair.Value.Object.SetActive(false);

        _status = candidates.Count == 0
            ? "No wall colliders found on the kart's collision layers in this track scene"
            : $"Showing {visualized} wall/respawn shapes (of {candidates.Count} nearby; {Math.Max(0, candidates.Count - count)} beyond view limit)";
        Plugin.Instance?.Log.LogInfo("Track Inspector: " + _status + ".");
    }

    private static PixelEasyCharMoveKartController? FindLocalKart()
    {
        foreach (var kart in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>(true))
            if (kart != null && kart.parentPlayer != null && kart.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL &&
                kart.gameObject.activeInHierarchy) return kart;
        return null;
    }

    private static bool BelongsToTrackScene(Ant_MapData map, int scene)
    {
        if (map.gameObject.scene.handle == scene || SceneManager.GetActiveScene().handle == scene) return true;
        if (map.mapPhysicsTriggerColliderList == null) return false;
        foreach (var trigger in map.mapPhysicsTriggerColliderList)
            if (trigger != null && trigger.collider != null && trigger.collider.gameObject.scene.handle == scene) return true;
        return false;
    }

    private Overlay? CreateOverlay(Collider collider, WallKind kind)
    {
        Mesh? mesh;
        Vector3 position;
        Vector3 scale;
        Quaternion rotation;
        switch (collider)
        {
            case MeshCollider meshCollider:
                mesh = meshCollider.sharedMesh;
                position = Vector3.zero; scale = Vector3.one; rotation = Quaternion.identity;
                break;
            case BoxCollider box:
                mesh = BoxMesh(); position = box.center; scale = box.size; rotation = Quaternion.identity;
                break;
            case SphereCollider sphere:
                mesh = PrimitiveMesh(PrimitiveType.Sphere); position = sphere.center;
                scale = Vector3.one * (sphere.radius * 2f); rotation = Quaternion.identity;
                break;
            case CapsuleCollider capsule:
                mesh = PrimitiveMesh(PrimitiveType.Capsule); position = capsule.center;
                scale = new Vector3(capsule.radius * 2f, capsule.height * .5f, capsule.radius * 2f);
                rotation = capsule.direction == 0 ? Quaternion.Euler(0, 0, 90) :
                    capsule.direction == 2 ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
                break;
            default:
                return null;
        }
        if (mesh == null) return null;
        var obj = new GameObject("TK2 track wall overlay");
        obj.hideFlags = HideFlags.DontSave;
        obj.transform.SetParent(collider.transform, false);
        obj.layer = 0;
        obj.transform.localPosition = position;
        obj.transform.localRotation = rotation;
        obj.transform.localScale = scale;
        var filter = obj.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = MaterialFor(kind);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = true;
        return new Overlay { Collider = collider, Object = obj, Mesh = mesh, Renderer = renderer, Kind = kind };
    }

    private Mesh PrimitiveMesh(PrimitiveType primitive)
    {
        if (_primitiveMeshes.TryGetValue(primitive, out var cached) && cached != null) return cached;
        var temporary = GameObject.CreatePrimitive(primitive);
        var filter = temporary.GetComponent<MeshFilter>();
        var mesh = filter != null ? filter.sharedMesh : null;
        UnityEngine.Object.Destroy(temporary);
        if (mesh == null) throw new InvalidOperationException("Unity did not provide a primitive collider mesh.");
        _primitiveMeshes[primitive] = mesh;
        return mesh;
    }

    private Material MaterialFor(WallKind kind)
    {
        if (_materials.TryGetValue(kind, out var material) && material != null) return material;
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Color");
        if (shader == null) throw new InvalidOperationException("No unlit shader is available for Track Inspector.");
        material = new Material(shader) { hideFlags = HideFlags.DontSave, color = kind switch
        {
            WallKind.AlwaysRespawn => new Color(1f, .55f, .12f, .30f),
            WallKind.Respawn => new Color(1f, .88f, .12f, .30f),
            _ => new Color(.08f, .82f, 1f, .28f)
        }};
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.renderQueue = 3100;
        _materials[kind] = material;
        return material;
    }

    private Mesh BoxMesh()
    {
        if (_boxMesh != null) return _boxMesh;
        _boxMesh = new Mesh { name = "TK2 track wall box", hideFlags = HideFlags.DontSave };
        _boxMesh.vertices = new[]
        {
            new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
        };
        _boxMesh.triangles = new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 2,3,7, 2,7,6, 1,2,6, 1,6,5, 3,0,4, 3,4,7 };
        _boxMesh.RecalculateNormals();
        _boxMesh.RecalculateBounds();
        return _boxMesh;
    }

    internal static void DrawStatus() => _instance?.OnGUI();

    private void OnGUI()
    {
        if (!_enabled.Value || !Plugin.OfflineLabAllowed || Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) return;
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, .72f);
        GUI.Box(new Rect(16, 16, 450, 56), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(28, 22, 430, 22), $"TRACK INSPECTOR  ·  {_key.Value}: {(_visible ? "ON" : "OFF")}");
        GUI.Label(new Rect(28, 44, 430, 22), _visible ? _status : "Press " + _key.Value + " to show nearby wall colliders");
        GUI.color = old;
    }

    private void HideOverlays()
    {
        foreach (var overlay in _overlays.Values) if (overlay.Object != null) overlay.Object.SetActive(false);
    }

    private void RemoveOverlay(int id)
    {
        if (!_overlays.TryGetValue(id, out var overlay)) return;
        if (overlay.Object != null) UnityEngine.Object.Destroy(overlay.Object);
        _overlays.Remove(id);
    }

    private void RestoreMapSnapshot(Ant_MapData map)
    {
        int id = map.GetInstanceID();
        if (_mapDefaults.TryGetValue(id, out var saved))
        {
            if (saved.Map != null) saved.Map.bForceDebugShowTriggerCollisionMeshes = saved.OriginalFlag;
            _mapDefaults.Remove(id);
        }
        _map = null;
        _mapDefault = false;
        _nativeVisible = false;
        _nativeMapId = 0;
    }

    public void Restore()
    {
        _visible = false;
        foreach (var saved in _mapDefaults.Values)
            if (saved.Map != null) saved.Map.bForceDebugShowTriggerCollisionMeshes = saved.OriginalFlag;
        _mapDefaults.Clear();
        foreach (var saved in _rendererDefaults.Values)
            if (saved.Renderer != null) saved.Renderer.enabled = saved.OriginalEnabled;
        _rendererDefaults.Clear();
        foreach (int id in new List<int>(_overlays.Keys)) RemoveOverlay(id);
        foreach (var material in _materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
        _materials.Clear();
        if (_boxMesh != null) UnityEngine.Object.Destroy(_boxMesh);
        _boxMesh = null;
        _primitiveMeshes.Clear();
        _map = null;
        _mapDefault = false;
        _nativeVisible = false;
        _nativeMapId = 0;
        _nextScan = 0;
    }
}

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

/// <summary>Uses the game's own trigger-collision debug meshes; no custom renderer or scene scan.</summary>
public sealed class TrackInspector : IModRecipe
{
    public string Name => "TrackInspector";
    public bool ChangesGameplay => false;

    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _enabled = null!;
    private static TrackInspector? _instance;
    private readonly Dictionary<int, (Ant_MapData Map, bool OriginalFlag)> _mapDefaults = new();
    private Ant_MapData? _map;
    private bool _mapDefault;
    private bool _lastApplied;
    private bool _visible = true;

    public void Configure(ConfigFile config)
    {
        _instance = this;
        _enabled = config.Bind("Recipe." + Name, "Enabled", false,
            "Show the game's trigger collision meshes in local offline sessions. Toggle visibility with the configured key.");
        _key = config.Bind("Recipe." + Name, "ToggleKey", KeyCode.F10,
            "Show or hide the game's native trigger collision meshes. Works before or during a local offline race.");

        // Native Init/Start are the game's own debug-mesh path. Set the native flag before
        // those methods run, then use the map list below for a toggle made mid-race.
        var harmony = Plugin.Instance!.Harmony;
        PatchTriggerSetup(harmony, "Init");
        PatchTriggerSetup(harmony, "Start");

        // Remove settings used by the retired sampled-mesh overlay.
        foreach (string key in new[] { "ShowWalls", "ShowRespawn", "ShowKillTriggers", "ShowOtherTriggers", "DrawDistance",
                     "Opacity", "ShowThroughTrack", "ShowLabels", "ColliderSampleResolution", "LineWidth", "ShowBoundsFallback" })
            config.Remove(new ConfigDefinition("Recipe.TrackInspector", key));
    }

    private static void PatchTriggerSetup(Harmony harmony, string method)
    {
        var target = AccessTools.DeclaredMethod(typeof(PTK_ModTKLogic_PhysicsTrigger), method, Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(PTK_ModTKLogic_PhysicsTrigger).FullName, method + "()");
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(TrackInspector), nameof(BeforeTriggerSetup)));
    }

    private static void BeforeTriggerSetup()
    {
        var module = _instance;
        if (module == null || !module.ShouldShowNativeMeshes) return;
        var map = Ant_MapData.instance;
        if (map != null)
        {
            module.CaptureDefault(map);
            map.bForceDebugShowTriggerCollisionMeshes = true;
        }
    }

    private void CaptureDefault(Ant_MapData map)
    {
        int id = map.GetInstanceID();
        if (!_mapDefaults.ContainsKey(id)) _mapDefaults[id] = (map, map.bForceDebugShowTriggerCollisionMeshes);
    }

    private bool ShouldShowNativeMeshes => _enabled.Value && _visible && Plugin.OfflineLabAllowed;

    public void Tick()
    {
        if (Plugin.OfflineLabAllowed && Input.GetKeyDown(_key.Value)) _visible = !_visible;

        var map = Ant_MapData.instance;
        if (map == null) { RestoreMap(); return; }
        if (map != _map)
        {
            if (_map != null) RestoreMap();
            _map = map;
            CaptureDefault(map);
            _mapDefault = _mapDefaults[map.GetInstanceID()].OriginalFlag;
            _lastApplied = _mapDefault;
        }

        // Keep this enabled while the local session loads so the game's own trigger
        // Init/Start code can expose meshes as they are created.
        bool show = ShouldShowNativeMeshes;
        bool force = _mapDefault || show;
        if (force == _lastApplied) return;
        var result = SetGameDebugMeshes(map, force);
        _lastApplied = force;
        if (force && result.Renderers == 0)
            Plugin.Instance?.Log.LogWarning($"Track Inspector enabled the native debug flag, but the map currently exposes no trigger MeshRenderers (trigger entries: {result.Entries}).");
        else
            Plugin.Instance?.Log.LogInfo($"Track Inspector {(force ? "showing" : "hiding")} native trigger meshes ({result.Renderers} renderers across {result.Entries} entries).");
    }

    private static (int Entries, int Renderers) SetGameDebugMeshes(Ant_MapData map, bool visible)
    {
        map.bForceDebugShowTriggerCollisionMeshes = visible;
        var colliders = map.mapPhysicsTriggerColliderList;
        if (colliders == null) return (0, 0);
        int entries = 0;
        int renderers = 0;
        foreach (var entry in colliders)
        {
            entries++;
            var collider = entry == null ? null : entry.collider;
            var renderer = collider == null ? null : collider.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
                renderers++;
            }
        }
        return (entries, renderers);
    }

    private void RestoreMap()
    {
        var map = _map ?? Ant_MapData.instance;
        _map = null;
        if (map == null)
        {
            _mapDefaults.Clear();
            _lastApplied = false;
            return;
        }
        int id = map.GetInstanceID();
        bool originalFlag = _mapDefaults.TryGetValue(id, out var saved) ? saved.OriginalFlag : _mapDefault;
        SetGameDebugMeshes(map, originalFlag);
        _mapDefaults.Remove(id);
        _lastApplied = false;
    }

    public void Restore()
    {
        _visible = true;
        RestoreMap();
    }
}

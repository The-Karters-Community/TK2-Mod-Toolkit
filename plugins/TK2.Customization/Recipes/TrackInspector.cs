using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

/// <summary>Uses the same trigger-child mesh path as the game's native debug flag.</summary>
public sealed class TrackInspector : IModRecipe
{
    public string Name => "TrackInspector";
    public bool ChangesGameplay => false;

    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _enabled = null!;
    private static TrackInspector? _instance;
    private readonly Dictionary<int, (Ant_MapData Map, bool OriginalFlag)> _mapDefaults = new();
    private readonly Dictionary<int, (MeshRenderer Renderer, bool OriginalEnabled)> _rendererDefaults = new();
    private Ant_MapData? _map;
    private bool _mapDefault;
    private bool _lastApplied;
    private bool _visible = true;

    public void Configure(ConfigFile config)
    {
        _instance = this;
        _enabled = config.Bind("Recipe." + Name, "Enabled", false,
            "Show the game's own trigger collision meshes in a local offline session. Toggle with the configured key.");
        _key = config.Bind("Recipe." + Name, "ToggleKey", KeyCode.F10,
            "Show or hide the game's native trigger meshes. Applies in the current race without restarting.");

        // The game reads this map flag in PhysicsTrigger.Init and Start. Set it before
        // those methods run, then use their own child-collider/MeshRenderer shape mid-race.
        var harmony = Plugin.Instance!.Harmony;
        PatchTriggerSetup(harmony, "Init");
        PatchTriggerSetup(harmony, "Start");

        // Retire options from the old sampled-mesh overlay.
        foreach (string key in new[] { "ShowWalls", "ShowRespawn", "ShowKillTriggers", "ShowOtherTriggers", "DrawDistance",
                     "Opacity", "ShowThroughTrack", "ShowLabels", "ColliderSampleResolution", "LineWidth", "ShowBoundsFallback" })
            config.Remove(new ConfigDefinition("Recipe.TrackInspector", key));
    }

    private static void PatchTriggerSetup(Harmony harmony, string method)
    {
        var target = AccessTools.DeclaredMethod(typeof(PTK_ModTKLogic_PhysicsTrigger), method, System.Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(PTK_ModTKLogic_PhysicsTrigger).FullName, method + "()");
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(TrackInspector), nameof(BeforeTriggerSetup)));
    }

    private static void BeforeTriggerSetup(PTK_ModTKLogic_PhysicsTrigger __instance)
    {
        var module = _instance;
        if (module == null || !module.ShouldShowNativeMeshes) return;

        var map = Ant_MapData.instance;
        if (map != null)
        {
            bool original = module.CaptureMap(map);
            map.bForceDebugShowTriggerCollisionMeshes = original || module.ShouldShowNativeMeshes;
        }
        module.CaptureTriggerDefaults(__instance);
    }

    private bool CaptureMap(Ant_MapData map)
    {
        int id = map.GetInstanceID();
        if (!_mapDefaults.TryGetValue(id, out var saved))
        {
            saved = (map, map.bForceDebugShowTriggerCollisionMeshes);
            _mapDefaults[id] = saved;
        }
        return saved.OriginalFlag;
    }

    private void CaptureTriggerDefaults(PTK_ModTKLogic_PhysicsTrigger trigger)
    {
        if (trigger == null) return;
        var colliders = trigger.GetComponentsInChildren<Collider>(true);
        if (colliders == null) return;
        foreach (var collider in colliders)
        {
            var renderer = collider == null ? null : collider.GetComponent<MeshRenderer>();
            if (renderer == null) continue;
            int id = renderer.GetInstanceID();
            if (!_rendererDefaults.ContainsKey(id)) _rendererDefaults[id] = (renderer, renderer.enabled);
        }
    }

    private bool ShouldShowNativeMeshes => _enabled.Value && _visible && Plugin.OfflineLabAllowed;

    public void Tick()
    {
        if (Plugin.OfflineLabAllowed && Input.GetKeyDown(_key.Value)) _visible = !_visible;

        var map = Ant_MapData.instance;
        if (map == null)
        {
            if (_map != null || _mapDefaults.Count != 0 || _rendererDefaults.Count != 0) Restore();
            return;
        }
        if (map != _map)
        {
            if (_map != null) RestoreMapSnapshot(_map);
            _map = map;
            _mapDefault = CaptureMap(map);
            _lastApplied = _mapDefault;
        }

        bool show = ShouldShowNativeMeshes;
        bool force = _mapDefault || show;
        if (force == _lastApplied && map.bForceDebugShowTriggerCollisionMeshes == force) return;

        var counts = SetTriggerMeshes(map, force);
        _lastApplied = force;
        if (show && counts.Renderers == 0)
            Plugin.Instance?.Log.LogWarning($"Track Inspector enabled the native trigger flag, but found no trigger mesh renderers ({counts.Triggers} triggers, {counts.Colliders} colliders).");
        else
            Plugin.Instance?.Log.LogInfo($"Track Inspector {(force ? "showing" : "hiding")} native trigger meshes ({counts.Renderers} renderers across {counts.Triggers} triggers).");
    }

    private (int Triggers, int Colliders, int Renderers) SetTriggerMeshes(Ant_MapData map, bool visible)
    {
        map.bForceDebugShowTriggerCollisionMeshes = visible;
        int triggers = 0, collidersSeen = 0, renderers = 0;
        foreach (var trigger in UnityEngine.Object.FindObjectsOfType<PTK_ModTKLogic_PhysicsTrigger>())
        {
            if (trigger == null) continue;
            triggers++;
            var colliders = trigger.GetComponentsInChildren<Collider>(true);
            if (colliders == null) continue;
            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                collidersSeen++;
                var renderer = collider.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                CaptureRendererDefault(renderer);
                bool original = _rendererDefaults[renderer.GetInstanceID()].OriginalEnabled;
                renderer.enabled = visible || original;
                renderers++;
            }
        }
        PruneDestroyedRenderers();
        return (triggers, collidersSeen, renderers);
    }

    private void CaptureRendererDefault(MeshRenderer renderer)
    {
        int id = renderer.GetInstanceID();
        if (!_rendererDefaults.ContainsKey(id)) _rendererDefaults[id] = (renderer, renderer.enabled);
    }

    private void RestoreMapSnapshot(Ant_MapData map)
    {
        int id = map.GetInstanceID();
        if (_mapDefaults.TryGetValue(id, out var saved))
        {
            if (saved.Map != null) saved.Map.bForceDebugShowTriggerCollisionMeshes = saved.OriginalFlag;
            _mapDefaults.Remove(id);
        }
        RestoreRenderers(clear: false);
        _map = null;
        _mapDefault = false;
        _lastApplied = false;
    }

    private void RestoreRenderers(bool clear)
    {
        foreach (var saved in _rendererDefaults.Values)
            if (saved.Renderer != null) saved.Renderer.enabled = saved.OriginalEnabled;
        if (clear) _rendererDefaults.Clear();
        else PruneDestroyedRenderers();
    }

    private void PruneDestroyedRenderers()
    {
        var stale = new List<int>();
        foreach (var item in _rendererDefaults)
            if (item.Value.Renderer == null) stale.Add(item.Key);
        foreach (int id in stale) _rendererDefaults.Remove(id);
    }

    public void Restore()
    {
        _visible = true;
        foreach (var saved in _mapDefaults.Values)
            if (saved.Map != null) saved.Map.bForceDebugShowTriggerCollisionMeshes = saved.OriginalFlag;
        _mapDefaults.Clear();
        RestoreRenderers(clear: true);
        _map = null;
        _mapDefault = false;
        _lastApplied = false;
    }
}

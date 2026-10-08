using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

/// <summary>Uses the game's own trigger-collision debug meshes; no custom renderer or scene scan.</summary>
public sealed class TrackInspector : IModRecipe
{
    public string Name => "TrackInspector";
    public bool ChangesGameplay => false;

    private ConfigEntry<KeyCode> _key = null!;
    private Ant_MapData? _map;
    private bool _mapDefault;
    private bool _lastApplied;
    private bool _visible = true;

    public void Configure(ConfigFile config)
    {
        _key = config.Bind("Recipe." + Name, "ToggleKey", KeyCode.F10,
            "Show or hide the game's trigger-collision debug meshes during an offline race.");

        // Remove settings used by the retired sampled-mesh overlay.
        foreach (string key in new[] { "ShowWalls", "ShowRespawn", "ShowKillTriggers", "ShowOtherTriggers", "DrawDistance",
                     "Opacity", "ShowThroughTrack", "ShowLabels", "ColliderSampleResolution", "LineWidth", "ShowBoundsFallback" })
            config.Remove(new ConfigDefinition("Recipe.TrackInspector", key));
    }

    public void Tick()
    {
        bool inOfflineRace = Plugin.OfflineLabAllowed &&
            Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
        if (inOfflineRace && Input.GetKeyDown(_key.Value)) _visible = !_visible;

        var map = Ant_MapData.instance;
        if (map == null) { RestoreMap(); return; }
        if (map != _map)
        {
            RestoreMap();
            _map = map;
            _mapDefault = map.bForceDebugShowTriggerCollisionMeshes;
            _lastApplied = !_mapDefault;
        }

        bool show = inOfflineRace && _visible;
        bool force = _mapDefault || show;
        if (force == _lastApplied) return;
        SetGameDebugMeshes(map, force);
        _lastApplied = force;
    }

    private static void SetGameDebugMeshes(Ant_MapData map, bool visible)
    {
        map.bForceDebugShowTriggerCollisionMeshes = visible;
        var colliders = map.mapPhysicsTriggerColliderList;
        if (colliders == null) return;
        foreach (var entry in colliders)
        {
            var collider = entry == null ? null : entry.collider;
            var renderer = collider == null ? null : collider.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = visible;
        }
    }

    private void RestoreMap()
    {
        var map = _map;
        _map = null;
        if (map == null) return;
        SetGameDebugMeshes(map, _mapDefault);
        _lastApplied = false;
    }

    public void Restore()
    {
        _visible = true;
        RestoreMap();
    }
}

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Rendering;
using Kind = TK2.Customization.TrackInspectorRules.Kind;

namespace TK2.Customization;

// Current-build native evidence and coverage limits: docs/TRACK-INSPECTOR.md.
// Scene overlay only: never changes a source collider, transform, tag or layer.
public sealed class TrackInspector : IModRecipe
{
    public string Name => "TrackInspector";
    public bool ChangesGameplay => false;
    private ConfigEntry<KeyCode> _key = null!;
    private ConfigEntry<bool> _walls = null!, _respawn = null!, _kill = null!, _other = null!, _through = null!, _labels = null!;
    private ConfigEntry<float> _distance = null!, _opacity = null!;
    private readonly Dictionary<int, Target> _targets = new();
    private readonly Dictionary<Kind, Material> _materials = new();
    private readonly List<Target> _nearby = new();
    private readonly List<Label> _drawLabels = new();
    private readonly HashSet<int> _trackScenes = new();
    private GameObject? _root;
    private bool _visible = true, _hasRace;
    private float _nextScan, _nextDraw;
    private int _scene;
    private const int MaxVisible = 96, MaxEdges = 1800, MaxCached = 192;
    internal struct Label { internal Camera Camera; internal Vector3 Position; internal string Text; internal Color Color; }
    private sealed class Target
    {
        internal Collider Collider = null!;
        internal Kind Kind;
        internal GameObject? Overlay;
        internal Mesh? Mesh;
        internal Vector3[]? Local, World;
        internal int[]? Indices;
        internal bool BoundsOnly;
        internal string Caption = "";
    }

    public void Configure(ConfigFile config)
    {
        _key = config.Bind("Recipe." + Name, "ToggleKey", KeyCode.F10, "Show or hide track outlines in a local race.");
        _walls = config.Bind("Recipe." + Name, "ShowWalls", true, "Physical wall layers, including invisible barriers. Cyan.");
        _respawn = config.Bind("Recipe." + Name, "ShowRespawn", true, "Always and conditional respawn wall layers. Orange and yellow.");
        _kill = config.Bind("Recipe." + Name, "ShowKillTriggers", true, "Triggers linked to an enabled kill command. Red; game conditions still apply.");
        _other = config.Bind("Recipe." + Name, "ShowOtherTriggers", false, "Other game logic trigger volumes. Purple; they are not assumed lethal.");
        _distance = config.Bind("Recipe." + Name, "DrawDistance", 150f, new ConfigDescription("Outline distance in metres. Nearest 96 colliders are drawn.", new AcceptableValueRange<float>(20f, 1000f)));
        _opacity = config.Bind("Recipe." + Name, "Opacity", 0.8f, new ConfigDescription("Outline opacity.", new AcceptableValueRange<float>(0.1f, 1f)));
        _through = config.Bind("Recipe." + Name, "ShowThroughTrack", false, "Draw outlines through scenery. Labels are screen annotations.");
        _labels = config.Bind("Recipe." + Name, "ShowLabels", true, "Show the category, object name and distance for nearby outlines; bounds fallbacks are marked.");
    }

    public void Tick()
    {
        if (!Plugin.OfflineLabAllowed || Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING)
        { Restore(); return; }
        var cameras = new List<Camera>();
        PixelEasyCharMoveKartController? local = null;
        foreach (var controller in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
        {
            var player = controller == null ? null : controller.parentPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) continue;
            local ??= controller;
            var brain = player.gameplayCamera;
            if (brain == null || brain.bIsSpectatorCamera) continue;
            var camera = brain.GetCamera()?.unityCamera;
            if (camera != null && camera.isActiveAndEnabled && camera.targetTexture == null && !cameras.Contains(camera)) cameras.Add(camera);
        }
        // Menu/lobby and spectator-only scenes must not receive track overlays.
        if (local == null || local.gameObject == null || !local.gameObject.activeInHierarchy) { Restore(); return; }
        if (cameras.Count == 0) { Hide(); return; }
        var map = Ant_MapData.instance;
        if (map == null || map.gameObject == null || !map.gameObject.activeInHierarchy) { Restore(); return; }
        // The kart can live in the persistent gameplay scene while the map is loaded additively.
        int scene = map.gameObject.scene.handle;
        if (_hasRace && scene != _scene) Restore();
        _hasRace = true; _scene = scene;
        if (Input.GetKeyDown(_key.Value)) _visible = !_visible;
        if (!_visible) { Hide(); return; }
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 4f;
            Scan(local, map);
        }
        if (Time.unscaledTime < _nextDraw) return;
        _nextDraw = Time.unscaledTime + 0.1f;
        EnsureRoot(); UpdateMaterials();
        _nearby.Clear(); _drawLabels.Clear();
        foreach (var target in _targets.Values)
        {
            if (target.Overlay != null) target.Overlay.SetActive(false);
            var collider = target.Collider;
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || !Show(target.Kind)) continue;
            if (DistanceSquared(collider, cameras) <= _distance.Value * _distance.Value) _nearby.Add(target);
        }
        _nearby.Sort((a, b) => DistanceSquared(a.Collider, cameras).CompareTo(DistanceSquared(b.Collider, cameras)));
        int count = Math.Min(MaxVisible, _nearby.Count);
        for (int i = 0; i < count; i++)
        {
            var target = _nearby[i];
            try { Draw(target); }
            catch (Exception ex)
            {
                // Unsupported collision meshes cannot disable the whole module.
                target.BoundsOnly = true; target.Local = null; target.Caption = "bounds";
                if (target.Overlay != null) target.Overlay.SetActive(false);
                Plugin.Instance?.Log.LogDebug($"Track Inspector bounds fallback: {ex.Message}");
            }
            if (!_labels.Value || i >= 24 || target.Overlay == null || !target.Overlay.activeSelf) continue;
            foreach (var camera in cameras)
            {
                var point = camera.WorldToViewportPoint(target.Collider.bounds.center);
                if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                float distance = Vector3.Distance(camera.transform.position, target.Collider.bounds.center);
                _drawLabels.Add(new Label { Camera = camera, Position = target.Collider.bounds.center, Color = ColorFor(target.Kind),
                    Text = $"{Caption(target.Kind)} · {target.Collider.name} · {distance:0}m" + (target.BoundsOnly ? " [bounds]" : "") });
            }
        }
        // Release stale geometry to bound memory even when roaming an entire track.
        if (_targets.Count > MaxCached)
            foreach (var target in _targets.Values)
                if (target.Overlay != null && !target.Overlay.activeSelf) Release(target);
    }

    private static float DistanceSquared(Collider collider, List<Camera> cameras)
    {
        float distance = float.MaxValue;
        var bounds = collider.bounds;
        foreach (var camera in cameras) distance = Mathf.Min(distance, bounds.SqrDistance(camera.transform.position));
        return distance;
    }
    private bool Show(Kind kind) => kind == Kind.Wall ? _walls.Value : kind == Kind.KillLinked ? _kill.Value :
        kind == Kind.OtherTrigger ? _other.Value : _respawn.Value;

    private void Scan(PixelEasyCharMoveKartController local, Ant_MapData map)
    {
        _trackScenes.Clear(); _trackScenes.Add(map.gameObject.scene.handle);
        if (map.mapPhysicsTriggerColliderList != null)
            foreach (var registered in map.mapPhysicsTriggerColliderList)
                if (registered != null && registered.collider != null) _trackScenes.Add(registered.collider.gameObject.scene.handle);
        var killTriggers = new HashSet<int>();
        foreach (var executor in UnityEngine.Object.FindObjectsOfType<PTK_TriggerArrayCommandsExecutor>())
        {
            if (executor == null || !executor.isActiveAndEnabled || !executor.bModTriggerCommandExecutorEnabled || !_trackScenes.Contains(executor.gameObject.scene.handle)) continue;
            bool kills = false;
            var behaviours = executor.commandBehavioursLogicsToRun;
            if (behaviours != null)
                foreach (var behaviour in behaviours)
                {
                    if (behaviour == null) continue;
                    foreach (var command in behaviour.GetComponents<PTK_Command_05_PlayerLogicEffects>())
                        if (command != null && command.enabled && command.KillPlayer != null && command.KillPlayer.bExecute) kills = true;
                }
            if (!kills) continue;
            var triggers = executor.receiveEventsFromTriggers;
            if (triggers != null) foreach (var trigger in triggers)
                if (trigger != null && trigger.bIsTriggerEnabled) killTriggers.Add(trigger.GetInstanceID());
        }
        int walls = local.wallLayerToCollDetect.value, flat = local.wallRespawn_Flat_ColliderLayer.value,
            always = local.wallRespawn_Always_ColliderLayer.value;
        var active = new HashSet<int>();
        foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || !_trackScenes.Contains(collider.gameObject.scene.handle)) continue;
            // Player/weapon attachments must not be classified as static track obstacles.
            if (collider.GetComponentInParent<Ant_Player>() != null) continue;
            var physicsTrigger = collider.GetComponentInParent<PTK_ModTKLogic_PhysicsTrigger>();
            bool trigger = physicsTrigger != null && physicsTrigger.triggerInfo != null &&
                physicsTrigger.triggerInfo.bEnabledAndDetectedByPlayers && RegisteredTriggerCollider(physicsTrigger, collider);
            bool kill = trigger && killTriggers.Contains(physicsTrigger!.triggerInfo!.GetInstanceID());
            var kind = TrackInspectorRules.Classify(collider.gameObject.layer, walls, flat, always, kill, trigger);
            if (kind == Kind.None) continue;
            int id = collider.GetInstanceID(); active.Add(id);
            if (!_targets.TryGetValue(id, out var target)) _targets[id] = target = new Target { Collider = collider };
            if (target.Kind != kind) { Release(target); target.Kind = kind; }
        }
        var stale = new List<int>();
        foreach (var pair in _targets) if (!active.Contains(pair.Key)) { Release(pair.Value); stale.Add(pair.Key); }
        foreach (int id in stale) _targets.Remove(id);
    }

    private static bool RegisteredTriggerCollider(PTK_ModTKLogic_PhysicsTrigger trigger, Collider collider)
    {
        int id = collider.GetInstanceID();
        if (trigger.triggerCollider != null)
            foreach (var registered in trigger.triggerCollider)
                if (registered != null && registered.GetInstanceID() == id) return true;
        if (trigger.triggerColliders != null)
            foreach (var registered in trigger.triggerColliders)
                if (registered != null && registered.collider != null && registered.bEnabledAndDetectedByPlayers && registered.collider.GetInstanceID() == id) return true;
        return false;
    }

    private void EnsureRoot()
    {
        if (_root != null) return;
        _root = new GameObject("TK2 Track Inspector overlays");
        _root.hideFlags = HideFlags.DontSave;
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<TrackInspectorLabels>()) ClassInjector.RegisterTypeInIl2Cpp<TrackInspectorLabels>();
        _root.AddComponent<TrackInspectorLabels>().Owner = this;
    }
    private void UpdateMaterials()
    {
        foreach (Kind kind in new[] { Kind.Wall, Kind.ConditionalRespawn, Kind.AlwaysRespawn, Kind.KillLinked, Kind.OtherTrigger })
        {
            if (!_materials.TryGetValue(kind, out var material) || material == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) throw new InvalidOperationException("Track outline shader Hidden/Internal-Colored is unavailable.");
                _materials[kind] = material = new Material(shader) { hideFlags = HideFlags.DontSave };
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)CullMode.Off); material.SetInt("_ZWrite", 0);
                material.renderQueue = 3100;
            }
            var color = ColorFor(kind); color.a = _opacity.Value; material.color = color;
            material.SetInt("_ZTest", (int)(_through.Value ? CompareFunction.Always : CompareFunction.LessEqual));
        }
    }
    private static Color ColorFor(Kind kind) => kind == Kind.Wall ? new Color(.15f, .85f, 1f) : kind == Kind.KillLinked ? new Color(1f, .2f, .25f) :
        kind == Kind.AlwaysRespawn ? new Color(1f, .5f, .1f) : kind == Kind.ConditionalRespawn ? new Color(1f, .9f, .2f) : new Color(.85f, .4f, 1f);
    private static string Caption(Kind kind) => kind == Kind.Wall ? "Wall" : kind == Kind.AlwaysRespawn ? "Respawn wall" :
        kind == Kind.ConditionalRespawn ? "Conditional respawn" : kind == Kind.KillLinked ? "Kill-linked trigger" : "Other trigger";

    private void Draw(Target target)
    {
        if (target.Local == null) BuildShape(target);
        if (target.Overlay == null)
        {
            target.Overlay = new GameObject("TK2 outline"); target.Overlay.hideFlags = HideFlags.DontSave;
            target.Overlay.transform.SetParent(_root!.transform, false);
            target.Mesh = new Mesh { name = "TK2 collision outline", hideFlags = HideFlags.DontSave };
            target.Mesh.MarkDynamic();
            target.Overlay.AddComponent<MeshFilter>().sharedMesh = target.Mesh;
            var renderer = target.Overlay.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _materials[target.Kind];
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        Vector3[] world = target.World!;
        if (target.BoundsOnly)
        {
            var bounds = target.Collider.bounds;
            for (int i = 0; i < world.Length; i++) world[i] = bounds.center + Vector3.Scale(target.Local![i], bounds.extents);
        }
        else if (target.Collider.TryCast<SphereCollider>() is SphereCollider sphere)
        {
            Vector3 scale = sphere.transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            for (int i = 0; i < world.Length; i++) world[i] = center + target.Local![i] * radius;
        }
        else if (target.Collider.TryCast<CapsuleCollider>() is CapsuleCollider capsule)
        {
            Vector3 scale = capsule.transform.lossyScale;
            float axial = Mathf.Abs(capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z);
            float radial = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) :
                capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float radius = capsule.radius * radial, half = Mathf.Max(0, capsule.height * axial * .5f - radius);
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            var rotation = capsule.transform.rotation * Quaternion.FromToRotation(Vector3.up, axis);
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            for (int i = 0; i < world.Length; i++)
            {
                Vector3 point = target.Local![i]; float hemisphere = point.y >= 0 ? 1f : -1f;
                world[i] = center + rotation * (point * radius + Vector3.up * (half * hemisphere));
            }
        }
        else for (int i = 0; i < world.Length; i++) world[i] = target.Collider.transform.TransformPoint(target.Local![i]);
        target.Mesh!.vertices = world; target.Mesh.SetIndices(target.Indices!, MeshTopology.Lines, 0); target.Mesh.RecalculateBounds();
        target.Overlay.SetActive(true);
    }

    private static void BuildShape(Target target)
    {
        var points = new List<Vector3>(); var edges = new List<int>();
        if (target.Collider.TryCast<BoxCollider>() is BoxCollider box)
        {
            Box(points, edges);
            for (int i = 0; i < points.Count; i++) points[i] = box.center + Vector3.Scale(points[i], box.size * .5f);
        }
        else if (target.Collider.TryCast<SphereCollider>() is SphereCollider) Rings(points, edges, false);
        else if (target.Collider.TryCast<CapsuleCollider>() is CapsuleCollider) Rings(points, edges, true);
        else if (target.Collider.TryCast<MeshCollider>() is MeshCollider collider && !collider.convex && collider.sharedMesh != null && collider.sharedMesh.isReadable)
        {
            var mesh = collider.sharedMesh;
            if (mesh.vertexCount > 150000) target.BoundsOnly = true;
            else
            {
                var vertices = mesh.vertices; var tris = mesh.triangles;
                int triangleLimit = Math.Min(tris.Length, 300000);
                int[] native = new int[triangleLimit]; for (int i = 0; i < native.Length; i++) native[i] = tris[i];
                var indices = TrackInspectorRules.MeshEdges(native, vertices.Length, MaxEdges);
                // Compact to referenced vertices; each edge is independent and bounded.
                foreach (int index in indices) { edges.Add(points.Count); points.Add(vertices[index]); }
                if (points.Count == 0) target.BoundsOnly = true;
            }
        }
        else target.BoundsOnly = true;
        if (target.BoundsOnly) { points.Clear(); edges.Clear(); Box(points, edges); }
        target.Local = points.ToArray(); target.World = new Vector3[target.Local.Length]; target.Indices = edges.ToArray();
    }
    private static void Box(List<Vector3> points, List<int> edges)
    {
        for (int i = 0; i < 8; i++) points.Add(new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        for (int i = 0; i < 8; i++) foreach (int bit in new[] { 1, 2, 4 }) if ((i & bit) == 0) { edges.Add(i); edges.Add(i | bit); }
    }
    private static void Rings(List<Vector3> points, List<int> edges, bool capsule)
    {
        const int steps = 32;
        for (int ring = 0; ring < 3; ring++)
        {
            int first = points.Count;
            for (int i = 0; i < steps; i++)
            {
                float angle = i * Mathf.PI * 2 / steps, a = Mathf.Cos(angle), b = Mathf.Sin(angle);
                // Equator + two vertical circles; capsule equator is split into the two hemisphere rims.
                points.Add(ring == 0 ? new Vector3(a, capsule ? .000001f : 0, b) : ring == 1 ? new Vector3(a, b, 0) : new Vector3(0, b, a));
                edges.Add(first + i); edges.Add(first + (i + 1) % steps);
            }
        }
        if (!capsule) return;
        int rim = points.Count;
        for (int i = 0; i < steps; i++)
        {
            float angle = i * Mathf.PI * 2 / steps;
            points.Add(new Vector3(Mathf.Cos(angle), -.000001f, Mathf.Sin(angle)));
            edges.Add(rim + i); edges.Add(rim + (i + 1) % steps);
        }
    }

    internal void DrawLabels()
    {
        if (!_visible || !_labels.Value || !Plugin.OfflineLabAllowed) return;
        foreach (var label in _drawLabels)
        {
            if (label.Camera == null) continue;
            Vector3 point = label.Camera.WorldToScreenPoint(label.Position);
            if (point.z <= 0) continue;
            Rect viewport = label.Camera.pixelRect;
            float width = Mathf.Min(340, viewport.width), left = Mathf.Clamp(point.x, viewport.xMin, viewport.xMax - width);
            float top = Screen.height - Mathf.Clamp(point.y, viewport.yMin + 24, viewport.yMax);
            var rect = new Rect(left, top, width, 24);
            GUI.color = new Color(0, 0, 0, .85f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = label.Color; GUI.Label(rect, label.Text);
        }
        GUI.color = Color.white;
    }
    private void Hide()
    {
        _drawLabels.Clear();
        foreach (var target in _targets.Values) if (target.Overlay != null) target.Overlay.SetActive(false);
    }
    private static void Release(Target target)
    {
        if (target.Mesh != null) UnityEngine.Object.Destroy(target.Mesh);
        if (target.Overlay != null) UnityEngine.Object.Destroy(target.Overlay);
        target.Mesh = null; target.Overlay = null; target.Local = null; target.World = null; target.Indices = null;
    }
    public void Restore()
    {
        foreach (var target in _targets.Values) Release(target);
        foreach (var material in _materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _targets.Clear(); _materials.Clear(); _drawLabels.Clear(); _nearby.Clear(); _trackScenes.Clear();
        _root = null; _hasRace = false; _visible = true; _nextScan = _nextDraw = 0;
    }
}

public sealed class TrackInspectorLabels : MonoBehaviour
{
    public TrackInspectorLabels(IntPtr pointer) : base(pointer) { }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp] internal TrackInspector? Owner { get; set; }
    public void OnGUI() { Owner?.DrawLabels(); }
}

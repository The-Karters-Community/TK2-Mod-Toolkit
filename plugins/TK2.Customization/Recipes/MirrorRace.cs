using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using GPUInstancer;
using UnityEngine;

namespace TK2.Customization;

// Reflect the physical track before native road/checkpoint caches are constructed.
// No projection, culling, steering, global negative scale or screen-image patches.
public sealed class MirrorRace : IModRecipe
{
    public string Name => "MirrorRace";
    public bool ChangesGameplay => true;
    private ConfigEntry<bool> _enabled = null!;
    private static MirrorRace? _instance;
    private static Track? _track;
    public static string Status { get; private set; } = "Off; applies on the next track load.";

    public void Configure(ConfigFile config)
    {
        _enabled = config.Bind("Recipe." + Name, "Enabled", false, "Physically mirror supported offline tracks on the next track load. Changing this during a race takes effect after leaving and loading a track again.");
        _instance = this;
        var target = AccessTools.DeclaredMethod(typeof(Ant_MapData), "Awake", Type.EmptyTypes)
            ?? throw new MissingMethodException("Ant_MapData.Awake() signature changed.");
        Plugin.Instance!.Harmony.Patch(target, prefix: new HarmonyMethod(typeof(MirrorRace), nameof(BeforeMapAwake)));
        // Modded tracks create additional cached paths before MapData.Awake: explicitly refuse them.
    }

    private static void BeforeMapAwake(Ant_MapData __instance)
    {
        if (_instance == null || !_instance._enabled.Value || !Plugin.OfflineLabAllowed) return;
        CleanupUnloaded();
        if (_track != null) return;
        Track? plan = null;
        try
        {
            plan = Track.Prepare(__instance); // Clones/buffer reads only; original scene untouched.
            plan.Apply();
            _track = plan;
            Plugin.Instance!.SessionModified = true;
            Status = "Physical mirror active; changes apply after the next track load.";
            Plugin.Instance.Log.LogInfo($"Mirror Race: mirrored {plan.TransformCount} transforms and {plan.MeshCount} unique meshes in {__instance.gameObject.scene.name}. Ordinary camera and controls preserved.");
        }
        catch (Exception ex)
        {
            try { plan?.Rollback(); } catch (Exception restore) { Plugin.Instance?.Log.LogError($"Mirror rollback: {restore}"); }
            Status = "Mirror unavailable on this track: " + ex.Message;
            Plugin.Instance?.Log.LogWarning(Status);
        }
    }

    private static void CleanupUnloaded()
    {
        if (_track == null || _track.Map != null) return;
        _track.ReleaseMeshes(); _track = null;
    }

    public void Tick()
    {
        CleanupUnloaded();
        if (_track == null && Status.StartsWith("Off", StringComparison.Ordinal)) Status = "Ready; load an offline track to apply physical mirror.";
    }

    public void Restore()
    {
        CleanupUnloaded();
        // Restoring transforms mid-race would invalidate native readonly progress and AI caches.
        if (_track != null) Status = "Current track stays mirrored; disable takes effect after the next track load.";
        else if (!_enabled.Value) Status = "Off; applies on the next track load.";
    }

    private sealed class Track
    {
        internal readonly Ant_MapData Map;
        private readonly List<Transform> _transforms = new();
        private readonly List<(Transform Item, Vector3 Position, Quaternion Rotation, Vector3 LocalPosition, Quaternion LocalRotation)> _poses = new();
        private readonly List<(Transform Item, Vector3 Position, Quaternion Rotation, Vector3 LocalPosition, Quaternion LocalRotation)> _protectedPoses = new();
        private readonly List<MeshFilter> _filters = new();
        private readonly List<Collider> _colliders = new();
        private readonly List<BezierSolution.BezierPoint> _bezier = new();
        private readonly List<GPUInstancerPrefabManager> _managers = new();
        private readonly List<GPUInstancerPrefab> _prefabs = new();
        private readonly HashSet<int> _fallbackPrefabs = new();
        private readonly Dictionary<int, Mesh> _meshes = new();
        private readonly List<Action> _undo = new();
        private Texture2D? _minimap;
        private Track(Ant_MapData map) { Map = map; }
        internal int TransformCount => _transforms.Count;
        internal int MeshCount => _meshes.Count;

        internal static Track Prepare(Ant_MapData map)
        {
            var plan = new Track(map);
            try
            {
                var scene = map.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded) throw new NotSupportedException("Track scene is not loaded.");
                var config = Ant_CurrentGameConfiguration.GetFinalChoosedMapConfig(-1);
                if (config == null || config.bIsCustomTrack || config.bIsModdedConfig)
                    throw new NotSupportedException("Custom track creation needs separate path/trigger reconstruction.");
                foreach (var root in scene.GetRootGameObjects())
                {
                    var protectedRoots = new List<Transform>();
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) protectedRoots.Add(canvas.transform);
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true)) protectedRoots.Add(camera.transform);
                    for (int i = protectedRoots.Count - 1; i >= 0; i--)
                        for (int j = 0; j < protectedRoots.Count; j++)
                            if (i != j && protectedRoots[i].IsChildOf(protectedRoots[j])) { protectedRoots.RemoveAt(i); break; }
                    foreach (var protectedRoot in protectedRoots)
                        plan._protectedPoses.Add((protectedRoot, protectedRoot.position, protectedRoot.rotation, protectedRoot.localPosition, protectedRoot.localRotation));
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.gameObject.scene.handle != scene.handle) throw new NotSupportedException("Cross-scene track hierarchy.");
                        if (++plan._scanCount > 30_000) throw new NotSupportedException("Track hierarchy exceeds the 30,000 transform limit.");
                        bool protectedTree = false;
                        foreach (var protectedRoot in protectedRoots) if (t == protectedRoot || t.IsChildOf(protectedRoot)) { protectedTree = true; break; }
                        if (protectedTree) continue;
                        // Hierarchy traversal returns parents before children. Save every world pose before changing any parent.
                        plan._transforms.Add(t); plan._poses.Add((t, t.position, t.rotation, t.localPosition, t.localRotation));
                    }
                    if (root.GetComponentsInChildren<Terrain>(true).Length != 0 || root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0 ||
                        root.GetComponentsInChildren<Animator>(true).Length != 0 || root.GetComponentsInChildren<Animation>(true).Length != 0)
                        throw new NotSupportedException("Terrain or animated track geometry requires authored mirrored assets.");
                    if (root.GetComponentsInChildren<PTK_ModTrack>(true).Length != 0 || root.GetComponentsInChildren<PTK_QuickCutLevel>(true).Length != 0)
                        throw new NotSupportedException("Generated/mod-track gameplay is not supported by this authored-scene reflector.");
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.isPartOfStaticBatch) throw new NotSupportedException("Static-batched geometry requires a mirrored asset build.");
                        if (r.lightmapIndex >= 0 && r.lightmapIndex < 0xfffe && LightmapSettings.lightmapsMode == LightmapsMode.CombinedDirectional)
                            throw new NotSupportedException("Directional baked lighting needs a mirrored lighting asset build.");
                    }
                    foreach (var manager in root.GetComponentsInChildren<GPUInstancerManager>(true))
                    {
                        if (!manager.enabled || !manager.gameObject.activeInHierarchy) continue;
                        var prefabManager = manager.TryCast<GPUInstancerPrefabManager>();
                        if (prefabManager == null) throw new NotSupportedException("GPU-instanced terrain/detail/tree data cannot be reflected safely.");
                        plan._managers.Add(prefabManager);
                    }
                    foreach (var prefab in root.GetComponentsInChildren<GPUInstancerPrefab>(true)) plan._prefabs.Add(prefab);
                    foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                        if (!rb.isKinematic) throw new NotSupportedException("Dynamic track rigidbodies require mirrored motion controllers.");
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    { if (plan._transforms.Contains(filter.transform) && filter.sharedMesh != null) { plan.EnsureMesh(filter.sharedMesh); plan._filters.Add(filter); } }
                    foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                    {
                        if (!plan._transforms.Contains(collider.transform))
                            throw new NotSupportedException("Collisions inside a protected camera/UI hierarchy need an authored mirrored layout.");
                        var mc = collider.TryCast<MeshCollider>();
                        if (mc != null && mc.sharedMesh != null) plan.EnsureMesh(mc.sharedMesh);
                        else if (collider.TryCast<BoxCollider>() == null && collider.TryCast<SphereCollider>() == null && collider.TryCast<CapsuleCollider>() == null)
                            throw new NotSupportedException($"Unsupported collider: {collider.GetIl2CppType().Name}.");
                        plan._colliders.Add(collider);
                    }
                    foreach (var point in root.GetComponentsInChildren<BezierSolution.BezierPoint>(true))
                    {
                        if (!plan._transforms.Contains(point.transform))
                            throw new NotSupportedException("Paths inside a protected camera/UI hierarchy need an authored mirrored layout.");
                        plan._bezier.Add(point);
                    }
                }
                if (plan._filters.Count == 0) throw new NotSupportedException("No authored track mesh found in the map scene.");
                if (map.RaceProgressPaths != null && map.RaceProgressPaths.Length != 0)
                    throw new NotSupportedException("Race progress was initialized before the mirror hook.");
                plan.CheckGpuCoverage(scene.handle);
                plan.PrepareMinimap();
                return plan;
            }
            catch { plan.ReleaseMeshes(); throw; }
        }

        private int _scanCount;
        private long _meshBytes;
        private void EnsureMesh(Mesh mesh)
        {
            if (_meshes.ContainsKey(mesh.GetInstanceID())) return;
            long bytes = 0;
            for (int i = 0; i < mesh.vertexBufferCount; i++) bytes += (long)mesh.vertexCount * mesh.GetVertexBufferStride(i);
            long indexEnd = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) { var sub = mesh.GetSubMesh(i); indexEnd = Math.Max(indexEnd, (long)sub.indexStart + sub.indexCount); }
            bytes += indexEnd * (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 ? 2 : 4);
            _meshBytes = checked(_meshBytes + bytes);
            if (_meshBytes > 128L * 1024 * 1024) throw new NotSupportedException("Mirrored track meshes exceed the 128 MiB allocation budget.");
            _meshes.Add(mesh.GetInstanceID(), MirrorMesh.Clone(mesh));
        }

        private void PrepareMinimap()
        {
            var source = Map.minimapImage;
            if (source == null) return;
            if (source.width > 4096 || source.height > 4096) throw new NotSupportedException("Minimap exceeds 4096 pixels.");
            _minimap = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            if (!source.isReadable)
            {
                var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                try { Graphics.Blit(source, temporary); RenderTexture.active = temporary; _minimap.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false); _minimap.Apply(false, false); }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
            }
            var pixels = source.isReadable ? source.GetPixels32() : _minimap.GetPixels32();
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width / 2; x++)
                { int a = y * source.width + x, b = y * source.width + source.width - 1 - x; var c = pixels[a]; pixels[a] = pixels[b]; pixels[b] = c; }
            _minimap.SetPixels32(pixels); _minimap.Apply(false, false);
        }

        private void CheckGpuCoverage(int sceneHandle)
        {
            foreach (var manager in _managers)
            {
                var registered = manager.GetRegisteredPrefabsRuntimeData();
                if (registered == null && manager.isInitialized) throw new NotSupportedException("GPU instance source registry is missing.");
                if (registered == null) continue;
                foreach (var group in registered)
                {
                    if (group.Value == null) throw new NotSupportedException("GPU instance source registry is incomplete.");
                    foreach (var prefab in group.Value)
                    {
                        if (prefab == null || prefab.gameObject.scene.handle != sceneHandle || !_prefabs.Contains(prefab) || !_transforms.Contains(prefab.transform) ||
                            prefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
                            throw new NotSupportedException("GPU instance has no complete ordinary-renderer source in this track scene.");
                        if (manager.isActiveAndEnabled && prefab.state == PrefabInstancingState.Instanced && prefab.gameObject.activeInHierarchy) _fallbackPrefabs.Add(prefab.GetInstanceID());
                    }
                }
                if (manager.runtimeDataList == null) continue;
                foreach (var runtime in manager.runtimeDataList)
                    if (runtime != null && (!registered.TryGetValue(runtime.prototype, out var sources) || sources == null || sources.Count < runtime.instanceCount))
                        throw new NotSupportedException("GPU-only matrix instances require mirrored instancing data.");
            }
        }

        internal void Apply()
        {
            foreach (var manager in _managers)
            {
                bool old = manager.enabled, restore = manager.enableMROnManagerDisable, initialized = manager.isInitialized;
                _undo.Add(() => { if (manager != null) { manager.enableMROnManagerDisable = restore; manager.enabled = old; if (old && initialized) manager.InitializeRuntimeDataAndBuffers(true); } });
                // Native ClearInstancingData normally calls SetRenderersEnabled, which can add rigidbodies.
                // Prevent that side effect and restore ordinary renderers explicitly instead.
                manager.enableMROnManagerDisable = false; manager.enabled = false;
            }
            foreach (var prefab in _prefabs)
            {
                if (!_fallbackPrefabs.Contains(prefab.GetInstanceID())) continue;
                foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                { bool old = r.enabled; _undo.Add(() => { if (r != null) r.enabled = old; }); r.enabled = true; }
                foreach (var lod in prefab.GetComponentsInChildren<LODGroup>(true))
                { bool old = lod.enabled; _undo.Add(() => { if (lod != null) lod.enabled = old; }); lod.enabled = true; }
            }
            foreach (var pose in _poses)
            { _undo.Add(() => { if (pose.Item != null) { pose.Item.localPosition = pose.LocalPosition; pose.Item.localRotation = pose.LocalRotation; } }); pose.Item.SetPositionAndRotation(MirrorMesh.Point(pose.Position), MirrorMesh.Rotation(pose.Rotation)); }
            foreach (var pose in _protectedPoses)
            { _undo.Add(() => { if (pose.Item != null) { pose.Item.localPosition = pose.LocalPosition; pose.Item.localRotation = pose.LocalRotation; } }); pose.Item.SetPositionAndRotation(pose.Position, pose.Rotation); }
            foreach (var filter in _filters)
            { var source = filter.sharedMesh; _undo.Add(() => { if (filter != null) filter.sharedMesh = source; }); filter.sharedMesh = _meshes[source.GetInstanceID()]; }
            foreach (var collider in _colliders) MirrorCollider(collider);
            foreach (var point in _bezier) MirrorBezier(point);
            var original = Map.minimapImage; Vector2 bl = Map.minimapImage_WorldPos_BL, tr = Map.minimapImage_WorldPos_TR;
            _undo.Add(() => { if (Map != null) { Map.minimapImage = original; Map.minimapImage_WorldPos_BL = bl; Map.minimapImage_WorldPos_TR = tr; } });
            if (_minimap != null) Map.minimapImage = _minimap;
            Map.minimapImage_WorldPos_BL = new Vector2(-tr.x, bl.y); Map.minimapImage_WorldPos_TR = new Vector2(-bl.x, tr.y);
            foreach (var spline in Map.gameObject.scene.GetRootGameObjects())
                foreach (var curve in spline.GetComponentsInChildren<BezierSolution.BezierSpline>(true)) curve.Refresh();
            Physics.SyncTransforms();
        }

        private void MirrorCollider(Collider collider)
        {
            var mesh = collider.TryCast<MeshCollider>();
            if (mesh != null)
            { var source = mesh.sharedMesh; if (source == null) return; _undo.Add(() => { if (mesh != null) mesh.sharedMesh = source; }); mesh.sharedMesh = _meshes[source.GetInstanceID()]; return; }
            var box = collider.TryCast<BoxCollider>();
            if (box != null) { var center = box.center; _undo.Add(() => { if (box != null) box.center = center; }); box.center = MirrorMesh.Point(center); return; }
            var sphere = collider.TryCast<SphereCollider>();
            if (sphere != null) { var center = sphere.center; _undo.Add(() => { if (sphere != null) sphere.center = center; }); sphere.center = MirrorMesh.Point(center); return; }
            var capsule = collider.Cast<CapsuleCollider>();
            var c = capsule.center; _undo.Add(() => { if (capsule != null) capsule.center = c; }); capsule.center = MirrorMesh.Point(c);
        }

        private void MirrorBezier(BezierSolution.BezierPoint point)
        {
            var p = point.m_position; var before = point.m_precedingControlPointLocalPosition; var after = point.m_followingControlPointLocalPosition;
            var wb = point.m_precedingControlPointPosition; var wa = point.m_followingControlPointPosition;
            var left = point.vLeftSidePoint; var right = point.vRightSidePoint;
            var distances = new[] { point.fDistanceToLeftSidePoint_NoY, point.fDistanceToRightSidePoint_NoY,
                point.fDistanceToLeftSidePoint_NoY_Orginal_NoOffsets, point.fDistanceToRightSidePoint_NoY_Orginal_NoOffsets,
                point.fLeftSideDistanceOffsetOverride, point.fRightSideDistanceOffsetOverride,
                point.fLeftSideCenterOffsetDistOverride, point.fRightSideCenterOffsetDistOverride };
            _undo.Add(() => { if (point == null) return; point.m_position = p; point.m_precedingControlPointLocalPosition = before; point.m_followingControlPointLocalPosition = after;
                point.m_precedingControlPointPosition = wb; point.m_followingControlPointPosition = wa; point.vLeftSidePoint = left; point.vRightSidePoint = right;
                SetWidths(point, distances, false); });
            point.m_position = MirrorMesh.Point(p); point.m_precedingControlPointLocalPosition = MirrorMesh.Point(before); point.m_followingControlPointLocalPosition = MirrorMesh.Point(after);
            point.m_precedingControlPointPosition = MirrorMesh.Point(wb); point.m_followingControlPointPosition = MirrorMesh.Point(wa);
            point.vLeftSidePoint = MirrorMesh.Point(right); point.vRightSidePoint = MirrorMesh.Point(left);
            SetWidths(point, distances, true);
        }

        private static void SetWidths(BezierSolution.BezierPoint p, float[] widths, bool mirrored)
        {
            int l = mirrored ? 1 : 0, r = mirrored ? 0 : 1;
            p.fDistanceToLeftSidePoint_NoY = widths[l]; p.fDistanceToRightSidePoint_NoY = widths[r];
            p.fDistanceToLeftSidePoint_NoY_Orginal_NoOffsets = widths[2 + l]; p.fDistanceToRightSidePoint_NoY_Orginal_NoOffsets = widths[2 + r];
            p.fLeftSideDistanceOffsetOverride = widths[4 + l]; p.fRightSideDistanceOffsetOverride = widths[4 + r];
            p.fLeftSideCenterOffsetDistOverride = widths[6 + l]; p.fRightSideCenterOffsetDistOverride = widths[6 + r];
        }

        internal void Rollback()
        {
            Exception? failure = null;
            for (int i = _undo.Count - 1; i >= 0; i--) try { _undo[i](); } catch (Exception ex) { failure ??= ex; }
            _undo.Clear();
            try
            {
                if (Map != null) foreach (var root in Map.gameObject.scene.GetRootGameObjects())
                    foreach (var curve in root.GetComponentsInChildren<BezierSolution.BezierSpline>(true)) curve.Refresh();
            }
            catch (Exception ex) { failure ??= ex; }
            try { Physics.SyncTransforms(); } catch (Exception ex) { failure ??= ex; }
            ReleaseMeshes();
            if (failure != null) throw new InvalidOperationException("Mirror rollback failed; leave this track before continuing.", failure);
        }
        internal void ReleaseMeshes()
        { foreach (var mesh in _meshes.Values) UnityEngine.Object.Destroy(mesh); _meshes.Clear(); if (_minimap != null) UnityEngine.Object.Destroy(_minimap); _minimap = null; }
    }
}

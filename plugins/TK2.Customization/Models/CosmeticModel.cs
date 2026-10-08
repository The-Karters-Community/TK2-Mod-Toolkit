using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// An imported shell follows the kart's visual root. Game physics and driver rig stay intact.
public sealed class CosmeticModel : IModRecipe
{
    public string Name => "CosmeticModel";
    public bool ChangesGameplay => false;
    private ConfigEntry<string> _path = null!, _asset = null!, _tint = null!;
    private ConfigEntry<float> _scale = null!, _x = null!, _y = null!, _z = null!, _rx = null!, _ry = null!, _rz = null!;
    private ConfigEntry<bool> _hide = null!, _mirror = null!;
    private ConfigEntry<int> _reload = null!;
    private readonly List<UnityEngine.Object> _owned = new();
    private readonly Dictionary<int, Attachment> _attachments = new();
    private AssetBundle? _bundle;
    private GameObject? _template;
    private string _signature = "";
    private float _nextScan;
    private sealed class Attachment
    {
        internal Transform Parent = null!;
        internal GameObject Object = null!;
        internal readonly List<(Renderer Renderer, bool ForceRenderingOff)> Hidden = new();
        internal readonly List<(Material Material, Color Color)> Materials = new();
    }

    public void Configure(ConfigFile config)
    {
        string s = "Recipe." + Name;
        _path = config.Bind(s, "ModelPath", "", "Imported OBJ or Unity AssetBundle path relative to BepInEx/models.");
        _asset = config.Bind(s, "AssetName", "", "Bundle prefab asset name; empty selects its first static prefab.");
        _scale = Number(config, s, "Scale", 1, .01f, 100, "Uniform cosmetic scale; does not change collision.");
        _x = Number(config, s, "OffsetX", 0, -10, 10, "Local sideways offset in metres.");
        _y = Number(config, s, "OffsetY", 0, -10, 10, "Local upward offset in metres.");
        _z = Number(config, s, "OffsetZ", 0, -10, 10, "Local forward offset in metres.");
        _rx = Number(config, s, "RotationX", 0, -180, 180, "Local pitch in degrees.");
        _ry = Number(config, s, "RotationY", 0, -180, 180, "Local yaw in degrees.");
        _rz = Number(config, s, "RotationZ", 0, -180, 180, "Local roll in degrees.");
        _hide = config.Bind(s, "HideOriginalKart", false, "Hide original static kart meshes; keep the driver and physics intact.");
        _mirror = config.Bind(s, "MirrorX", true, "Convert OBJ right-handed coordinates to Unity; toggle if your exporter already converted them.");
        _tint = config.Bind(s, "Tint", "#FFFFFF", "Multiply model diffuse colour by this #RRGGBB colour.");
        _reload = config.Bind(s, "ReloadToken", 0, new ConfigDescription("Increase to reload edited model/material files during a local race.", new AcceptableValueRange<int>(0, int.MaxValue)));
    }
    private static ConfigEntry<float> Number(ConfigFile config, string section, string key, float value, float min, float max, string help) =>
        config.Bind(section, key, value, new ConfigDescription(help, new AcceptableValueRange<float>(min, max)));

    public void Tick()
    {
        if (Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet ||
            Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING)
        { Restore(); return; }
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 1;
            CheckModel();
            if (_template != null) FindPlayers();
        }
        Color tint = ParseTint(_tint.Value);
        foreach (Attachment attachment in _attachments.Values)
        {
            if (attachment.Object == null || attachment.Parent == null) continue;
            Transform transform = attachment.Object.transform;
            transform.localPosition = new Vector3(_x.Value, _y.Value, _z.Value);
            transform.localRotation = Quaternion.Euler(_rx.Value, _ry.Value, _rz.Value);
            transform.localScale = Vector3.one * _scale.Value;
            foreach (var item in attachment.Materials)
            {
                if (item.Material == null) continue;
                Color color = new(item.Color.r * tint.r, item.Color.g * tint.g, item.Color.b * tint.b, item.Color.a);
                if (item.Material.HasProperty("_BaseColor")) item.Material.SetColor("_BaseColor", color);
                if (item.Material.HasProperty("_Color")) item.Material.SetColor("_Color", color);
            }
            foreach (var item in attachment.Hidden) if (item.Renderer != null) item.Renderer.forceRenderingOff = _hide.Value || item.ForceRenderingOff;
        }
    }

    private void CheckModel()
    {
        string signature = _path.Value + "|" + _asset.Value + "|" + _mirror.Value + "|" + _reload.Value;
        string? path = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(_path.Value))
            {
                path = ModelPath.Resolve(Path.Combine(Paths.BepInExRootPath, "models"), _path.Value);
                signature += "|" + File.GetLastWriteTimeUtc(path).Ticks;
            }
            if (signature == _signature && (_template != null || _owned.Count == 0)) return;
            Clear(); _signature = signature;
            if (path == null) return;
            if (Path.GetExtension(path).Equals(".obj", StringComparison.OrdinalIgnoreCase))
            {
                Shader? shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Texture");
                if (shader == null) throw new InvalidDataException("No compatible diffuse shader is available; use a matching Unity AssetBundle.");
                var prototype = new Material(shader); _owned.Add(prototype);
                _template = ObjModel.Create(ObjModel.Parse(path, _mirror.Value), prototype, _owned);
            }
            else if (Path.GetExtension(path).ToLowerInvariant() is ".bundle" or ".unity3d" or ".assetbundle")
            {
                ModelPath.RequireSize(path, 256L * 1024 * 1024);
                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null) throw new InvalidDataException("Bundle could not load. Build for Windows x64 in Unity 6000.0.75f1.");
                GameObject? source = null;
                if (!string.IsNullOrWhiteSpace(_asset.Value)) source = _bundle.LoadAsset<GameObject>(_asset.Value);
                else foreach (string name in _bundle.GetAllAssetNames())
                { if (name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) { source = _bundle.LoadAsset<GameObject>(name); if (source != null) break; } }
                if (source == null) throw new InvalidDataException("Bundle contains no selected GameObject prefab.");
                _template = new GameObject("TK2 imported bundle"); _template.SetActive(false); _owned.Add(_template);
                int nodes = 0, meshes = 0, vertices = 0; long indices = 0;
                CopyStatic(source.transform, _template.transform, ref nodes, ref meshes, ref vertices, ref indices);
                if (meshes == 0) throw new InvalidDataException("Bundle prefab has no static meshes. Animated character replacement is not supported.");
            }
            else throw new InvalidDataException("Use Toolkit FBX import, OBJ, or a matching Unity model bundle.");
            Plugin.Instance!.Log.LogInfo($"Cosmetic model ready: {_path.Value}; static visual only.");
        }
        catch (Exception ex)
        {
            Clear(); _signature = signature;
            Plugin.Instance!.Log.LogWarning($"Cosmetic model: {ex.Message}. Change selection or ReloadToken to retry.");
        }
    }

    private void FindPlayers()
    {
        var stale = new List<int>();
        foreach (var pair in _attachments) if (pair.Value.Parent == null || pair.Value.Object == null) stale.Add(pair.Key);
        foreach (int key in stale) { RestoreAttachment(_attachments[key]); _attachments.Remove(key); }
        foreach (Ant_Player player in UnityEngine.Object.FindObjectsOfType<Ant_Player>())
        {
            if (player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) continue;
            var visual = player.visualInstanceSyncedParams?.antVisualKart;
            if (visual == null || !visual.IsKartLoaded) continue;
            Transform parent = visual.GetVisualKartRoot(); if (parent == null) continue;
            int id = player.GetInstanceID();
            if (_attachments.TryGetValue(id, out Attachment? existing))
            {
                if (existing.Parent == parent) continue;
                RestoreAttachment(existing); _attachments.Remove(id);
            }
            Transform? character = visual.GetCharacterSocket();
            var attachment = new Attachment { Parent = parent, Object = UnityEngine.Object.Instantiate(_template!) };
            attachment.Object.name = "TK2 cosmetic shell"; attachment.Object.transform.SetParent(parent, false);
            foreach (MeshRenderer renderer in parent.GetComponentsInChildren<MeshRenderer>(true))
                if (!renderer.transform.IsChildOf(attachment.Object.transform) && (character == null || !renderer.transform.IsChildOf(character)))
                    attachment.Hidden.Add((renderer, renderer.forceRenderingOff));
            foreach (MeshRenderer renderer in attachment.Object.GetComponentsInChildren<MeshRenderer>(true))
                foreach (Material material in renderer.materials)
                {
                    _owned.Add(material);
                    Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    attachment.Materials.Add((material, color));
                }
            _attachments.Add(id, attachment); attachment.Object.SetActive(true);
        }
    }

    private static void CopyStatic(Transform source, Transform target, ref int nodes, ref int meshes, ref int vertices, ref long indices)
    {
        if (++nodes > 512) throw new InvalidDataException("Bundle hierarchy exceeds 512 nodes.");
        // Read prefab data only; never instantiate imported scripts, colliders, lights or cameras.
        var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
        if (filter != null && filter.sharedMesh != null && renderer != null)
        {
            if (++meshes > 64 || filter.sharedMesh.vertexCount > 250000) throw new InvalidDataException("Bundle static mesh limit exceeded.");
            vertices += filter.sharedMesh.vertexCount;
            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++) indices += filter.sharedMesh.GetIndexCount(submesh);
            if (vertices > 1500000 || indices > 1500000) throw new InvalidDataException("Bundle exceeds the total 1.5 million vertices/indices limit.");
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            target.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        for (int i = 0; i < source.childCount; i++)
        {
            Transform child = source.GetChild(i); var copy = new GameObject(child.name).transform;
            copy.SetParent(target, false); copy.localPosition = child.localPosition; copy.localRotation = child.localRotation; copy.localScale = child.localScale;
            CopyStatic(child, copy, ref nodes, ref meshes, ref vertices, ref indices); copy.gameObject.SetActive(child.gameObject.activeSelf);
        }
    }
    private static Color ParseTint(string text)
    {
        string value = text.Trim().TrimStart('#');
        if (value.Length == 6 && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint rgb))
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
        return Color.white;
    }
    private void RestoreAttachment(Attachment attachment)
    {
        foreach (var item in attachment.Hidden) if (item.Renderer != null) item.Renderer.forceRenderingOff = item.ForceRenderingOff;
        if (attachment.Object != null) { attachment.Object.SetActive(false); UnityEngine.Object.Destroy(attachment.Object); }
        foreach (var item in attachment.Materials)
        {
            if (item.Material is not null) _owned.Remove(item.Material);
            if (item.Material != null) UnityEngine.Object.Destroy(item.Material);
        }
    }
    private void Clear()
    {
        foreach (Attachment attachment in _attachments.Values) RestoreAttachment(attachment);
        _attachments.Clear();
        foreach (UnityEngine.Object value in _owned) if (value != null) UnityEngine.Object.Destroy(value);
        _owned.Clear(); _template = null;
        if (_bundle != null) { _bundle.Unload(true); _bundle = null; }
    }
    public void Restore() { if (_signature.Length != 0 || _owned.Count != 0 || _attachments.Count != 0 || _bundle != null) Clear(); _signature = ""; _nextScan = 0; }
}

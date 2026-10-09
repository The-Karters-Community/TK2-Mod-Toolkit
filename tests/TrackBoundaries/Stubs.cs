using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T> { public T Value; public ConfigEntry(T value) => Value = value; }
    public sealed class ConfigDescription { public ConfigDescription(string text, object range) { } }
    public sealed class AcceptableValueRange<T> { public AcceptableValueRange(T min, T max) { } }
    public sealed class ConfigFile
    {
        readonly Dictionary<string, object> entries = new();
        public ConfigEntry<T> Bind<T>(string section, string name, T value, object description)
        { var entry = new ConfigEntry<T>(value); entries[section + "/" + name] = entry; return entry; }
        public ConfigEntry<T> Entry<T>(string name) => (ConfigEntry<T>)entries["TrackBoundaries/" + name];
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppReferenceArray<T> : IEnumerable<T>
    {
        readonly T[] items;
        public Il2CppReferenceArray(T[] values) => items = values.ToArray();
        public int Length => items.Length;
        public T this[int index] => items[index];
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public static implicit operator Il2CppReferenceArray<T>(T[] values) => new(values);
    }
}
namespace UnityEngine
{
    public class Object
    {
        static int next; public readonly int Id = ++next; public bool Destroyed;
        public static readonly List<Object> Registry = new();
        public Object() => Registry.Add(this);
        public int GetInstanceID() => Id;
        public static readonly Dictionary<Type, int> Finds = new();
        public static T[] FindObjectsOfType<T>() where T : Object
        { Finds[typeof(T)] = Finds.GetValueOrDefault(typeof(T)) + 1; return Registry.OfType<T>().Where(x => !x.Destroyed && (x is not Component c || c.gameObject.activeInHierarchy)).ToArray(); }
        public static void Destroy(Object obj)
        {
            obj.Destroyed = true;
            if (obj is GameObject go)
            {
                go.activeInHierarchy = false;
                foreach (var component in go.Components) component.Destroyed = true;
            }
        }
        public static bool operator ==(Object? a, Object? b)
        { bool an = ReferenceEquals(a, null) || a.Destroyed, bn = ReferenceEquals(b, null) || b.Destroyed; return an || bn ? an == bn : ReferenceEquals(a, b); }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => Id;
    }
    public sealed class GameObject : Object
    {
        public readonly List<Component> Components = new(); public readonly Transform transform;
        public bool activeInHierarchy = true; public int layer; public string name; public HideFlags hideFlags; public SceneManagement.Scene scene = new(1);
        public GameObject() : this("") { }
        public GameObject(string name) { this.name = name; transform = new Transform(this); Components.Add(transform); }
        public T Add<T>() where T : Component, new() { var value = new T { gameObject = this }; Components.Add(value); return value; }
        public T AddComponent<T>() where T : Component, new() => Add<T>();
        public void SetActive(bool active) => activeInHierarchy = active;
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault()!;
    }
    public class Component : Object
    {
        public GameObject gameObject = null!; public Transform transform => gameObject.transform;
        public virtual T? TryCast<T>() where T : Component => this as T;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T? GetComponentInParent<T>() where T : Component
        { for (Transform? t = transform; t != null; t = t.parent) if (t.gameObject.GetComponent<T>() is T value) return value; return null; }
        public T[] GetComponentsInChildren<T>() where T : Component => Registry.OfType<T>().Where(c => !c.Destroyed && c.gameObject.activeInHierarchy && IsChild(c.transform, transform)).ToArray();
        static bool IsChild(Transform candidate, Transform root) { for (Transform? t = candidate; t != null; t = t.parent) if (t == root) return true; return false; }
    }
    public class MonoBehaviour : Component { }
    public sealed class Transform : Component
    {
        public Transform? parent; public Vector3 localPosition, localScale = Vector3.one; public Quaternion localRotation = Quaternion.identity;
        public Vector3 position { get => parent == null ? localPosition : parent.TransformPoint(localPosition); set => localPosition = value; }
        public Quaternion rotation { get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = value; }
        public Vector3 lossyScale => parent == null ? localScale : Vector3.Product(parent.lossyScale, localScale);
        public Matrix4x4 localToWorldMatrix => new();
        public Transform(GameObject owner) => gameObject = owner;
        public void SetParent(Transform parent, bool worldPositionStays) => this.parent = parent;
        public Vector3 TransformPoint(Vector3 p) => position + rotation.Rotate(Vector3.Product(lossyScale, p));
    }
    public struct Vector3
    {
        public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0, 0, 0); public static Vector3 one => new(1, 1, 1);
        public static Vector3 operator *(Vector3 v, float f) => new(v.x*f, v.y*f, v.z*f);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x, a.y+b.y, a.z+b.z);
        public static Vector3 Product(Vector3 a, Vector3 b) => new(a.x*b.x, a.y*b.y, a.z*b.z);
    }
    public struct Quaternion
    {
        public System.Numerics.Quaternion Value;
        public static Quaternion identity => new() { Value = System.Numerics.Quaternion.Identity };
        public static Quaternion Euler(float x, float y, float z) => new() { Value = System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180, x*MathF.PI/180, z*MathF.PI/180) };
        public static Quaternion operator *(Quaternion a, Quaternion b) => new() { Value = a.Value*b.Value };
        public Vector3 Rotate(Vector3 p) { var v = System.Numerics.Vector3.Transform(new(p.x,p.y,p.z), Value); return new(v.X,v.Y,v.Z); }
    }
    public struct Bounds
    { public Vector3 center; public float SqrDistance(Vector3 p) => (center.x - p.x) * (center.x - p.x) + (center.y - p.y) * (center.y - p.y) + (center.z - p.z) * (center.z - p.z); }
    public class Collider : Component
    {
        public Collider? NativeSubtype; private bool _enabled = true; private Bounds _bounds;
        public bool enabled { get => NativeSubtype?.enabled ?? _enabled; set { if (NativeSubtype != null) NativeSubtype.enabled = value; else _enabled = value; } }
        public Bounds bounds { get => NativeSubtype?.bounds ?? _bounds; set { if (NativeSubtype != null) NativeSubtype.bounds = value; else _bounds = value; } }
        public override T? TryCast<T>() where T : class => NativeSubtype as T ?? this as T;
    }
    public sealed class MeshCollider : Collider { public Mesh? sharedMesh; }
    public sealed class BoxCollider : Collider { public Vector3 center, size = Vector3.one; }
    public sealed class SphereCollider : Collider { public Vector3 center; public float radius = .5f; }
    public sealed class CapsuleCollider : Collider { public Vector3 center; public float radius = .5f, height = 2; public int direction = 1; }
    public sealed class Mesh : Object
    {
        public string name = ""; public HideFlags hideFlags; public bool ThrowOnVertexRead; public int subMeshCount = 1;
        private Vector3[] _vertices = Array.Empty<Vector3>();
        public Vector3[] vertices { get => ThrowOnVertexRead ? throw new Exception("Native unreadable mesh vertex access") : _vertices; set => _vertices = value; }
        public int[] triangles = Array.Empty<int>(); public void RecalculateBounds() { }
    }
    public sealed class MeshFilter : Component { public Mesh? sharedMesh; }
    public sealed class MeshRenderer : Component
    {
        public bool enabled, forceRenderingOff, receiveShadows = true; public Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On;
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material> sharedMaterials = new(Array.Empty<Material>());
        public Material? sharedMaterial { get => sharedMaterials.Length == 0 ? null : sharedMaterials[0]; set => sharedMaterials = value == null ? Array.Empty<Material>() : new[] { value }; }
    }
    public struct LayerMask { public int value; }
    public sealed class Shader : Object { public bool Colored = true; public static Shader? Internal; public static Shader? Find(string name) => Internal; }
    public enum HideFlags { DontSave }
    public sealed class Material : Object
    {
        public Shader shader; public HideFlags hideFlags; public int renderQueue; public Color Color;
        public Material(Shader shader) => this.shader = shader;
        public Material(Material source) => shader = source.shader;
        public bool HasProperty(string property) => shader.Colored;
        public void SetColor(string property, Color color) => Color = color;
        public readonly Dictionary<string, int> Ints = new();
        public void SetInt(string property, int value) => Ints[property] = value;
        public bool BindPass = true;
        public bool SetPass(int index) => BindPass;
    }
    public sealed class Camera : Component
    { public int cullingMask; public CameraType cameraType = CameraType.Game; public static Camera? current; public static Camera[] allCameras => FindObjectsOfType<Camera>(); }
    public enum CameraType { Game, Reflection, Preview }
    public struct Matrix4x4 { }
    public static class GL { public static bool wireframe; }
    public static class Graphics
    {
        public static int Draws; public static bool ThrowOnDraw; public static readonly List<int> Submeshes = new();
        public static void DrawMeshNow(Mesh mesh, Matrix4x4 matrix, int submesh)
        {
            if (!GL.wireframe) throw new Exception("Filled triangles were drawn");
            if (ThrowOnDraw) throw new Exception("Injected draw failure");
            Draws++; Submeshes.Add(submesh);
        }
    }
    public enum KeyCode { None, F10 }
    public static class Input { public static bool Pressed; public static bool GetKeyDown(KeyCode key) { bool p = Pressed; Pressed = false; return p; } }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool isFocused = true; }
    public static class Screen { public static int height = 1080; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => new(1, 1, 1, 1); }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public static class GUI { public static Color color; public static void Box(Rect rect, string text) { } public static void Label(Rect rect, string text) { } }
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On }
    public enum BlendMode { SrcAlpha, OneMinusSrcAlpha }
    public enum CompareFunction { LessEqual, Always }
    public enum CullMode { Off }
}
namespace UnityEngine.SceneManagement
{
    public readonly struct Scene { public readonly int handle; public Scene(int handle) => this.handle = handle; }
    public static class SceneManager { public static Scene Active = new(1); public static Scene GetActiveScene() => Active; }
}
public sealed class MenuManager : UnityEngine.MonoBehaviour { public static MenuManager? Instance; }
public sealed class Ant_MapData : UnityEngine.MonoBehaviour { public static Ant_MapData? instance; }
public sealed class Ant_Player : UnityEngine.MonoBehaviour { public enum EPlayerType { E_HUMAN_LOCAL, E_AI_LOCAL } public EPlayerType ePlayerType; }
public sealed class PixelEasyCharMoveKartController : UnityEngine.MonoBehaviour
{ public Ant_Player? parentPlayer; public UnityEngine.LayerMask wallLayerToCollDetect, wallRespawn_Flat_ColliderLayer, wallRespawn_Always_ColliderLayer; }
public sealed class PTK_HideMeshRendererInPlayMode : UnityEngine.MonoBehaviour { public bool bEnabled = true, bIncludeChildrenMeshRenderers = true; }
public static class Ant_CurrentGameConfiguration { public enum ERaceState { E_RACE_RUNNING, E_RACE_FINISHED } public static ERaceState eCurrentRaceState; }
namespace TK2.Customization
{
    public sealed class LogStub { public readonly List<string> Warnings = new(), Infos = new(); public void LogWarning(string value) => Warnings.Add(value); public void LogInfo(string value) => Infos.Add(value); }
    internal sealed class Plugin
    { public static Plugin? Instance; public static bool OfflineLabAllowed = true; public bool GameplayReady = true; public readonly BepInEx.Configuration.ConfigFile Config = new(); public readonly LogStub Log = new(); }
}

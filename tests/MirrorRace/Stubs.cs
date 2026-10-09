using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T> { public T Value; public ConfigEntry(T value) => Value = value; }
    public sealed class ConfigFile
    {
        public ConfigEntry<bool> Enabled = new(false);
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string text) => (ConfigEntry<T>)(object)Enabled;
    }
}
namespace HarmonyLib
{
    public sealed class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public sealed class Harmony { public void Patch(MethodInfo method, HarmonyMethod prefix) { } }
    public static class AccessTools { public static MethodInfo? DeclaredMethod(Type type, string name, Type[] args) => type.GetMethod(name, args); }
}
namespace Il2CppInterop.Runtime.Injection
{ public static class ClassInjector { public static void RegisterTypeInIl2Cpp<T>() { } } }
namespace UnityEngine
{
    public class Object
    {
        static int next; public int Id = ++next; public bool Destroyed;
        public static int Scans;
        public static readonly List<Object> All = new();
        public Object() => All.Add(this);
        public int GetInstanceID() => Id;
        public static T[] FindObjectsOfType<T>() where T : Object { Scans++; return All.OfType<T>().Where(o => !o.Destroyed).ToArray(); }
        public static void Destroy(Object value) => value.Destroyed = true;
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a?.Destroyed == true ? null : a, b?.Destroyed == true ? null : b);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => Id;
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component
    { public MonoBehaviour() { } public MonoBehaviour(IntPtr pointer) { } public bool enabled = true; }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        readonly List<Component> components = new();
        public T AddComponent<T>() where T : Component
        {
            var value = typeof(T).GetConstructor(new[] { typeof(IntPtr) }) != null ?
                (T)Activator.CreateInstance(typeof(T), IntPtr.Zero)! : (T)Activator.CreateInstance(typeof(T))!;
            value.gameObject = this; components.Add(value);
            typeof(T).GetMethod("Awake")?.Invoke(value, null);
            return value;
        }
        public T? GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault(v => !v.Destroyed);
    }
    public class Camera : MonoBehaviour { public bool isActiveAndEnabled => enabled && !Destroyed && gameObject.activeInHierarchy; }
    public class RenderTexture : Object { }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } }
    public static class Graphics
    {
        public static int Flips, Copies;
        public static void Blit(RenderTexture source, RenderTexture destination) => Copies++;
        public static void Blit(RenderTexture source, RenderTexture destination, Vector2 scale, Vector2 offset)
        { if (scale.x != -1 || scale.y != 1 || offset.x != 1 || offset.y != 0) throw new Exception("Bad mirror mapping"); Flips++; }
    }
    public static class Time { public static float unscaledTime; }
}
namespace TK2.Customization
{
    public interface IModRecipe { string Name { get; } bool ChangesGameplay { get; } void Configure(BepInEx.Configuration.ConfigFile config); void Tick(); void Restore(); }
    public class Plugin
    {
        public static Plugin? Instance = new(); public static bool OfflineLabAllowed = true;
        public HarmonyLib.Harmony Harmony = new(); public Logger Log = new();
    }
    public class Logger { public readonly List<string> Lines = new(); public void LogInfo(string text) => Lines.Add(text); public void LogWarning(string text) => Lines.Add(text); }
}
public class MenuManager : UnityEngine.Object { public static MenuManager? Instance; }
public class Ant_MapData : UnityEngine.Object { public static Ant_MapData? instance; }
public class Ant_Player : UnityEngine.Component
{
    public enum EPlayerType { E_HUMAN_LOCAL, E_AI_LOCAL, E_HUMAN_ONLINE }
    public EPlayerType ePlayerType; public CameraBrain? gameplayCamera;
}
public class CameraBrain { public bool bIsSpectatorCamera; public CameraData? Data; public CameraData? GetCamera() => Data; }
public class CameraData { public UnityEngine.Camera? unityCamera; }
public class PixelEasyCharMoveKartController : UnityEngine.Component { public Ant_Player? parentPlayer; public void SteerInput(float amount) { } }

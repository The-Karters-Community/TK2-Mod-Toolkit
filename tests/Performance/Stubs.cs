using System;
using System.Collections.Generic;
using System.Reflection;

namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T> { public T Value { get; set; } public ConfigEntry(T value) { Value = value; } }
    public sealed class ConfigDescription { public ConfigDescription(string text, object range) { } }
    public sealed class AcceptableValueRange<T> { public AcceptableValueRange(T min, T max) { } }
    public sealed class ConfigFile
    {
        public readonly Dictionary<string, object> Entries = new();
        public ConfigEntry<T> Bind<T>(string section, string name, T value, object description)
        {
            var entry = new ConfigEntry<T>(value);
            Entries[section + "/" + name] = entry;
            return entry;
        }
        public ConfigEntry<T> Entry<T>(string name) => (ConfigEntry<T>)Entries["Performance/" + name];
        public ConfigEntry<T> Entry<T>(string section, string name) => (ConfigEntry<T>)Entries[section + "/" + name];
    }
}
namespace BepInEx
{
    public static class Paths { public static string BepInExRootPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tk2-diagnostic-test-" + Guid.NewGuid().ToString("N")); }
}
namespace HarmonyLib
{
    public static class AccessTools { public static MethodInfo? DeclaredMethod(Type type, string name, Type[] args) => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, args, null); }
    public sealed class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public sealed class Harmony
    {
        public Harmony(string? owner = null) { }
        public readonly List<MethodInfo> Targets = new();
        public int FailAt;
        public void Patch(MethodInfo target, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
        { if (FailAt == Targets.Count + 1) throw new InvalidOperationException("patch failed"); Targets.Add(target); }
        public void UnpatchSelf() => Targets.Clear();
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppReferenceArray<T> where T : class
    {
        private readonly T?[] _items;
        public int Length => _items.Length;
        public Il2CppReferenceArray(int count) { _items = new T?[count]; }
        public T? this[int i] { get => _items[i]; set => _items[i] = value; }
    }
    public sealed class Il2CppStructArray<T>
    {
        private readonly T[] _items;
        public int Length => _items.Length;
        public Il2CppStructArray(int count) { _items = new T[count]; }
        public T this[int i] { get => _items[i]; set => _items[i] = value; }
    }
}
namespace UnityEngine
{
    public class Object
    {
        private static int _next;
        public int Id = ++_next;
        public bool Destroyed;
        public IntPtr Pointer => new(Id);
        public int GetInstanceID() => Id;
        public static bool operator ==(Object? a, Object? b)
        {
            bool an = ReferenceEquals(a, null) || a.Destroyed, bn = ReferenceEquals(b, null) || b.Destroyed;
            return an || bn ? an == bn : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => Id;
    }
    public class MonoBehaviour : Object { }
    public sealed class Camera : Object
    {
        public static readonly List<Camera> Cameras = new();
        public static int allCamerasCount => Cameras.Count;
        public static bool ThrowOnGet;
        public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Camera>? LastBuffer;
        public int SetterCalls;
        private Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>? _distances;
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>? layerCullDistances
        { get => _distances; set { SetterCalls++; _distances = value; } }
        public static int GetAllCameras(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Camera> buffer)
        {
            if (ThrowOnGet) throw new InvalidOperationException("camera failed");
            LastBuffer = buffer;
            for (int i = 0; i < Cameras.Count; i++) buffer[i] = Cameras[i];
            return Cameras.Count;
        }
    }
    public static class Time { public static float unscaledTime, timeScale = 1, fixedDeltaTime = .00833f; }
    public static class Application { public static bool isFocused = true; }
}
namespace UnityEngine.Profiling
{
    public static class Profiler
    {
        public static long GetTotalAllocatedMemoryLong() => 100;
        public static long GetTotalReservedMemoryLong() => 200;
        public static long GetMonoUsedSizeLong() => 300;
        public static long GetMonoHeapSizeLong() => 400;
    }
}
namespace UnityEngine.Scripting
{
    public static class GarbageCollector
    {
        public enum Mode { Disabled, Enabled, Manual }
        public static Mode GCMode { get; set; } = Mode.Enabled;
    }
}
namespace KinematicCharacterController
{
    public sealed class KinematicCharacterSystem { public void FixedUpdate() { } }
    public sealed class KinematicCharacterMotor : UnityEngine.MonoBehaviour
    {
        public bool ThrowQualitySet, ThrowIntervalSet;
        private bool _quality;
        private int _interval = 1;
        public bool bLowerPhysicsQualityForAI
        { get => _quality; set { if (ThrowQualitySet) throw new InvalidOperationException("motor quality unavailable"); _quality = value; } }
        public int iLowerQualityPhysicsUpdateEveryOnlyX
        { get => _interval; set { if (ThrowIntervalSet) throw new InvalidOperationException("motor cadence unavailable"); _interval = value; } }
    }
}
public sealed class MenuManager : UnityEngine.MonoBehaviour { public static MenuManager? Instance; }
public sealed class Ant_Player : UnityEngine.MonoBehaviour
{
    public enum EPlayerType { E_HUMAN_LOCAL, E_NETWORK, E_AI_LOCAL, E_DISABLED, E_GHOST_TIME_TRIAL }
    public EPlayerType ePlayerType;
}
public sealed class PixelEasyCharMoveKartController : UnityEngine.MonoBehaviour
{
    public Ant_Player? parentPlayer;
    public KinematicCharacterController.KinematicCharacterMotor? Motor;
    public bool ThrowIntervalSet;
    private int _interval = 1;
    public int iAILowerQualityPhysicsUpdateEveryOnlyX
    { get => _interval; set { if (ThrowIntervalSet) throw new InvalidOperationException("controller cadence unavailable"); _interval = value; } }
    public void FixedUpdate() { }
    public void Update() { }
    public void UpdatePlayerPlayerCollision() { }
}
public sealed class AILogicController { public void Update() { } }
public sealed class AIWeaponLogicController { public void Update() { } }
public sealed class AIDistToTargetBehavController { public void Update() { } }
public sealed class PlayerBezierFollower { public void Update() { } }
public sealed class PixelKartPhysics { public void Update() { } }
public sealed class MapConfig { public int iConfigID = 1; }
public sealed class ModeConfig { public bool bDisablePlayerPlayerCollisionForTT; }
public static class Ant_CurrentGameConfiguration
{
    public static string eGameModeType = "Race";
    public static MapConfig? GetFinalChoosedMapConfig(int id) => new();
    public static ModeConfig? GetFinalChoosedGameModeConfig(int id) => new();
    public enum ERaceState { E_RACE_NOT_STARTED, E_RACE_RUNNING, E_RACE_FINISHED }
    public static ERaceState eCurrentRaceState;
    public static bool bLowerPhysicsQualityOnAIVehicles;
    public static int PolicyCalls;
    public static Action? Policy;
    public static void PTK_GC_DisableForRace() { UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Disabled; }
    public static void PTK_GC_ApplyCurrentPolicy() { PolicyCalls++; Policy?.Invoke(); }
}
public sealed class PTK_GraphicsDetailApplier : UnityEngine.MonoBehaviour
{
    public static int Enviro, Shadow, Draw, Terrain;
    public static int EffEnviro() => Enviro;
    public static int EffectiveShadowDetailIndex => Shadow;
    public static int EffDraw() => Draw;
    public static int EffectiveTerrainQualityIndex => Terrain;
    public bool _isSuppressedForMainMenu;
    public int _appliedEnviro = -999, _appliedShadow = -999, _appliedTerrainQuality = -999, _appliedDraw;
    public bool ThrowApply;
    public readonly List<string> Calls = new();
    public void LateUpdate() { }
    public void ApplyTerrainQuality(int value) { Calls.Add("terrain:" + value); if (ThrowApply) throw new InvalidOperationException("native apply failed"); }
    public void ApplyEnviro(int value) { Calls.Add("enviro:" + value); }
    public void ApplyShadow(int value) { Calls.Add("shadow:" + value); }
    public void RestoreGameTerrain() { Calls.Add("restore-terrain"); }
    public void RestoreTerrainQualityOverrides() { Calls.Add("restore-quality"); }
}
namespace TK2.Customization
{
    public sealed class LogStub
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public void LogWarning(string message) => Warnings.Add(message);
        public void LogInfo(string message) { }
        public void LogError(string message) => Errors.Add(message);
    }
    internal sealed class Plugin
    {
        public static Plugin? Instance;
        public static bool OfflineLabAllowed = true;
        public bool GameplayReady = true, SessionModified;
        public readonly BepInEx.Configuration.ConfigFile Config = new();
        public readonly HarmonyLib.Harmony Harmony = new();
        public readonly LogStub Log = new();
    }
}

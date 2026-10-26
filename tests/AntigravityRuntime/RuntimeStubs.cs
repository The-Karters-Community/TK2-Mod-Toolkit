using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    using UnityEngine.SceneManagement;
    internal sealed class Transform
    {
        internal Vector3 position;
        internal Quaternion rotation = new(0, 0, 0, 1);
        internal Vector3 forward => rotation * Vector3.forward;
    }
    internal readonly struct Rect { internal Rect(float x, float y, float width, float height) { } }
    internal static class Screen { internal static int height = 1080; }
    internal static class GUI { internal static void Label(Rect rect, string text) { } }

    internal sealed class GameObject
    {
        internal readonly Transform transform = new();
        internal int layer;
        internal Scene scene = new(1);
    }

    internal readonly struct LayerMask
    {
        internal readonly int value;
        internal LayerMask(int value) => this.value = value;
        public static implicit operator LayerMask(int value) => new(value);
    }

    internal sealed class Rigidbody { internal bool isKinematic; }

    internal sealed class Collider
    {
        internal GameObject gameObject = new();
        internal bool isTrigger;
        internal Rigidbody attachedRigidbody = null!;
        internal bool PlayerOwned;
        internal T? GetComponentInParent<T>() where T : class => PlayerOwned ? new PixelEasyCharMoveKartController() as T : null;
    }

    internal readonly struct RaycastHit
    {
        internal readonly Collider collider;
        internal readonly Vector3 normal;
        internal readonly float distance;
        internal RaycastHit(Collider collider, Vector3 normal, float distance)
        { this.collider = collider; this.normal = normal; this.distance = distance; }
    }

    internal enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    internal static class Physics
    {
        internal static Func<Vector3, Vector3, float, LayerMask, QueryTriggerInteraction, (bool Hit, RaycastHit Result)>? RaycastHandler;
        internal static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance,
            LayerMask layerMask, QueryTriggerInteraction query)
        {
            var result = RaycastHandler?.Invoke(origin, direction, maxDistance, layerMask, query) ?? (false, default);
            hit = result.Result;
            return result.Hit;
        }
    }

    internal static class Time { internal static float fixedDeltaTime = 1f / 60f, unscaledTime = 0f; }
}

namespace UnityEngine.SceneManagement
{
    internal readonly struct Scene
    {
        internal readonly int handle;
        internal Scene(int handle) => this.handle = handle;
    }
    internal enum LoadSceneMode { Single, Additive }
    internal static class SceneManager
    {
        internal static event Action<Scene, LoadSceneMode>? sceneLoaded;
        internal static Scene ActiveScene = new(1);
        internal static Scene GetActiveScene() => ActiveScene;
        internal static void RaiseSceneLoaded(Scene scene, LoadSceneMode mode) => sceneLoaded?.Invoke(scene, mode);
    }
}

namespace KinematicCharacterController
{
    using UnityEngine;
    internal sealed class GroundingStatus
    {
        internal bool FoundAnyGround, IsStableOnGround;
        internal Collider GroundCollider = null!;
        internal Vector3 GroundNormal = Vector3.up;
    }

    internal sealed class KinematicCharacterMotor
    {
        private GroundingStatus _groundingStatus = new();
        internal bool ThrowOnGroundingRead;
        internal GroundingStatus GroundingStatus { get { if (ThrowOnGroundingRead) throw new Exception("grounding access fault"); return _groundingStatus; } set => _groundingStatus = value; }
        internal LayerMask StableGroundLayers = 1 << 8;
        internal Vector3 Velocity, BaseVelocity, CharacterTransformToCapsuleBottom = Vector3.zero;
        internal Quaternion TransientRotation { get; set; } = new(0, 0, 0, 1);
        // In KCC CharacterUp follows the accepted transient frame, not an independent test value.
        internal Vector3 CharacterUp => TransientRotation * Vector3.up;
        internal bool MustUngroundValue;
        internal Transform Transform = new();
        internal bool MustUnground() => MustUngroundValue;
        internal void SetRotation(Quaternion rotation, bool bypassInterpolation)
        {
            Transform.rotation = rotation;
            TransientRotation = rotation;
        }
        internal void SimulateCommit() => Transform.rotation = TransientRotation;
    }
}

namespace BepInEx.Configuration
{
    internal sealed class ConfigEntry<T> { internal T Value = default!; }
    internal sealed class ConfigFile
    {
        internal ConfigEntry<T> Bind<T>(string section, string key, T value, string description) => new() { Value = value };
    }
}

namespace TK2.Customization
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using KinematicCharacterController;

    internal sealed class TestConfig<T> { internal T Value = default!; }
    internal sealed class TestLogger
    {
        internal readonly List<string> Errors = new();
        internal readonly List<string> Warnings = new();
        internal void LogError(string message) => Errors.Add(message);
        internal void LogWarning(string message) => Warnings.Add(message);
    }
    internal sealed class Plugin
    {
        internal static Plugin? Instance;
        internal static bool OfflineLabAllowed = true;
        internal TestConfig<bool> AntigravityEnabled = new();
        internal BepInEx.Configuration.ConfigFile Config = new();
        internal bool SessionModified;
        internal object Harmony = new();
        internal TestLogger Log = new();
    }
    internal static class AirShiftFeature
    {
        internal enum RunMode { Live, Paused, Respawning, Replay }
        internal static bool CanRun = true;
        internal static RunMode Mode = RunMode.Live;
        internal static int Calls;
        internal static bool Running(PixelEasyCharMoveKartController? controller)
        {
            Calls++;
            return Plugin.OfflineLabAllowed && CanRun && Mode == RunMode.Live;
        }
    }
    internal sealed class FeatureHook
    {
        internal readonly Type Target;
        internal readonly string Name;
        internal readonly Type[] Parameters;
        internal readonly string? Prefix, Postfix, Finalizer;
        internal FeatureHook(Type target, string name, Type[] parameters, string? Prefix = null, string? Postfix = null, string? Finalizer = null)
        { Target = target; Name = name; Parameters = parameters; this.Prefix = Prefix; this.Postfix = Postfix; this.Finalizer = Finalizer; }
        internal static void Install(object harmony, Type handlers, FeatureHook[] hooks) { }
    }
}

internal sealed class Ant_Player
{
    internal enum EPlayerType { E_HUMAN_LOCAL, E_AI }
    internal EPlayerType ePlayerType;
}

internal static class Ant_CurrentGameConfiguration
{
    internal static bool IsOnlineGame_InRoom_WithInternet;
}

internal sealed class MenuManager { internal static MenuManager? Instance = null; }

internal sealed class PixelEasyCharMoveKartController
{
    private static int _nextId;
    internal int InstanceId = ++_nextId;
    internal int GetInstanceIDCalls;
    internal int NativePropertyReads;
    private PixelKartPhysics? _kartPhysics;
    private KinematicCharacterController.KinematicCharacterMotor? _motor;
    internal PixelKartPhysics kartPhysics { get { NativePropertyReads++; return _kartPhysics!; } set => _kartPhysics = value; }
    internal KinematicCharacterController.KinematicCharacterMotor Motor { get { NativePropertyReads++; return _motor!; } set => _motor = value; }
    internal Ant_Player? parentPlayer = new() { ePlayerType = Ant_Player.EPlayerType.E_HUMAN_LOCAL };
    internal UnityEngine.LayerMask wallLayerToCollDetect, wallRespawn_Flat_ColliderLayer, wallRespawn_Always_ColliderLayer;
    internal UnityEngine.GameObject gameObject = new();
    internal UnityEngine.Transform transform => gameObject.transform;
    internal UnityEngine.SceneManagement.Scene Scene => gameObject.scene;
    internal int GetInstanceID() { GetInstanceIDCalls++; return InstanceId; }
    internal void KinematicCharacterController_ICharacterController_AfterCharacterUpdate(float deltaTime) { }
    internal void KinematicCharacterController_ICharacterController_UpdateRotation(ref UnityEngine.Quaternion rotation, float deltaTime) { }
}

internal sealed class PixelKartPhysics
{
    internal PixelEasyCharMoveKartController? kartController;
    internal UnityEngine.Quaternion qSteeringOrientation = new(0, 0, 0, 1);
    internal bool bAlignToGround = true;
    internal bool bIsDrifting = false, bCatapultUsedLowerLateralFriction = false;
    internal bool bUseAccelerationVectorAsMidForwardMidVelocity = false, bIsMovingBackward = false;
    internal UnityEngine.Vector3 vForward_SteeringChar_NotAligned = UnityEngine.Vector3.forward;
    internal UnityEngine.Vector3 vRight_SteeringChar_NotAligned = UnityEngine.Vector3.right;
    internal UnityEngine.Vector3 vForward_SteeringChar_AlignedToSurfacce_Normalized = UnityEngine.Vector3.forward;
    internal UnityEngine.Vector3 vRight_SteeringChar_AlignedToSurfacce_Normalized = UnityEngine.Vector3.right;
    internal UnityEngine.Vector3 vUP_CharacterMotorAligned = UnityEngine.Vector3.up;
    internal UnityEngine.Vector3 vAccelerationForwardDirection = UnityEngine.Vector3.forward;
    internal bool ThrowOnNativeAccess;
    internal bool IsTeleportingFromAtoB() { if (ThrowOnNativeAccess) throw new Exception("native physics accessed"); return false; }
    internal void UpdateSteeringCentiperalForceAndOrient(UnityEngine.Vector3 acceleration,
        ref UnityEngine.Vector3 velocity, ref UnityEngine.Vector3 direction, ref float speed,
        float a, float b, float c, float d) { }
    internal void CalculatePhysicsAxisDirections(ref UnityEngine.Vector3 forward, float angle) { }
    internal void UpdateGravityWhenNotGrounded(ref UnityEngine.Vector3 acceleration, ref UnityEngine.Vector3 direction, float deltaTime) { }
    internal void TriggerPhysicsJump(ref UnityEngine.Vector3 velocity, float jumpSpeed) { }
    internal void UpdateSlopeVelocity(ref UnityEngine.Vector3 velocity, ref UnityEngine.Vector3 direction, ref float speed, float deltaTime) { }
    internal void LimitVerticalVelocity(ref UnityEngine.Vector3 velocity) { }
}

internal sealed class PlayerRaceLogic { internal void OnRaceResetted() { } }

internal sealed class Fixture
{
    internal TK2.Customization.Plugin Plugin = null!;
    internal PixelEasyCharMoveKartController Kart = null!;
    internal PixelKartPhysics Physics = null!;
    internal KinematicCharacterController.KinematicCharacterMotor Motor = null!;
    internal UnityEngine.Collider Road = null!;
}

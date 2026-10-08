using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

/// <summary>
/// Mirrors the local race camera and steering input. The track, colliders, and game assets stay untouched.
/// </summary>
public sealed class MirrorRace : IModRecipe
{
    public string Name => "MirrorRace";
    public bool ChangesGameplay => true;

    private static MirrorRace? _instance;
    private static readonly Dictionary<int, CameraRenderState> RenderStates = new();
    private readonly HashSet<int> _localCameras = new();
    private ConfigEntry<bool> _enabled = null!;
    private float _nextCameraScan;
    private bool _active;
    private int _lastLoggedCameraCount = -1;
    private bool _loggedProjection;
    private bool _loggedWaitingForRace;

    private sealed class CameraRenderState
    {
        internal Camera Camera = null!;
        internal Matrix4x4 Projection;
        internal bool PreviousInvertCulling;
        internal bool CullingWasInverted;
    }

    public void Configure(ConfigFile config)
    {
        _enabled = config.Bind("Recipe." + Name, "Enabled", false,
            "Mirror the local camera and steering in offline races and time trials. Applies immediately; track assets stay unchanged.");
        _instance = this;

        var harmony = Plugin.Instance!.Harmony;
        PatchCameraEvent(harmony, "OnPreCull", nameof(AfterCameraPreCull));
        PatchCameraEvent(harmony, "OnPreRender", nameof(AfterCameraPreRender));
        PatchCameraEvent(harmony, "OnPostRender", nameof(AfterCameraPostRender));

        MethodInfo frameCamera = AccessTools.DeclaredMethod(typeof(PixelGameKartCamera), "GetRacingCamPositionAndRotation",
            new[] { typeof(float), typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType(), typeof(float).MakeByRefType() })
            ?? throw new MissingMethodException(typeof(PixelGameKartCamera).FullName, "GetRacingCamPositionAndRotation(float, ref Vector3, ref Quaternion, ref float)");
        harmony.Patch(frameCamera,
            prefix: new HarmonyMethod(typeof(MirrorRace), nameof(BeforeRacingCameraFraming)),
            postfix: new HarmonyMethod(typeof(MirrorRace), nameof(AfterRacingCameraFraming)));

        MethodInfo steer = AccessTools.DeclaredMethod(typeof(PixelEasyCharMoveKartController), "SteerInput", new[] { typeof(float) })
            ?? throw new MissingMethodException(typeof(PixelEasyCharMoveKartController).FullName, "SteerInput(float)");
        harmony.Patch(steer, prefix: new HarmonyMethod(typeof(MirrorRace), nameof(BeforeSteerInput)));
    }

    private static void PatchCameraEvent(Harmony harmony, string name, string callback)
    {
        MethodInfo target = AccessTools.DeclaredMethod(typeof(PixelSDK_CameraEvents), name, Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(PixelSDK_CameraEvents).FullName, name + "()");
        harmony.Patch(target, postfix: new HarmonyMethod(typeof(MirrorRace), callback));
    }

    public void Tick()
    {
        bool canRun = _enabled.Value && Plugin.OfflineLabAllowed &&
            Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
        if (!canRun)
        {
            if (_enabled.Value && Plugin.OfflineLabAllowed && !_loggedWaitingForRace)
            {
                _loggedWaitingForRace = true;
                Plugin.Instance?.Log.LogInfo("Mirror Race is armed; waiting for the local race to enter its running state.");
            }
            _active = false;
            _localCameras.Clear();
            RestoreOutstanding();
            _lastLoggedCameraCount = -1;
            _loggedProjection = false;
            return;
        }
        _loggedWaitingForRace = false;

        if (Time.unscaledTime < _nextCameraScan) return;
        _nextCameraScan = Time.unscaledTime + .5f;
        _localCameras.Clear();
        foreach (var kart in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
        {
            var player = kart == null ? null : kart.parentPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) continue;
            var brain = player.gameplayCamera;
            if (brain == null || brain.bIsSpectatorCamera) continue;
            var camera = brain.GetCamera()?.unityCamera;
            if (camera != null && camera.isActiveAndEnabled) _localCameras.Add(camera.GetInstanceID());
        }
        _active = _localCameras.Count != 0;
        if (_localCameras.Count != _lastLoggedCameraCount)
        {
            _lastLoggedCameraCount = _localCameras.Count;
            if (_active) Plugin.Instance?.Log.LogInfo($"Mirror Race found {_localCameras.Count} local gameplay camera(s).");
            else Plugin.Instance?.Log.LogWarning("Mirror Race is enabled, but no local gameplay camera was found.");
        }
    }

    private static Camera? CameraFor(PixelSDK_CameraEvents events)
    {
        var pixelCamera = events == null ? null : events.parentCamera;
        return pixelCamera == null ? null : pixelCamera.unityCamera;
    }

    private static void AfterCameraPreCull(PixelSDK_CameraEvents __instance)
    {
        var module = _instance;
        if (module == null || !module._active || !Plugin.OfflineLabAllowed) return;
        var camera = CameraFor(__instance);
        if (camera == null || !module._localCameras.Contains(camera.GetInstanceID())) return;

        int id = camera.GetInstanceID();
        try
        {
            Restore(id);
            var state = new CameraRenderState
            {
                Camera = camera,
                Projection = camera.projectionMatrix,
                PreviousInvertCulling = GL.invertCulling
            };
            RenderStates[id] = state;

            Matrix4x4 flipX = Matrix4x4.identity;
            flipX.m00 = -1f;
            // Flip clip-space X, preserving asymmetric/off-center projections.
            camera.projectionMatrix = flipX * state.Projection;
            if (!module._loggedProjection)
            {
                module._loggedProjection = true;
                Plugin.Instance?.Log.LogInfo($"Mirror Race applied to local camera {id}.");
            }
        }
        catch (Exception ex)
        {
            module._active = false;
            Restore(id);
            Plugin.Instance?.Log.LogError($"Mirror race disabled after camera setup failed: {ex}");
        }
    }

    private static void AfterCameraPreRender(PixelSDK_CameraEvents __instance)
    {
        var camera = CameraFor(__instance);
        if (camera == null || !RenderStates.TryGetValue(camera.GetInstanceID(), out var state)) return;
        try
        {
            GL.invertCulling = !state.PreviousInvertCulling;
            state.CullingWasInverted = true;
        }
        catch (Exception ex)
        {
            if (_instance != null) _instance._active = false;
            Restore(camera.GetInstanceID());
            Plugin.Instance?.Log.LogError($"Mirror race disabled after culling setup failed: {ex}");
        }
    }

    private static void AfterCameraPostRender(PixelSDK_CameraEvents __instance)
    {
        var camera = CameraFor(__instance);
        if (camera != null) Restore(camera.GetInstanceID());
    }

    private static void Restore(int id)
    {
        if (!RenderStates.Remove(id, out var state)) return;
        try { if (state.Camera != null) state.Camera.projectionMatrix = state.Projection; }
        catch (Exception ex) { Plugin.Instance?.Log.LogError($"Mirror race could not restore a camera projection: {ex}"); }
        finally { if (state.CullingWasInverted) GL.invertCulling = state.PreviousInvertCulling; }
    }

    private static void BeforeSteerInput(PixelEasyCharMoveKartController __instance, ref float __0)
    {
        var module = _instance;
        if (module == null || !module._active || !Plugin.OfflineLabAllowed || __instance.parentPlayer == null) return;
        if (__instance.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL) __0 = -__0;
    }

    // The native chase camera adds a steering/drift orbit after following the kart's
    // forward direction. That extra orbit makes reflected track details appear to swim
    // across the view. Skip only that branch for the local player while Mirror Race runs.
    private static void BeforeRacingCameraFraming(PixelGameKartCamera __instance, out bool __state)
    {
        __state = false;
        var module = _instance;
        var player = __instance == null ? null : __instance.antPlayer;
        if (__instance == null || module == null || !module._active || !Plugin.OfflineLabAllowed || player == null ||
            player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;

        __state = __instance.bUseRotateTowardsNoLateralVelocity;
        if (__state) __instance.bUseRotateTowardsNoLateralVelocity = false;
    }

    private static void AfterRacingCameraFraming(PixelGameKartCamera __instance, bool __state)
    {
        if (__state && __instance != null) __instance.bUseRotateTowardsNoLateralVelocity = true;
    }

    public void Restore()
    {
        _active = false;
        _localCameras.Clear();
        RestoreOutstanding();
        _nextCameraScan = 0;
        _lastLoggedCameraCount = -1;
        _loggedProjection = false;
        _loggedWaitingForRace = false;
    }

    private static void RestoreOutstanding()
    {
        if (RenderStates.Count == 0) return;
        foreach (int id in new List<int>(RenderStates.Keys)) Restore(id);
    }
}

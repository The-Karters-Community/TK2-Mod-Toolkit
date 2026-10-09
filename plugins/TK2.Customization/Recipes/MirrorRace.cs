using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace TK2.Customization;

/// <summary>Mirrors the local race image while preserving the game's native camera and lighting.</summary>
public sealed class MirrorRace : IModRecipe
{
    public string Name => "MirrorRace";
    public bool ChangesGameplay => true;

    private static MirrorRace? _instance;
    private readonly HashSet<int> _localCameras = new();
    private readonly HashSet<MirrorImageEffect> _effects = new();
    private readonly HashSet<MirrorImageEffect> _activeEffects = new();
    private ConfigEntry<bool> _enabled = null!;
    private float _nextCameraScan;
    private bool _active;
    private int _lastLoggedCameraCount = -1;

    public void Configure(ConfigFile config)
    {
        _enabled = config.Bind("Recipe." + Name, "Enabled", false,
            "Mirror the local race camera and steering in offline races and time trials. The image is flipped after native rendering.");
        _instance = this;
        ClassInjector.RegisterTypeInIl2Cpp<MirrorImageEffect>();

        var harmony = Plugin.Instance!.Harmony;
        MethodInfo steer = AccessTools.DeclaredMethod(typeof(PixelEasyCharMoveKartController), "SteerInput", new[] { typeof(float) })
            ?? throw new MissingMethodException(typeof(PixelEasyCharMoveKartController).FullName, "SteerInput(float)");
        harmony.Patch(steer, prefix: new HarmonyMethod(typeof(MirrorRace), nameof(BeforeSteerInput)));
    }

    public void Tick()
    {
        if (!RaceContextAllowed)
        {
            Restore();
            return;
        }

        // No scene scans or image effects in menus. Find the local race camera as
        // soon as the map creates it. Do not wait for
        // E_RACE_RUNNING: that state is set only after the countdown has completed.
        if (Time.unscaledTime < _nextCameraScan) return;
        _nextCameraScan = Time.unscaledTime + .25f;
        _localCameras.Clear();
        _activeEffects.Clear();
        foreach (var kart in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
        {
            var player = kart == null ? null : kart.parentPlayer;
            if (player == null || !kart!.gameObject.activeInHierarchy || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) continue;
            var brain = player.gameplayCamera;
            if (brain == null || brain.bIsSpectatorCamera) continue;
            var camera = brain.GetCamera()?.unityCamera;
            if (camera == null || !camera.isActiveAndEnabled) continue;

            _localCameras.Add(camera.GetInstanceID());
            var effect = camera.gameObject.GetComponent<MirrorImageEffect>();
            if (effect == null) effect = camera.gameObject.AddComponent<MirrorImageEffect>();
            if (effect == null) continue;
            effect.enabled = true;
            _activeEffects.Add(effect);
        }

        foreach (var effect in _effects)
            if (effect != null && !_activeEffects.Contains(effect)) RemoveEffect(effect);
        _effects.Clear();
        foreach (var effect in _activeEffects) _effects.Add(effect);

        _active = _effects.Count != 0;
        if (_localCameras.Count == _lastLoggedCameraCount) return;
        _lastLoggedCameraCount = _localCameras.Count;
        if (_active) Plugin.Instance?.Log.LogInfo($"Mirror Race attached to {_localCameras.Count} local camera(s); active before countdown.");
        else Plugin.Instance?.Log.LogWarning("Mirror Race is enabled, but no local gameplay camera is available yet.");
    }

    private bool RaceContextAllowed => _enabled != null && _enabled.Value && Plugin.OfflineLabAllowed &&
        MenuManager.Instance == null && Ant_MapData.instance != null;

    internal static bool ShouldMirror(Camera? camera)
    {
        var module = _instance;
        return camera != null && camera.isActiveAndEnabled && module != null && module._active && module.RaceContextAllowed &&
            module._localCameras.Contains(camera.GetInstanceID());
    }

    private static void BeforeSteerInput(PixelEasyCharMoveKartController __instance, ref float __0)
    {
        var module = _instance;
        if (module == null || !module._active || !module.RaceContextAllowed || __instance.parentPlayer == null || !__instance.gameObject.activeInHierarchy) return;
        if (__instance.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL) __0 = -__0;
    }

    public void Restore()
    {
        _active = false;
        _localCameras.Clear();
        foreach (var effect in _effects)
            if (effect != null) RemoveEffect(effect);
        _effects.Clear();
        _activeEffects.Clear();
        _nextCameraScan = 0;
        _lastLoggedCameraCount = -1;
    }

    private static void RemoveEffect(MirrorImageEffect effect)
    {
        // Remove the injected render callback entirely when it is no longer needed.
        // Destroy the component, not the native camera or its GameObject.
        effect.enabled = false;
        UnityEngine.Object.Destroy(effect);
    }
}

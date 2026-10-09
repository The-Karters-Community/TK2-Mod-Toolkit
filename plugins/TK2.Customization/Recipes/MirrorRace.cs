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
        if (!_enabled.Value || !Plugin.OfflineLabAllowed)
        {
            Restore();
            return;
        }

        // Find the local race camera as soon as the game creates it. Do not wait for
        // E_RACE_RUNNING: that state is set only after the countdown has completed.
        if (Time.unscaledTime < _nextCameraScan) return;
        _nextCameraScan = Time.unscaledTime + (_active ? .25f : .05f);
        _localCameras.Clear();
        var activeEffects = new HashSet<MirrorImageEffect>();
        foreach (var kart in UnityEngine.Object.FindObjectsOfType<PixelEasyCharMoveKartController>())
        {
            var player = kart == null ? null : kart.parentPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) continue;
            var brain = player.gameplayCamera;
            if (brain == null || brain.bIsSpectatorCamera) continue;
            var camera = brain.GetCamera()?.unityCamera;
            if (camera == null || !camera.isActiveAndEnabled) continue;

            _localCameras.Add(camera.GetInstanceID());
            var effect = camera.gameObject.GetComponent<MirrorImageEffect>();
            if (effect == null) effect = camera.gameObject.AddComponent<MirrorImageEffect>();
            if (effect == null) continue;
            effect.enabled = true;
            activeEffects.Add(effect);
        }

        foreach (var effect in _effects)
            if (effect != null && !activeEffects.Contains(effect)) effect.enabled = false;
        _effects.Clear();
        foreach (var effect in activeEffects) _effects.Add(effect);

        _active = _localCameras.Count != 0;
        if (_localCameras.Count == _lastLoggedCameraCount) return;
        _lastLoggedCameraCount = _localCameras.Count;
        if (_active) Plugin.Instance?.Log.LogInfo($"Mirror Race attached to {_localCameras.Count} local camera(s); active before countdown.");
        else Plugin.Instance?.Log.LogWarning("Mirror Race is enabled, but no local gameplay camera is available yet.");
    }

    internal static bool ShouldMirror(Camera? camera)
    {
        var module = _instance;
        return camera != null && module != null && module._active && Plugin.OfflineLabAllowed &&
            module._localCameras.Contains(camera.GetInstanceID());
    }

    private static void BeforeSteerInput(PixelEasyCharMoveKartController __instance, ref float __0)
    {
        var module = _instance;
        if (module == null || !module._active || !Plugin.OfflineLabAllowed || __instance.parentPlayer == null) return;
        if (__instance.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL) __0 = -__0;
    }

    public void Restore()
    {
        _active = false;
        _localCameras.Clear();
        foreach (var effect in _effects)
            if (effect != null) effect.enabled = false;
        _effects.Clear();
        _nextCameraScan = 0;
        _lastLoggedCameraCount = -1;
    }
}

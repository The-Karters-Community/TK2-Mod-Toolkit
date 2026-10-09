using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

[BepInPlugin("local.tk2.customization", "TK2 Mod Toolkit Pack", "0.6.18")]
public sealed partial class Plugin : BasePlugin
{
    internal static Plugin? Instance;
    internal readonly Harmony Harmony = new("local.tk2.customization");
    internal ConfigEntry<bool> UiEnabled = null!, AudioEnabled = null!, CameraEnabled = null!, PhysicsEnabled = null!;
    internal ConfigEntry<float> HudScale = null!, Volume = null!, Fov = null!, FallAcceleration = null!, AirTime = null!;
    internal ConfigEntry<string> CanvasFilter = null!;
    internal ConfigEntry<bool> PreserveKartFraming = null!;
    internal ConfigEntry<bool> VignetteDisabled = null!;
    internal ConfigEntry<float> CameraDistance = null!, CameraHeight = null!;
    internal ConfigEntry<float> MusicVolume = null!, SfxVolume = null!, VoiceVolume = null!, UiVolume = null!;
    internal bool GameplayReady, PhysicsFaulted, SessionModified;
    private StudioBehaviour? _behaviour;

    // Evidence: Ghidra database and installed GameAssembly SHA-256 agree on this build.
    private const string KnownGameHash = "e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130";

    public override void Load()
    {
        Instance = this;
        // Config.Reload must not save every setting back over a GUI edit.
        Config.SaveOnConfigSet = false;
        UiEnabled = Config.Bind("UI", "Enabled", false, "Scale matching root overlay HUD canvases.");
        BindOnlineProtection();
        InstallOnlineProtection();
        HudScale = Config.Bind("UI", "HudScale", 1f, new ConfigDescription("Multiplier of captured Canvas scale.", new AcceptableValueRange<float>(0.5f, 2f)));
        CanvasFilter = Config.Bind("UI", "CanvasNameFilter", "HUD", "Case-insensitive canvas name substring. Empty filters do not match.");
        AudioEnabled = Config.Bind("Audio", "Enabled", false, "Multiply the game's Wwise volume settings.");
        Volume = Config.Bind("Audio", "MasterVolume", 1f, new ConfigDescription("Multiplier of game volume settings (0 to 1).", new AcceptableValueRange<float>(0f, 1f)));
        MusicVolume = Config.Bind("Audio", "MusicVolume", 1f, new ConfigDescription("Music bus multiplier.", new AcceptableValueRange<float>(0,1)));
        SfxVolume = Config.Bind("Audio", "SfxVolume", 1f, new ConfigDescription("Sound effects bus multiplier.", new AcceptableValueRange<float>(0,1)));
        VoiceVolume = Config.Bind("Audio", "VoiceVolume", 1f, new ConfigDescription("Voice-over bus multiplier.", new AcceptableValueRange<float>(0,1)));
        UiVolume = Config.Bind("Audio", "UiVolume", 1f, new ConfigDescription("Interface sounds bus multiplier.", new AcceptableValueRange<float>(0,1)));
        BindCamera();
        PhysicsEnabled = Config.Bind("Physics", "Enabled", false, "Offline fast fall for testing. Leaderboard uploads are unchanged.");
        FallAcceleration = Config.Bind("Physics", "FastFallAcceleration", 100f, new ConfigDescription("Extra downwards acceleration.", new AcceptableValueRange<float>(0f, 500f)));
        AirTime = Config.Bind("Physics", "MinimumAirTime", 0.4f, new ConfigDescription("Minimum airborne seconds.", new AcceptableValueRange<float>(0f, 3f)));
        BindPack();

        TryFeature("Wwise volume", () => PatchExact(typeof(PTK_AudioListenerManager), "SetVolume",
            new[] { typeof(string), typeof(int) }, nameof(AudioPrefix), prefix: true));
        TryFeature("offline fast fall", InstallPhysics);
        TryFeature("fast-fall controller input", () => FastFallFeature.Install(this));
        InstallPack();
        TryFeature("racing camera", () => CameraFeature.Install(this));
        LegacyMK.Install(this);
        CommunityMods.Install(this);
        NightmareAI.Install(this);
        TryFeature("native performance", () => PerformanceFeature.Install(this));
        TryFeature("performance diagnostics", () => PerformanceDiagnostics.Install(this));
        TryFeature("track boundaries", () => TrackBoundaries.Install(this));
        _behaviour = AddComponent<StudioBehaviour>();
        Config.Save();
        LiveConfig.Initialize(this);
        Log.LogInfo("TK2 Mod Toolkit 0.6.18: offline test features available; online protection active when configured.");
    }

    private void TryFeature(string name, Action install)
    {
        try { install(); Log.LogInfo($"Capability available: {name}"); }
        catch (Exception ex) { Log.LogWarning($"Capability unavailable: {name}: {ex.Message}"); }
    }

    private void PatchExact(Type type, string method, Type[] args, string patch, bool prefix)
    {
        MethodInfo target = AccessTools.DeclaredMethod(type, method, args)
            ?? throw new MissingMethodException(type.FullName, method);
        var hook = new HarmonyMethod(typeof(Plugin), patch);
        if (prefix) Harmony.Patch(target, prefix: hook); else Harmony.Patch(target, postfix: hook);
    }

    private void InstallPhysics()
    {
        string binary = Path.Combine(Paths.GameRootPath, "GameAssembly.dll");
        using var stream = File.OpenRead(binary);
        using var algorithm = SHA256.Create();
        string hash = Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
        if (hash != KnownGameHash) throw new InvalidOperationException("Unverified game build; physics disabled.");
        PatchExact(typeof(PixelKartPhysics), "FixedUpdate", Type.EmptyTypes, nameof(PhysicsPostfix), prefix: false);
        GameplayReady = true;
    }

    private static void AudioPrefix(PTK_AudioListenerManager __instance, string __0, ref int __1)
    {
        var p = Instance;
        if (p == null) return;
        try { AudioFeature.BeforeSetVolume(__instance, __0, ref __1, p.AudioEnabled.Value, AudioFeature.Multiplier(p, __0)); }
        catch (Exception ex) { p.Log.LogWarning($"Volume hook failed: {ex.Message}"); }
    }

    private static void PhysicsPostfix(PixelKartPhysics __instance)
    {
        var p = Instance;
        if (p == null || !p.GameplayReady) return;
        try
        {
            FastFallFeature.Tick(__instance, p);
        }
        catch (Exception ex)
        {
            p.PhysicsFaulted = true;
            p.Log.LogError($"Fast fall disabled after runtime failure: {ex.Message}");
        }
    }

    public override bool Unload()
    {
        if (SessionModified) { Log.LogWarning("Restart required to unload after modifying a race."); return false; }
        CameraPanel.Close(this);
        if (_behaviour != null) { _behaviour.RestoreAll(); UnityEngine.Object.Destroy(_behaviour); }
        AudioFeature.Restore();
        RecipeHost.Restore();
        FastFallFeature.Restore();
        CommunityMods.Restore();
        NightmareAI.Restore();
        LegacyMK.Restore();
        RestoreTuning();
        PerformanceFeature.Restore();
        PerformanceDiagnostics.Stop();
        TrackBoundaries.Restore();
        Harmony.UnpatchSelf();
        Instance = null;
        return true;
    }
}

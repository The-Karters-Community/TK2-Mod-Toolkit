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

[BepInPlugin("local.tk2.customization", "TK2 Customization", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin? Instance;
    internal readonly Harmony Harmony = new("local.tk2.customization");
    internal ConfigEntry<bool> UiEnabled = null!, AudioEnabled = null!, CameraEnabled = null!, PhysicsEnabled = null!;
    internal ConfigEntry<float> HudScale = null!, Volume = null!, Fov = null!, FallAcceleration = null!, AirTime = null!;
    internal ConfigEntry<string> CanvasFilter = null!;
    internal bool GameplayReady, PhysicsFaulted, SessionModified;
    private StudioBehaviour? _behaviour;

    // Evidence: Ghidra database and installed GameAssembly SHA-256 agree on this build.
    private const string KnownGameHash = "e55fab2cdcbf3852160183504a847b1a1b7af61c24bc9619e3736e3753aee130";

    public override void Load()
    {
        Instance = this;
        UiEnabled = Config.Bind("UI", "Enabled", false, "Scale matching root overlay HUD canvases.");
        HudScale = Config.Bind("UI", "HudScale", 1f, new ConfigDescription("Multiplier of captured Canvas scale.", new AcceptableValueRange<float>(0.5f, 2f)));
        CanvasFilter = Config.Bind("UI", "CanvasNameFilter", "HUD", "Case-insensitive canvas name substring. Empty filters do not match.");
        AudioEnabled = Config.Bind("Audio", "Enabled", false, "Multiply the game's Wwise volume settings.");
        Volume = Config.Bind("Audio", "MasterVolume", 1f, new ConfigDescription("Multiplier of game volume settings (0 to 1).", new AcceptableValueRange<float>(0f, 1f)));
        CameraEnabled = Config.Bind("Camera", "Enabled", false, "Override main perspective camera FOV.");
        Fov = Config.Bind("Camera", "FieldOfView", 65f, new ConfigDescription("Vertical FOV in degrees.", new AcceptableValueRange<float>(35f, 110f)));
        PhysicsEnabled = Config.Bind("Physics", "Enabled", false, "Experimental offline fast fall: hold Down Arrow after minimum air time. Requires verified targets and leaderboard guard.");
        FallAcceleration = Config.Bind("Physics", "FastFallAcceleration", 100f, new ConfigDescription("Extra downwards acceleration.", new AcceptableValueRange<float>(0f, 500f)));
        AirTime = Config.Bind("Physics", "MinimumAirTime", 0.4f, new ConfigDescription("Minimum airborne seconds.", new AcceptableValueRange<float>(0f, 3f)));

        TryFeature("Wwise volume", () => PatchExact(typeof(PTK_AudioListenerManager), "SetVolume",
            new[] { typeof(string), typeof(int) }, nameof(AudioPrefix), prefix: true));
        TryFeature("offline fast fall", InstallPhysics);
        _behaviour = AddComponent<StudioBehaviour>();
        Log.LogInfo("TK2 Customization compiled for 0.1.4.18. Features default off; runtime testing required.");
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
        // The guard must install before the gameplay hook. A failure leaves physics disabled.
        var upload = AccessTools.DeclaredMethod(typeof(KartersLeaderboardsManager), "RaceFinished_UploadLeaderboard",
            new[] { typeof(float), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>),
                typeof(Il2CppSystem.Collections.Generic.List<PTK_LeaderboardFacet.CCheckpointTimes>),
                typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Action) })
            ?? throw new MissingMethodException("Leaderboard upload signature changed");
        Harmony.Patch(upload, prefix: new HarmonyMethod(typeof(Plugin), nameof(UploadPrefix)));
        PatchExact(typeof(PixelKartPhysics), "FixedUpdate", Type.EmptyTypes, nameof(PhysicsPostfix), prefix: false);
        GameplayReady = true;
    }

    private static bool UploadPrefix() => Instance == null || !Instance.SessionModified;

    private static void AudioPrefix(PTK_AudioListenerManager __instance, string __0, ref int __1)
    {
        var p = Instance;
        if (p == null) return;
        try { AudioFeature.BeforeSetVolume(__instance, __0, ref __1, p.AudioEnabled.Value, p.Volume.Value); }
        catch (Exception ex) { p.Log.LogWarning($"Volume hook failed: {ex.Message}"); }
    }

    private static void PhysicsPostfix(PixelKartPhysics __instance)
    {
        var p = Instance;
        if (p == null || !p.GameplayReady || p.PhysicsFaulted || !p.PhysicsEnabled.Value) return;
        try
        {
            // Generated interop exposes ObscuredBool conversions and private fields as managed properties.
            if (Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet) return;
            if (Time.timeScale <= 0 || Ant_CurrentGameConfiguration.eCurrentRaceState !=
                Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) return;
            var controller = __instance.kartController;
            if (controller == null || controller.parentPlayer == null || controller.parentPlayer.ePlayerType !=
                Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            if (!Input.GetKey(KeyCode.DownArrow) || __instance.bWasGrounded || __instance.bIsDrifting ||
                __instance.fTimeInAir < p.AirTime.Value) return;
            // Keep protection active after toggling off: a modified race can still finish later.
            p.SessionModified = true;
            controller.AddVelocity(Vector3.down * Time.fixedDeltaTime * p.FallAcceleration.Value);
        }
        catch (Exception ex)
        {
            p.PhysicsFaulted = true;
            p.Log.LogError($"Fast fall disabled after runtime failure: {ex.Message}");
        }
    }

    public override bool Unload()
    {
        // Removing the upload guard after a modified race could expose its result.
        // Restart the process to unload in that case.
        if (SessionModified) { Log.LogWarning("Restart required to unload after modified physics."); return false; }
        if (_behaviour != null) { _behaviour.RestoreAll(); UnityEngine.Object.Destroy(_behaviour); }
        AudioFeature.Restore();
        Harmony.UnpatchSelf();
        Instance = null;
        return true;
    }
}

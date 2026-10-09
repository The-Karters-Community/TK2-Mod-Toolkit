using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Scripting;

namespace TK2.Customization;

// Current 0.1.4.18 native evidence and limits: docs/PERFORMANCE-INVESTIGATION-2026-10-09.md.
internal static class PerformanceFeature
{
    private static ConfigEntry<bool> Enabled = null!, CacheDraw = null!, RaceGc = null!, LowerAi = null!;
    private static ConfigEntry<int> AiInterval = null!;
    private static bool _installed, _faulted, _wasActive, _gcOwned, _restoringGc, _aiOwned, _originalAiQuality;
    private static float _nextPrune;
    private static Il2CppReferenceArray<Camera>? _cameras;
    private static readonly Il2CppStructArray<float>?[] CullDistances = new Il2CppStructArray<float>?[3];
    private sealed record SavedAi(PixelEasyCharMoveKartController Controller, int Interval,
        KinematicCharacterController.KinematicCharacterMotor Motor, bool LowerQuality, int MotorInterval);
    private static readonly Dictionary<int, SavedAi> AiOriginals = new();
    private static readonly List<int> Stale = new();

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("Performance", "Enabled", false, "Cache draw-distance arrays and enable selected offline race optimizations.");
        CacheDraw = p.Config.Bind("Performance", "CacheDrawDistance", true, "Reuse native camera buffers without changing draw distance or graphics quality.");
        RaceGc = p.Config.Bind("Performance", "RaceGarbageCollection", true, "Keep Unity garbage collection enabled in offline races to reclaim managed allocations. May trade memory growth for collection hitches.");
        LowerAi = p.Config.Bind("Performance", "LowerAIPhysics", false, "Opt in to the game's existing lower-quality AI physics. Offline races only; opponent movement and collision precision can change.");
        AiInterval = p.Config.Bind("Performance", "AIPhysicsInterval", 2,
            new ConfigDescription("Heavy AI motor updates every this many physics ticks. 1 retains full cadence; humans, ghosts and global timestep are unchanged.", new AcceptableValueRange<int>(1, 4)));
        if (!p.GameplayReady) { p.Log.LogWarning("Performance module unavailable: current native build was not verified."); return; }
        // Exact parameterless methods verified against current interop; no stub-body transpilers.
        Patch(p, typeof(PTK_GraphicsDetailApplier), "LateUpdate", nameof(GraphicsPrefix), true);
        Patch(p, typeof(Ant_CurrentGameConfiguration), "PTK_GC_DisableForRace", nameof(GcPostfix), false);
        Patch(p, typeof(Ant_CurrentGameConfiguration), "PTK_GC_ApplyCurrentPolicy", nameof(GcPostfix), false);
        Patch(p, typeof(PixelEasyCharMoveKartController), "FixedUpdate", nameof(AiPrefix), true);
        _installed = true;
        p.Log.LogInfo("Performance module available: cached draw distance, offline race GC, optional native AI cadence.");
    }

    private static void Patch(Plugin p, Type type, string name, string hook, bool prefix)
    {
        var target = AccessTools.DeclaredMethod(type, name, Type.EmptyTypes)
            ?? throw new MissingMethodException(type.FullName, name);
        var patch = new HarmonyMethod(typeof(PerformanceFeature), hook);
        if (prefix) p.Harmony.Patch(target, prefix: patch); else p.Harmony.Patch(target, postfix: patch);
    }

    private static bool Active => _installed && !_faulted && Enabled != null && Enabled.Value && Plugin.Instance?.GameplayReady == true;
    internal static object DiagnosticSettings() => new { active = Active, cacheDraw = CacheDraw?.Value,
        raceGc = RaceGc?.Value, lowerAi = LowerAi?.Value, aiInterval = AiInterval?.Value };
    private static bool Offline => Plugin.OfflineLabAllowed;
    private static bool OfflineRunning => Offline && MenuManager.Instance == null &&
        Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;

    // Narrow semantic replacement of 0x1806cf040. Terrain/environment/shadow branches are retained.
    private static bool GraphicsPrefix(PTK_GraphicsDetailApplier __instance)
    {
        if (!Active || !CacheDraw.Value || !Offline) return true;
        try
        {
            if (MenuManager.Instance != null)
            {
                ReleaseCameraBuffers();
                if (__instance._isSuppressedForMainMenu) return false;
                __instance.RestoreGameTerrain();
                __instance.RestoreTerrainQualityOverrides();
                __instance._appliedEnviro = __instance._appliedShadow = __instance._appliedTerrainQuality = -999;
                __instance._isSuppressedForMainMenu = true;
                return false;
            }
            __instance._isSuppressedForMainMenu = false;
            int enviro = PTK_GraphicsDetailApplier.EffEnviro();
            int shadow = PTK_GraphicsDetailApplier.EffectiveShadowDetailIndex;
            int draw = PTK_GraphicsDetailApplier.EffDraw();
            int terrain = PTK_GraphicsDetailApplier.EffectiveTerrainQualityIndex;
            if (terrain != __instance._appliedTerrainQuality)
            { __instance.ApplyTerrainQuality(terrain); __instance._appliedTerrainQuality = terrain; }
            bool enviroChanged = enviro != __instance._appliedEnviro;
            if (enviroChanged) { __instance.ApplyEnviro(enviro); __instance._appliedEnviro = enviro; }
            if (enviroChanged || shadow != __instance._appliedShadow)
            { __instance.ApplyShadow(shadow); __instance._appliedShadow = shadow; }
            if (draw != 0 || __instance._appliedDraw != 0) ApplyDraw(draw);
            __instance._appliedDraw = draw;
            return false;
        }
        catch (Exception ex) { Fault(ex); return true; } // Original LateUpdate remains the fallback.
    }

    private static void ApplyDraw(int draw)
    {
        int mode = draw == 0 ? 0 : draw == 1 ? 1 : 2; // Native: all nonzero values except 1 use 300.
        var distances = CullDistances[mode];
        if (distances == null)
        {
            distances = new Il2CppStructArray<float>(32);
            float distance = mode == 0 ? 0f : mode == 1 ? 600f : 300f;
            for (int layer = 0; layer < 32; layer++) distances[layer] = layer == 8 || layer == 31 ? 0f : distance;
            CullDistances[mode] = distances;
        }
        int count = Camera.allCamerasCount;
        if (count == 0) return;
        if (_cameras == null || _cameras.Length < count) _cameras = new Il2CppReferenceArray<Camera>(count);
        int filled = Camera.GetAllCameras(_cameras);
        for (int i = 0; i < filled; i++)
        {
            var camera = _cameras[i];
            if (camera != null) camera.layerCullDistances = distances;
            _cameras[i] = null!; // Buffer must not retain cameras from unloaded scenes.
        }
        // Keep setters every native frame: do not silently clobber another owner's later camera changes.
    }

    private static void GcPostfix()
    {
        if (_restoringGc || !Active || !RaceGc.Value || !Offline) return;
        try { EnableRaceGc(); } catch (Exception ex) { Fault(ex); }
    }
    private static void EnableRaceGc()
    {
        if (GarbageCollector.GCMode != GarbageCollector.Mode.Disabled) return;
        GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
        _gcOwned = true;
    }
    private static void RestoreGc()
    {
        if (!_gcOwned) return;
        _gcOwned = false;
        // Ask the game for today's policy, not a stale pre-race Disabled value in the menu.
        if (GarbageCollector.GCMode != GarbageCollector.Mode.Enabled) return;
        _restoringGc = true;
        try { Ant_CurrentGameConfiguration.PTK_GC_ApplyCurrentPolicy(); }
        finally { _restoringGc = false; }
    }

    private static void AiPrefix(PixelEasyCharMoveKartController __instance)
    {
        try
        {
            if (!Active || !LowerAi.Value || AiInterval.Value <= 1 || !OfflineRunning)
            { if (_aiOwned) RestoreAi(); return; }
            var player = __instance.parentPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_AI_LOCAL || __instance.Motor == null) return;
            if (!_aiOwned)
            {
                _originalAiQuality = Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles;
                _aiOwned = true;
            }
            int id = __instance.GetInstanceID();
            if (!AiOriginals.TryGetValue(id, out var previous) || previous.Controller == null || previous.Controller.Pointer != __instance.Pointer)
                AiOriginals[id] = new SavedAi(__instance, __instance.iAILowerQualityPhysicsUpdateEveryOnlyX,
                    __instance.Motor, __instance.Motor.bLowerPhysicsQualityForAI, __instance.Motor.iLowerQualityPhysicsUpdateEveryOnlyX);
            Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles = true;
            __instance.iAILowerQualityPhysicsUpdateEveryOnlyX = Math.Clamp(AiInterval.Value, 1, 4);
            Plugin.Instance!.SessionModified = true;
            // Original controller and KCC implement the cadence. Never skip kart collisions or change fixedDeltaTime.
        }
        catch (Exception ex) { Fault(ex); }
    }

    private static void RestoreAi()
    {
        Exception? failure = null;
        if (_aiOwned)
        {
            try { Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles = _originalAiQuality; }
            catch (Exception ex) { failure = ex; }
        }
        _aiOwned = false;
        foreach (var saved in AiOriginals.Values)
        {
            try { if (saved.Controller != null) saved.Controller.iAILowerQualityPhysicsUpdateEveryOnlyX = saved.Interval; }
            catch (Exception ex) { failure ??= ex; }
            try
            {
                if (saved.Motor != null)
                { saved.Motor.bLowerPhysicsQualityForAI = saved.LowerQuality; saved.Motor.iLowerQualityPhysicsUpdateEveryOnlyX = saved.MotorInterval; }
            }
            catch (Exception ex) { failure ??= ex; }
        }
        AiOriginals.Clear();
        if (failure != null) throw new InvalidOperationException("AI restoration was incomplete: " + failure.Message, failure);
    }

    internal static void Tick()
    {
        try
        {
            bool active = Active;
            if (active != _wasActive)
            {
                _wasActive = active;
                Plugin.Instance?.Log.LogInfo(active
                    ? $"Race performance active: draw cache={CacheDraw.Value}, offline GC={RaceGc.Value}, lower AI={LowerAi.Value}, AI interval={AiInterval.Value}."
                    : "Race performance disabled; returning control to native code.");
            }
            if (!active) { Restore(); return; }
            if (!RaceGc.Value || !Offline) RestoreGc();
            else if (OfflineRunning) EnableRaceGc(); // Also supports enabling the module mid-race.
            if (!LowerAi.Value || AiInterval.Value <= 1 || !OfflineRunning) RestoreAi();
            if (!CacheDraw.Value) ReleaseCameraBuffers();
            if (AiOriginals.Count != 0 && Time.unscaledTime >= _nextPrune)
            {
                _nextPrune = Time.unscaledTime + 1f;
                Stale.Clear();
                foreach (var pair in AiOriginals) if (pair.Value.Controller == null) Stale.Add(pair.Key);
                foreach (int id in Stale) AiOriginals.Remove(id);
                Stale.Clear();
            }
        }
        catch (Exception ex) { Fault(ex); }
    }
    private static void ReleaseCameraBuffers()
    {
        _cameras = null;
        for (int i = 0; i < CullDistances.Length; i++) CullDistances[i] = null;
    }
    internal static void Restore()
    {
        try { RestoreAi(); }
        finally { try { RestoreGc(); } finally { ReleaseCameraBuffers(); } }
    }
    private static void Fault(Exception ex)
    {
        if (_faulted) return;
        _faulted = true;
        Plugin.Instance?.Log.LogError($"Performance module suspended; falling back to native behavior: {ex.Message}");
        try { Restore(); } catch (Exception restoreEx) { Plugin.Instance?.Log.LogWarning(restoreEx.Message); }
    }
}

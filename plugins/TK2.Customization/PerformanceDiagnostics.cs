using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace TK2.Customization;

// Observation only. Current native exports/signatures: docs/PERFORMANCE-DIAGNOSTICS.md.
internal static class PerformanceDiagnostics
{
    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<int> _seconds = null!;
    private static readonly Harmony Hooks = new("local.tk2.performance.diagnostics");
    private static readonly (Type Type, string Name)[] Targets = {
        (typeof(PixelEasyCharMoveKartController), "FixedUpdate"),
        (typeof(PixelEasyCharMoveKartController), "UpdatePlayerPlayerCollision"),
        (typeof(KinematicCharacterController.KinematicCharacterSystem), "FixedUpdate"),
        (typeof(AILogicController), "Update"), (typeof(AIWeaponLogicController), "Update"),
        (typeof(AIDistToTargetBehavController), "Update"), (typeof(PlayerBezierFollower), "Update"),
        (typeof(PixelKartPhysics), "Update"), (typeof(PixelEasyCharMoveKartController), "Update"),
        (typeof(PTK_GraphicsDetailApplier), "LateUpdate") };
    private static readonly Dictionary<MethodBase, int> Slots = new();
    private static readonly PerformanceSamples Samples = new(Targets.Length);
    private static readonly PerformanceEngineSamples Engine = new();
    private static bool _inRace, _hooked, _recording, _completed, _faulted;
    private static long _lastFrame;
    private static double _warmUntil, _captureUntil, _windowStart;
    private static string? _file;
    private static object? _context;
    private static double _processCpu;
    private static int _aiSamples, _lowerAiSamples, _observedInterval;
    private static bool _boundaryVisibleThisWindow;

    internal static void Install(Plugin p)
    {
        _enabled = p.Config.Bind("PerformanceDiagnostics", "Enabled", false,
            "Record bounded offline race timing/memory samples. Diagnostic overhead can lower FPS; this is not an optimization.");
        _seconds = p.Config.Bind("PerformanceDiagnostics", "CaptureSeconds", 60,
            new ConfigDescription("Seconds captured per race after a 5-second warmup. Reports every 10 seconds; hooks are removed afterward.", new AcceptableValueRange<int>(10, 180)));
    }
    private static void StartHooks()
    {
        try
        {
            Slots.Clear();
            for (int i = 0; i < Targets.Length; i++)
            {
                var target = AccessTools.DeclaredMethod(Targets[i].Type, Targets[i].Name, Type.EmptyTypes)
                    ?? throw new MissingMethodException(Targets[i].Type.FullName, Targets[i].Name);
                if (target.ReturnType != typeof(void) || target.IsStatic) throw new InvalidOperationException("Unexpected diagnostic signature.");
                Slots.Add(target, i);
                string postfix = i == 0 ? nameof(ControllerAfter) : nameof(After);
                Hooks.Patch(target, prefix: new HarmonyMethod(typeof(PerformanceDiagnostics), nameof(Before)),
                    postfix: new HarmonyMethod(typeof(PerformanceDiagnostics), postfix));
            }
            _hooked = true;
        }
        catch { Hooks.UnpatchSelf(); Slots.Clear(); throw; }
    }
    private static void Before(out long __state) => __state = _recording ? Stopwatch.GetTimestamp() : 0;
    private static void After(MethodBase __originalMethod, long __state)
    {
        if (__state == 0 || !_recording) return;
        if (Slots.TryGetValue(__originalMethod, out int slot)) Samples.Method(slot, Stopwatch.GetTimestamp() - __state);
    }
    private static void ControllerAfter(PixelEasyCharMoveKartController __instance, MethodBase __originalMethod, long __state)
    {
        After(__originalMethod, __state);
        if (__state == 0 || !_recording || Samples.Calls[0] % 127 != 0) return;
        try
        {
            var player = __instance.parentPlayer; var motor = __instance.Motor;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_AI_LOCAL || motor == null) return;
            _aiSamples++; if (motor.bLowerPhysicsQualityForAI) _lowerAiSamples++;
            _observedInterval = motor.iLowerQualityPhysicsUpdateEveryOnlyX;
        }
        catch (Exception ex) { Fail(ex); }
    }
    internal static void Tick()
    {
        if (_enabled == null) return;
        if (_faulted)
        {
            if (_hooked) { try { RemoveHooks(); } catch (Exception ex) { Plugin.Instance?.Log.LogWarning(ex.Message); } }
            return;
        }
        try
        {
            bool running = _enabled.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null &&
                Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING &&
                Time.timeScale > 0 && Application.isFocused;
            double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            if (!running) { if (_inRace) Stop(); return; }
            if (!_inRace)
            {
                _inRace = true; _completed = false; _warmUntil = now + 5;
                if (_file == null)
                {
                    string directory = Path.Combine(Paths.BepInExRootPath, "diagnostics"); Directory.CreateDirectory(directory);
                    _file = Path.Combine(directory, $"tk2-performance-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.jsonl");
                }
                Plugin.Instance?.Log.LogInfo($"Performance diagnostics armed; 5-second warmup. Output: {_file}");
            }
            if (_completed || now < _warmUntil) return;
            if (!_hooked)
            {
                StartHooks(); Engine.Start(); now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                _captureUntil = now + Math.Clamp(_seconds.Value, 10, 180); _windowStart = now;
                _processCpu = ProcessCpu(); _context = Context(); Samples.Reset(); _boundaryVisibleThisWindow = false; _lastFrame = 0; _recording = true;
            }
            long stamp = Stopwatch.GetTimestamp();
            if (_lastFrame != 0) Samples.Frame((stamp - _lastFrame) / (double)Stopwatch.Frequency);
            _lastFrame = stamp;
            _boundaryVisibleThisWindow |= TrackBoundaries.Visible;
            Engine.Tick();
            if (now - _windowStart >= 10 || now >= _captureUntil)
            {
                WriteWindow(now);
                _lastFrame = 0; _windowStart = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                _context = Context();
                if (now >= _captureUntil) { RemoveHooks(); _completed = true; Plugin.Instance?.Log.LogInfo("Performance diagnostics capture complete; timing hooks removed."); }
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static object Context()
    {
        return new { gameMode = Ant_CurrentGameConfiguration.eGameModeType.ToString(),
            mapId = Ant_CurrentGameConfiguration.GetFinalChoosedMapConfig(-1)?.iConfigID,
            timeTrialCollisionBypass = Ant_CurrentGameConfiguration.GetFinalChoosedGameModeConfig(-1)?.bDisablePlayerPlayerCollisionForTT,
            fixedDeltaTime = Time.fixedDeltaTime, drawDistanceIndex = PTK_GraphicsDetailApplier.EffDraw(),
            performance = PerformanceFeature.DiagnosticSettings(), trackBoundariesVisible = TrackBoundaries.Visible,
            screenWidth = Screen.width, screenHeight = Screen.height, vSyncCount = QualitySettings.vSyncCount,
            targetFrameRate = Application.targetFrameRate, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            graphicsDevice = SystemInfo.graphicsDeviceName, unityVersion = Application.unityVersion };
    }
    private static double ProcessCpu() { using var process = Process.GetCurrentProcess(); return process.TotalProcessorTime.TotalMilliseconds; }
    private static long? ReadMemory(Func<long> read) { try { return read(); } catch { return null; } }
    private static void WriteWindow(double now)
    {
        _recording = false;
        if (Samples.Frames == 0) { Samples.Reset(); return; }
        double elapsed = now - _windowStart, cpu = ProcessCpu();
        var methods = new object[Targets.Length];
        for (int i = 0; i < methods.Length; i++)
            methods[i] = new { method = Targets[i].Type.FullName + "." + Targets[i].Name, calls = Samples.Calls[i],
                inclusiveMs = Samples.Ticks[i] * 1000d / Stopwatch.Frequency,
                maxCallMs = Samples.MaxTicks[i] * 1000d / Stopwatch.Frequency };
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var record = new { schema = 2, utc = DateTime.UtcNow, runtime = "0.6.13", elapsedSeconds = elapsed, context = _context,
            frames = Samples.Frames, averageFps = Samples.Frames / Samples.FrameSeconds,
            averageFrameMs = Samples.FrameSeconds * 1000 / Samples.Frames, p95FrameMsUpperBound = Samples.FrameP95Ms(),
            p95OverflowAt200Ms = Samples.FrameP95Ms() == null, maxFrameMs = Samples.MaxFrameMs,
            workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
            unityAllocatedBytes = ReadMemory(Profiler.GetTotalAllocatedMemoryLong), unityReservedBytes = ReadMemory(Profiler.GetTotalReservedMemoryLong),
            il2cppUsedBytes = ReadMemory(Profiler.GetMonoUsedSizeLong), il2cppHeapBytes = ReadMemory(Profiler.GetMonoHeapSizeLong),
            modClrUsedBytes = GC.GetTotalMemory(false), gcMode = GarbageCollector.GCMode.ToString(),
            processCpuMs = cpu - _processCpu, observedAiMotorSamples = _aiSamples, observedLowerAiMotorSamples = _lowerAiSamples,
            lastObservedAiMotorInterval = _aiSamples == 0 ? (int?)null : _observedInterval, methods, engine = Engine.Report(),
            trackBoundaryViewObservedDuringWindow = _boundaryVisibleThisWindow,
            limitation = "Instrumented inclusive wall times overlap; do not sum nested spans. Engine counters expose only registered release metrics; availability and units are reported individually. Hooks and recorders add overhead. Unity memory values can be unavailable/zero in release builds." };
        File.AppendAllText(_file!, JsonSerializer.Serialize(record) + Environment.NewLine);
        Plugin.Instance?.Log.LogInfo($"Performance sample: {record.averageFps:F1} FPS, p95 {record.p95FrameMsUpperBound?.ToString("F2") ?? ">=200"} ms, private {record.privateBytes / 1073741824d:F2} GiB; {_file}");
        Samples.Reset(); Engine.ResetWindow(); _boundaryVisibleThisWindow = false; _aiSamples = _lowerAiSamples = _observedInterval = 0; _processCpu = cpu; _recording = true;
    }
    private static void RemoveHooks()
    {
        _recording = false; _lastFrame = 0;
        try { Hooks.UnpatchSelf(); } finally { Engine.Dispose(); _hooked = false; Slots.Clear(); }
    }
    internal static void Stop()
    {
        try { if (_recording) WriteWindow(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency); }
        catch (Exception ex) { Fail(ex); }
        finally
        {
            try { RemoveHooks(); } catch (Exception ex) { Fail(ex); }
            _inRace = false; Samples.Reset(); _aiSamples = _lowerAiSamples = _observedInterval = 0;
        }
    }
    private static void Fail(Exception ex)
    {
        _faulted = true; _recording = false;
        Plugin.Instance?.Log.LogWarning($"Performance diagnostics stopped: {ex.Message}");
        // Tick removes hooks next update; do not rebuild the active detour inside its callback.
    }
}

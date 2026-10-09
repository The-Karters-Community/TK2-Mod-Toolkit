using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TK2.Customization;
using UnityEngine;
using UnityEngine.Scripting;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
Type feature = typeof(PerformanceFeature);
object? Field(string name) => feature.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
void Set(string name, object? value) => feature.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, value);
object? Call(string name, params object?[] args) => feature.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
Plugin Reset()
{
    PerformanceFeature.Restore();
    foreach (string field in new[] { "_installed", "_faulted", "_gcOwned", "_restoringGc", "_aiOwned", "_originalAiQuality", "_wasActive" }) Set(field, false);
    Set("_nextPrune", 0f);
    Camera.Cameras.Clear(); Camera.ThrowOnGet = false; Camera.LastBuffer = null;
    MenuManager.Instance = null; Time.unscaledTime = 0;
    Ant_CurrentGameConfiguration.eCurrentRaceState = Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;
    Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles = false;
    Ant_CurrentGameConfiguration.PolicyCalls = 0; Ant_CurrentGameConfiguration.Policy = null;
    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
    PTK_GraphicsDetailApplier.Enviro = 1; PTK_GraphicsDetailApplier.Shadow = 2; PTK_GraphicsDetailApplier.Draw = 1; PTK_GraphicsDetailApplier.Terrain = 3;
    var p = new Plugin(); Plugin.Instance = p; Plugin.OfflineLabAllowed = true;
    PerformanceFeature.Install(p);
    return p;
}
void Enable(Plugin p) => p.Config.Entry<bool>("Enabled").Value = true;
bool Graphics(PTK_GraphicsDetailApplier applier) => (bool)Call("GraphicsPrefix", applier)!;
bool ForcesCollection()
{
    var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
    foreach (var method in feature.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null) continue;
        for (int cursor = 0; cursor < il.Length;)
        {
            ushort key = il[cursor++];
            if (key == 0xfe) key = (ushort)(0xfe00 | il[cursor++]);
            var op = opcodes[key];
            if (op.OperandType == OperandType.InlineMethod)
            {
                var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, cursor));
                if (called?.DeclaringType == typeof(GC) && called.Name == "Collect") return true;
            }
            cursor += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, cursor),
                _ => 4
            };
        }
    }
    return false;
}
PixelEasyCharMoveKartController Kart(Ant_Player.EPlayerType type, int interval = 1) => new()
{
    parentPlayer = new() { ePlayerType = type }, iAILowerQualityPhysicsUpdateEveryOnlyX = interval,
    Motor = new() { bLowerPhysicsQualityForAI = true, iLowerQualityPhysicsUpdateEveryOnlyX = 3 }
};
void SimulateNativeAi(PixelEasyCharMoveKartController kart)
{
    bool lower = Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && kart.parentPlayer!.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL && kart.parentPlayer.ePlayerType != Ant_Player.EPlayerType.E_GHOST_TIME_TRIAL;
    kart.Motor!.bLowerPhysicsQualityForAI = lower;
    kart.Motor.iLowerQualityPhysicsUpdateEveryOnlyX = lower ? kart.iAILowerQualityPhysicsUpdateEveryOnlyX : 1;
}

var p = Reset();
Check(!p.Config.Entry<bool>("Enabled").Value && !p.Config.Entry<bool>("LowerAIPhysics").Value, "master and AI behavior default off");
Check(!ForcesCollection(), "compiled production module never calls System.GC.Collect");
Check(p.Harmony.Targets.Count == 4 && p.Harmony.Targets.All(t => t.GetParameters().Length == 0), "four exact parameterless native methods patched");
var applier = new PTK_GraphicsDetailApplier();
Check(Graphics(applier) && applier.Calls.Count == 0, "inactive graphics leaves original running");
Enable(p); p.Config.Entry<bool>("CacheDrawDistance").Value = false;
Check(Graphics(applier), "cache option off leaves original graphics running");
p.Config.Entry<bool>("CacheDrawDistance").Value = true;
var firstCamera = new Camera(); Camera.Cameras.Add(firstCamera);
Check(!Graphics(applier), "active replacement suppresses original");
Check(applier.Calls.SequenceEqual(new[] { "terrain:3", "enviro:1", "shadow:2" }), "native terrain/enviro/shadow order retained");
var firstDistances = firstCamera.layerCullDistances!;
Check(firstDistances.Length == 32 && Enumerable.Range(0, 32).All(i => firstDistances[i] == (i == 8 || i == 31 ? 0f : 600f)), "draw1 uses600 and exempts layers8/31");
var firstBuffer = Camera.LastBuffer;
Graphics(applier);
Check(applier.Calls.Count == 3 && firstCamera.SetterCalls == 2, "unchanged native apply branches skipped; draw setter stays everyframe");
Check(ReferenceEquals(firstDistances, firstCamera.layerCullDistances) && ReferenceEquals(firstBuffer, Camera.LastBuffer), "unchanged frame reuses both native buffers");
Check(firstBuffer![0] == null, "camera reference cleared after setter");
firstCamera.layerCullDistances = new Il2CppStructArray<float>(32);
Graphics(applier);
Check(ReferenceEquals(firstDistances, firstCamera.layerCullDistances), "later camera changes reset next frame to native draw policy");
PTK_GraphicsDetailApplier.Shadow = 4; Graphics(applier);
Check(applier.Calls.Last() == "shadow:4" && applier.Calls.Count == 4, "shadow-only edit applies only shadow");
PTK_GraphicsDetailApplier.Enviro = 2; Graphics(applier);
Check(applier.Calls.TakeLast(2).SequenceEqual(new[] { "enviro:2", "shadow:4" }), "enviro edit reapplies shadow even unchanged");
PTK_GraphicsDetailApplier.Draw = 2; Graphics(applier);
var lowDistances = firstCamera.layerCullDistances;
Check(Enumerable.Range(0, 32).All(i => lowDistances![i] == (i == 8 || i == 31 ? 0f : 300f)), "draw2 uses300 with exemptions");
PTK_GraphicsDetailApplier.Draw = -8; Graphics(applier);
Check(ReferenceEquals(lowDistances, firstCamera.layerCullDistances), "native nonzero non1 draw indices use300 cache");
PTK_GraphicsDetailApplier.Draw = 0; Graphics(applier);
Check(Enumerable.Range(0, 32).All(i => firstCamera.layerCullDistances![i] == 0), "switch back to default clears prior draw limits");
int setterCalls = firstCamera.SetterCalls; Graphics(applier);
Check(firstCamera.SetterCalls == setterCalls, "native unchanged defaultdraw skips setter");
PTK_GraphicsDetailApplier.Draw = 1;
var secondCamera = new Camera(); Camera.Cameras.Add(secondCamera); Graphics(applier);
var grownBuffer = Camera.LastBuffer;
Check(grownBuffer!.Length == 2 && !ReferenceEquals(grownBuffer, firstBuffer), "camera join grows reusable enumeration buffer");
Check(ReferenceEquals(firstCamera.layerCullDistances, secondCamera.layerCullDistances), "joined camera receives same draw distances");
Camera.Cameras.Remove(secondCamera); Graphics(applier);
Check(ReferenceEquals(grownBuffer, Camera.LastBuffer) && grownBuffer[0] == null && grownBuffer[1] == null, "camera shrink retains capacity without scene references");
Camera.Cameras.Clear(); Graphics(applier);
Check(ReferenceEquals(grownBuffer, Field("_cameras")), "zero cameras is safe");
MenuManager.Instance = new MenuManager(); Graphics(applier);
Check(applier._isSuppressedForMainMenu && applier.Calls.TakeLast(2).SequenceEqual(new[] { "restore-terrain", "restore-quality" }), "menu restores terrain and overrides");
Check(applier._appliedEnviro == -999 && applier._appliedShadow == -999 && applier._appliedTerrainQuality == -999 && Field("_cameras") == null, "menu invalidates appliedworld branches and buffers");
int menuCalls = applier.Calls.Count; Graphics(applier);
Check(applier.Calls.Count == menuCalls, "suppressed menu does not repeat restoration");
MenuManager.Instance = null; Camera.Cameras.Add(firstCamera); Graphics(applier);
Check(!applier._isSuppressedForMainMenu && applier.Calls.TakeLast(3).SequenceEqual(new[] { "terrain:3", "enviro:2", "shadow:4" }), "menu exit reapplies all worldbranches");

p = Reset(); Enable(p); applier = new() { ThrowApply = true };
Check(Graphics(applier) && p.Log.Errors.Count == 1 && (bool)Field("_faulted")!, "native apply failure fails open and suspends feature");
Check(Graphics(applier) && p.Log.Errors.Count == 1, "faulted replacement remains native until restart");
p = Reset(); Enable(p); Camera.Cameras.Add(new()); Camera.ThrowOnGet = true;
Check(Graphics(new()) && Field("_cameras") == null, "camera enumeration failure releases cache and fails open");

p = Reset(); Enable(p); p.Config.Entry<bool>("LowerAIPhysics").Value = true;
var human = Kart(Ant_Player.EPlayerType.E_HUMAN_LOCAL, 3); var ghost = Kart(Ant_Player.EPlayerType.E_GHOST_TIME_TRIAL, 4); var ai = Kart(Ant_Player.EPlayerType.E_AI_LOCAL, 4);
Call("AiPrefix", human); Call("AiPrefix", ghost);
Check(!Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && human.iAILowerQualityPhysicsUpdateEveryOnlyX == 3 && ghost.iAILowerQualityPhysicsUpdateEveryOnlyX == 4 && !p.SessionModified, "human/ghost prefixes unchanged and no ownership acquired");
Call("AiPrefix", ai); SimulateNativeAi(ai);
Check(ai.iAILowerQualityPhysicsUpdateEveryOnlyX == 2 && Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && p.SessionModified, "offline AI interval applies nativecadence and marks modifiedsession");
Call("AiPrefix", ai); SimulateNativeAi(ai);
p.Config.Entry<bool>("LowerAIPhysics").Value = false; PerformanceFeature.Tick();
Check(!Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && ai.iAILowerQualityPhysicsUpdateEveryOnlyX == 4 && ai.Motor!.bLowerPhysicsQualityForAI && ai.Motor.iLowerQualityPhysicsUpdateEveryOnlyX == 3, "disable restores original controller and motor state despite repeated prefix");
Check(human.iAILowerQualityPhysicsUpdateEveryOnlyX == 3 && ghost.iAILowerQualityPhysicsUpdateEveryOnlyX == 4, "restoration never changes human/ghost intervals");
p.Config.Entry<bool>("LowerAIPhysics").Value = true; Call("AiPrefix", ai); SimulateNativeAi(ai); Plugin.OfflineLabAllowed = false; PerformanceFeature.Tick();
Check(!Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && ai.iAILowerQualityPhysicsUpdateEveryOnlyX == 4 && ai.Motor!.iLowerQualityPhysicsUpdateEveryOnlyX == 3, "online transition restores AI state");
Plugin.OfflineLabAllowed = true; p.Config.Entry<int>("AIPhysicsInterval").Value = 1; Call("AiPrefix", ai);
Check(!(bool)Field("_aiOwned")! && ai.iAILowerQualityPhysicsUpdateEveryOnlyX == 4, "interval1 acquires no nativeoverride");
p.Config.Entry<int>("AIPhysicsInterval").Value = 9; Call("AiPrefix", ai);
Check(ai.iAILowerQualityPhysicsUpdateEveryOnlyX == 4, "out ofrange interval safely clamps to4");
MenuManager.Instance = new MenuManager(); PerformanceFeature.Tick();
Check(!(bool)Field("_aiOwned")! && !Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles, "menu transition restores AI ownership");
MenuManager.Instance = null; Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles = true;
Call("AiPrefix", ai); SimulateNativeAi(ai); p.Config.Entry<bool>("Enabled").Value = false; PerformanceFeature.Tick();
Check(Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && ai.iAILowerQualityPhysicsUpdateEveryOnlyX == 4, "master disable preserves originally enabled nativeAI qualitypolicy");
Enable(p);
MenuManager.Instance = null; p.Config.Entry<int>("AIPhysicsInterval").Value = 2;
var doomed = Kart(Ant_Player.EPlayerType.E_AI_LOCAL); Call("AiPrefix", doomed); doomed.Destroyed = true; Time.unscaledTime = 2; PerformanceFeature.Tick();
Check(((IDictionary)Field("AiOriginals")!).Count == 0, "destroyed controller removed from saved AI registry");

p = Reset(); Enable(p); GarbageCollector.GCMode = GarbageCollector.Mode.Disabled; PerformanceFeature.Tick();
Check(GarbageCollector.GCMode == GarbageCollector.Mode.Enabled && (bool)Field("_gcOwned")!, "midrace activation enables managed collection");
MenuManager.Instance = new MenuManager();
Ant_CurrentGameConfiguration.Policy = () => GarbageCollector.GCMode = MenuManager.Instance == null ? GarbageCollector.Mode.Disabled : GarbageCollector.Mode.Enabled;
p.Config.Entry<bool>("Enabled").Value = false; PerformanceFeature.Tick();
Check(Ant_CurrentGameConfiguration.PolicyCalls == 1 && GarbageCollector.GCMode == GarbageCollector.Mode.Enabled, "disable in menu consults current policy instead of restoring staleDisabled");
Enable(p); MenuManager.Instance = null; GarbageCollector.GCMode = GarbageCollector.Mode.Disabled; Call("GcPostfix");
Check(GarbageCollector.GCMode == GarbageCollector.Mode.Enabled, "native race disable hook allows optin collection");
Plugin.OfflineLabAllowed = false; PerformanceFeature.Tick();
Check(Ant_CurrentGameConfiguration.PolicyCalls == 2 && GarbageCollector.GCMode == GarbageCollector.Mode.Disabled && !(bool)Field("_gcOwned")!, "online transition restores current nativepolicy");
Plugin.OfflineLabAllowed = true; GarbageCollector.GCMode = GarbageCollector.Mode.Manual; PerformanceFeature.Tick();
Check(GarbageCollector.GCMode == GarbageCollector.Mode.Manual && !(bool)Field("_gcOwned")!, "external manualGC policy is preserved");
GarbageCollector.GCMode = GarbageCollector.Mode.Disabled; PerformanceFeature.Tick(); GarbageCollector.GCMode = GarbageCollector.Mode.Manual;
int policyCalls = Ant_CurrentGameConfiguration.PolicyCalls; PerformanceFeature.Restore();
Check(GarbageCollector.GCMode == GarbageCollector.Mode.Manual && Ant_CurrentGameConfiguration.PolicyCalls == policyCalls, "restore avoids clobbering another GC owner");
p = Reset(); Enable(p); Plugin.OfflineLabAllowed = false; GarbageCollector.GCMode = GarbageCollector.Mode.Disabled; Call("GcPostfix");
Check(GarbageCollector.GCMode == GarbageCollector.Mode.Disabled, "online GC hook leaves nativepolicy intact");
p = Reset(); Enable(p); MenuManager.Instance = new MenuManager(); GarbageCollector.GCMode = GarbageCollector.Mode.Disabled; PerformanceFeature.Tick();
Check(GarbageCollector.GCMode == GarbageCollector.Mode.Disabled && !(bool)Field("_gcOwned")!, "menu tick does not acquire raceGC ownership");

// A disappearing IL2CPP object can throw from setters even after a successful native null check.
// Acquire ownership first, then break its restoration writes while another saved AI remains valid.
foreach (string fault in new[] { "controller", "motor-cadence", "motor-quality", "direct-restore" })
{
    p = Reset(); Enable(p); p.Config.Entry<bool>("LowerAIPhysics").Value = true;
    var broken = Kart(Ant_Player.EPlayerType.E_AI_LOCAL, 4);
    var healthy = Kart(Ant_Player.EPlayerType.E_AI_LOCAL, 3);
    Call("AiPrefix", broken); SimulateNativeAi(broken);
    Call("AiPrefix", healthy); SimulateNativeAi(healthy);
    Camera.Cameras.Add(new()); Graphics(new());
    GarbageCollector.GCMode = GarbageCollector.Mode.Disabled; PerformanceFeature.Tick();
    Check(Field("_cameras") != null && (bool)Field("_gcOwned")!, fault + ": precondition camera/GC ownership acquired");
    if (fault == "controller") broken.ThrowIntervalSet = true;
    else if (fault == "motor-quality") broken.Motor!.ThrowQualitySet = true;
    else broken.Motor!.ThrowIntervalSet = true;
    MenuManager.Instance = new MenuManager();
    Ant_CurrentGameConfiguration.Policy = () => GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
    p.Config.Entry<bool>("LowerAIPhysics").Value = false;
    if (fault == "direct-restore")
    {
        bool threwAggregate = false;
        try { PerformanceFeature.Restore(); }
        catch (InvalidOperationException ex) { threwAggregate = ex.Message.Contains("AI restoration was incomplete"); }
        Check(threwAggregate, "direct restore reports incompleteAI restoration after best effort");
    }
    else
    {
        // This reflection call throws if the restore exception escapes the production prefix.
        Call("AiPrefix", broken);
        Check((bool)Field("_faulted")! && p.Log.Errors.Count == 1, fault + ": prefix catches restoration failure and suspends module");
        Check(Graphics(new()), fault + ": subsequent graphics use native fallback");
    }
    Check(!Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles && !(bool)Field("_aiOwned")!, fault + ": globalAI policy restored despite setter failure");
    Check(healthy.iAILowerQualityPhysicsUpdateEveryOnlyX == 3 && healthy.Motor!.bLowerPhysicsQualityForAI && healthy.Motor.iLowerQualityPhysicsUpdateEveryOnlyX == 3, fault + ": later healthyAI record fully restored");
    Check(((IDictionary)Field("AiOriginals")!).Count == 0, fault + ": saved records released after best effort");
    Check(Ant_CurrentGameConfiguration.PolicyCalls == 1 && GarbageCollector.GCMode == GarbageCollector.Mode.Enabled && !(bool)Field("_gcOwned")!, fault + ": GC consults current menupolicy despite failedAI restoration");
    Check(Field("_cameras") == null && ((Il2CppStructArray<float>?[])Field("CullDistances")!).All(buffer => buffer == null), fault + ": camera and cullingbuffers always released");
    if (fault == "controller")
        Check(broken.Motor!.iLowerQualityPhysicsUpdateEveryOnlyX == 3, "controller restoration failure still attempts same record motor restoration");
}
var sample = new PerformanceSamples(2);
Check(sample.FrameP95Ms() == null, "empty percentile unavailable");
sample.Frame(double.NaN); sample.Frame(double.PositiveInfinity); sample.Frame(-1); sample.Frame(0);
Check(sample.Frames == 0, "invalid frame samples excluded");
for (int i = 0; i < 95; i++) sample.Frame(.01);
for (int i = 0; i < 5; i++) sample.Frame(.1);
Check(sample.Frames == 100 && sample.FrameP95Ms() == 10.25, "p95 uses exact histogram rank with upper bound");
Check(Math.Abs(sample.MaxFrameMs - 100) < .001, "raw maximum retained");
sample.Method(0, 10); sample.Method(0, 30); sample.Method(1, -1);
Check(sample.Calls[0] == 2 && sample.Ticks[0] == 40 && sample.MaxTicks[0] == 30 && sample.Calls[1] == 0, "method aggregation independent and ignores invalid elapsed");
sample.Reset(); sample.Frame(.201);
Check(sample.FrameP95Ms() == null && sample.MaxFrameMs == 201, "overflow percentile is not reported as false exact value");
sample.Reset(); Check(sample.Frames == 0 && sample.Ticks[0] == 0 && sample.MaxFrameMs == 0, "window reset clears all metrics");

Type diagnostic = typeof(PerformanceDiagnostics);
object? DField(string name) => diagnostic.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
void DSet(string name, object? value) => diagnostic.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, value);
object? DCall(string name, params object?[] args) => diagnostic.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
var hooks = (HarmonyLib.Harmony)DField("Hooks")!;
var metrics = (PerformanceSamples)DField("Samples")!;
Plugin ResetDiagnostic()
{
    PerformanceDiagnostics.Stop();
    foreach (string field in new[] { "_inRace", "_hooked", "_recording", "_completed", "_faulted" }) DSet(field, false);
    DSet("_file", null); hooks.FailAt = 0;
    var plugin = Reset(); Time.timeScale = 1; Application.isFocused = true;
    PerformanceDiagnostics.Install(plugin); return plugin;
}
void BeginCapture(Plugin plugin)
{
    plugin.Config.Entry<bool>("PerformanceDiagnostics", "Enabled").Value = true;
    PerformanceDiagnostics.Tick(); DSet("_warmUntil", -1d); PerformanceDiagnostics.Tick();
}
p = ResetDiagnostic(); PerformanceDiagnostics.Tick();
Check(hooks.Targets.Count == 0, "disabled diagnostics have no timing detours");
Check(!p.Config.Entry<bool>("PerformanceDiagnostics", "Enabled").Value, "diagnostics default off");
BeginCapture(p);
Check(hooks.Targets.Count == 10 && (bool)DField("_recording")!, "capture arms all exact native targets after warmup");
Check(!p.Config.Entry<bool>("Enabled").Value && !p.SessionModified && !Ant_CurrentGameConfiguration.bLowerPhysicsQualityOnAIVehicles, "diagnostics do not enable optimization or change simulation");
var target = typeof(PixelEasyCharMoveKartController).GetMethod("FixedUpdate")!;
object?[] state = { 0L }; DCall("Before", state);
DCall("After", target, (long)state[0]!);
Check(metrics.Calls[0] == 1, "production callbacks aggregate a dispatched target");
metrics.Calls[0] = 126;
var observed = Kart(Ant_Player.EPlayerType.E_AI_LOCAL); observed.Motor!.bLowerPhysicsQualityForAI = true; observed.Motor.iLowerQualityPhysicsUpdateEveryOnlyX = 4;
DCall("ControllerAfter", observed, target, System.Diagnostics.Stopwatch.GetTimestamp());
Check((int)DField("_aiSamples")! == 1 && (int)DField("_lowerAiSamples")! == 1 && (int)DField("_observedInterval")! == 4, "samples read actual post-native AI motor flags");
metrics.Frame(.01); metrics.Frame(.02);
DSet("_windowStart", System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency - 10);
DSet("_captureUntil", -1d); PerformanceDiagnostics.Tick();
string output = (string)DField("_file")!;
using (var json = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadLines(output).First()))
{
    var root = json.RootElement;
    Check(root.GetProperty("methods").GetArrayLength() == 10 && root.GetProperty("frames").GetInt32() >= 2, "report is parseable and contains method and frame evidence");
    Check(root.GetProperty("observedLowerAiMotorSamples").GetInt32() == 1 && root.GetProperty("lastObservedAiMotorInterval").GetInt32() == 4, "report includes observed cadence rather than selected setting only");
}
Check(hooks.Targets.Count == 0 && (bool)DField("_completed")!, "bounded capture removes detours");
PerformanceDiagnostics.Tick(); Check(hooks.Targets.Count == 0, "completed race does not rearm every frame");
MenuManager.Instance = new(); PerformanceDiagnostics.Tick(); MenuManager.Instance = null;
PerformanceDiagnostics.Tick(); DSet("_warmUntil", -1d); PerformanceDiagnostics.Tick();
Check(hooks.Targets.Count == 10, "next race can capture without relaunch");
Plugin.OfflineLabAllowed = false; PerformanceDiagnostics.Tick();
Check(hooks.Targets.Count == 0 && !(bool)DField("_recording")!, "online transition stops capture and detours");
p = ResetDiagnostic(); BeginCapture(p); Application.isFocused = false; PerformanceDiagnostics.Tick();
Check(hooks.Targets.Count == 0, "unfocused window not mistaken for race slowdown");
p = ResetDiagnostic(); BeginCapture(p); p.Config.Entry<bool>("PerformanceDiagnostics", "Enabled").Value = false; PerformanceDiagnostics.Tick();
Check(hooks.Targets.Count == 0, "live disable removes only diagnostic hooks");
p = ResetDiagnostic(); hooks.FailAt = 3; BeginCapture(p);
Check(hooks.Targets.Count == 0 && (bool)DField("_faulted")! && p.Log.Warnings.Count != 0, "partial hook installation cleans up and fails safely");
p = ResetDiagnostic(); BeginCapture(p); metrics.Frame(.01);
DSet("_file", BepInEx.Paths.BepInExRootPath); // directory cannot be opened as a report file
PerformanceDiagnostics.Stop();
Check(hooks.Targets.Count == 0 && (bool)DField("_faulted")!, "report write failure does not escape shutdown or retain hot hooks");
Console.WriteLine($"Production performance and diagnostics: {assertions} assertions passed (stub APIs; no engine/game runtime proof).");

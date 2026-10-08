using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace HelloMod;

[BepInPlugin("local.tk2.hellomod", "HelloMod", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    private readonly Harmony _harmony = new("local.tk2.hellomod");

    public override void Load()
    {
        var enabled = Config.Bind("General", "Enabled", false, "Log when a race initializes.");
        // Always specify parameter types. Updated games can introduce overloads.
        var target = AccessTools.DeclaredMethod(typeof(Ant_MainGame), "Start", Type.EmptyTypes);
        if (target == null) { Log.LogWarning("Ant_MainGame.Start() unavailable; feature disabled."); return; }
        OnStarted = () => { if (enabled.Value) Log.LogInfo("Race initialized — HelloMod is running."); };
        _harmony.Patch(target, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterStart)));
    }

    private static Action? OnStarted;
    private static void AfterStart() => OnStarted?.Invoke();
    public override bool Unload() { _harmony.UnpatchSelf(); OnStarted = null; return true; }
}

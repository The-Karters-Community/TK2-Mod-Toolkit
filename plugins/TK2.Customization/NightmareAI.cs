using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

// Readable current-build port of NightmareAIs 1.2.0 from the supplied managed DLL.
// Keep every baseline per object; the old mod used one shared speed baseline.
internal static class NightmareAI
{
    private static Plugin? _plugin;
    private static ConfigEntry<bool> _enabled = null!, _debug = null!, _rubberband = null!, _customNames = null!, _champion = null!;
    private static ConfigEntry<float> _aiSpeed = null!, _humanSpeed = null!, _aiReserve = null!, _humanReserve = null!, _aiDamage = null!, _humanDamage = null!, _aiWall = null!, _humanWall = null!, _rubberDistance = null!, _rubberSpeed = null!;
    private static ConfigEntry<int> _aiHealth = null!, _humanHealth = null!, _aiExtra = null!, _humanExtra = null!;
    private static ConfigEntry<string> _names = null!;
    private static float _nextScan;
    private static string _namesCurrent = "";
    private static PTK_AIPlayersDifficultyManager.EDifficultyType? _difficulty;
    private static readonly Dictionary<int, (PixelKartPhysics Kart, float Speed)> Karts = new();
    private static readonly Dictionary<int, (HpBarController Hp, int Maximum, int Extra, int Starting)> Health = new();
    private static readonly Dictionary<int, (Ant_Player Player, string Name)> Names = new();
    private static readonly Dictionary<int, (AIDistToTargetBehavController Ai, AIDistToTargetBehavController.EAI_PLAYER_TYPE Type, AIDistToTargetBehavController.ECurrentDrivingState State)> Driving = new();
    [ThreadStatic] private static HashSet<int>? _damageActive;

    internal static void Install(Plugin plugin)
    {
        Restore();
        _plugin = plugin;
        _nextScan = 0;
        _namesCurrent = "";
        var cfg = plugin.Config;
        _enabled = cfg.Bind("NightmareAI", "Enabled", false, "Offline Nightmare AI pack. AI and human tuning, champion behavior and catch-up. Default off.");
        _debug = cfg.Bind("NightmareAI", "DebugLogging", false, "Log capability setup and damage changes without per-frame spam.");
        _aiSpeed = cfg.Bind("NightmareAI", "AISpeedMultiplier", 1.2f, new ConfigDescription("AI forward-speed multiplier.", new AcceptableValueRange<float>(0.1f, 20f)));
        _aiReserve = cfg.Bind("NightmareAI", "AIReserveGainMultiplier", 5f, new ConfigDescription("AI reserve gain multiplier.", new AcceptableValueRange<float>(0f, 20f)));
        _aiDamage = cfg.Bind("NightmareAI", "AIDamageMultiplier", 0.5f, new ConfigDescription("Multiplier of damage received by AI.", new AcceptableValueRange<float>(0f, 20f)));
        _aiHealth = cfg.Bind("NightmareAI", "AIMaxHealth", 300, new ConfigDescription("AI maximum/starting HP at race reset.", new AcceptableValueRange<int>(1, 10000)));
        _aiExtra = cfg.Bind("NightmareAI", "AIMaxExtraHealth", 500, new ConfigDescription("AI HP ceiling with overflow healing.", new AcceptableValueRange<int>(1, 10000)));
        _aiWall = cfg.Bind("NightmareAI", "AIReserveLossMultiplierOnCollision", 0.9f, new ConfigDescription("Fraction of reserve retained by AI after a wall collision.", new AcceptableValueRange<float>(0f, 1f)));
        _rubberband = cfg.Bind("NightmareAI", "AIIsRubberbandingEnabled", true, "Enable extra catch-up speed when an AI falls behind its human target.");
        _rubberDistance = cfg.Bind("NightmareAI", "AIRubberbandingMinimumDistance", 200f, new ConfigDescription("Minimum target distance for catch-up speed.", new AcceptableValueRange<float>(0f, 5000f)));
        _rubberSpeed = cfg.Bind("NightmareAI", "AIRubberbandingSpeedMultiplier", 10f, new ConfigDescription("Additional catch-up speed multiplier.", new AcceptableValueRange<float>(1f, 20f)));
        _customNames = cfg.Bind("NightmareAI", "AIUseCustomNames", false, "Use the configurable community names for AI racers.");
        _names = cfg.Bind("NightmareAI", "Names", "CPU3,Mia,Bobby,Bubble,Sir Ronald,Pablo,Zig,Zag,Zoom,Otto,sboczek,Edward,Mortus,Basia,Tazua,Delecto,VyZiXeN,Arca,Silver Nargacuga,Justin,gub,NinjaMonkey,Tasmaniac,DarkSlayer,M.K.,Cathret,Tobi,Kolfo,rudolph,Matt,Nimo,Ugo,Goatse,KingKingTTV,Makys,SuperChris,HighLanderPony,Seren", "Comma-separated AI names. Empty keeps normal names.");
        _champion = cfg.Bind("NightmareAI", "ForceChampionAndChase", true, "Force champion difficulty and AI chasing behavior as in the original mod.");
        _humanSpeed = cfg.Bind("NightmareAI", "PlayerSpeedMultiplier", 1f, new ConfigDescription("Local human forward-speed multiplier.", new AcceptableValueRange<float>(0.1f, 20f)));
        _humanReserve = cfg.Bind("NightmareAI", "PlayerReserveGainMultiplier", 1f, new ConfigDescription("Local human reserve gain multiplier.", new AcceptableValueRange<float>(0f, 20f)));
        _humanDamage = cfg.Bind("NightmareAI", "PlayerDamageMultiplier", 1f, new ConfigDescription("Multiplier of damage received by local humans.", new AcceptableValueRange<float>(0f, 20f)));
        _humanHealth = cfg.Bind("NightmareAI", "PlayerMaxHealth", 300, new ConfigDescription("Local human maximum/starting HP at race reset.", new AcceptableValueRange<int>(1, 10000)));
        _humanExtra = cfg.Bind("NightmareAI", "PlayerMaxExtraHealth", 500, new ConfigDescription("Local human HP ceiling with overflow healing.", new AcceptableValueRange<int>(1, 10000)));
        _humanWall = cfg.Bind("NightmareAI", "PlayerReserveLossMultiplierOnCollision", 0.7f, new ConfigDescription("Fraction of reserves retained by local humans after a wall collision.", new AcceptableValueRange<float>(0f, 1f)));
        Patch(typeof(PixelKartPhysics), "Update", Type.EmptyTypes, nameof(SpeedPrefix));
        Patch(typeof(Ant_BoostManager), "AddReserves", new[] { typeof(float) }, nameof(ReservePrefix));
        Patch(typeof(Ant_BoostManager), "WallCollisionOccuredInTimeFromLastOne", Type.EmptyTypes, nameof(WallPrefix), nameof(WallPostfix));
        Patch(typeof(HpBarController), "RaceResettedPrepareForNewOne", Type.EmptyTypes, nameof(HealthPrefix));
        Patch(typeof(Ant_Player), "Update", Type.EmptyTypes, null, nameof(BehaviorPostfix));
        Patch(typeof(HpBarController), "Hit", new[] { typeof(int), typeof(int), typeof(PixelWeaponObject.EWeaponType) }, nameof(DamagePrefix), nameof(DamagePostfix), nameof(DamageFinalizer));
        Patch(typeof(HpBarController), "Hit", new[] { typeof(int), typeof(int), typeof(PixelWeaponObject.EWeaponType), typeof(PTK_Command_05_PlayerLogicEffects.EPlayerDamageVFXType) }, nameof(DamagePrefix), nameof(DamagePostfix), nameof(DamageFinalizer));
    }

    private static void Patch(Type type, string method, Type[] signature, string? prefix, string? postfix = null, string? finalizer = null)
    {
        try
        {
            var target = AccessTools.DeclaredMethod(type, method, signature) ?? throw new MissingMethodException(type.FullName, method);
            _plugin!.Harmony.Patch(target, prefix == null ? null : new HarmonyMethod(typeof(NightmareAI), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(NightmareAI), postfix), null,
                finalizer == null ? null : new HarmonyMethod(typeof(NightmareAI), finalizer), null);
            _plugin.Log.LogInfo($"Nightmare AI capability: {type.Name}.{method} ({signature.Length} arguments)");
        }
        catch (Exception ex) { _plugin!.Log.LogWarning($"Nightmare AI capability unavailable: {type.Name}.{method}: {ex.Message}"); }
    }

    private static bool Allowed => _plugin != null && _enabled.Value && Plugin.OfflineLabAllowed;
    private static bool Local(Ant_Player? player) => player != null && player.ePlayerType is Ant_Player.EPlayerType.E_AI_LOCAL or Ant_Player.EPlayerType.E_HUMAN_LOCAL;
    private static bool Ai(Ant_Player player) => player.ePlayerType == Ant_Player.EPlayerType.E_AI_LOCAL;
    private static void Fail(Exception exception)
    {
        _plugin?.Log.LogWarning($"Nightmare AI disabled after runtime failure: {exception.Message}");
        if (_enabled != null) _enabled.Value = false;
    }

    private static void SpeedPrefix(PixelKartPhysics __instance)
    {
        if (!Allowed) return;
        try
        {
            var player = __instance.kartController?.parentPlayer;
            if (!Local(player)) return;
            int id = __instance.GetInstanceID();
            if (!Ai(player!) && (_humanSpeed.Value == 1 || LegacyMK.OverridesSpeed || Plugin.Instance!.TuningEnabled.Value)) {
                ReleaseSpeed(__instance); return;
            }
            if (!Karts.TryGetValue(id, out var baseline))
            { baseline = (__instance, __instance.max_speed_accel_forward); Karts[id] = baseline; }
            float multiplier = Ai(player!) ? _aiSpeed.Value : _humanSpeed.Value;
            if (Ai(player!) && _rubberband.Value && player!.aiController != null && player.aiController.aiDistController != null)
            {
                var controller = player.aiController.aiDistController;
                var target = controller.GetCurrentTargetPlayer();
                if (target != null && target.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && target.playerRaceLogic != null && player.playerRaceLogic != null &&
                    target.playerRaceLogic.GetCurrentPlayerRacePositionIndex() < player.playerRaceLogic.GetCurrentPlayerRacePositionIndex() &&
                    Mathf.Abs(controller.fDistanceToTargetPlayer) >= _rubberDistance.Value) multiplier *= _rubberSpeed.Value;
            }
            // Recompute on every frame, so catch-up speed stops once the condition ends.
            __instance.max_speed_accel_forward = baseline.Speed * multiplier;
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void ReservePrefix(Ant_BoostManager __instance, ref float __0)
    {
        if (!Allowed || !Local(__instance.kartController?.parentPlayer)) return;
        __0 *= Ai(__instance.kartController!.parentPlayer) ? _aiReserve.Value : _humanReserve.Value;
    }
    internal static void ReleaseSpeed(PixelKartPhysics kart)
    {
        if (Karts.Remove(kart.GetInstanceID(), out var baseline) && baseline.Kart != null)
            baseline.Kart.max_speed_accel_forward = baseline.Speed;
    }

    private static void WallPrefix(Ant_BoostManager __instance, out float? __state)
    {
        __state = Allowed && Local(__instance.kartController?.parentPlayer) ? __instance.GetCurrentBoostReservesTime() : null;
    }
    private static void WallPostfix(Ant_BoostManager __instance, float? __state)
    {
        if (__state == null || !Allowed) return;
        __instance.fCurrentLoadedLateralVelocity = 0;
        __instance.fBoostingReserves = __state.Value * (Ai(__instance.kartController.parentPlayer) ? _aiWall.Value : _humanWall.Value);
    }

    private static void DamagePrefix(HpBarController __instance, ref int __0, out bool __state)
    {
        __state = false;
        if (!Allowed || !Local(__instance.player)) return;
        _damageActive ??= new HashSet<int>();
        __state = _damageActive.Add(__instance.GetInstanceID());
        if (!__state) return;
        float multiplier = Ai(__instance.player) ? _aiDamage.Value : _humanDamage.Value;
        int previous = __0;
        __0 = (int)Math.Clamp((double)__0 * multiplier, 0, int.MaxValue);
        if (_debug.Value) _plugin!.Log.LogInfo($"Nightmare AI damage: {previous} -> {__0}");
    }
    private static void DamagePostfix(HpBarController __instance, bool __state)
    { if (__state) _damageActive?.Remove(__instance.GetInstanceID()); }
    private static Exception? DamageFinalizer(HpBarController __instance, bool __state, Exception? __exception)
    { DamagePostfix(__instance, __state); return __exception; }

    private static void HealthPrefix(HpBarController __instance)
    {
        if (!Allowed || !Local(__instance.player)) return;
        try { ApplyHealth(__instance); } catch (Exception ex) { Fail(ex); }
    }

    private static void BehaviorPostfix(Ant_Player __instance)
    {
        if (!Allowed || !_champion.Value || !Ai(__instance)) return;
        try
        {
            var ai = __instance.aiController?.aiDistController;
            if (ai == null) return;
            int id = ai.GetInstanceID();
            if (!Driving.ContainsKey(id)) Driving[id] = (ai, ai.eAIPlayerType, ai.eDrivingState);
            ai.eAIPlayerType = AIDistToTargetBehavController.EAI_PLAYER_TYPE.E_STAYING_IN_FRONT_OF_TOP_HPLAYER;
            ai.eDrivingState = AIDistToTargetBehavController.ECurrentDrivingState.E_CHASING_TARGET_PLAYER;
            _difficulty ??= Ant_CurrentGameConfiguration.eAIDifficulty;
            Ant_CurrentGameConfiguration.eAIDifficulty = PTK_AIPlayersDifficultyManager.EDifficultyType.E4_CHAMPION;
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static void ApplyHealth(HpBarController hp)
    {
        int id = hp.GetInstanceID();
        if (!Health.ContainsKey(id)) Health[id] = (hp, hp.maxHp_Default, hp.maxHpWithExtraOverflow, hp.startingHp);
        bool ai = Ai(hp.player);
        int maximum = ai ? _aiHealth.Value : _humanHealth.Value;
        hp.startingHp = hp.maxHp_Default = maximum;
        hp.maxHpWithExtraOverflow = Math.Max(maximum, ai ? _aiExtra.Value : _humanExtra.Value);
    }

    internal static void Tick()
    {
        if (!Allowed) { RestoreValues(); return; }
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + .5f;
        try
        {
            if (_namesCurrent != _names.Value) { RestoreNames(); _namesCurrent = _names.Value; }
            bool anyAi = false;
            foreach (var player in UnityEngine.Object.FindObjectsOfType<Ant_Player>())
            {
                if (!Local(player)) continue;
                if (player.hpBarController != null) ApplyHealth(player.hpBarController);
                if (!Ai(player)) continue;
                anyAi = true;
                if (_customNames.Value && !string.IsNullOrWhiteSpace(_names.Value))
                {
                    int id = player.GetInstanceID();
                    if (!Names.ContainsKey(id))
                    {
                        var names = new List<string>();
                        foreach (string name in _names.Value.Split(',')) if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
                        if (names.Count > 0) { Names[id] = (player, player.GetPlayerName()); player.SetPlayerName(names[UnityEngine.Random.Range(0, names.Count)]); }
                    }
                }
                var ai = player.aiController?.aiDistController;
                if (ai == null || !_champion.Value) continue;
                int controllerId = ai.GetInstanceID();
                if (!Driving.ContainsKey(controllerId)) Driving[controllerId] = (ai, ai.eAIPlayerType, ai.eDrivingState);
                ai.eAIPlayerType = AIDistToTargetBehavController.EAI_PLAYER_TYPE.E_STAYING_IN_FRONT_OF_TOP_HPLAYER;
                ai.eDrivingState = AIDistToTargetBehavController.ECurrentDrivingState.E_CHASING_TARGET_PLAYER;
            }
            if (!_customNames.Value || string.IsNullOrWhiteSpace(_names.Value)) RestoreNames();
            if (anyAi && _champion.Value)
            {
                _difficulty ??= Ant_CurrentGameConfiguration.eAIDifficulty;
                Ant_CurrentGameConfiguration.eAIDifficulty = PTK_AIPlayersDifficultyManager.EDifficultyType.E4_CHAMPION;
            }
            else RestoreDriving();
            PruneDestroyed();
        }
        catch (Exception ex) { Fail(ex); RestoreValues(); }
    }

    private static void PruneDestroyed()
    {
        foreach (int key in new List<int>(Karts.Keys)) if (Karts[key].Kart == null) Karts.Remove(key);
        foreach (int key in new List<int>(Health.Keys)) if (Health[key].Hp == null) Health.Remove(key);
        foreach (int key in new List<int>(Names.Keys)) if (Names[key].Player == null) Names.Remove(key);
        foreach (int key in new List<int>(Driving.Keys)) if (Driving[key].Ai == null) Driving.Remove(key);
    }
    private static void RestoreNames()
    {
        foreach (var baseline in Names.Values) if (baseline.Player != null) baseline.Player.SetPlayerName(baseline.Name);
        Names.Clear();
    }
    private static void RestoreDriving()
    {
        foreach (var baseline in Driving.Values) if (baseline.Ai != null) { baseline.Ai.eAIPlayerType = baseline.Type; baseline.Ai.eDrivingState = baseline.State; }
        Driving.Clear();
        if (_difficulty != null) { Ant_CurrentGameConfiguration.eAIDifficulty = _difficulty.Value; _difficulty = null; }
    }
    private static void RestoreValues()
    {
        foreach (var baseline in Karts.Values) if (baseline.Kart != null) baseline.Kart.max_speed_accel_forward = baseline.Speed;
        Karts.Clear();
        foreach (var baseline in Health.Values) if (baseline.Hp != null)
        { baseline.Hp.maxHp_Default = baseline.Maximum; baseline.Hp.maxHpWithExtraOverflow = baseline.Extra; baseline.Hp.startingHp = baseline.Starting; }
        Health.Clear();
        RestoreNames(); RestoreDriving();
        _damageActive?.Clear();
    }
    internal static void Restore() { RestoreValues(); _plugin = null; }
}

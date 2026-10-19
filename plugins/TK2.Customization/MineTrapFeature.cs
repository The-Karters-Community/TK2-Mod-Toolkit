using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using ItemEffects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static class MineTrapFeature
{
    private static ConfigEntry<bool> Enabled = null!, ItemBoxes = null!, OwnerCanTrigger = null!;
    private static ConfigEntry<KeyCode> DropKey = null!;
    private static ConfigEntry<string> Controller = null!;
    private static ConfigEntry<float> Chance = null!, ArmDelay = null!, Lifetime = null!, Radius = null!, Behind = null!;
    private static ConfigEntry<int> Ammo = null!, Damage = null!;
    private sealed record Trap(MineBehavior Mine, Ant_Player Owner, float ArmAt, float Expires, float Radius);
    private sealed record WeaponCopy(WeaponsView View, PixelWeaponObject Weapon);
    private static readonly Dictionary<int, Trap> Traps = new();
    private static readonly List<WeaponCopy> Copies = new();
    private static readonly Dictionary<int, (PixelWeaponObject Weapon, bool RequireTarget, PixelWeaponObject.EItemTargetType Target)> Originals = new();
    private static readonly Collider[] Hits = new Collider[128];
    private static readonly Dictionary<int, int> LastDropFrame = new();
    private static MineBehavior? Prefab;
    private static int Scene;
    private static float NextCheck;
    private static string Status = "Mine traps: waiting for a race";
    private static bool Installed;

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("MineTraps", "Enabled", false, "Offline revival of the game's dormant MINA_09 mine. Runtime validation pending.");
        ItemBoxes = p.Config.Bind("MineTraps", "ItemBoxRewards", true, "Include mines in local item-box rewards, using the normal inventory and fire button.");
        Chance = p.Config.Bind("MineTraps", "ItemBoxChancePercent", 20f, new ConfigDescription("Chance to replace the first reward in an item box with mines.", new AcceptableValueRange<float>(0, 100)));
        Ammo = p.Config.Bind("MineTraps", "RewardAmmo", 1, "Mines awarded per selected item box. Suggested 1-3; no upper limit.");
        DropKey = p.Config.Bind("MineTraps", "DropKey", KeyCode.M, "Extra testing shortcut: drop a mine without using inventory ammo. Choose None to disable.");
        Controller = p.Config.Bind("MineTraps", "ControllerAction", "", "Optional physical controller button for the extra drop shortcut.");
        ArmDelay = p.Config.Bind("MineTraps", "ArmDelay", .6f, "Arming delay in seconds; suggested 0.5-1.5. No upper limit.");
        Lifetime = p.Config.Bind("MineTraps", "Lifetime", 45f, "Lifetime in seconds; suggested 20-60. No upper limit.");
        Radius = p.Config.Bind("MineTraps", "TriggerRadius", 2f, "Trigger radius in metres; suggested 1-3. No upper limit.");
        Behind = p.Config.Bind("MineTraps", "DropDistance", 4f, "Distance behind the kart in metres; suggested 3-6. No upper limit.");
        Damage = p.Config.Bind("MineTraps", "Damage", 0, "Zero keeps the native mine damage; positive values override it. No upper limit.");
        OwnerCanTrigger = p.Config.Bind("MineTraps", "OwnerCanTrigger", false, "Allow a mine to hit its owner after arming.");
        FeatureHook.Install(p.Harmony, typeof(MineTrapFeature), Hooks());
        Installed = true;
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(Ant_KartInput), nameof(Ant_KartInput.ProcessRacingInput), Type.EmptyTypes, Postfix: nameof(InputPostfix)),
        new FeatureHook(typeof(WeaponsController), nameof(WeaponsController.WeaponBoxReward_AddWeapon), new[] { typeof(PixelWeaponObject.EWeaponType), typeof(int), typeof(PixelWeaponObject.EWeaponType), typeof(int), typeof(bool) }, Prefix: nameof(RewardPrefix)),
        new FeatureHook(typeof(WeaponsView), nameof(WeaponsView.GetWeapon), new[] { typeof(PixelWeaponObject.EWeaponType) }, Prefix: nameof(GetWeaponPrefix)),
        new FeatureHook(typeof(WeaponsView), nameof(WeaponsView.FireWeapon), new[] { typeof(int), typeof(bool), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>), typeof(bool), typeof(int), typeof(int), typeof(int) }, Prefix: nameof(FirePrefix)),
        new FeatureHook(typeof(MineBehavior), nameof(MineBehavior.OnTriggerStay), new[] { typeof(Collider) }, Prefix: nameof(TriggerPrefix)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(Reset))
    };
    private static bool Allowed(Ant_Player? owner) => Installed && Enabled.Value && Plugin.OfflineLabAllowed &&
        owner?.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL && MenuManager.Instance == null &&
        Ant_CurrentGameConfiguration.eCurrentRaceState == Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING;

    private static MineBehavior Source()
    {
        if (Prefab != null) return Prefab;
        var asset = Resources.Load<GameObject>("mineeffect");
        Prefab = asset?.GetComponent<MineBehavior>();
        return Prefab ?? throw new InvalidOperationException("The game's Resources/mineeffect prefab is unavailable.");
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Allowed(__instance.antPlayer) || Time.timeScale <= 0) return;
        try {
            if (Input.GetKeyDown(DropKey.Value) || ShortcutInput.Read(__instance, Controller.Value)) Drop(__instance.antPlayer, __instance.boostManager.kartController);
        } catch (Exception ex) { Fail(ex); }
    }
    private static void RewardPrefix(WeaponsController __instance, ref PixelWeaponObject.EWeaponType __0, ref int __1, bool __4)
    {
        // Ignore instant grants/restores from practice modules; only normal item-box draw rewards participate.
        if (__4 || !Allowed(__instance.player) || !ItemBoxes.Value || __0 >= PixelWeaponObject.EWeaponType.__WEAPONS_COUNT || UnityEngine.Random.value * 100 >= Chance.Value) return;
        try {
            Source(); // Resolve the real asset before modifying the inventory reward.
            if (Ammo.Value < 1) throw new ArgumentException("Mine ammo must be positive.");
            Plugin.Instance!.SessionModified = true;
            __0 = PixelWeaponObject.EWeaponType.MINA_09; __1 = Ammo.Value;
            Status = $"Item-box reward selected: {Ammo.Value} mine(s)";
            Plugin.Instance.Log.LogInfo(Status);
        } catch (Exception ex) { Fail(ex); }
    }

    private static bool GetWeaponPrefix(WeaponsView __instance, PixelWeaponObject.EWeaponType __0, ref PixelWeaponObject __result)
    {
        if (__0 != PixelWeaponObject.EWeaponType.MINA_09 || !Allowed(__instance.player)) return true;
        try {
            Plugin.Instance!.SessionModified = true;
            foreach (var item in __instance.weapons) if (item != null && item.eWeaponType == __0) {
                if (item.gameObject.name != "Toolkit Mine Inventory" && !Originals.ContainsKey(item.GetInstanceID()))
                    Originals[item.GetInstanceID()] = (item, item.bRequirePlayerTarget, item.targetType);
                item.bRequirePlayerTarget = false; item.targetType = PixelWeaponObject.EItemTargetType.NONE;
                return true;
            }
            // Some character sets omit the dormant mine's inventory model. Create a local slot from a valid
            // weapon shell, retaining its animation references but displaying the game's mine mesh.
            if (__instance.weapons.Count == 0) throw new InvalidOperationException("No weapon shell is available for the mine inventory slot.");
            var mineSource = Source();
            PixelWeaponObject? shell = null;
            foreach (var candidate in __instance.weapons) if (candidate != null) { shell = candidate; break; }
            if (shell == null) throw new InvalidOperationException("No usable weapon shell exists.");
            var go = UnityEngine.Object.Instantiate(shell.gameObject, shell.transform.parent);
            go.name = "Toolkit Mine Inventory"; go.SetActive(false);
            var weapon = go.GetComponent<PixelWeaponObject>();
            weapon.eWeaponType = __0; weapon.parentPlayer = __instance.player;
            weapon.bRequirePlayerTarget = false; weapon.targetType = PixelWeaponObject.EItemTargetType.NONE;
            weapon.autoShoot = false; weapon.firstShotDelay = weapon.shotDelay = weapon.throwDelay = 0;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            __instance.weapons.Add(weapon); Copies.Add(new WeaponCopy(__instance, weapon));
            var model = UnityEngine.Object.Instantiate(mineSource.gameObject, go.transform);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * .35f;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var rb = model.GetComponent<Rigidbody>(); if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            var animator = model.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            weapon.weaponRenderers = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Renderer>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++) weapon.weaponRenderers[i] = renderers[i];
            weapon.additionalEffects = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ParticleSystem>(0);
            Plugin.Instance!.SessionModified = true;
            __result = weapon; return false;
        } catch (Exception ex) { Fail(ex); return true; }
    }

    private static bool FirePrefix(WeaponsView __instance, int __0)
    {
        if (__0 != (int)PixelWeaponObject.EWeaponType.MINA_09 || !Allowed(__instance.player)) return true;
        try {
            foreach (var kart in UnityEngine.Object.FindObjectsByType<PixelEasyCharMoveKartController>(FindObjectsSortMode.None))
                if (kart.parentPlayer == __instance.player) { Drop(__instance.player, kart); return false; }
        } catch (Exception ex) { Fail(ex); }
        return true;
    }

    private static void Drop(Ant_Player owner, PixelEasyCharMoveKartController kart)
    {
        int id = owner.GetInstanceID();
        if (!AirShiftFeature.Running(kart) || LastDropFrame.TryGetValue(id, out int frame) && frame == Time.frameCount) return;
        foreach (float value in new[] { ArmDelay.Value, Lifetime.Value, Radius.Value, Behind.Value })
            if (!float.IsFinite(value) || value < 0) throw new ArgumentException("Mine settings must be finite, non-negative numbers.");
        var forward = kart.kartPhysics.GetMainMotorSteeringOrientation() * Vector3.forward;
        forward.y = 0; forward.Normalize();
        var point = kart.Motor.TransientPosition - forward * Behind.Value;
        int mask = owner.weaponsController.enviroGroundWallColliderMask;
        if (!Physics.Raycast(point + Vector3.up * 3, Vector3.down, out var ground, 30, mask, QueryTriggerInteraction.Ignore)) {
            Status = "Mine not dropped: no track surface behind the kart"; return;
        }
        var prefab = Source();
        Plugin.Instance!.SessionModified = true;
        var mine = UnityEngine.Object.Instantiate(prefab, ground.point + ground.normal * Mathf.Max(.1f, prefab.bounds), Quaternion.identity);
        mine.name = "Toolkit Mine Trap"; mine.SetParentPlayer(owner); mine.customPropParentNo = (int)owner.eAntPlayerNr;
        mine.enabled = false; mine.tryToAttach = false; mine.isActivated = false; mine.exploded = false;
        if (Damage.Value > 0) mine.damage = Damage.Value;
        if (mine.rb != null) { mine.rb.isKinematic = true; mine.rb.useGravity = false; }
        if (mine.mineAnimator != null) mine.mineAnimator.enabled = false;
        mine.gameObject.SetActive(true);
        Traps[mine.GetInstanceID()] = new Trap(mine, owner, Time.time + ArmDelay.Value, Time.time + Lifetime.Value, Radius.Value);
        LastDropFrame[id] = Time.frameCount; Status = "Mine dropped — arming";
        Plugin.Instance.Log.LogInfo($"Mine trap placed at {mine.transform.position}; arming in {ArmDelay.Value:0.00}s.");
    }

    private static bool TriggerPrefix(MineBehavior __instance) => !Traps.ContainsKey(__instance.GetInstanceID());
    internal static void Tick()
    {
        if (!Installed) return;
        int scene = SceneManager.GetActiveScene().handle;
        if (!Enabled.Value || !Plugin.OfflineLabAllowed || Scene != 0 && Scene != scene) { Reset(); Scene = scene; return; }
        Scene = scene;
        if (Time.timeScale <= 0 || Time.time < NextCheck || MenuManager.Instance != null ||
            Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) return;
        NextCheck = Time.time + .05f;
        try {
            foreach (var pair in new List<KeyValuePair<int, Trap>>(Traps)) {
                var trap = pair.Value; var mine = trap.Mine;
                if (mine == null) { Traps.Remove(pair.Key); continue; }
                if (Time.time >= trap.Expires) { Traps.Remove(pair.Key); UnityEngine.Object.Destroy(mine.gameObject); continue; }
                if (Time.time < trap.ArmAt) continue;
                mine.isActivated = true;
                int count = Physics.OverlapSphereNonAlloc(mine.transform.position, trap.Radius, Hits, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++) {
                    var hit = Hits[i]; if (hit == null) continue;
                    var target = hit.GetComponentInParent<Ant_Player>() ?? hit.GetComponentInParent<PixelEasyCharMoveKartController>()?.parentPlayer;
                    if (target == null || target.hpBarController == null || target.hpBarController.currentHp <= 0 || !OwnerCanTrigger.Value && target == trap.Owner) continue;
                    Traps.Remove(pair.Key); mine.ExplodeMine(target);
                    UnityEngine.Object.Destroy(mine.gameObject); Status = "Mine triggered"; break;
                }
            }
        } catch (Exception ex) { Fail(ex); }
    }
    internal static void DrawStatus() {
        if (Enabled != null && Enabled.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 102, 950, 28), Installed
                ? $"Mine traps: {Traps.Count} deployed — {Status}. {DropKey.Value}: test drop; inventory mines use the normal fire button."
                : "Mine traps: hook unavailable; check the plugin log");
    }
    private static void Fail(Exception ex) { Enabled.Value = false; Plugin.Instance?.Log.LogError($"Mine traps stopped after an actual error: {ex.Message}"); Reset(); }
    internal static void Reset()
    {
        foreach (var trap in Traps.Values) if (trap.Mine != null) UnityEngine.Object.Destroy(trap.Mine.gameObject);
        Traps.Clear(); LastDropFrame.Clear();
        foreach (var original in Originals.Values) if (original.Weapon != null) {
            var owner = original.Weapon.parentPlayer;
            if (owner?.weaponsController?.currentWeaponType == PixelWeaponObject.EWeaponType.MINA_09) owner.weaponsController.ImmediateResetWeaponState();
            original.Weapon.bRequirePlayerTarget = original.RequireTarget; original.Weapon.targetType = original.Target;
        }
        Originals.Clear();
        foreach (var copy in Copies) {
            if (copy.View != null && copy.Weapon != null) {
                if (copy.View.player?.weaponsController?.currentWeaponType == PixelWeaponObject.EWeaponType.MINA_09) copy.View.player.weaponsController.ImmediateResetWeaponState();
                copy.View.weapons.Remove(copy.Weapon); UnityEngine.Object.Destroy(copy.Weapon.gameObject);
            }
        }
        Copies.Clear(); NextCheck = 0;
    }
}

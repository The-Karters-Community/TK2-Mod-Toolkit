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
    private const float ArmDelaySeconds = .6f;
    private static ConfigEntry<bool> Enabled = null!, ItemBoxes = null!;
    private static ConfigEntry<KeyCode> DropKey = null!;
    private static ConfigEntry<string> Controller = null!, Placement = null!;
    private static ConfigEntry<float> Chance = null!, DetonationDelay = null!, ThrowSpeed = null!, RearDistance = null!, MineScale = null!, TriggerRadius = null!, BlastRadius = null!;
    private static ConfigEntry<int> Ammo = null!, Damage = null!;
    private sealed class Trap
    {
        internal readonly MineBehavior Mine;
        internal readonly Ant_Player Owner;
        internal readonly float ArmAt, Expires;
        internal Vector3 LastPosition;
        internal Vector3 VisualScale;
        internal bool Armed;
        internal bool Settled;
        internal bool FlightLogged;
        internal Trap(MineBehavior mine, Ant_Player owner, float armAt, float expires, Vector3 lastPosition, Vector3 visualScale)
        { Mine = mine; Owner = owner; ArmAt = armAt; Expires = expires; LastPosition = lastPosition; VisualScale = visualScale; }
    }
    private sealed record WeaponCopy(WeaponsView View, PixelWeaponObject Weapon);
    private static readonly Dictionary<int, Trap> Traps = new();
    private static readonly List<WeaponCopy> Copies = new();
    private static readonly Dictionary<int, (PixelWeaponObject Weapon, bool RequireTarget, PixelWeaponObject.EItemTargetType Target)> Originals = new();
    private static readonly Dictionary<int, int> LastDropFrame = new();
    private static readonly List<KeyValuePair<int, Trap>> TrapSnapshot = new();
    private static PixelEasyCharMoveKartController[] CachedKarts = Array.Empty<PixelEasyCharMoveKartController>();
    private static float RefreshKartsAt;
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
        Placement = p.Config.Bind("MineTraps", "PlacementMode", "Forward throw", "Choose whether mines are thrown ahead or placed behind the kart.");
        DetonationDelay = p.Config.Bind("MineTraps", "TimedDetonationDelay", 45f, "Detonate after this many seconds. A nearby kart is used as the native explosion target.");
        ThrowSpeed = p.Config.Bind("MineTraps", "ForwardThrowSpeed", 30f, "Forward launch speed in metres per second. Used only by Forward throw mode; no upper limit.");
        RearDistance = p.Config.Bind("MineTraps", "RearDropDistance", 4f, "Place a stationary mine this far behind the kart. No upper limit.");
        MineScale = p.Config.Bind("MineTraps", "MineSizeMultiplier", 5f, "Visual size multiplier; does not enlarge the mine's trigger collider. No upper limit.");
        TriggerRadius = p.Config.Bind("MineTraps", "TriggerRadius", 4f, "Offline proximity fallback radius in metres. Suggested 4 metres; no upper limit.");
        BlastRadius = p.Config.Bind("MineTraps", "TimedBlastRadius", 10f, "Nearest kart within this radius is targeted when the mine's timer expires. Suggested 10 metres; no upper limit.");
        Damage = p.Config.Bind("MineTraps", "Damage", 80, "Damage dealt by the mine trap. No upper limit.");
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
        foreach (float value in new[] { DetonationDelay.Value, ThrowSpeed.Value, RearDistance.Value, MineScale.Value, TriggerRadius.Value, BlastRadius.Value })
            if (!float.IsFinite(value) || value < 0) throw new ArgumentException("Mine settings must be finite, non-negative numbers.");
        if (ThrowSpeed.Value <= 0) throw new ArgumentException("Forward mine throw speed must be positive.");
        if (MineScale.Value <= 0) throw new ArgumentException("Mine visual size multiplier must be positive.");
        if (TriggerRadius.Value < .1f) throw new ArgumentException("Mine trigger radius must be at least 0.1 metres.");
        if (BlastRadius.Value < .1f) throw new ArgumentException("Timed blast radius must be at least 0.1 metres.");
        var forward = kart.transform.forward;
        forward.y = 0; forward.Normalize();
        var prefab = Source();
        Plugin.Instance!.SessionModified = true;
        bool forwardThrow = Placement.Value == "Forward throw";
        Vector3 position;
        RaycastHit ground = default;
        if (forwardThrow) {
            var kartVelocity = kart.Motor.Velocity; kartVelocity.y = 0;
            if (kartVelocity.sqrMagnitude > 4 && Vector3.Dot(forward, kartVelocity) < 0) forward = -forward;
            if (forward.sqrMagnitude < .5f && kartVelocity.sqrMagnitude > 4) forward = kartVelocity.normalized;
            position = kart.transform.position + Vector3.up * 2f + forward * 6f;
        }
        else {
            var point = kart.transform.position - forward * RearDistance.Value;
            int mask = owner.weaponsController.enviroGroundWallColliderMask;
            if (!Physics.Raycast(point + Vector3.up * 6, Vector3.down, out ground, 40, mask, QueryTriggerInteraction.Ignore)) {
                Status = "Mine not placed: no track surface behind the kart"; return;
            }
            position = ground.point + ground.normal * Mathf.Max(.1f, prefab.bounds);
        }
        var rotation = forwardThrow ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.FromToRotation(Vector3.up, ground.normal);
        var mine = UnityEngine.Object.Instantiate(prefab, position, rotation);
        mine.name = "Toolkit Mine Trap"; mine.SetParentPlayer(owner); mine.customPropParentNo = (int)owner.eAntPlayerNr;
        ScaleMineVisual(mine, MineScale.Value);
        // Do not use the native throw path: it applies the prefab's backwards
        // vector and the kart can immediately collect/attach the spawned mine.
        // The forward test throw uses Rigidbody physics and settles below.
        mine.enabled = true; mine.tryToAttach = false; mine.isActivated = false; mine.exploded = false;
        mine.damage = Damage.Value;
        if (mine.rb != null) { mine.rb.isKinematic = !forwardThrow; mine.rb.useGravity = forwardThrow; }
        mine.gameObject.SetActive(true);
        if (forwardThrow && mine.rb != null) {
            var inherited = kart.Motor.Velocity;
            mine.rb.velocity = new Vector3(inherited.x, 0, inherited.z) + forward * ThrowSpeed.Value + Vector3.up * 4f;
            mine.rb.angularVelocity = Vector3.zero;
        }
        Traps[mine.GetInstanceID()] = new Trap(mine, owner, Time.time + ArmDelaySeconds,
            Time.time + DetonationDelay.Value, mine.transform.position, mine.transform.localScale) { Settled = !forwardThrow };
        LastDropFrame[id] = Time.frameCount; Status = "Mine dropped — arming";
        Plugin.Instance.Log.LogInfo($"Mine {(forwardThrow ? "forward launch" : "rear placement")} spawned at {mine.transform.position}; kart position {kart.transform.position}; heading {forward}; initial velocity {(mine.rb != null ? mine.rb.velocity : Vector3.zero)}; model scale {mine.transform.localScale}; detonation delay {DetonationDelay.Value:0.0}s.");
    }

    private static bool TriggerPrefix(MineBehavior __instance, Collider __0)
    {
        if (!Traps.TryGetValue(__instance.GetInstanceID(), out var trap)) return true;
        if (!trap.Armed || !__instance.CanExplode()) return false;
        try {
            // Unity trigger callbacks identify the kart reliably even when its damage
            // controller is not a parent of the broad overlap-sphere query.
            var target = __0.GetComponentInParent<Ant_Player>() ??
                __0.GetComponentInParent<PixelEasyCharMoveKartController>()?.parentPlayer;
            if (target == null || target.hpBarController == null || target.hpBarController.currentHp <= 0) return false;
            Traps.Remove(__instance.GetInstanceID());
            __instance.ExplodeMine(target);
            Status = "Mine triggered — native explosion called";
            Plugin.Instance?.Log.LogInfo(Status);
            // Keep the object alive: MineBehavior owns its explosion animation/effect cleanup.
            return false;
        } catch (Exception ex) { Fail(ex); return false; }
    }
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
            if (Traps.Count == 0) return;
            if (Time.time >= RefreshKartsAt) {
                CachedKarts = UnityEngine.Object.FindObjectsByType<PixelEasyCharMoveKartController>(FindObjectsSortMode.None);
                RefreshKartsAt = Time.time + .5f;
            }
            TrapSnapshot.Clear();
            foreach (var pair in Traps) TrapSnapshot.Add(pair);
            foreach (var pair in TrapSnapshot) {
                var trap = pair.Value; var mine = trap.Mine;
                if (mine == null) { Traps.Remove(pair.Key); continue; }
                if (Time.time >= trap.Expires) {
                    Traps.Remove(pair.Key);
                    if (mine.CanExplode()) {
                        // The game's ExplodeMine(target) anchors the effect to the
                        // supplied target, which made a remote fallback explode at
                        // the owner. Timed traps instead play their prefab at the
                        // mine and use the native HP hit path for every kart within
                        // the configured radius.
                        var blastPosition = mine.transform.position;
                        int hitCount = 0;
                        float radiusSquared = BlastRadius.Value * BlastRadius.Value;
                        foreach (var kart in CachedKarts) {
                            var target = kart.parentPlayer;
                            if (target == null || target.hpBarController == null || target.hpBarController.currentHp <= 0 ||
                                (kart.Motor.TransientPosition - blastPosition).sqrMagnitude > radiusSquared) continue;
                            target.hpBarController.Hit(Damage.Value, (int)trap.Owner.eAntPlayerNr,
                                PixelWeaponObject.EWeaponType.MINA_09);
                            hitCount++;
                        }
                        var effectPrefab = mine.mineExplosionPrefab;
                        if (effectPrefab != null) {
                            var effect = UnityEngine.Object.Instantiate(effectPrefab, blastPosition, mine.transform.rotation);
                            effect.name = "Toolkit Mine Timed Explosion";
                            UnityEngine.Object.Destroy(effect, 4f);
                        }
                        mine.exploded = true; mine.enabled = false;
                        UnityEngine.Object.Destroy(mine.gameObject);
                        Status = $"Mine timer blast at mine — {hitCount} kart(s) within {BlastRadius.Value:0.0} m";
                        Plugin.Instance?.Log.LogInfo(Status);
                    } else {
                        UnityEngine.Object.Destroy(mine.gameObject);
                        Status = "Mine expired — no kart in timed blast radius";
                    }
                    continue;
                }
                if ((mine.transform.localScale - trap.VisualScale).sqrMagnitude > .0001f)
                    mine.transform.localScale = trap.VisualScale;
                if (!trap.Settled && mine.rb != null) {
                    // The mine prefab uses a trigger sphere, so it cannot physically
                    // stop on the road. Let the forward throw arc, then park it on
                    // the first track surface it reaches.
                    int mask = trap.Owner.weaponsController.enviroGroundWallColliderMask;
                    var currentPosition = mine.transform.position;
                    var travel = currentPosition - trap.LastPosition;
                    RaycastHit landing = default;
                    bool hitTrack = travel.sqrMagnitude > .0001f && Physics.Raycast(trap.LastPosition, travel.normalized,
                        out landing, travel.magnitude + .25f, mask, QueryTriggerInteraction.Ignore);
                    if (!hitTrack && travel.sqrMagnitude > .0001f)
                        hitTrack = Physics.Raycast(trap.LastPosition, travel.normalized, out landing,
                            travel.magnitude + .25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (hitTrack) {
                        mine.transform.position = landing.point + landing.normal * Mathf.Max(.1f, mine.bounds * MineScale.Value);
                        mine.transform.rotation = Quaternion.FromToRotation(Vector3.up, landing.normal) * mine.transform.rotation;
                        mine.rb.velocity = Vector3.zero; mine.rb.angularVelocity = Vector3.zero;
                        mine.rb.isKinematic = true; mine.rb.useGravity = false; trap.Settled = true;
                        Plugin.Instance?.Log.LogInfo($"Forward-thrown mine settled on track at {mine.transform.position}.");
                    }
                    else trap.LastPosition = currentPosition;
                    if (!trap.Settled && !trap.FlightLogged && Time.time >= trap.ArmAt) {
                        trap.FlightLogged = true;
                        Plugin.Instance?.Log.LogInfo($"Forward mine still in flight at {mine.transform.position}; velocity {mine.rb.velocity}; native surface mask {mask}. It will keep searching for a landing surface.");
                    }
                }
                if (!trap.Armed && trap.Settled && Time.time >= trap.ArmAt) {
                    mine.ActivateMine(); trap.Armed = true;
                    Status = "Mine armed — scanning nearby karts";
                    Plugin.Instance?.Log.LogInfo($"Mine armed at {mine.transform.position}; scanning within {TriggerRadius.Value:0.0} m.");
                }
                if (trap.Armed && mine.CanExplode()) {
                    foreach (var kart in CachedKarts) {
                        var target = kart.parentPlayer;
                        if (target == null || target.hpBarController == null || target.hpBarController.currentHp <= 0 ||
                            (kart.Motor.TransientPosition - mine.transform.position).sqrMagnitude > TriggerRadius.Value * TriggerRadius.Value) continue;
                        Traps.Remove(pair.Key);
                        mine.ExplodeMine(target);
                        Status = "Mine triggered — native explosion called";
                        Plugin.Instance?.Log.LogInfo($"Mine trap triggered by player {target.eAntPlayerNr}; native explosion called.");
                        break;
                    }
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
        TrapSnapshot.Clear(); CachedKarts = Array.Empty<PixelEasyCharMoveKartController>(); RefreshKartsAt = 0;
    }

    private static void ScaleMineVisual(MineBehavior mine, float scale)
    {
        // The shipped prefab renders from children of the MineEffect root.
        // Scaling child renderers misses the mesh hierarchy and made the mine
        // remain tiny. Scale the whole model, then preserve its native trigger
        // volume by inversely scaling the root sphere collider.
        mine.transform.localScale *= scale;
        foreach (var collider in mine.GetComponentsInChildren<SphereCollider>(true)) {
            collider.radius /= scale; collider.center /= scale;
        }
        foreach (var collider in mine.GetComponentsInChildren<BoxCollider>(true)) {
            collider.size /= scale; collider.center /= scale;
        }
        foreach (var collider in mine.GetComponentsInChildren<CapsuleCollider>(true)) {
            collider.radius /= scale; collider.height /= scale; collider.center /= scale;
        }
    }
}

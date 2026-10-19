using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using ItemEffects;
using UnityEngine;

namespace TK2.Customization;

// Current native Hit forwards to the explosion delegate. Intercept only a proven wall hit.
internal static class RicochetItemsFeature
{
    private static ConfigEntry<bool> Enabled = null!, StraightBazooka = null!;
    private static readonly Dictionary<int, Vector3> Reflections = new();
    private static readonly Dictionary<int, Vector3> StraightFlights = new(), LaunchDirections = new();
    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("RicochetItems", "Enabled", false, "Experimental: local straight non-homing missiles bounce once on static walls. Runtime validation pending.");
        StraightBazooka = p.Config.Bind("RicochetItems", "StraightBazooka", true, "Fire the local bazooka along the kart's heading instead of auto-aiming. Homing missiles are unchanged.");
        FeatureHook.Install(p.Harmony, typeof(RicochetItemsFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(StraightMissileController), nameof(StraightMissileController.Init), new[] { typeof(MissileInitializeParams) }, Prefix: nameof(InitPrefix), Postfix: nameof(InitPostfix)),
        new FeatureHook(typeof(RaycastCollisionDetector), nameof(RaycastCollisionDetector.Hit), new[] { typeof(GameObject), typeof(Vector3), typeof(Vector3) }, Prefix: nameof(HitPrefix)),
        new FeatureHook(typeof(VisualLogicMissileController), nameof(VisualLogicMissileController.RefreshBulletConstantMovementTowardsTarget), new[] { typeof(float) }, Prefix: nameof(MovementPrefix)),
        new FeatureHook(typeof(VisualLogicMissileController), nameof(VisualLogicMissileController.OnDestroy), Type.EmptyTypes, Postfix: nameof(DestroyPostfix)),
        new FeatureHook(typeof(VisualLogicMissileController), nameof(VisualLogicMissileController.CheckIfPassedThrough), new[] { typeof(float) }, Prefix: nameof(PassedTargetPrefix)),
        new FeatureHook(typeof(WeaponsController), nameof(WeaponsController.FireInput), new[] { typeof(bool) }, Prefix: nameof(FireInputPrefix), Finalizer: nameof(FireInputFinalizer)),
        new FeatureHook(typeof(WeaponsController), nameof(WeaponsController.Shoot), Type.EmptyTypes, Prefix: nameof(FireInputPrefix), Finalizer: nameof(FireInputFinalizer)),
        new FeatureHook(typeof(WeaponsController), nameof(WeaponsController.GetCurrentPlayerTargets), Type.EmptyTypes, Postfix: nameof(TargetsPostfix)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(Reset))
    };

    private sealed record FireSettings(PixelWeaponObject Weapon, bool RequireTarget, PixelWeaponObject.EItemTargetType Target,
        bool AutoBack);
    private static bool StraightAllowed(WeaponsController controller) => Enabled.Value && StraightBazooka.Value &&
        Plugin.OfflineLabAllowed && controller.player?.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL &&
        controller.currentWeaponType == PixelWeaponObject.EWeaponType.BAZOOKA_00;

    private static void FireInputPrefix(WeaponsController __instance, out FireSettings? __state)
    {
        __state = null;
        if (!StraightAllowed(__instance)) return;
        var weapon = __instance.GetCurrentWeapon();
        if (weapon == null) return;
        __state = new(weapon, weapon.bRequirePlayerTarget, weapon.targetType, __instance.bIsTargettingBackBecauseFrontTargetsAreEmpty);
        // Both FireInput and Shoot reject an empty target list when this flag is set. Scope the
        // override to the native firing call; preserve native ammo, cooldown, animation and other weapons.
        Plugin.Instance!.SessionModified = true;
        weapon.bRequirePlayerTarget = false; weapon.targetType = PixelWeaponObject.EItemTargetType.NONE;
        __instance.bIsTargettingBackBecauseFrontTargetsAreEmpty = false;
    }

    private static void FireInputFinalizer(WeaponsController __instance, FireSettings? __state)
    {
        if (__state == null) return;
        if (__state.Weapon != null) {
            __state.Weapon.bRequirePlayerTarget = __state.RequireTarget; __state.Weapon.targetType = __state.Target;
        }
        __instance.bIsTargettingBackBecauseFrontTargetsAreEmpty = __state.AutoBack;
    }

    private static void TargetsPostfix(WeaponsController __instance,
        ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Ant_Player> __result)
    {
        if (StraightAllowed(__instance)) __result = WeaponsController.EmptyPlayerTargets;
    }

    private static void InitPrefix(StraightMissileController __instance, MissileInitializeParams __0)
    {
        if (!Enabled.Value || !StraightBazooka.Value || !Plugin.OfflineLabAllowed || __0 == null || __0.type != (int)PixelWeaponObject.EWeaponType.BAZOOKA_00) return;
        try {
            foreach (var kart in UnityEngine.Object.FindObjectsByType<PixelEasyCharMoveKartController>(FindObjectsSortMode.None)) {
                if (!AirShiftFeature.Running(kart) || (int)kart.parentPlayer.eAntPlayerNr != __0.parentPlayerNo) continue;
                var direction = kart.kartPhysics.GetMainMotorSteeringOrientation() * Vector3.forward;
                direction.y = 0; direction.Normalize();
                if (Vector3.Dot(direction, __0.visualForward) < -.1f) direction = -direction;
                Plugin.Instance!.SessionModified = true;
                __0.targetNo = __0.targetItemBoxID = __0.targetTriggerID = -1;
                __0.visualForward = direction; __0.endPoint = kart.Motor.TransientPosition + direction * 1000;
                LaunchDirections[__instance.GetInstanceID()] = direction;
                break;
            }
        } catch (Exception ex) { Enabled.Value = false; Plugin.Instance?.Log.LogError($"Straight bazooka stopped: {ex.Message}"); }
    }

    private static void InitPostfix(StraightMissileController __instance)
    {
        if (!LaunchDirections.Remove(__instance.GetInstanceID(), out var direction)) return;
        var logic = __instance.visualLogicMissile;
        if (logic == null) return;
        logic.bIsTargetingVehicle = false;
        logic.raycastCollisionDetector.bAutoExplodeOnPassingThroughBulletTargetPosition = false;
        StraightFlights[logic.GetInstanceID()] = direction;
    }

    private static bool HitPrefix(RaycastCollisionDetector __instance, GameObject __0, Vector3 __1, Vector3 __2)
    {
        if (!Enabled.Value || !Plugin.OfflineLabAllowed || Time.timeScale <= 0 ||
            MenuManager.Instance != null || Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING) return true;
        try {
            var missile = __instance.GetComponent<StraightMissileController>();
            var logic = __instance.visualLogicMissileController;
            if (missile == null || missile.exploded || logic == null || logic.bIsTargetingVehicle ||
                missile.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL || Reflections.ContainsKey(logic.GetInstanceID())) return true;
            if (__0 == null || __0.GetComponentInParent<Ant_Player>() != null ||
                __0.GetComponentInParent<ItemBox>() != null || __0.GetComponentInParent<PTK_ModTKLogic_PhysicsTrigger>() != null) return true;
            var collider = __0.GetComponent<Collider>();
            if (collider == null || collider.isTrigger || collider.attachedRigidbody != null ||
                __2.sqrMagnitude < .9f || Mathf.Abs(__2.normalized.y) > .35f) return true;
            var incoming = logic.vLastBulletMovingDirWorldSpace;
            if (incoming.sqrMagnitude < .001f) incoming = logic.transform.position - __instance.lastPos;
            incoming.y = 0;
            if (incoming.sqrMagnitude < .001f || Vector3.Dot(incoming.normalized, __2.normalized) >= -.05f) return true;
            var direction = Vector3.Reflect(incoming.normalized, __2.normalized); direction.y = 0; direction.Normalize();
            // Retain native height alignment, damage and lifetime. Only horizontal motion changes after the bounce.
            var position = __1 + __2.normalized * Mathf.Max(.1f, __instance.sphereRayRadius + .05f);
            Plugin.Instance!.SessionModified = true;
            Reflections.Add(logic.GetInstanceID(), direction);
            logic.vThisFrameCalculatedPos = position;
            logic.vLastBulletPos = position; logic.vLastBulletPosTransform = position;
            logic.vLastBulletMovingDirWorldSpace = direction;
            logic.transform.position = position;
            __instance.lastPos = position;
            __instance.bMoveBulletUp = false;
            __instance.bAutoExplodeOnPassingThroughBulletTargetPosition = false;
            return false;
        } catch (Exception ex) { Enabled.Value = false; Plugin.Instance?.Log.LogError($"Ricochet stopped: {ex.Message}"); return true; }
    }

    private static bool MovementPrefix(VisualLogicMissileController __instance, float __0)
    {
        // Finish already-bounced projectiles even if the switch is turned off mid-flight.
        if (!Plugin.OfflineLabAllowed || !Reflections.TryGetValue(__instance.GetInstanceID(), out var direction) &&
            !StraightFlights.TryGetValue(__instance.GetInstanceID(), out direction)) return true;
        try {
            float step = Mathf.Max(0, __instance.fBulletTargetSpeed) * __0;
            __instance.vThisFrameCalculatedPos += direction * step;
            __instance.distanceFromStart += step;
            __instance.vFromMuzzleToTargetDirection = direction;
            __instance.vLastFromMuzzleToTargetDirection = direction;
            if (__instance.distanceFromStart > __instance.fMaximumAllowedDistanceToTravel)
                __instance.raycastCollisionDetector.ManualForceExplosionWithoutHit();
            return false;
        } catch (Exception ex) {
            Reflections.Remove(__instance.GetInstanceID()); StraightFlights.Remove(__instance.GetInstanceID()); Enabled.Value = false;
            Plugin.Instance?.Log.LogError($"Ricochet movement stopped: {ex.Message}"); return true;
        }
    }

    private static void DestroyPostfix(VisualLogicMissileController __instance) {
        Reflections.Remove(__instance.GetInstanceID()); StraightFlights.Remove(__instance.GetInstanceID());
    }
    private static bool PassedTargetPrefix(VisualLogicMissileController __instance, ref bool __result)
    {
        if (!Plugin.OfflineLabAllowed || !Reflections.ContainsKey(__instance.GetInstanceID()) && !StraightFlights.ContainsKey(__instance.GetInstanceID())) return true;
        __result = false; return false;
    }
    internal static void Reset() { Reflections.Clear(); StraightFlights.Clear(); LaunchDirections.Clear(); }
}

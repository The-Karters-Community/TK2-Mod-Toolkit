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
        var init = AccessTools.DeclaredMethod(typeof(StraightMissileController), "Init", new[] { typeof(MissileInitializeParams) })
            ?? throw new MissingMethodException("StraightMissileController.Init(MissileInitializeParams)");
        p.Harmony.Patch(init, prefix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(InitPrefix)),
            postfix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(InitPostfix)));
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(RaycastCollisionDetector), "Hit", new[] { typeof(GameObject), typeof(Vector3), typeof(Vector3) })
            ?? throw new MissingMethodException("RaycastCollisionDetector.Hit(GameObject, Vector3, Vector3)"),
            prefix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(HitPrefix)));
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(VisualLogicMissileController), "RefreshBulletConstantMovementTowardsTarget", new[] { typeof(float) })
            ?? throw new MissingMethodException("VisualLogicMissileController.RefreshBulletConstantMovementTowardsTarget(float)"),
            prefix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(MovementPrefix)));
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(VisualLogicMissileController), "OnDestroy", Type.EmptyTypes)
            ?? throw new MissingMethodException("VisualLogicMissileController.OnDestroy()"),
            postfix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(DestroyPostfix)));
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(VisualLogicMissileController), "CheckIfPassedThrough", Type.EmptyTypes)
            ?? throw new MissingMethodException("VisualLogicMissileController.CheckIfPassedThrough()"),
            prefix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(PassedTargetPrefix)));
        p.Harmony.Patch(AccessTools.DeclaredMethod(typeof(PlayerRaceLogic), "OnRaceResetted", Type.EmptyTypes)!,
            postfix: new HarmonyMethod(typeof(RicochetItemsFeature), nameof(Reset)));
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

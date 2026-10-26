using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static void MainVelocityPrefix(PixelKartPhysics __instance, ref Vector3 __0, out MainVelocityPatchState __state)
    {
        __state = default;
        if (!TryGetAttached(__instance, out var entry)) return;
        float tilt = Vector3.Angle(Vector3.up, entry.Up);
        if (tilt < DirectSurfaceActivationDegrees && !entry.AcceptedHop) return;
        __state.Patched = true;
        if (IsAttachedAi(__instance))
        {
            __state.RestoreDriftForce = true;
            __state.OriginalDriftForce = __instance.bAddLateralForceAtStartOfTheDriftToTweakVel;
            __instance.bAddLateralForceAtStartOfTheDriftToTweakVel = false;
        }
        // Keep the game's vertical cap on ordinary banks; remove it only once
        // the frame is steep enough for world-Y to oppose surface motion.
        if (tilt < SteepSurfaceDegrees && !entry.AcceptedHop)
            return;
        bool original = __instance.bLimitVerticalUpVelocity;
        __state.RestoreCap = true;
        __state.OriginalCap = original;
        __instance.bLimitVerticalUpVelocity = false;
    }

    private static void MainVelocityPostfix(PixelKartPhysics __instance, ref Vector3 __0, float __1,
        ref MainVelocityPatchState __state)
    {
        if (!__state.Patched) return;
        try
        {
            if (TryGetAttached(__instance, out var entry) && entry.Motor != null &&
                !entry.Motor.GroundingStatus.FoundAnyGround &&
                (Vector3.Angle(Vector3.up, entry.Up) >= DirectSurfaceActivationDegrees || entry.AcceptedHop))
            {
                Vector3 scratchVelocity = Vector3.zero;
                Vector3 nativeGravityDelta = Vector3.zero;
                __instance.UpdateGravityWhenNotGrounded(ref scratchVelocity, ref nativeGravityDelta, __1);
                Vector3 surfaceGravityDelta = AntigravityPolicy.RedirectAddedDelta(
                    Vector3.zero, nativeGravityDelta, Vector3.down, -entry.Up);
                Vector3 corrected = __0 - nativeGravityDelta + surfaceGravityDelta;
                if ((corrected - __0).sqrMagnitude > .000001f && Plugin.Instance != null)
                    Plugin.Instance.SessionModified = true;
                __0 = corrected;
            }
        }
        catch (Exception ex)
        {
            if (Plugin.Instance != null) Fail(__instance.kartController?.GetInstanceID() ?? 0, Plugin.Instance, ex);
        }
        finally { RestoreMainVelocityCap(__instance, ref __state); }
    }

    private static Exception? MainVelocityFinalizer(PixelKartPhysics __instance,
        ref MainVelocityPatchState __state, Exception? __exception)
    {
        RestoreMainVelocityCap(__instance, ref __state);
        return __exception;
    }

    private static void RestoreMainVelocityCap(PixelKartPhysics physics, ref MainVelocityPatchState state)
    {
        if (state.RestoreDriftForce)
        {
            physics.bAddLateralForceAtStartOfTheDriftToTweakVel = state.OriginalDriftForce;
            state.RestoreDriftForce = false;
        }
        if (!state.RestoreCap) return;
        physics.bLimitVerticalUpVelocity = state.OriginalCap;
        state.RestoreCap = false;
    }

    private static void CharacterVelocityPostfix(PixelEasyCharMoveKartController __instance, ref Vector3 __0, float __1)
    {
        if (!CanReadController(__instance) || !TryGet(__instance, out var entry) ||
            !entry.Attached || !entry.Applied) return;
        var motor = entry.Motor;
        var grounding = motor.GroundingStatus;
        bool surfaceHop = entry.AcceptedHop || entry.HopElapsed > 0f;
        float surfaceTilt = Vector3.Angle(Vector3.up, entry.Up);
        // Leave shallow road hops entirely to the game. Reprojecting or carrying
        // their velocity here cancels the native jump impulse and flattens the
        // jump-drift pose.
        if (motor.MustUnground() && surfaceTilt < DirectSurfaceActivationDegrees) return;
        if (surfaceHop || motor.MustUnground())
        {
            if (entry.HopElapsed == 0f) TraceJump(entry, __instance.kartPhysics, "velocity", __0);
            return;
        }
        Vector3 aligned = AntigravityPolicy.AlignVelocityTangent(__0, entry.Normal, entry.Forward);
        bool changed = (aligned - __0).sqrMagnitude > .000001f;
        __0 = aligned;

        // When a loop crosses the native slope boundary, UpdateVelocity can
        // return an almost-zero tangent even though the kart was travelling
        // normally one tick earlier. Keep bounded forward momentum on steep
        // direct surfaces (and during the accepted drift hop), while letting
        // flat-road braking and reverse input behave natively.
        if (__instance.kartPhysics != null &&
            !__instance.kartPhysics.bIsMovingBackward)
        {
            Vector3 up = entry.Up.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(entry.Forward, up).normalized;
            float tilt = Vector3.Angle(Vector3.up, up);
            if (forward.sqrMagnitude > .5f && tilt >= 65f)
            {
                float currentForward = Vector3.Dot(Vector3.ProjectOnPlane(__0, up), forward);
                float dt = Mathf.Min(Time.fixedDeltaTime, .05f);
                float carried = Mathf.Max(0f, entry.SurfaceForwardSpeed - 35f * dt);
                float target = Mathf.Max(currentForward, carried);
                if (target > 8f && currentForward < target - 1.5f)
                {
                    Vector3 right = Vector3.Cross(up, forward).normalized;
                    float lateral = Mathf.Clamp(Vector3.Dot(__0, right), -target * .6f, target * .6f);
                    __0 = forward * target + right * lateral + up * Vector3.Dot(__0, up);
                    changed = true;
                }
                entry.SurfaceForwardSpeed = target;
            }
            else
            {
                entry.SurfaceForwardSpeed = Mathf.Max(0f,
                    Vector3.Dot(Vector3.ProjectOnPlane(__0, up), forward));
            }
        }
        GuideAiVelocity(entry, ref __0, __1);
        // Keep contact sweeps gently directed into the road instead of perfectly
        // tangent to it. The hop/forced-unground returns above preserve jumps.
        float normalSpeed = Vector3.Dot(__0, entry.Up);
        if (normalSpeed > -1f)
        {
            __0 -= entry.Up * (normalSpeed + 1f);
            changed = true;
        }
        if (changed && Plugin.Instance != null) Plugin.Instance.SessionModified = true;
    }

}

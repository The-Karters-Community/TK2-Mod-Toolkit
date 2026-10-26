using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static void UpdateRotationPostfix(PixelEasyCharMoveKartController __instance, ref Quaternion __0)
    {
        if (PendingReleaseRotation.Count > 0 && IsLocalRacer(__instance) &&
            PendingReleaseRotation.Remove(__instance.GetInstanceID(), out var pending))
        {
            var release = AntigravityPolicy.ReleaseAtCurrentHeading(__instance.transform.forward, pending.Motor.Velocity);
            __0 = release.Rotation;
            return;
        }
        if (!CanReadController(__instance) ||
            !TryGet(__instance, out var entry) || !entry.Attached) return;
        Quaternion corrected = Quaternion.LookRotation(entry.Forward, entry.Up);
        if (Quaternion.Angle(__0, corrected) > .001f && Plugin.Instance != null)
            Plugin.Instance.SessionModified = true;
        __0 = corrected;
    }

    private static void AxisDirectionsPostfix(PixelKartPhysics __instance, ref Vector3 __0)
    {
        if (!CanReadState(__instance)) return;
        var controller = __instance.kartController;
        if (controller == null || !TryGet(controller, out var entry) || !entry.Attached) return;
        if (ApplyAxes(__instance, entry.Forward, entry.Up, __0) && Plugin.Instance != null)
            Plugin.Instance.SessionModified = true;
    }

    private struct SteeringCallState
    {
        internal bool Applied;
        internal Quaternion ToSurface, StartYaw;
        internal Vector3 Forward, Right, Up, Acceleration;
    }

    private static void SteeringPrefix(PixelKartPhysics __instance, ref Vector3 __0,
        ref Vector3 __1, ref Vector3 __2, out SteeringCallState __state)
    {
        __state = default;
        if (!TryGetAttached(__instance, out var entry)) return;
        Quaternion yaw = __instance.qSteeringOrientation;
        Vector3 flatForward = Vector3.ProjectOnPlane(yaw * Vector3.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < .5f) return;
        Quaternion flat = Quaternion.LookRotation(flatForward, Vector3.up);
        Quaternion toSurface = Quaternion.LookRotation(entry.Forward, entry.Up) * Quaternion.Inverse(flat);
        Quaternion toFlat = Quaternion.Inverse(toSurface);
        __state = new SteeringCallState { Applied = true, ToSurface = toSurface, StartYaw = yaw,
            Forward = __instance.vForward_SteeringChar_AlignedToSurfacce_Normalized,
            Right = __instance.vRight_SteeringChar_AlignedToSurfacce_Normalized,
            Up = __instance.vUP_CharacterMotorAligned,
            Acceleration = __instance.vAccelerationForwardDirection };
        // The native lateral/centripetal math uses world-up internally. Give this
        // one call a flat coordinate system, including every vector it consumes.
        __0 = toFlat * __0; __1 = toFlat * __1; __2 = toFlat * __2;
        __instance.vForward_SteeringChar_AlignedToSurfacce_Normalized = flatForward;
        __instance.vRight_SteeringChar_AlignedToSurfacce_Normalized = Vector3.Cross(Vector3.up, flatForward);
        __instance.vUP_CharacterMotorAligned = Vector3.up;
        __instance.vAccelerationForwardDirection = toFlat * __state.Acceleration;
        __instance.vVelocityDrivingCentiperalAddVal = toFlat * __instance.vVelocityDrivingCentiperalAddVal;
    }

    private static void RestoreSteeringFrame(PixelKartPhysics physics, ref Vector3 velocity,
        ref Vector3 acceleration, ref SteeringCallState state)
    {
        if (!state.Applied) return;
        velocity = state.ToSurface * velocity;
        acceleration = state.ToSurface * acceleration;
        physics.vVelocityDrivingCentiperalAddVal = state.ToSurface * physics.vVelocityDrivingCentiperalAddVal;
        physics.vForward_SteeringChar_AlignedToSurfacce_Normalized = state.Forward;
        physics.vRight_SteeringChar_AlignedToSurfacce_Normalized = state.Right;
        physics.vUP_CharacterMotorAligned = state.Up;
        physics.vAccelerationForwardDirection = state.Acceleration;
        state.Applied = false;
    }

    private static void SteeringPostfix(PixelKartPhysics __instance, ref Vector3 __1,
        ref Vector3 __2, ref SteeringCallState __state)
    {
        bool applied = __state.Applied;
        RestoreSteeringFrame(__instance, ref __1, ref __2, ref __state);
        if (!applied || !TryGetAttached(__instance, out var entry)) return;
        float delta = IsAttachedAi(__instance) && entry.AiGuidanceReady
            ? 0f : LocalSteeringDelta(__state.StartYaw, __instance.qSteeringOrientation);
        Vector3 forward = Quaternion.AngleAxis(delta, entry.Up) * entry.Forward;
        forward = Vector3.ProjectOnPlane(forward, entry.Up).normalized;
        if (forward.sqrMagnitude > .5f) entry.Forward = forward;
        if (ApplyAxes(__instance, entry.Forward, entry.Up, __1) && Plugin.Instance != null)
            Plugin.Instance.SessionModified = true;
        if (IsLocalHuman(entry.Controller) && Mathf.Abs(__instance.steer_input) > .1f && Time.unscaledTime >= entry.NextSteeringLog &&
            entry.SteeringLogCount < 24)
        {
            entry.SteeringLogCount++;
            entry.NextSteeringLog = Time.unscaledTime + .4f;
            float side = Vector3.Dot(__1, Vector3.Cross(entry.Up, entry.Forward));
            Plugin.Instance?.Log.LogInfo($"Antigravity steering: input={__instance.steer_input:0.00} " +
                $"drift={__instance.bIsDrifting} driftDir={__instance.fDriftingSteerDir:0.00} " +
                $"yawDelta={delta:0.000} lateralSpeed={side:0.00} tilt={Vector3.Angle(Vector3.up, entry.Up):0}");
        }
    }

    private static void SteeringFinalizer(PixelKartPhysics __instance, ref Vector3 __1,
        ref Vector3 __2, ref SteeringCallState __state) =>
        RestoreSteeringFrame(__instance, ref __1, ref __2, ref __state);

    private static bool ApplyFrame(PixelKartPhysics physics, KinematicCharacterMotor motor,
        Vector3 forward, Vector3 up, Vector3 velocity, bool updateTransform)
    {
        forward = Vector3.ProjectOnPlane(forward, up).normalized;
        bool changed = ApplyAxes(physics, forward, up, velocity);
        Quaternion rotation = Quaternion.LookRotation(forward, up);
        if (updateTransform && Quaternion.Angle(motor.TransientRotation, rotation) > .001f)
        {
            // The after-update callback runs before KinematicCharacterSystem commits transients
            // to Transform. Updating the native transient rotation refreshes CharacterUp and
            // its derived axes without resetting the interpolation baselines every physics tick.
            motor.TransientRotation = rotation;
            changed = true;
        }
        return changed;
    }

    private static bool ApplyAxes(PixelKartPhysics physics, Vector3 forward, Vector3 up, Vector3 velocity)
    {
        forward = Vector3.ProjectOnPlane(forward, up).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 acceleration = AntigravityPolicy.AccelerationForward(velocity, forward, up,
            physics.bUseAccelerationVectorAsMidForwardMidVelocity, physics.bIsMovingBackward);
        bool changed = Vector3.SqrMagnitude(physics.vForward_SteeringChar_AlignedToSurfacce_Normalized - forward) > .000001f ||
            Vector3.SqrMagnitude(physics.vRight_SteeringChar_AlignedToSurfacce_Normalized - right) > .000001f ||
            Vector3.SqrMagnitude(physics.vUP_CharacterMotorAligned - up) > .000001f ||
            Vector3.SqrMagnitude(physics.vAccelerationForwardDirection - acceleration) > .000001f;
        physics.vForward_SteeringChar_AlignedToSurfacce_Normalized = forward;
        physics.vRight_SteeringChar_AlignedToSurfacce_Normalized = right;
        physics.vUP_CharacterMotorAligned = up;
        physics.vAccelerationForwardDirection = acceleration;
        return changed;
    }

    private static float LocalSteeringDelta(Quaternion previous, Quaternion current)
    {
        Quaternion delta = Quaternion.Inverse(previous) * current;
        if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
        float degrees = 2f * Mathf.Atan2(delta.y, delta.w) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(0f, degrees);
    }

    private static bool JumpPrefix(PixelKartPhysics __instance, ref Vector3 __0, out Vector3 __state)
    {
        __state = __0;
        // Cover a native AI jump queued just before entering the steep section.
        // Human hop impulses and ordinary-road AI jumps keep their native path.
        return !IsAttachedAi(__instance);
    }

    private static void JumpPostfix(PixelKartPhysics __instance, ref Vector3 __0, Vector3 __state)
    {
        if (IsAttachedAi(__instance)) return;
        if (!TryGetAttached(__instance, out var entry)) return;
        // A normal jump-drift on road belongs to the native game. Surface-frame
        // impulse transport is only needed once direct mode is on a wall/loop.
        if (Vector3.Angle(Vector3.up, entry.Up) < DirectSurfaceActivationDegrees) return;
        Vector3 nativeDelta = __0 - __state;
        if (nativeDelta.sqrMagnitude > .000001f) entry.AcceptedHop = true;
        Vector3 redirected = AntigravityPolicy.RedirectAddedDelta(__state, __0, Vector3.up, entry.Up);
        // Native edge/tilt bonuses assume world-up roads and can multiply a loop
        // hop into a launch. Keep the configured base hop as the outward ceiling.
        float outward = Vector3.Dot(redirected, entry.Up);
        float ceiling = Mathf.Max(0f, __instance.fJumpStrength);
        if (outward > ceiling) redirected -= entry.Up * (outward - ceiling);
        if ((redirected - __0).sqrMagnitude > .000001f && Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        __0 = redirected;
        TraceJump(entry, __instance, "impulse", __0);
    }

    private static void JumpInputPostfix(PixelKartPhysics __instance, bool __0)
    {
        if (!__0 || !TryGetAttached(__instance, out var entry) ||
            entry.AcceptedHop || !HasJumpContact(entry) ||
            Vector3.Angle(Vector3.up, entry.Up) < DirectSurfaceActivationDegrees) return;

        // Native jump input only accepts a run jump when the motor reports ground.
        // Steep loop contact is tracked by Antigravity instead, so allow the same
        // jump-drift request while the kart remains attached to that surface.
        __instance.bPlayerJumpedWhenGrounedOrJustAfterMissedGround_RunJump = true;
        TraceJump(entry, __instance, "input", entry.Motor.Velocity);
        if (Plugin.Instance != null) Plugin.Instance.SessionModified = true;
    }

    private static void ControllerJumpInputPostfix(PixelEasyCharMoveKartController __instance, bool __0)
    {
        // Cover native call sites that inline the small physics input wrapper.
        if (__instance.kartPhysics != null) JumpInputPostfix(__instance.kartPhysics, __0);
    }

    private static void JumpGroundPrefix(PixelKartPhysics __instance, out KinematicCharacterMotor? __state)
    {
        __state = null;
        if (!TryGetAttached(__instance, out var entry) || entry.AcceptedHop ||
            entry.HopElapsed > 0f || entry.Motor.MustUnground() || !HasJumpContact(entry)) return;
        var report = entry.Motor.GroundingStatus;
        if (report.FoundAnyGround) return;
        // Run the native landing/rearm gate against our established contact. The
        // original report is restored immediately after this one native call.
        report.FoundAnyGround = true;
        entry.Motor.GroundingStatus = report;
        __state = entry.Motor;
    }

    private static void RestoreJumpGround(ref KinematicCharacterMotor? __state)
    {
        if (__state == null) return;
        var report = __state.GroundingStatus;
        report.FoundAnyGround = false;
        __state.GroundingStatus = report;
        __state = null;
    }

    private static void TraceJump(KartState entry, PixelKartPhysics physics, string stage, Vector3 velocity)
    {
        if (!IsLocalHuman(entry.Controller) || entry.JumpLogCount++ >= 36) return;
        Plugin.Instance?.Log.LogInfo($"Antigravity jump {stage}: state={physics.eJumpState} " +
            $"ground={entry.Motor.GroundingStatus.FoundAnyGround} unground={entry.Motor.MustUnground()} " +
            $"hop={entry.AcceptedHop}/{entry.HopElapsed:0.000}s normalSpeed={Vector3.Dot(velocity, entry.Up):0.00} " +
            $"baseJump={physics.fJumpStrength:0.00} tilt={Vector3.Angle(Vector3.up, entry.Up):0}");
    }

    private static bool SlopePrefix(PixelKartPhysics __instance) =>
        !TryGetAttached(__instance, out var entry) ||
        Vector3.Angle(Vector3.up, entry.Up) < SteepSurfaceDegrees;

}

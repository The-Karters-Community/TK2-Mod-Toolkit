using System;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// CTR fixed-point physics cannot be directly converted to TK2 units. Multipliers are experimental.
internal static class CtrPhysicsFeature
{
    private static ConfigEntry<bool> Enabled = null!, Speedghost = null!, Downhill = null!;
    private static ConfigEntry<float> Speed = null!, Acceleration = null!, Friction = null!, Gravity = null!, Jump = null!, AirLoss = null!, SlopeAcceleration = null!, UphillAcceleration = null!, SlopeLoss = null!, MaxSpeedRatio = null!;
    private struct Scope { internal bool Applied; internal float Speed, Acceleration, Friction, Mass; }
    private struct Movement { internal bool Applied, Grounded, Airborne; internal float BeforeHorizontal, SoftCap; }
    internal static string SlopeStatus = "Slope: waiting for driving";
    private static float NextSlopeLog, NextSlopeStatus;
    private struct JumpScope { internal bool Applied; internal float Strength; }

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("CtrHandling", "Enabled", false, "Experimental offline CTR-inspired handling.");
        Speed = p.Config.Bind("CtrHandling", "BaseSpeedMultiplier", .85f, "Multiply native base speed. 1 restores the native value.");
        Acceleration = p.Config.Bind("CtrHandling", "AccelerationMultiplier", 1f, "Multiply native ground acceleration.");
        Friction = p.Config.Bind("CtrHandling", "GroundFrictionMultiplier", 1f, "Multiply native ground friction.");
        Gravity = p.Config.Bind("CtrHandling", "GravityMultiplier", 1.25f, "Multiply gravity while airborne.");
        Jump = p.Config.Bind("CtrHandling", "JumpStrengthMultiplier", .85f, "Multiply jump impulse. 1 restores the native value.");
        Speedghost = p.Config.Bind("CtrHandling", "Speedghost", true, "Preserve above-cap momentum during airborne hops.");
        AirLoss = p.Config.Bind("CtrHandling", "SpeedghostAirLoss", .6f, "Maximum airborne overspeed loss in game speed units per second.");
        Downhill = p.Config.Bind("CtrHandling", "DownhillSpeedghost", true, "Build above-cap speed while driving downhill.");
        SlopeAcceleration = p.Config.Bind("CtrHandling", "DownhillAcceleration", 35f, "Downhill acceleration in game speed units per second at full slope.");
        UphillAcceleration = p.Config.Bind("CtrHandling", "UphillDeceleration", 20f, "Uphill speed loss in game speed units per second at full slope.");
        SlopeLoss = p.Config.Bind("CtrHandling", "DownhillOverspeedLoss", 2f, "Maximum natural overspeed loss per second on a downhill.");
        MaxSpeedRatio = p.Config.Bind("CtrHandling", "DownhillMaxSpeedRatio", 2f, "Limit additional downhill speed relative to the current soft cap.");
        FeatureHook.Install(p.Harmony, typeof(CtrPhysicsFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_UpdateVelocity),
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(BeforeMovement), Postfix: nameof(AfterMovement)),
        new FeatureHook(typeof(PixelKartPhysics), nameof(PixelKartPhysics.UpdateVelocity),
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(BeforeVelocity),
            Postfix: nameof(AfterVelocity), Finalizer: nameof(FinalizeVelocity)),
        new FeatureHook(typeof(PixelKartPhysics), "TriggerPhysicsJump",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(BeforeJump),
            Postfix: nameof(AfterJump), Finalizer: nameof(FinalizeJump))
    };

    private static bool Allowed(PixelKartPhysics physics) => Enabled?.Value == true &&
        AirShiftFeature.Running(physics.kartController) &&
        physics.kartController.parentPlayer.playerRaceLogic?.bPlayerEndRaceEventTriggered == false;

    private static float Value(ConfigEntry<float> setting)
    {
        float value = setting.Value;
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentException("CTR physics multipliers must be finite and positive.");
        return value;
    }

    private static void BeforeVelocity(PixelKartPhysics __instance, out Scope __state)
    {
        __state = default;
        try {
            if (!Allowed(__instance)) return;
            float speed = Value(Speed), acceleration = Value(Acceleration), friction = Value(Friction), gravity = Value(Gravity);
            __state = new Scope { Applied = true, Speed = __instance.max_speed_accel_forward,
                Acceleration = __instance.acceleration_factor, Friction = __instance.ground_friction_factor, Mass = __instance.fMass };
            __instance.max_speed_accel_forward = __state.Speed * speed;
            __instance.acceleration_factor = __state.Acceleration * acceleration;
            __instance.ground_friction_factor = __state.Friction * friction;
            __instance.fMass = __state.Mass * gravity;
            Plugin.Instance!.SessionModified = true;
        } catch (Exception ex) { Restore(__instance, ref __state); Fail(ex); }
    }

    private static void BeforeJump(PixelKartPhysics __instance, out JumpScope __state)
    {
        __state = default;
        try {
            if (!Allowed(__instance)) return;
            float jump = Value(Jump);
            __state = new JumpScope { Applied = true, Strength = __instance.fJumpStrength };
            __instance.fJumpStrength = __state.Strength * jump;
            Plugin.Instance!.SessionModified = true;
        } catch (Exception ex) { RestoreJump(__instance, ref __state); Fail(ex); }
    }
    private static void AfterJump(PixelKartPhysics __instance, ref JumpScope __state) => RestoreJump(__instance, ref __state);
    private static Exception? FinalizeJump(PixelKartPhysics __instance, ref JumpScope __state, Exception? __exception)
    { RestoreJump(__instance, ref __state); return __exception; }
    private static void RestoreJump(PixelKartPhysics physics, ref JumpScope state)
    { if (state.Applied) { physics.fJumpStrength = state.Strength; state.Applied = false; } }
    private static void AfterVelocity(PixelKartPhysics __instance, ref Scope __state) => Restore(__instance, ref __state);

    private static void BeforeMovement(PixelEasyCharMoveKartController __instance, ref Vector3 __0, out Movement __state)
    {
        __state = default;
        try {
            var physics = __instance.kartPhysics;
            if (!Allowed(physics) || physics.bIsBreakingInput) return;
            __state.Applied = true;
            __state.Grounded = __instance.Motor.GroundingStatus.IsStableOnGround;
            __state.Airborne = !__instance.IsGrounded_Now();
            __state.BeforeHorizontal = new Vector2(__0.x, __0.z).magnitude;
            __state.SoftCap = Math.Max(0f, physics.max_speed_accel_forward * Value(Speed) + physics.boostManager.GetCurrentBoostMaxSpeedIncrease());
        } catch (Exception ex) { Fail(ex); }
    }

    private static void AfterMovement(PixelEasyCharMoveKartController __instance, ref Vector3 __0, float __1, Movement __state)
    {
        try {
            if (!__state.Applied || !Allowed(__instance.kartPhysics)) return;
            float horizontal = new Vector2(__0.x, __0.z).magnitude;
            float target = horizontal;
            float slope = 0f;
            if (__state.Grounded && Downhill.Value) {
                var normal = __instance.Motor.GroundingStatus.GroundNormal;
                slope = CtrSpeedGhostMath.SignedSlope(normal.x, normal.y, normal.z, __0.x, __0.z);
                if (slope > 0)
                    target = CtrSpeedGhostMath.DownhillSpeed(__state.BeforeHorizontal, horizontal, __state.SoftCap,
                        slope, SlopeAcceleration.Value, SlopeLoss.Value, MaxSpeedRatio.Value, __1);
                else if (slope < 0)
                    target = CtrSpeedGhostMath.UphillSpeed(__state.BeforeHorizontal, horizontal, slope, UphillAcceleration.Value, __1);
            } else if (__state.Airborne && Speedghost.Value) {
                target = CtrSpeedGhostMath.RetainedSpeed(__state.BeforeHorizontal, horizontal, __state.SoftCap, AirLoss.Value, __1);
            }
            if (horizontal > .001f && float.IsFinite(target)) {
                float scale = target / horizontal;
                __0.x *= scale; __0.z *= scale;
            }
            if (Time.unscaledTime >= NextSlopeStatus) {
                NextSlopeStatus = Time.unscaledTime + .25f;
                SlopeStatus = $"Slope {(slope > .01f ? "downhill" : slope < -.01f ? "uphill" : __state.Airborne ? "air" : "flat")} {slope:+0.00;-0.00;0.00} | speed {horizontal:0.0} -> {target:0.0}";
            }
            if (Time.unscaledTime >= NextSlopeLog && Math.Abs(slope) > .03f) {
                NextSlopeLog = Time.unscaledTime + 2f;
                Plugin.Instance?.Log.LogInfo($"CTR {SlopeStatus}, dt {__1:0.000}, cap {__state.SoftCap:0.0}");
            }
        } catch (Exception ex) { Fail(ex); }
    }
    private static Exception? FinalizeVelocity(PixelKartPhysics __instance, ref Scope __state, Exception? __exception)
    { Restore(__instance, ref __state); return __exception; }
    private static void Restore(PixelKartPhysics physics, ref Scope state)
    {
        if (!state.Applied) return;
        physics.max_speed_accel_forward = state.Speed;
        physics.acceleration_factor = state.Acceleration;
        physics.ground_friction_factor = state.Friction;
        physics.fMass = state.Mass;
        state.Applied = false;
    }
    private static void Fail(Exception ex)
    {
        Enabled.Value = false;
        Plugin.Instance?.Log.LogError($"CTR physics disabled: {ex.Message}");
    }
    internal static void Reset() { SlopeStatus = "Slope: waiting for driving"; NextSlopeLog = 0; NextSlopeStatus = 0; }
}

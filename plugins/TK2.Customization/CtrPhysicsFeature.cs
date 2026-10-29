using System;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// CTR fixed-point physics cannot be directly converted to TK2 units. Multipliers are experimental.
internal static class CtrPhysicsFeature
{
    private static ConfigEntry<bool> Enabled = null!, Speedghost = null!;
    private static ConfigEntry<float> Speed = null!, Acceleration = null!, Friction = null!, Gravity = null!, Jump = null!, AirLoss = null!;
    private struct Scope { internal bool Applied; internal float Speed, Acceleration, Friction, Mass, BeforeHorizontal, SoftCap; internal bool Airborne; }
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
        FeatureHook.Install(p.Harmony, typeof(CtrPhysicsFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
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

    private static void BeforeVelocity(PixelKartPhysics __instance, ref Vector3 __0, out Scope __state)
    {
        __state = default;
        try {
            if (!Allowed(__instance)) return;
            float speed = Value(Speed), acceleration = Value(Acceleration), friction = Value(Friction), gravity = Value(Gravity);
            __state = new Scope { Applied = true, Speed = __instance.max_speed_accel_forward,
                Acceleration = __instance.acceleration_factor, Friction = __instance.ground_friction_factor, Mass = __instance.fMass };
            __state.Airborne = Speedghost.Value && !__instance.kartController.IsGrounded_Now() && !__instance.bIsBreakingInput;
            __state.BeforeHorizontal = new Vector2(__0.x, __0.z).magnitude;
            __state.SoftCap = Math.Max(0f, __state.Speed * speed + __instance.boostManager.GetCurrentBoostMaxSpeedIncrease());
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
    private static void AfterVelocity(PixelKartPhysics __instance, ref Vector3 __0, float __1, ref Scope __state)
    {
        try {
            if (__state.Applied && __state.Airborne && Speedghost.Value && Allowed(__instance)) {
                float horizontal = new Vector2(__0.x, __0.z).magnitude;
                float target = CtrSpeedGhostMath.RetainedSpeed(__state.BeforeHorizontal, horizontal, __state.SoftCap,
                    AirLoss.Value, __1);
                if (horizontal > .001f && target > horizontal) {
                    float scale = target / horizontal;
                    __0.x *= scale; __0.z *= scale;
                }
            }
        } catch (Exception ex) { Fail(ex); }
        finally { Restore(__instance, ref __state); }
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
    internal static void Reset() { }
}

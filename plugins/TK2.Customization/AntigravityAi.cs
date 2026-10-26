using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    internal static void InstallAiSteering(Plugin plugin) => FeatureHook.Install(plugin.Harmony,
        typeof(AntigravityFeature), new[] {
            new FeatureHook(typeof(AILogicController), "RefreshSteeringToPoint", new[] { typeof(float) }, Prefix: nameof(AiSteeringPrefix)),
            new FeatureHook(typeof(PixelEasyCharMoveKartController), "SteerInput", new[] { typeof(float) }, Prefix: nameof(AiInputPrefix)),
            new FeatureHook(typeof(PixelEasyCharMoveKartController), "JumpInput", new[] { typeof(bool) }, Prefix: nameof(AiHopInputPrefix)),
            new FeatureHook(typeof(PixelKartPhysics), "JumpInput", new[] { typeof(bool) }, Prefix: nameof(AiPhysicsHopInputPrefix)),
            new FeatureHook(typeof(PixelEasyCharMoveKartController), "DriftInput", new[] { typeof(bool) }, Prefix: nameof(AiDriftInputPrefix)),
            new FeatureHook(typeof(PixelKartPhysics), "DriftInput", new[] { typeof(bool) }, Prefix: nameof(AiPhysicsDriftInputPrefix))
        });

    private static bool TryGetAi(PixelKartPhysics physics, out KartState entry)
    {
        entry = null!;
        return physics != null && physics.kartController?.parentPlayer != null &&
            physics.kartController.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_AI_LOCAL &&
            TryGetAttached(physics, out entry);
    }

    private static bool IsAttachedAi(PixelKartPhysics physics) => TryGetAi(physics, out _);

    private static bool AiSteeringPrefix(AILogicController __instance, float __0)
    {
        var physics = __instance.kartController?.kartPhysics;
        if (physics == null || !TryGetAi(physics, out var entry)) return true;
        var follower = __instance.playerBezierFollower;
        var spline = follower?.currentUsedBezierSpline;
        Vector3 direction = entry.Forward;
        Vector3 roadRight = Vector3.Cross(entry.Up, entry.Forward);
        float lateral = 0f, normal = 0f;
        bool validRoute = false;
        if (spline != null && spline.Count >= 2 && float.IsFinite(follower!.fCurrentGreenMatchedSplineNormalized))
        {
            float progress = follower.fCurrentGreenMatchedSplineNormalized;
            Vector3 offset = spline.GetPoint(progress) - entry.Motor.TransientPosition;
            Vector3 tangent = Vector3.ProjectOnPlane(spline.GetTangent(progress), entry.Up).normalized;
            lateral = Vector3.Dot(offset, Vector3.Cross(entry.Up, tangent));
            normal = Vector3.Dot(offset, entry.Up);
            // A teleport route can point outside the physical loop. Keep the
            // transported road heading instead of steering toward a distant lane.
            validRoute = float.IsFinite(offset.sqrMagnitude) && tangent.sqrMagnitude > .5f &&
                Mathf.Abs(normal) < 4f && Mathf.Abs(lateral) < 12f;
            if (validRoute)
            {
                roadRight = Vector3.Cross(entry.Up, tangent);
                direction = tangent * 12f + roadRight * lateral;
            }
        }
        float angle = Vector3.SignedAngle(entry.Forward, direction, entry.Up);
        float turn = Mathf.Clamp(angle, -100f * Mathf.Clamp(__0, 0f, .05f), 100f * Mathf.Clamp(__0, 0f, .05f));
        entry.Forward = (Quaternion.AngleAxis(turn, entry.Up) * entry.Forward).normalized;
        entry.AiSteeringInput = Mathf.Clamp(angle / 45f, -1f, 1f);
        entry.AiGuidanceReady = true;
        entry.AiRoadRight = validRoute ? roadRight : Vector3.Cross(entry.Up, entry.Forward);
        entry.AiLateralTarget = validRoute ? Mathf.Clamp(lateral * 2f, -6f, 6f) : 0f;
        entry.AiCanDrift = validRoute && Mathf.Abs(lateral) < 3f && Mathf.Abs(angle) < 15f;
        if (physics.bIsDrifting && Mathf.Abs(angle) > 5f && Mathf.Sign(angle) != Mathf.Sign(physics.fDriftingSteerDir))
            entry.AiCanDrift = false;
        if (!entry.AiCanDrift)
        {
            physics.ForceStopDrifting();
            entry.Controller.bIsDriftInput = physics.bIsDriftInput = false;
            if (__instance.pixelAIKartInput?.inputData != null) __instance.pixelAIKartInput.inputData.bDriftInput = false;
        }
        entry.Controller.steer_input = physics.steer_input = entry.AiSteeringInput;
        if (__instance.pixelAIKartInput?.inputData != null)
            __instance.pixelAIKartInput.inputData.fTurningInput = entry.AiSteeringInput;
        if (__instance.bUseBoosting && physics.bIsDrifting) __instance.UpdateBoostUsingWhileDrifting();
        if (Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        if (entry.AiSteeringLogCount < 12 && Time.unscaledTime >= entry.NextAiSteeringLog)
        {
            entry.AiSteeringLogCount++;
            entry.NextAiSteeringLog = Time.unscaledTime + 2f;
            float sideSpeed = Vector3.Dot(entry.Motor.Velocity, entry.AiRoadRight);
            Plugin.Instance?.Log.LogInfo($"Antigravity AI road guidance: kart={entry.Controller.GetInstanceID()} route={validRoute} angle={angle:0.0} centre={lateral:0.0} normal={normal:0.0} drift={physics.bIsDrifting} sideSpeed={sideSpeed:0.0} sideTarget={entry.AiLateralTarget:0.0}");
        }
        return false;
    }

    private static void GuideAiVelocity(KartState entry, ref Vector3 velocity, float deltaTime)
    {
        if (!entry.AiGuidanceReady || !IsAttachedAi(entry.Controller.kartPhysics) || !HasJumpContact(entry)) return;
        Vector3 right = Vector3.ProjectOnPlane(entry.AiRoadRight, entry.Up).normalized;
        if (right.sqrMagnitude < .5f) return;
        float side = Vector3.Dot(velocity, right);
        float corrected = Mathf.MoveTowards(side, entry.AiLateralTarget, 35f * Mathf.Clamp(deltaTime, 0f, .05f));
        // Turning the heading alone leaves drift momentum sliding off the road.
        velocity += right * (corrected - side);
    }

    private static void AiInputPrefix(PixelEasyCharMoveKartController __instance, ref float __0)
    { if (TryGetAi(__instance.kartPhysics, out var entry) && entry.AiGuidanceReady) __0 = entry.AiSteeringInput; }

    private static void AiHopInputPrefix(PixelEasyCharMoveKartController __instance, ref bool __0)
    { if (IsAttachedAi(__instance.kartPhysics)) __0 = false; }

    private static void AiPhysicsHopInputPrefix(PixelKartPhysics __instance, ref bool __0)
    { if (IsAttachedAi(__instance)) __0 = false; }

    private static void AiDriftInputPrefix(PixelEasyCharMoveKartController __instance, ref bool __0)
    { if (TryGetAi(__instance.kartPhysics, out var entry)) __0 &= entry.AiCanDrift; }

    private static void AiPhysicsDriftInputPrefix(PixelKartPhysics __instance, ref bool __0)
    { if (TryGetAi(__instance, out var entry)) __0 &= entry.AiCanDrift; }
}

using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private const float DirectSurfaceActivationDegrees = 45f;
    private static BepInEx.Configuration.ConfigEntry<bool>? ShowStatus;
    private static string Status = "Antigravity: waiting for eligible road";
    private const float ContactGraceSeconds = .35f;
    private const float ContactGraceDistance = 1.5f;
    private const float HopGraceSeconds = .8f;
    private const float HopGraceDistance = 2.5f;
    private const float SteepSurfaceDegrees = 70f;

    private sealed class KartState
    {
        internal PixelEasyCharMoveKartController Controller = null!;
        internal KinematicCharacterMotor Motor = null!;
        internal bool Attached;
        internal Vector3 Up, Normal, Forward;
        internal float HopElapsed, ContactGraceElapsed;
        internal int KartSceneHandle, ActiveSceneHandle;
        internal bool Applied;
        internal float NextSteeringLog;
        internal int SteeringLogCount;
        internal int VisualContactFrame = -1, VisualLogCount;
        internal bool VisualContact;
        internal float NextVisualLog;
        internal bool AcceptedHop;
        internal int JumpLogCount;
        internal int AiSteeringLogCount;
        internal float NextAiSteeringLog;
        internal float AiSteeringInput;
        internal bool AiGuidanceReady, AiCanDrift;
        internal bool LayersOverridden;
        // Direct surface mode must carry tangential speed through the native
        // CharacterUp/steep-slope transition. The stock velocity pass can
        // otherwise collapse the forward component to zero once the kart is
        // past the normal 60-degree world-up limit.
        internal float SurfaceForwardSpeed;
        // Presentation has a higher bar than generic solid contact. This avoids
        // the camera entering on ordinary flat or lightly banked road pieces.
        internal bool CameraEngaged;
        internal int OriginalStableLayers;
    }

    private struct MainVelocityPatchState
    {
        internal bool Patched, RestoreCap, OriginalCap;
    }

    private static readonly Dictionary<int, KartState> States = new();
    private static readonly HashSet<int> AnnouncedAiRacers = new();
    private static readonly Dictionary<int, KartState> PendingReleaseRotation = new();
    internal static void Install(Plugin plugin)
    {
        ShowStatus = plugin.Config.Bind("Antigravity", "ShowStatus", true, "Show current surface-contact status while testing.");
        FeatureHook.Install(plugin.Harmony, typeof(AntigravityFeature), Hooks());
        plugin.Log.LogInfo("Antigravity revision: shared human/AI offline surface physics and visual grounding; per-racer recovery.");
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_AfterCharacterUpdate),
            new[] { typeof(float) }, Postfix: nameof(AfterCharacterUpdate)),
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_BeforeCharacterUpdate),
            new[] { typeof(float) }, Prefix: nameof(BeforeCharacterUpdatePrefix)),
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_UpdateRotation),
            new[] { typeof(Quaternion).MakeByRefType(), typeof(float) }, Postfix: nameof(UpdateRotationPostfix)),
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_UpdateVelocity),
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Postfix: nameof(CharacterVelocityPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "CalculatePhysicsAxisDirections",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Postfix: nameof(AxisDirectionsPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateSteeringCentiperalForceAndOrient",
            new[] { typeof(Vector3), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float), typeof(float), typeof(float), typeof(float) },
            Prefix: nameof(SteeringPrefix), Postfix: nameof(SteeringPostfix), Finalizer: nameof(SteeringFinalizer)),
        new FeatureHook(typeof(PixelKartPhysics), "TriggerPhysicsJump",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(JumpPrefix), Postfix: nameof(JumpPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "JumpInput",
            new[] { typeof(bool) }, Postfix: nameof(JumpInputPostfix)),
        new FeatureHook(typeof(PixelEasyCharMoveKartController), "JumpInput",
            new[] { typeof(bool) }, Postfix: nameof(ControllerJumpInputPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "CheckAndDecideIfAllowedToJumpInPhysics",
            new[] { typeof(float) }, Prefix: nameof(JumpGroundPrefix),
            Postfix: nameof(RestoreJumpGround), Finalizer: nameof(RestoreJumpGround)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateSlopeVelocity",
            new[] { typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float) }, Prefix: nameof(SlopePrefix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateVelocity",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(MainVelocityPrefix),
            Postfix: nameof(MainVelocityPostfix), Finalizer: nameof(MainVelocityFinalizer)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(RaceResetPostfix))
    };
}

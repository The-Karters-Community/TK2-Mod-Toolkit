using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static BepInEx.Configuration.ConfigEntry<bool>? ShowStatus;
    private static BepInEx.Configuration.ConfigEntry<bool>? Diagnostics;
    private static string Status = "Antigravity: waiting for eligible road";
    private static float NextAttachedStatusTime;
    private static float NextDiagnosticLogTime;
    private struct LookaheadDiagnostic
    {
        internal bool Attempted, Hit, Eligible, SameTrack;
        internal float HeightError, NormalTurnDegrees;
        internal Vector3 Normal;
    }

    private sealed class KartState
    {
        internal PixelEasyCharMoveKartController Controller = null!;
        internal KinematicCharacterMotor Motor = null!;
        internal AntigravityPolicy.State Policy;
        internal int KartSceneHandle, ActiveSceneHandle;
        internal bool HasNativeYaw;
        internal bool Applied;
        internal float LastNativeYaw;
        internal bool AcceptedHop;
        internal Collider? SupportCollider;
        internal bool SurfaceMode, LayersOverridden;
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
    private static readonly Dictionary<int, KartState> PendingReleaseRotation = new();
    private static readonly AntigravityPolicy.Settings Settings = AntigravityPolicy.Settings.Default;
    internal static void Install(Plugin plugin)
    {
        ShowStatus = plugin.Config.Bind("Antigravity", "ShowStatus", true, "Show current surface-contact status while testing.");
        Diagnostics = plugin.Config.Bind("Antigravity", "Diagnostics", false,
            "Show contact/frame details and rate-limited Antigravity logs during a local offline race.");
        FeatureHook.Install(plugin.Harmony, typeof(AntigravityFeature), Hooks());
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
            new[] { typeof(Vector3), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float), typeof(float), typeof(float), typeof(float) }, Postfix: nameof(SteeringPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "TriggerPhysicsJump",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(JumpPrefix), Postfix: nameof(JumpPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateSlopeVelocity",
            new[] { typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float) }, Prefix: nameof(SlopePrefix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateVelocity",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(MainVelocityPrefix),
            Postfix: nameof(MainVelocityPostfix), Finalizer: nameof(MainVelocityFinalizer)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(RaceResetPostfix))
    };

    // Called from StudioBehaviour.Update only while entries exist. It does no scene or kart scans.
    internal static void Tick()
    {
        if (States.Count == 0 && PendingReleaseRotation.Count == 0) return;
        var plugin = Plugin.Instance;
        int scene = SceneManager.GetActiveScene().handle;
        foreach (var pending in PendingReleaseRotation.Values)
            if (pending.Controller == null || pending.ActiveSceneHandle != scene ||
                pending.KartSceneHandle != pending.Controller.gameObject.scene.handle)
            { PendingReleaseRotation.Clear(); break; }
        if (States.Count == 0) return;
        foreach (var entry in States.Values)
            if (entry.Controller == null || entry.ActiveSceneHandle != scene ||
                entry.KartSceneHandle != entry.Controller.gameObject.scene.handle)
            { ReleaseAll(); PendingReleaseRotation.Clear(); return; }
        if (plugin != null && plugin.AntigravityEnabled.Value && Plugin.OfflineLabAllowed)
        {
            foreach (var entry in States.Values)
                if (!AirShiftFeature.Running(entry.Controller))
                { ReleaseAll(); SetStatus("Antigravity: waiting for a local offline race"); return; }
            return;
        }
        ReleaseAll();
        SetStatus("Antigravity: disabled or unavailable");
    }

    private static void AfterCharacterUpdate(PixelEasyCharMoveKartController __instance, float __0)
    {
        var plugin = Plugin.Instance;
        if (plugin == null) return;
        if (!plugin.AntigravityEnabled.Value || !AirShiftFeature.Running(__instance))
        {
            Tick();
            return;
        }
        if (__instance?.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
        int id = __instance.GetInstanceID();

        try
        {
            var physics = __instance.kartPhysics;
            var motor = __instance.Motor;
            if (physics == null || motor == null) { Release(id, true); return; }
            States.TryGetValue(id, out var entry);
            if (plugin.AntigravityAnySurface.Value)
            {
                UpdateSolidSurface(__instance, physics, motor, entry);
                return;
            }
            if (entry?.SurfaceMode == true) { Release(id, true); entry = null; }
            AntigravityPolicy.State policyState = entry?.Policy ?? default;
            var grounding = motor.GroundingStatus;
            Collider ground = grounding.GroundCollider;
            bool nativeStable = grounding.FoundAnyGround && grounding.IsStableOnGround && ground != null && !motor.MustUnground();
            bool launching = physics.bCatapultUsedLowerLateralFriction || physics.IsTeleportingFromAtoB();
            Vector3 normal = nativeStable ? grounding.GroundNormal : Vector3.zero;
            AntigravityPolicy.ContactKind contact = nativeStable
                ? AntigravityPolicy.ContactKind.NativeStableRoad : AntigravityPolicy.ContactKind.None;
            float probeSeparation = 0f;
            Collider? candidate = nativeStable ? ground : null;
            bool sameEligibleTrack = false;
            bool acceptedHop = entry?.AcceptedHop == true;
            bool driftHopCandidate = !nativeStable && policyState.Attached && acceptedHop && !launching;
            bool activeContactCandidate = !nativeStable && policyState.Attached && !driftHopCandidate && !launching;
            float probeLimit = driftHopCandidate ? Settings.MaxHopDistance : Settings.MaxContactGraceDistance;
            Collider? referenceRoad = entry?.SupportCollider ?? ground;
            if (!nativeStable && policyState.Attached && !launching &&
                TryContinuityProbe(__instance, motor, policyState.Normal, probeLimit,
                    out candidate, out var probeNormal, out probeSeparation))
            {
                contact = AntigravityPolicy.ContactKind.ContinuousRoadProbe;
                normal = probeNormal;
                if (!EligibleCollider(candidate!, __instance) ||
                    !SameEligibleTrack(referenceRoad!, candidate!))
                {
                    candidate = null;
                    contact = AntigravityPolicy.ContactKind.None;
                    normal = Vector3.zero;
                }
                else sameEligibleTrack = true;
            }

            LayerMask stableLayers = motor.StableGroundLayers;
            bool inMask = candidate != null && IsEligibleLayer(candidate.gameObject.layer,
                stableLayers.value, __instance.wallLayerToCollDetect.value,
                __instance.wallRespawn_Flat_ColliderLayer.value, __instance.wallRespawn_Always_ColliderLayer.value);
            bool trigger = candidate != null && candidate.isTrigger;
            bool playerCollider = candidate != null && candidate.GetComponentInParent<PixelEasyCharMoveKartController>() != null;
            Rigidbody? body = candidate != null ? candidate.attachedRigidbody : null;
            bool dynamicBody = body != null && !body.isKinematic;

            Vector3 velocity = motor.Velocity;
            Vector3 nativeHeading = physics.qSteeringOrientation * Vector3.forward;
            float tangentialSpeed = Vector3.ProjectOnPlane(velocity, normal.sqrMagnitude > .0001f ? normal.normalized : motor.CharacterUp).magnitude;
            Vector3 position = motor.TransientPosition;
            float forwardProgress = policyState.Armed
                ? AntigravityPolicy.ForwardProgress(position - policyState.LastPosition,
                    policyState.ArmForward, policyState.ArmNormal) : 0f;
            bool hasLookahead = false;
            var lookahead = new LookaheadDiagnostic { HeightError = float.PositiveInfinity };
            float nativeSlopeLimit = motor.MaxStableSlopeAngle;
            Vector3 nativeUp = motor.CharacterUp;
            float worldActivationThreshold = AntigravityPolicy.WorldActivationThreshold(nativeSlopeLimit, in Settings);
            if (!policyState.Attached && nativeStable && ground != null &&
                worldActivationThreshold >= Settings.MinActivationAngle &&
                Vector3.Angle(Vector3.up, normal) >= Mathf.Max(Settings.MinActivationAngle, worldActivationThreshold - 25f))
            {
                hasLookahead = TryForwardLookahead(__instance, motor, ground, normal, nativeHeading,
                    tangentialSpeed, Time.fixedDeltaTime, out lookahead);
                sameEligibleTrack = lookahead.SameTrack;
            }
            var sample = new AntigravityPolicy.Sample(Time.fixedDeltaTime, tangentialSpeed,
                position, normal, nativeHeading, probeSeparation, 0f, contact,
                true, true, !Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet,
                inMask, trigger, playerCollider, dynamicBody, launching, acceptedHop,
                nativeUp, nativeSlopeLimit, forwardProgress, hasLookahead, lookahead.Normal,
                lookahead.HeightError, sameEligibleTrack,
                activeContactCandidate && contact == AntigravityPolicy.ContactKind.ContinuousRoadProbe,
                lookahead.Attempted, lookahead.Hit, lookahead.Eligible, lookahead.SameTrack,
                lookahead.NormalTurnDegrees);
            var decision = AntigravityPolicy.Step(ref policyState, in sample, in Settings);
            if (!decision.Attached)
            {
                if (decision.Armed)
                {
                    if (entry?.Applied == true)
                    {
                        Release(id, true);
                        entry = null;
                    }
                    if (entry == null)
                    {
                        if (States.Count >= 4) { ReleaseAll(); PendingReleaseRotation.Clear(); }
                        entry = new KartState { Controller = __instance, Motor = motor,
                            KartSceneHandle = __instance.gameObject.scene.handle,
                            ActiveSceneHandle = SceneManager.GetActiveScene().handle, Policy = policyState,
                            SupportCollider = ground };
                        if (TryNativeYaw(physics.qSteeringOrientation, out float armYaw))
                        { entry.LastNativeYaw = armYaw; entry.HasNativeYaw = true; }
                        States[id] = entry;
                    }
                    else
                    {
                        entry.Policy = policyState;
                        entry.SupportCollider = ground;
                        if (TryNativeYaw(physics.qSteeringOrientation, out float armYaw))
                        { entry.LastNativeYaw = armYaw; entry.HasNativeYaw = true; }
                    }
                    SetArmedStatus(decision.ArmSlopeAngle, nativeSlopeLimit, policyState, in sample);
                }
                else
                {
                    if (entry != null) Release(id, true);
                    SetStatus(StatusFor(decision.Reason));
                }
                return;
            }
            if (nativeStable && entry != null) entry.AcceptedHop = false;

            if (entry == null)
            {
                if (States.Count >= 4)
                {
                    ReleaseAll();
                    PendingReleaseRotation.Clear();
                }
                entry = new KartState { Controller = __instance, Motor = motor,
                    KartSceneHandle = __instance.gameObject.scene.handle,
                    ActiveSceneHandle = SceneManager.GetActiveScene().handle };
                if (TryNativeYaw(physics.qSteeringOrientation, out float yaw))
                { entry.LastNativeYaw = yaw; entry.HasNativeYaw = true; }
                States[id] = entry;
            }
            if (!entry.Applied && TryNativeYaw(physics.qSteeringOrientation, out float activeYaw))
            { entry.LastNativeYaw = activeYaw; entry.HasNativeYaw = true; }
            entry.Applied = true;
            entry.Policy = policyState;
            UpdateCameraEngagement(entry, policyState.Up, motor.MaxStableSlopeAngle);
            if (nativeStable) entry.SupportCollider = ground;
            if (ApplyFrame(physics, motor, decision.Forward, decision.Up, velocity, true))
                plugin.SessionModified = true;
            SetAttachedStatus(policyState, contact, motor, grounding.FoundAnyGround,
                grounding.IsStableOnGround, grounding.GroundNormal);
        }
        catch (Exception ex) { Fail(id, plugin, ex); }
    }

    // KCC evaluates its stable-slope limit before AfterCharacterUpdate. Feed the
    // already-established surface frame back at this boundary so a loop remains
    // a stable surface when its world tilt crosses the native ~60-degree limit.
    private static void BeforeCharacterUpdatePrefix(PixelEasyCharMoveKartController __instance, float __0)
    {
        var plugin = Plugin.Instance;
        if (plugin?.AntigravityEnabled.Value != true || !plugin.AntigravityAnySurface.Value ||
            !AirShiftFeature.Running(__instance) || !States.TryGetValue(__instance.GetInstanceID(), out var entry) ||
            !entry.SurfaceMode || !entry.Policy.Attached || entry.Motor == null)
            return;
        Vector3 forward = Vector3.ProjectOnPlane(entry.Policy.Forward, entry.Policy.Up).normalized;
        if (forward.sqrMagnitude < .5f || entry.Policy.Up.sqrMagnitude < .5f) return;
        entry.Motor.TransientRotation = Quaternion.LookRotation(forward, entry.Policy.Up.normalized);
    }

    private static bool TryContinuityProbe(PixelEasyCharMoveKartController controller, KinematicCharacterMotor motor, Vector3 up, float maxDistance,
        out Collider? candidate, out Vector3 normal, out float separation)
    {
        candidate = null; normal = Vector3.zero; separation = 0f;
        Vector3 bottom = WorldCapsuleBottom(motor);
        Vector3 origin = bottom + up * .1f;
        // In direct/all-surface mode the next quad may be on a wall layer that
        // KCC deliberately excludes from StableGroundLayers. Use the full
        // collision mask for this bounded probe; EligibleCollider still rejects
        // triggers, dynamic bodies, respawn surfaces and player colliders.
        LayerMask probeLayers = Plugin.Instance?.AntigravityAnySurface.Value == true
            ? motor.CollidableLayers : motor.StableGroundLayers;
        float lateral = Mathf.Clamp(motor.Capsule.radius * .65f, .08f, .55f);
        Vector3 forward = Vector3.ProjectOnPlane(motor.TransientRotation * Vector3.forward, up).normalized;
        Vector3 right = forward.sqrMagnitude > .5f ? Vector3.Cross(up, forward).normalized : Vector3.zero;
        RaycastHit best = default;
        bool found = false;
        ConsiderContinuityRay(controller, origin, -up, maxDistance, probeLayers, ref best, ref found);
        if (forward.sqrMagnitude > .5f)
        {
            ConsiderContinuityRay(controller, origin + forward * lateral, -up, maxDistance, probeLayers, ref best, ref found);
            ConsiderContinuityRay(controller, origin - forward * lateral, -up, maxDistance, probeLayers, ref best, ref found);
        }
        if (right.sqrMagnitude > .5f)
        {
            ConsiderContinuityRay(controller, origin + right * lateral, -up, maxDistance, probeLayers, ref best, ref found);
            ConsiderContinuityRay(controller, origin - right * lateral, -up, maxDistance, probeLayers, ref best, ref found);
        }
        if (!found) return false;
        candidate = best.collider;
        normal = best.normal;
        separation = Mathf.Max(0f, best.distance - .1f);
        return true;
    }

    private static void ConsiderContinuityRay(PixelEasyCharMoveKartController controller,
        Vector3 origin, Vector3 direction, float maxDistance, LayerMask layers,
        ref RaycastHit best, ref bool found)
    {
        if (!Physics.Raycast(origin, direction, out var hit, maxDistance + .1f,
            layers, QueryTriggerInteraction.Ignore) || !EligibleCollider(hit.collider, controller)) return;
        if (!found || hit.distance < best.distance)
        { best = hit; found = true; }
    }

    private static Vector3 WorldCapsuleBottom(KinematicCharacterMotor motor) =>
        motor.TransientPosition + motor.TransientRotation * motor.CharacterTransformToCapsuleBottom;

    private static bool TryForwardLookahead(PixelEasyCharMoveKartController controller,
        KinematicCharacterMotor motor, Collider currentRoad, Vector3 normal, Vector3 referenceForward,
        float tangentSpeed, float deltaTime, out LookaheadDiagnostic diagnostic)
    {
        diagnostic = new LookaheadDiagnostic { Attempted = true, HeightError = float.PositiveInfinity };
        Vector3 forward = Vector3.ProjectOnPlane(referenceForward, normal).normalized;
        if (forward.sqrMagnitude < .0001f) return false;
        float distance = Mathf.Clamp(tangentSpeed * deltaTime * 2f, .25f, 1.5f);
        Vector3 expectedRoadPoint = WorldCapsuleBottom(motor) + forward * distance;
        Vector3 origin = expectedRoadPoint + normal * .35f;
        float maxCastDistance = .35f + Settings.MaxLookaheadHeight;
        if (!Physics.Raycast(origin, -normal, out var hit, maxCastDistance,
            motor.StableGroundLayers, QueryTriggerInteraction.Ignore)) return false;
        diagnostic.Hit = true;
        if (!EligibleCollider(hit.collider, controller)) return false;
        diagnostic.Eligible = true;
        if (!SameEligibleTrack(currentRoad, hit.collider)) return false;
        diagnostic.SameTrack = true;
        diagnostic.HeightError = Mathf.Abs(Vector3.Dot(hit.point - expectedRoadPoint, normal));
        diagnostic.Normal = hit.normal;
        diagnostic.NormalTurnDegrees = Vector3.Angle(normal, hit.normal);
        if (diagnostic.HeightError > Settings.MaxLookaheadHeight) return false;
        return true;
    }

    private static bool SameEligibleTrack(Collider current, Collider candidate)
    {
        if (current == null || candidate == null) return false;
        if (current == candidate) return true;
        var a = current as MeshCollider;
        var b = candidate as MeshCollider;
        return a != null && b != null && a.sharedMesh != null && a.sharedMesh == b.sharedMesh;
    }

    internal static bool IsEligibleLayer(int layer, int stableGroundMask, int wallMask, int flatRespawnMask, int alwaysRespawnMask)
    {
        if (layer < 0 || layer > 31) return false;
        int bit = 1 << layer;
        return (stableGroundMask & bit) != 0 && (wallMask & bit) == 0 &&
            (flatRespawnMask & bit) == 0 && (alwaysRespawnMask & bit) == 0;
    }

    private static bool EligibleCollider(Collider collider, PixelEasyCharMoveKartController controller)
    {
        if (Plugin.Instance?.AntigravityAnySurface.Value == true)
            return EligibleSolidSurface(collider, controller);
        if (collider == null || collider.isTrigger || controller == null) return false;
        if (!IsEligibleLayer(collider.gameObject.layer, controller.Motor.StableGroundLayers.value,
            controller.wallLayerToCollDetect.value, controller.wallRespawn_Flat_ColliderLayer.value,
            controller.wallRespawn_Always_ColliderLayer.value)) return false;
        if (collider.GetComponentInParent<PixelEasyCharMoveKartController>() != null) return false;
        Rigidbody body = collider.attachedRigidbody;
        return body == null || body.isKinematic;
    }

    private static void UpdateRotationPostfix(PixelEasyCharMoveKartController __instance, ref Quaternion __0)
    {
        if (PendingReleaseRotation.Count > 0 && IsLocalHuman(__instance) &&
            PendingReleaseRotation.Remove(__instance.GetInstanceID(), out var pending))
        {
            var release = AntigravityPolicy.ReleaseAtCurrentHeading(__instance.transform.forward, pending.Motor.Velocity);
            __0 = release.Rotation;
            return;
        }
        if (!CanReadController(__instance) ||
            !TryGet(__instance, out var entry) || !entry.Policy.Attached) return;
        Quaternion corrected = Quaternion.LookRotation(entry.Policy.Forward, entry.Policy.Up);
        if (Quaternion.Angle(__0, corrected) > .001f && Plugin.Instance != null)
            Plugin.Instance.SessionModified = true;
        __0 = corrected;
    }

    private static void AxisDirectionsPostfix(PixelKartPhysics __instance, ref Vector3 __0)
    {
        if (!CanReadState(__instance)) return;
        var controller = __instance.kartController;
        if (controller == null || !TryGet(controller, out var entry) || !entry.Policy.Attached) return;
        if (ApplyAxes(__instance, entry.Policy.Forward, entry.Policy.Up, __0) && Plugin.Instance != null)
            Plugin.Instance.SessionModified = true;
    }

    // Native steering mutates qSteeringOrientation after its axis cache was calculated. Capture
    // each resulting yaw delta here and apply it around the current local road normal.
    private static void SteeringPostfix(PixelKartPhysics __instance, ref Vector3 __1)
    {
        if (!CanReadState(__instance) || __instance.kartController == null ||
            !TryGet(__instance.kartController, out var entry) || !entry.Policy.Attached) return;
        if (!TryNativeYaw(__instance.qSteeringOrientation, out float yaw)) return;
        if (entry.HasNativeYaw)
            AntigravityPolicy.ApplySteeringDelta(ref entry.Policy, Mathf.DeltaAngle(entry.LastNativeYaw, yaw));
        entry.LastNativeYaw = yaw;
        entry.HasNativeYaw = true;
        if (ApplyAxes(__instance, entry.Policy.Forward, entry.Policy.Up, __1) && Plugin.Instance != null)
            Plugin.Instance.SessionModified = true;
    }

    private static void MainVelocityPrefix(PixelKartPhysics __instance, ref Vector3 __0, out MainVelocityPatchState __state)
    {
        __state = default;
        if (!TryGetAttached(__instance, out _)) return;
        bool original = __instance.bLimitVerticalUpVelocity;
        __state = new MainVelocityPatchState { Patched = true, RestoreCap = true, OriginalCap = original };
        __instance.bLimitVerticalUpVelocity = false;
    }

    private static void MainVelocityPostfix(PixelKartPhysics __instance, ref Vector3 __0, float __1,
        ref MainVelocityPatchState __state)
    {
        if (!__state.Patched) return;
        try
        {
            if (TryGetAttachedHop(__instance, out var entry) && entry.Motor != null &&
                !entry.Motor.GroundingStatus.FoundAnyGround)
            {
                Vector3 scratchVelocity = Vector3.zero;
                Vector3 nativeGravityDelta = Vector3.zero;
                __instance.UpdateGravityWhenNotGrounded(ref scratchVelocity, ref nativeGravityDelta, __1);
                Vector3 surfaceGravityDelta = AntigravityPolicy.RedirectAddedDelta(
                    Vector3.zero, nativeGravityDelta, Vector3.down, -entry.Policy.Up);
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
        if (!state.RestoreCap) return;
        physics.bLimitVerticalUpVelocity = state.OriginalCap;
        state.RestoreCap = false;
    }

    private static void CharacterVelocityPostfix(PixelEasyCharMoveKartController __instance, ref Vector3 __0)
    {
        if (!CanReadController(__instance) || !TryGet(__instance, out var entry) ||
            !entry.Policy.Attached || !entry.Applied) return;
        var motor = entry.Motor;
        var grounding = motor.GroundingStatus;
        bool directSurface = entry.SurfaceMode;
        bool surfaceHop = directSurface && (entry.AcceptedHop || entry.Policy.HopElapsed > 0f);
        if (!directSurface && (!grounding.FoundAnyGround || !grounding.IsStableOnGround || motor.MustUnground())) return;
        if (directSurface && !grounding.FoundAnyGround && !surfaceHop) return;
        Vector3 aligned = AntigravityPolicy.AlignVelocityTangent(__0, entry.Policy.Normal, entry.Policy.Forward);
        bool changed = (aligned - __0).sqrMagnitude > .000001f;
        __0 = aligned;

        // When a loop crosses the native slope boundary, UpdateVelocity can
        // return an almost-zero tangent even though the kart was travelling
        // normally one tick earlier. Keep bounded forward momentum on steep
        // direct surfaces (and during the accepted drift hop), while letting
        // flat-road braking and reverse input behave natively.
        if (directSurface && __instance.kartPhysics != null &&
            !__instance.kartPhysics.bIsMovingBackward)
        {
            Vector3 up = entry.Policy.Up.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(entry.Policy.Forward, up).normalized;
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
        if (changed && Plugin.Instance != null) Plugin.Instance.SessionModified = true;
    }

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

    private static bool TryNativeYaw(Quaternion orientation, out float yaw)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(orientation * Vector3.forward, Vector3.up);
        if (horizontal.sqrMagnitude < .0001f) { yaw = 0f; return false; }
        yaw = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
        return true;
    }

    private static void JumpPrefix(PixelKartPhysics __instance, ref Vector3 __0, out Vector3 __state)
    {
        __state = __0;
    }

    private static void JumpPostfix(PixelKartPhysics __instance, ref Vector3 __0, Vector3 __state)
    {
        if (!TryGetAttached(__instance, out var entry)) return;
        float surfaceTilt = Vector3.Angle(Vector3.up, entry.Policy.Up);
        // Preserve ordinary jumps on flat/banked road. Surface-frame jump
        // transport is reserved for the steep/inverted portion where the
        // native world-up impulse would otherwise detach the kart.
        if (entry.SurfaceMode && surfaceTilt < 65f)
        {
            DiagnosticLog($"jump: native road jump tilt={surfaceTilt:0}°");
            return;
        }
        Vector3 nativeDelta = __0 - __state;
        if (nativeDelta.sqrMagnitude > .000001f) entry.AcceptedHop = true;
        DiagnosticLog($"jump: surface hop tilt={surfaceTilt:0}° delta={nativeDelta.magnitude:0.0}");
        Vector3 redirected = AntigravityPolicy.RedirectAddedDelta(__state, __0, Vector3.up, entry.Policy.Up);
        if ((redirected - __0).sqrMagnitude > .000001f && Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        __0 = redirected;
    }

    private static bool SlopePrefix(PixelKartPhysics __instance) =>
        !TryGetAttached(__instance, out var entry) ||
        (!entry.SurfaceMode && Vector3.Angle(Vector3.up, entry.Policy.Up) < Settings.SteepAngle);

    private static bool TryGetAttachedHop(PixelKartPhysics physics, out KartState entry) =>
        TryGetAttached(physics, out entry) && (entry.Policy.HopElapsed > 0f || entry.AcceptedHop);

    private static bool TryGetAttached(PixelKartPhysics physics, out KartState entry)
    {
        if (!CanReadState(physics)) { entry = null!; return false; }
        var controller = physics.kartController;
        if (controller != null) return TryGet(controller, out entry) && entry.Policy.Attached;
        entry = null!; return false;
    }

    private static bool CanReadState(PixelKartPhysics physics)
    {
        var plugin = Plugin.Instance;
        if (States.Count == 0 || plugin?.AntigravityEnabled.Value != true || physics == null) return false;
        var controller = physics.kartController;
        return controller != null && AirShiftFeature.Running(controller);
    }

    private static bool CanReadController(PixelEasyCharMoveKartController controller)
    {
        var plugin = Plugin.Instance;
        return States.Count != 0 && plugin?.AntigravityEnabled.Value == true && AirShiftFeature.Running(controller);
    }

    private static bool TryGet(PixelEasyCharMoveKartController controller, out KartState entry) =>
        States.TryGetValue(controller.GetInstanceID(), out entry!) && entry.KartSceneHandle == controller.gameObject.scene.handle;

    private static bool IsLocalHuman(PixelEasyCharMoveKartController controller) =>
        controller != null && controller.parentPlayer != null &&
        controller.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL;

    private static void UpdateCameraEngagement(KartState entry, Vector3 up, float nativeSlopeLimit)
    {
        float worldTilt = Vector3.Angle(Vector3.up, up);
        float enter = Mathf.Clamp(nativeSlopeLimit - 8f, 15f, 75f);
        float exit = Mathf.Clamp(enter - 8f, 8f, enter);
        entry.CameraEngaged = entry.CameraEngaged
            ? worldTilt >= exit
            : worldTilt >= enter;
    }

    internal static bool TryGetActiveFrame(Ant_Player player, out Vector3 up, out Vector3 forward)
    {
        up = Vector3.zero;
        forward = Vector3.zero;
        var plugin = Plugin.Instance;
        if (player == null || plugin?.AntigravityEnabled.Value != true || !Plugin.OfflineLabAllowed || States.Count == 0)
            return false;
        foreach (var entry in States.Values)
        {
            if (entry.Controller == null || entry.Controller.parentPlayer != player ||
                !entry.Policy.Attached || !entry.CameraEngaged) continue;
            up = entry.Policy.Up;
            forward = entry.Policy.Forward;
            return up.sqrMagnitude > .9f && forward.sqrMagnitude > .9f;
        }
        return false;
    }

    internal static void DrawStatus()
    {
        if (ShowStatus?.Value == true && Plugin.Instance?.AntigravityEnabled.Value == true &&
            Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 190, 1400, 48), Status);
    }

    private static void SetStatus(string status)
    {
        if (!string.Equals(Status, status, StringComparison.Ordinal)) Status = status;
    }

    private static void DiagnosticLog(string message, bool force = false)
    {
        var plugin = Plugin.Instance;
        if (plugin == null || Diagnostics?.Value != true || (!force && Time.unscaledTime < NextDiagnosticLogTime)) return;
        NextDiagnosticLogTime = Time.unscaledTime + .35f;
        plugin.Log.LogInfo("[Antigravity] " + message);
    }

    private static void SetAttachedStatus(AntigravityPolicy.State state, AntigravityPolicy.ContactKind contact,
        KinematicCharacterMotor motor, bool foundGround, bool stableGround, Vector3 groundNormal)
    {
        if (Time.unscaledTime < NextAttachedStatusTime) return;
        NextAttachedStatusTime = Time.unscaledTime + .2f;

        float roadTilt = Vector3.Angle(Vector3.up, state.Normal);
        float policyTilt = Vector3.Angle(Vector3.up, state.Up);
        float motorTilt = Vector3.Angle(Vector3.up, motor.CharacterUp);
        float frameError = Vector3.Angle(state.Up, motor.CharacterUp);
        string support = foundGround && stableGround
            ? "stable road" : contact == AntigravityPolicy.ContactKind.ContinuousRoadProbe
                ? "drift hop" : "frame installed";
        string landingError = foundGround
            ? $" / contact Δ{Vector3.Angle(state.Up, groundNormal):0}°" : string.Empty;
        string hop = state.HopElapsed > 0f ? " / hop grace" : string.Empty;
        SetStatus($"Antigravity: {support} road {roadTilt:0}° / policy {policyTilt:0}° / motor {motorTilt:0}° (Δ{frameError:0}°){landingError}{hop}");
    }

    private static void SetArmedStatus(float slopeAngle, float nativeLimit,
        AntigravityPolicy.State state, in AntigravityPolicy.Sample sample)
    {
        if (Time.unscaledTime < NextAttachedStatusTime) return;
        NextAttachedStatusTime = Time.unscaledTime + .2f;
        float target = AntigravityPolicy.WorldActivationThreshold(nativeLimit, in Settings);
        var blocker = AntigravityPolicy.GetArmBlockReason(in state, in sample, in Settings);
        float lookSlope = sample.LookaheadNormal.sqrMagnitude > .0001f
            ? Vector3.Angle(Vector3.up, sample.LookaheadNormal) : 0f;
        string lookahead = !sample.LookaheadAttempted ? "not-sampled" : !sample.LookaheadHit ? "miss" :
            !sample.LookaheadEligible ? "ineligible" : !sample.LookaheadSameTrack ? "other-track" : "hit";
        string reset = string.IsNullOrEmpty(state.LastArmResetCause) ? "none" : state.LastArmResetCause;
        SetStatus($"Antigravity ARM {blocker}: road {slopeAngle:0}° / target {target:0}° / native CharacterUp limit {nativeLimit:0}° / speed {sample.Speed:0.0}/12 m/s\n" +
            $"look {lookahead} h{sample.LookaheadHeightError:0.00}/.55m Δn{sample.LookaheadNormalTurnDegrees:0}/20° slope{lookSlope:0}°/{target:0}°; history {state.StableSamples}/4 travel{state.ArmForwardTravel:0.00}/.50m curve{state.ArmCurveProgress:0.0}/2° normal{state.ArmNormalTravel:0.0}/2° reset:{reset}");
    }

    private static string StatusFor(AntigravityPolicy.DetachReason reason) => reason switch
    {
        AntigravityPolicy.DetachReason.LowSpeed => "Antigravity: steep surface needs more speed",
        AntigravityPolicy.DetachReason.HopExpired => "Antigravity: hop grace expired",
        AntigravityPolicy.DetachReason.TooFar => "Antigravity: road contact too far away",
        AntigravityPolicy.DetachReason.CurvatureLimit => "Antigravity: road normal changed too quickly",
        AntigravityPolicy.DetachReason.UnsupportedSlopeLimit => "Antigravity: native slope limit is too low for safe activation",
        _ => "Antigravity: released; waiting for eligible road"
    };

    private static void RaceResetPostfix() { ReleaseAll(); PendingReleaseRotation.Clear(); }

    internal static void ReleaseAll()
    {
        foreach (var pair in States)
        {
            try { ReleaseKart(pair.Value); }
            catch (Exception ex) { Plugin.Instance?.Log.LogWarning($"Antigravity cleanup failed for one kart: {ex.Message}"); }
        }
        States.Clear();
    }

    internal static void ReleaseKartBeforeRestore(PixelKartPhysics physics)
    {
        if (physics?.kartController == null) return;
        int id = physics.kartController.GetInstanceID();
        if (!States.TryGetValue(id, out var entry)) return;
        ReleaseKart(entry, true);
        States.Remove(id);
        PendingReleaseRotation.Remove(id);
    }

    internal static void RestoreAll()
    {
        ReleaseAll();
        PendingReleaseRotation.Clear();
    }

    private static void Release(int id, bool safeCharacterBoundary = false)
    {
        if (!States.TryGetValue(id, out var entry)) return;
        ReleaseKart(entry, safeCharacterBoundary);
        States.Remove(id);
        if (safeCharacterBoundary) PendingReleaseRotation.Remove(id);
    }

    private static void ReleaseKart(KartState entry, bool safeCharacterBoundary = false)
    {
        if (entry.LayersOverridden && entry.Motor != null)
        {
            entry.Motor.StableGroundLayers = entry.OriginalStableLayers;
            entry.LayersOverridden = false;
        }
        if (!entry.Applied) return;
        if (entry.Controller == null || entry.Motor == null || entry.Controller.kartPhysics == null) return;
        var release = AntigravityPolicy.ReleaseAtCurrentHeading(entry.Controller.transform.forward, entry.Motor.Velocity);
        ApplyAxes(entry.Controller.kartPhysics, release.Forward, release.Up, release.Velocity);
        entry.Motor.BaseVelocity = release.Velocity;
        if (safeCharacterBoundary) entry.Motor.TransientRotation = release.Rotation;
        else PendingReleaseRotation[entry.Controller.GetInstanceID()] = entry;
    }

    private static void Fail(int id, Plugin plugin, Exception exception)
    {
        ReleaseAll();
        plugin.AntigravityEnabled.Value = false;
        plugin.Log.LogError($"Antigravity stopped after runtime failure: {exception.Message}");
    }
}

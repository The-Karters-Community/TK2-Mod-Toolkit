using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static class AntigravityFeature
{
    private static BepInEx.Configuration.ConfigEntry<bool>? ShowStatus;
    private static string Status = "Antigravity: waiting for eligible road";
    private sealed class KartState
    {
        internal PixelEasyCharMoveKartController Controller = null!;
        internal KinematicCharacterMotor Motor = null!;
        internal AntigravityPolicy.State Policy;
        internal int SceneHandle;
        internal bool HasNativeYaw;
        internal float LastNativeYaw;
        internal bool AcceptedHop;
    }

    private static readonly Dictionary<int, KartState> States = new();
    private static readonly Dictionary<int, KartState> PendingReleaseRotation = new();
    private static readonly AntigravityPolicy.Settings Settings = AntigravityPolicy.Settings.Default;
    internal static void Install(Plugin plugin)
    {
        ShowStatus = plugin.Config.Bind("Antigravity", "ShowStatus", true, "Show current surface-contact status while testing.");
        FeatureHook.Install(plugin.Harmony, typeof(AntigravityFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_AfterCharacterUpdate),
            new[] { typeof(float) }, Postfix: nameof(AfterCharacterUpdate)),
        new FeatureHook(typeof(PixelEasyCharMoveKartController), nameof(PixelEasyCharMoveKartController.KinematicCharacterController_ICharacterController_UpdateRotation),
            new[] { typeof(Quaternion).MakeByRefType(), typeof(float) }, Postfix: nameof(UpdateRotationPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "CalculatePhysicsAxisDirections",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Postfix: nameof(AxisDirectionsPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateSteeringCentiperalForceAndOrient",
            new[] { typeof(Vector3), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float), typeof(float), typeof(float), typeof(float) }, Postfix: nameof(SteeringPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateGravityWhenNotGrounded",
            new[] { typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(GravityPrefix), Postfix: nameof(GravityPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "TriggerPhysicsJump",
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Prefix: nameof(JumpPrefix), Postfix: nameof(JumpPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), "UpdateSlopeVelocity",
            new[] { typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float) }, Prefix: nameof(SlopePrefix)),
        new FeatureHook(typeof(PixelKartPhysics), "LimitVerticalVelocity",
            new[] { typeof(Vector3).MakeByRefType() }, Prefix: nameof(VerticalLimitPrefix)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(RaceResetPostfix))
    };

    // Called from StudioBehaviour.Update only while entries exist. It does no scene or kart scans.
    internal static void Tick()
    {
        if (States.Count == 0 && PendingReleaseRotation.Count == 0) return;
        var plugin = Plugin.Instance;
        int scene = SceneManager.GetActiveScene().handle;
        foreach (var pending in PendingReleaseRotation.Values)
            if (pending.Controller == null || pending.SceneHandle != scene) { PendingReleaseRotation.Clear(); break; }
        if (States.Count == 0) return;
        foreach (var entry in States.Values)
            if (entry.SceneHandle != scene) { ReleaseAll(); PendingReleaseRotation.Clear(); return; }
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
            AntigravityPolicy.State policyState = entry?.Policy ?? default;
            var grounding = motor.GroundingStatus;
            Collider ground = grounding.GroundCollider;
            bool nativeStable = grounding.FoundAnyGround && grounding.IsStableOnGround && ground != null && !motor.MustUnground();
            bool drifting = physics.bIsDrifting;
            bool launching = physics.bCatapultUsedLowerLateralFriction || physics.IsTeleportingFromAtoB();
            Vector3 normal = nativeStable ? grounding.GroundNormal : Vector3.zero;
            AntigravityPolicy.ContactKind contact = nativeStable
                ? AntigravityPolicy.ContactKind.NativeStableRoad : AntigravityPolicy.ContactKind.None;
            float probeSeparation = 0f;
            Collider? candidate = nativeStable ? ground : null;
            bool acceptedHop = entry?.AcceptedHop == true;
            if (!nativeStable && policyState.Attached && (drifting || acceptedHop) && !launching &&
                TryContinuityProbe(__instance, motor, policyState.Normal, out candidate, out var probeNormal, out probeSeparation))
            {
                contact = AntigravityPolicy.ContactKind.ContinuousRoadProbe;
                normal = probeNormal;
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
            var sample = new AntigravityPolicy.Sample(Time.fixedDeltaTime, tangentialSpeed,
                motor.Transform.position, normal, nativeHeading, probeSeparation, 0f, contact,
                true, true, !Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet,
                inMask, trigger, playerCollider, dynamicBody, launching, drifting || acceptedHop);
            Vector3 previousNormal = policyState.Normal;
            bool previouslyAttached = policyState.Attached;
            var decision = AntigravityPolicy.Step(ref policyState, in sample, in Settings);
            if (!decision.Attached)
            {
                if (entry != null) Release(id, true);
                SetStatus(StatusFor(decision.Reason));
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
                    SceneHandle = __instance.gameObject.scene.handle };
                if (TryNativeYaw(physics.qSteeringOrientation, out float yaw))
                { entry.LastNativeYaw = yaw; entry.HasNativeYaw = true; }
                States[id] = entry;
            }
            entry.Policy = policyState;

            // Parallel transport the entire existing velocity vector. Quaternion rotation
            // preserves its norm and introduces no steering speed.
            if (previouslyAttached && Vector3.Angle(previousNormal, decision.Normal) > .001f)
            {
                motor.BaseVelocity = contact == AntigravityPolicy.ContactKind.NativeStableRoad
                    ? AntigravityPolicy.AlignVelocityTangent(motor.BaseVelocity, decision.Normal, decision.Forward)
                    : AntigravityPolicy.TransportVelocity(previousNormal, decision.Normal, motor.BaseVelocity);
                plugin.SessionModified = true;
            }
            if (ApplyFrame(physics, motor, decision.Forward, decision.Up, velocity, true))
                plugin.SessionModified = true;
            SetStatus(policyState.HopElapsed > 0f ? "Antigravity: drift hop grace active" : "Antigravity: surface adhesion active");
        }
        catch (Exception ex) { Fail(id, plugin, ex); }
    }

    private static bool TryContinuityProbe(PixelEasyCharMoveKartController controller, KinematicCharacterMotor motor, Vector3 up,
        out Collider? candidate, out Vector3 normal, out float separation)
    {
        candidate = null; normal = Vector3.zero; separation = 0f;
        Vector3 origin = motor.CharacterTransformToCapsuleBottom + up * .1f;
        if (!Physics.Raycast(origin, -up, out var hit, AntigravityPolicy.Settings.Default.MaxHopDistance + .1f,
            motor.StableGroundLayers, QueryTriggerInteraction.Ignore)) return false;
        Collider collider = hit.collider;
        if (!EligibleCollider(collider, controller)) return false;
        candidate = collider;
        normal = hit.normal;
        separation = Mathf.Max(0f, hit.distance - .1f);
        return true;
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

    private static bool ApplyFrame(PixelKartPhysics physics, KinematicCharacterMotor motor,
        Vector3 forward, Vector3 up, Vector3 velocity, bool updateTransform)
    {
        forward = Vector3.ProjectOnPlane(forward, up).normalized;
        bool changed = ApplyAxes(physics, forward, up, velocity);
        Quaternion rotation = Quaternion.LookRotation(forward, up);
        if (updateTransform && Quaternion.Angle(motor.Transform.rotation, rotation) > .001f)
        { motor.Transform.rotation = rotation; changed = true; }
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

    private static void GravityPrefix(PixelKartPhysics __instance, ref Vector3 __1, out Vector3 __state)
    {
        __state = __1;
    }

    private static void GravityPostfix(PixelKartPhysics __instance, ref Vector3 __1, Vector3 __state)
    {
        if (!TryGetAttachedHop(__instance, out var entry)) return;
        Vector3 redirected = AntigravityPolicy.RedirectAddedDelta(__state, __1, Vector3.down, -entry.Policy.Up);
        if ((redirected - __1).sqrMagnitude > .000001f && Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        __1 = redirected;
    }

    private static void JumpPrefix(PixelKartPhysics __instance, ref Vector3 __0, out Vector3 __state)
    {
        __state = __0;
    }

    private static void JumpPostfix(PixelKartPhysics __instance, ref Vector3 __0, Vector3 __state)
    {
        if (!TryGetAttached(__instance, out var entry)) return;
        Vector3 nativeDelta = __0 - __state;
        if (nativeDelta.sqrMagnitude > .000001f) entry.AcceptedHop = true;
        Vector3 redirected = AntigravityPolicy.RedirectAddedDelta(__state, __0, Vector3.up, entry.Policy.Up);
        if ((redirected - __0).sqrMagnitude > .000001f && Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        __0 = redirected;
    }

    private static bool SlopePrefix(PixelKartPhysics __instance)
    {
        return !IsSteepAttached(__instance);
    }

    private static bool VerticalLimitPrefix(PixelKartPhysics __instance)
    {
        return !IsSteepAttached(__instance);
    }

    private static bool IsSteepAttached(PixelKartPhysics physics) =>
        TryGetAttached(physics, out var entry) && Vector3.Angle(Vector3.up, entry.Policy.Up) >= Settings.SteepAngle;

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
        States.TryGetValue(controller.GetInstanceID(), out entry!) && entry.SceneHandle == controller.gameObject.scene.handle;

    private static bool IsLocalHuman(PixelEasyCharMoveKartController controller) =>
        controller != null && controller.parentPlayer != null &&
        controller.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL;

    internal static void DrawStatus()
    {
        if (ShowStatus?.Value == true && Plugin.Instance?.AntigravityEnabled.Value == true &&
            Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 168, 850, 28), Status);
    }

    private static void SetStatus(string status)
    {
        if (!string.Equals(Status, status, StringComparison.Ordinal)) Status = status;
    }

    private static string StatusFor(AntigravityPolicy.DetachReason reason) => reason switch
    {
        AntigravityPolicy.DetachReason.LowSpeed => "Antigravity: steep surface needs more speed",
        AntigravityPolicy.DetachReason.HopExpired => "Antigravity: hop grace expired",
        AntigravityPolicy.DetachReason.TooFar => "Antigravity: road contact too far away",
        AntigravityPolicy.DetachReason.CurvatureLimit => "Antigravity: road normal changed too quickly",
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
        if (entry.Controller == null || entry.Motor == null || entry.Controller.kartPhysics == null) return;
        var release = AntigravityPolicy.ReleaseAtCurrentHeading(entry.Controller.transform.forward, entry.Motor.Velocity);
        ApplyAxes(entry.Controller.kartPhysics, release.Forward, release.Up, release.Velocity);
        entry.Motor.BaseVelocity = release.Velocity;
        if (safeCharacterBoundary) entry.Motor.Transform.rotation = release.Rotation;
        else PendingReleaseRotation[entry.Controller.GetInstanceID()] = entry;
    }

    private static void Fail(int id, Plugin plugin, Exception exception)
    {
        ReleaseAll();
        plugin.AntigravityEnabled.Value = false;
        plugin.Log.LogError($"Antigravity stopped after runtime failure: {exception.Message}");
    }
}

using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static bool EligibleSolidSurface(Collider? collider, PixelEasyCharMoveKartController controller)
    {
        if (collider == null || collider.isTrigger || controller?.Motor == null || collider.attachedRigidbody != null)
            return false;
        int bit = 1 << collider.gameObject.layer;
        int respawn = controller.wallRespawn_Flat_ColliderLayer.value | controller.wallRespawn_Always_ColliderLayer.value;
        return (controller.Motor.CollidableLayers.value & bit) != 0 && (respawn & bit) == 0 &&
            collider.GetComponentInParent<PixelEasyCharMoveKartController>() == null &&
            collider.GetComponentInParent<Ant_Player>() == null;
    }

    private static void UpdateSolidSurface(PixelEasyCharMoveKartController controller,
        PixelKartPhysics physics, KinematicCharacterMotor motor, KartState? entry)
    {
        int id = controller.GetInstanceID();
        bool reportStatus = IsLocalHuman(controller);
        if (physics.bCatapultUsedLowerLateralFriction || physics.IsTeleportingFromAtoB())
        { Release(id, true); return; }

        var grounding = motor.GroundingStatus;
        bool forcedHop = motor.MustUnground();
        bool attached = entry?.Attached == true;
        bool hop = forcedHop || (attached && entry!.AcceptedHop);
        if (hop && attached)
        {
            entry!.AcceptedHop = true;
            entry.HopElapsed += Mathf.Min(Time.fixedDeltaTime, .05f);
            if (!forcedHop && HasJumpContact(entry) && entry.HopElapsed >= .08f &&
                Vector3.Dot(motor.Velocity, entry.Up) <= .5f)
            {
                TraceJump(entry, physics, "landed", motor.Velocity);
                entry.AcceptedHop = false;
                entry.HopElapsed = 0f;
                hop = false;
            }
        }
        if (attached && !hop && grounding.FoundAnyGround &&
            Vector3.Angle(Vector3.up, grounding.GroundNormal) < DirectSurfaceActivationDegrees - 10f)
        { Release(id, true); if (reportStatus) SetStatus("Antigravity: native road handling"); return; }
        if (!attached && (!grounding.FoundAnyGround ||
            Vector3.Angle(Vector3.up, grounding.GroundNormal) < DirectSurfaceActivationDegrees))
        { if (reportStatus) SetStatus("Antigravity: waiting for steep surface"); return; }
        if (hop && attached && Vector3.Angle(Vector3.up, entry!.Up) < DirectSurfaceActivationDegrees)
        { ReleaseForNativeJump(id); return; }
        Collider? surface = !hop && grounding.FoundAnyGround &&
            EligibleSolidSurface(grounding.GroundCollider, controller) ? grounding.GroundCollider : null;
        Vector3 normal = surface != null ? grounding.GroundNormal : Vector3.zero;
        if (surface == null && attached)
        {
            float dt = Mathf.Min(Time.fixedDeltaTime, .05f);
            if (!entry!.AcceptedHop) entry.ContactGraceElapsed += dt;
            float elapsed = entry.AcceptedHop ? entry.HopElapsed : entry.ContactGraceElapsed;
            float limit = entry.AcceptedHop ? HopGraceSeconds : ContactGraceSeconds;
            float distance = entry.AcceptedHop ? HopGraceDistance : ContactGraceDistance;
            if (!TryContinuityProbe(controller, motor, entry.Up, out surface, out normal, distance))
            {
                if (elapsed > limit)
                {
                    TraceJump(entry, physics, "contact-timeout", motor.Velocity);
                    Release(id, true); if (reportStatus) SetStatus("Antigravity: lost surface"); return;
                }
                ApplyFrame(physics, motor, entry.Forward, entry.Up, motor.Velocity, true);
                if (reportStatus) SetStatus($"Antigravity: recovering contact {elapsed:0.00}s");
                return;
            }
        }
        if (!EligibleSolidSurface(surface, controller) || normal.sqrMagnitude < .5f)
        { Release(id, true); if (reportStatus) SetStatus("Antigravity: waiting for eligible surface"); return; }

        normal.Normalize();
        float dtTurn = Mathf.Min(Time.fixedDeltaTime, .05f);
        if (attached)
        {
            // Filter facet/contact noise while keeping the existing maximum turn
            // rate for genuine quad transitions. No extra ground queries are needed.
            float blend = 1f - Mathf.Exp(-dtTurn / .025f);
            Vector3 filtered = Vector3.Slerp(entry!.Up, normal, blend).normalized;
            normal = Vector3.RotateTowards(entry.Up, filtered, 720f * dtTurn * Mathf.Deg2Rad, 0f).normalized;
        }
        if (entry == null)
        {
            entry = new KartState { Controller = controller, Motor = motor,
                KartSceneHandle = controller.gameObject.scene.handle,
                ActiveSceneHandle = SceneManager.GetActiveScene().handle };
            States[id] = entry;
            if (!reportStatus)
            {
                if (AnnouncedAiRacers.Count >= 64) AnnouncedAiRacers.Clear();
                if (AnnouncedAiRacers.Add(id))
                    Plugin.Instance?.Log.LogInfo($"Antigravity AI attached: kart={id} tilt={Vector3.Angle(Vector3.up, normal):0}");
            }
        }
        if (!entry.LayersOverridden)
        {
            entry.OriginalStableLayers = motor.StableGroundLayers.value;
            entry.LayersOverridden = true;
        }
        int excluded = controller.wallRespawn_Flat_ColliderLayer.value | controller.wallRespawn_Always_ColliderLayer.value;
        motor.StableGroundLayers = (motor.CollidableLayers.value | entry.OriginalStableLayers) & ~excluded;

        Vector3 oldUp = attached ? entry.Up : motor.CharacterUp;
        Vector3 oldForward = attached ? entry.Forward : physics.qSteeringOrientation * Vector3.forward;
        Vector3 forward = Vector3.ProjectOnPlane(Quaternion.FromToRotation(oldUp, normal) * oldForward, normal).normalized;
        if (forward.sqrMagnitude < .5f) { Release(id, true); return; }
        // A hop must turn with the underlying loop without deleting its normal
        // impulse. Leaving its velocity in the old world frame launches it off
        // the curve as soon as the road normal changes underneath it.
        if (attached && entry.AcceptedHop)
            motor.BaseVelocity = AntigravityPolicy.TransportVelocity(oldUp, normal, motor.BaseVelocity);
        entry.Attached = entry.Applied = true;
        entry.Up = entry.Normal = normal;
        entry.Forward = forward;
        float speed = Vector3.Dot(Vector3.ProjectOnPlane(motor.Velocity, normal), forward);
        if (speed > 1f) entry.SurfaceForwardSpeed = Mathf.Max(entry.SurfaceForwardSpeed, Mathf.Min(speed, 160f));
        UpdateCameraEngagement(entry, normal, motor.MaxStableSlopeAngle);
        // A successful probe during a hop identifies the road; it is not a landing.
        // Keep the normal jump impulse until the motor actually lands above.
        if (!entry.AcceptedHop) entry.HopElapsed = 0f;
        entry.ContactGraceElapsed = 0f;
        ApplyFrame(physics, motor, forward, normal, motor.Velocity, true);
        if (Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        if (reportStatus) SetStatus($"Antigravity: attached {Vector3.Angle(Vector3.up, normal):0}° / speed {motor.Velocity.magnitude:0.0}");
    }
}

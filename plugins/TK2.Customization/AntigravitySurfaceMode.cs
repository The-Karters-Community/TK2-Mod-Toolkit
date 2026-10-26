using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    // Do not hook OnMovementHit: its non-blittable ref HitStabilityReport is unsafe
    // through the installed IL2CPP bridge. Use this local kart's support/probes only.

    private static bool EligibleSolidSurface(Collider? collider, PixelEasyCharMoveKartController controller)
    {
        if (collider == null || collider.isTrigger || controller == null || controller.Motor == null) return false;
        int layer = collider.gameObject.layer;
        if (layer < 0 || layer > 31) return false;
        int bit = 1 << layer;
        int respawn = controller.wallRespawn_Flat_ColliderLayer.value | controller.wallRespawn_Always_ColliderLayer.value;
        if ((respawn & bit) != 0 || (controller.Motor.CollidableLayers.value & bit) == 0) return false;
        if (collider.attachedRigidbody != null) return false;
        return collider.GetComponentInParent<PixelEasyCharMoveKartController>() == null &&
            collider.GetComponentInParent<Ant_Player>() == null;
    }

    private static void UpdateSolidSurface(PixelEasyCharMoveKartController controller,
        PixelKartPhysics physics, KinematicCharacterMotor motor, KartState? entry)
    {
        int id = controller.GetInstanceID();
        if (physics.bCatapultUsedLowerLateralFriction || physics.IsTeleportingFromAtoB())
        { Release(id, true); return; }

        var grounding = motor.GroundingStatus;
        Collider? surface = null;
        Vector3 normal = Vector3.zero;
        bool hop = motor.MustUnground();
        bool directContact = false;

        if (!hop && grounding.FoundAnyGround && EligibleSolidSurface(grounding.GroundCollider, controller))
        { surface = grounding.GroundCollider; normal = grounding.GroundNormal; directContact = true; }

        // A single near-body ray can pick up a wall being driven into. KCC still
        // performs the actual movement and penetration/collision resolution.
        if (!hop && entry?.Policy.Attached == true &&
            Vector3.Dot(motor.Velocity, entry.Policy.Forward) > .05f)
        {
            Vector3 origin = motor.TransientPosition +
                motor.TransientRotation * motor.CharacterTransformToCapsuleCenter;
            float reach = Mathf.Clamp(motor.Capsule.radius, .05f, 3f) + .1f;
            if (Physics.Raycast(origin, entry.Policy.Forward, out var wall, reach,
                motor.CollidableLayers, QueryTriggerInteraction.Ignore) &&
                EligibleSolidSurface(wall.collider, controller) &&
                Vector3.Dot(entry.Policy.Forward, -wall.normal) > .25f)
            {
                float turn = Vector3.Angle(entry.Policy.Up, wall.normal);
                // This is only a continuity probe, not a general wall selector.
                // Large front-facing normals are commonly boost-pad lips, steps,
                // and obstacle faces. Native grounding handles a real larger road
                // transition after contact; retain this probe only for faceted
                // curve seams (the observed 36° -> 55° tunnel step is 19°).
                if (turn > 5f && turn <= 35f)
                { surface = wall.collider; normal = wall.normal; directContact = true; }
            }
        }

        if (surface == null && entry?.Policy.Attached == true)
        {
            float dt = Mathf.Min(Time.fixedDeltaTime, .05f);
            if (entry.AcceptedHop)
            {
                entry.Policy.HopElapsed += dt;
                if (entry.Policy.HopElapsed > Settings.MaxHopSeconds) { Release(id, true); return; }
            }
            else
            {
                entry.Policy.ContactGraceElapsed += dt;
                if (entry.Policy.ContactGraceElapsed > Settings.MaxContactGraceSeconds) { Release(id, true); return; }
            }
            float distance = entry.AcceptedHop ? Settings.MaxHopDistance : Settings.MaxContactGraceDistance;
            if (TryContinuityProbe(controller, motor, entry.Policy.Up, distance,
                out var nearby, out var nearbyNormal, out _))
            { surface = nearby; normal = nearbyNormal; }
        }

        if (surface == null || !(normal.sqrMagnitude > .5f && normal.sqrMagnitude < 1.5f))
        { Release(id, true); SetStatus("Antigravity: waiting for solid contact"); return; }
        normal = normal.normalized;
        // Contact normals across joined quads can change in one physics tick.
        // Keep the physical frame continuous; native collision remains the source
        // of contact and the raw normal is reached within a short fraction of a second.
        if (entry?.Policy.Attached == true)
            normal = Vector3.RotateTowards(entry.Policy.Up, normal,
                240f * Mathf.Min(Time.fixedDeltaTime, .05f) * Mathf.Deg2Rad, 0f).normalized;

        if (entry == null)
        {
            if (States.Count >= 4) ReleaseAll();
            entry = new KartState { Controller = controller, Motor = motor,
                KartSceneHandle = controller.gameObject.scene.handle,
                ActiveSceneHandle = SceneManager.GetActiveScene().handle };
            States[id] = entry;
        }
        if (!entry.LayersOverridden)
        {
            entry.OriginalStableLayers = motor.StableGroundLayers.value;
            entry.LayersOverridden = true;
        }
        int respawn = controller.wallRespawn_Flat_ColliderLayer.value | controller.wallRespawn_Always_ColliderLayer.value;
        motor.StableGroundLayers = (motor.CollidableLayers.value | entry.OriginalStableLayers) & ~respawn;
        // Never disable the game's native wall collision while adhering to a
        // surface. Doing so can make a loop look driveable briefly but lets the
        // KCC pass through its mesh. Stable-ground eligibility is sufficient for
        // the local frame; all movement and penetration resolution remain native.

        Vector3 oldUp = entry.Policy.Attached ? entry.Policy.Up : motor.CharacterUp;
        Vector3 oldForward = entry.Policy.Attached ? entry.Policy.Forward : physics.qSteeringOrientation * Vector3.forward;
        Vector3 forward = Vector3.ProjectOnPlane(Quaternion.FromToRotation(oldUp, normal) * oldForward, normal).normalized;
        if (forward.sqrMagnitude < .5f) { Release(id, true); return; }
        entry.SurfaceMode = entry.Applied = true;
        entry.Policy.Armed = false;
        entry.Policy.Attached = true;
        entry.Policy.Up = entry.Policy.Normal = normal;
        entry.Policy.Forward = forward;
        entry.SupportCollider = surface;
        float worldTilt = Vector3.Angle(Vector3.up, normal);
        UpdateCameraEngagement(entry, normal, motor.MaxStableSlopeAngle);
        if (directContact)
        { entry.AcceptedHop = false; entry.Policy.HopElapsed = entry.Policy.ContactGraceElapsed = 0f; }
        if (TryNativeYaw(physics.qSteeringOrientation, out float yaw))
        { entry.LastNativeYaw = yaw; entry.HasNativeYaw = true; }
        ApplyFrame(physics, motor, forward, normal, motor.Velocity, true);
        if (Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        if (Time.unscaledTime >= NextAttachedStatusTime)
        {
            NextAttachedStatusTime = Time.unscaledTime + .2f;
            string camera = entry.CameraEngaged ? "camera ready" : "camera flat";
            SetStatus($"Antigravity: solid surface {worldTilt:0}° / layer {surface.gameObject.layer} / {camera}");
        }
    }
}

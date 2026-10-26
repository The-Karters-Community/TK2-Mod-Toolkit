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

        // Direct mode is also active on ordinary road so it can acquire a
        // wall later. A normal road jump must remain a normal game jump; only
        // a genuinely steep/inverted surface gets the surface-frame hop
        // transport. Without this gate, every jump was interpreted as an
        // adhesion hop and the kart appeared unable to leave the road.
        if (hop && entry?.SurfaceMode == true && entry.Policy.Attached &&
            Vector3.Angle(Vector3.up, entry.Policy.Up) < 65f)
        { Release(id, true); return; }

        // A direct-surface kart can become ungrounded for a jump or a faceted
        // loop transition even when TriggerPhysicsJump did not report a delta.
        // Preserve the surface frame and use the native hop grace instead of
        // treating that transition as an immediate loss of the road.
        if (hop && entry?.SurfaceMode == true && entry.Policy.Attached)
            entry.AcceptedHop = true;

        if (!hop && grounding.FoundAnyGround && EligibleSolidSurface(grounding.GroundCollider, controller))
        { surface = grounding.GroundCollider; normal = grounding.GroundNormal; }

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
                { surface = wall.collider; normal = wall.normal; }
            }
        }

        if (surface == null && entry?.Policy.Attached == true)
        {
            float dt = Mathf.Min(Time.fixedDeltaTime, .05f);
            // Direct loop surfaces can briefly leave KCC's GroundingStatus while
            // the capsule crosses a faceted seam. Give the next static collider
            // a short, bounded reacquisition window; the ordinary road policy
            // keeps its stricter grace values.
            float graceSeconds = entry.SurfaceMode && !entry.AcceptedHop ? .35f : Settings.MaxContactGraceSeconds;
            if (entry.AcceptedHop)
            {
                entry.Policy.HopElapsed += dt;
                if (entry.Policy.HopElapsed > Settings.MaxHopSeconds) { Release(id, true); return; }
            }
            else
            {
                entry.Policy.ContactGraceElapsed += dt;
                if (entry.Policy.ContactGraceElapsed > graceSeconds) { Release(id, true); return; }
            }
            float distance = entry.AcceptedHop ? Settings.MaxHopDistance :
                entry.SurfaceMode ? 1.5f : Settings.MaxContactGraceDistance;
            bool continuityHit = TryContinuityProbe(controller, motor, entry.Policy.Up, distance,
                out var nearby, out var nearbyNormal, out float separation);
            if (continuityHit)
            { surface = nearby; normal = nearbyNormal; }
            else if (entry.SurfaceMode)
                DiagnosticLog($"reacquire miss: grace={entry.Policy.ContactGraceElapsed:0.000}s/{graceSeconds:0.000}s distance={distance:0.00}m " +
                    $"speed={motor.Velocity.magnitude:0.0} upTilt={Vector3.Angle(Vector3.up, entry.Policy.Up):0}°");
        }

        if (surface == null || !(normal.sqrMagnitude > .5f && normal.sqrMagnitude < 1.5f))
        {
            bool hadAttachment = entry?.Policy.Attached == true;
            string reason = grounding.FoundAnyGround
                ? (grounding.IsStableOnGround ? "contact rejected" : "unstable contact")
                : "no ground contact";
            Release(id, true);
            SetStatus($"Antigravity: waiting — {reason}");
            DiagnosticLog($"release: {reason}; found={grounding.FoundAnyGround} stable={grounding.IsStableOnGround} mustUnground={hop}", hadAttachment);
            return;
        }
        normal = normal.normalized;
        Vector3 rawNormal = normal;
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
        float forwardSpeed = Vector3.Dot(Vector3.ProjectOnPlane(motor.Velocity, normal), forward);
        if (forwardSpeed > 1f)
            entry.SurfaceForwardSpeed = Mathf.Max(entry.SurfaceForwardSpeed, Mathf.Min(forwardSpeed, 160f));
        else if (worldTilt < 65f)
            entry.SurfaceForwardSpeed = Mathf.Max(0f, forwardSpeed);
        UpdateCameraEngagement(entry, normal, motor.MaxStableSlopeAngle);
        // Any successful contact (including the continuity ray used across a
        // faceted quad seam) ends the grace window. Resetting only for the
        // primary KCC contact lets ContactGraceElapsed leak across repeated
        // reacquisitions and eventually releases a perfectly supported kart.
        entry.AcceptedHop = false;
        entry.Policy.HopElapsed = entry.Policy.ContactGraceElapsed = 0f;
        if (TryNativeYaw(physics.qSteeringOrientation, out float yaw))
        { entry.LastNativeYaw = yaw; entry.HasNativeYaw = true; }
        ApplyFrame(physics, motor, forward, normal, motor.Velocity, true);
        if (Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        if (Time.unscaledTime >= NextAttachedStatusTime)
        {
            NextAttachedStatusTime = Time.unscaledTime + .2f;
            string camera = entry.CameraEngaged ? "camera ready" : "camera flat";
            if (Diagnostics?.Value == true)
            {
                float motorTilt = Vector3.Angle(Vector3.up, motor.CharacterUp);
                float frameError = Vector3.Angle(normal, motor.CharacterUp);
                SetStatus($"Antigravity: raw {Vector3.Angle(Vector3.up, rawNormal):0}° → frame {worldTilt:0}° / " +
                    $"motor {motorTilt:0}° Δ{frameError:0}° / limit {motor.MaxStableSlopeAngle:0}° / " +
                    $"contact {(grounding.FoundAnyGround ? (grounding.IsStableOnGround ? "stable" : "unstable") : "none")} / " +
                    $"speed {motor.Velocity.magnitude:0.0} carry {entry.SurfaceForwardSpeed:0.0} / layer {surface.gameObject.layer} / {camera}");
            }
            else SetStatus($"Antigravity: solid surface {worldTilt:0}° / layer {surface.gameObject.layer} / {camera}");
            DiagnosticLog($"contact: raw={Vector3.Angle(Vector3.up, rawNormal):0} frame={worldTilt:0} motor={Vector3.Angle(Vector3.up, motor.CharacterUp):0} " +
                $"error={Vector3.Angle(normal, motor.CharacterUp):0} limit={motor.MaxStableSlopeAngle:0} found={grounding.FoundAnyGround} stable={grounding.IsStableOnGround} speed={motor.Velocity.magnitude:0.0} carry={entry.SurfaceForwardSpeed:0.0} layer={surface.gameObject.layer}");
        }
    }
}

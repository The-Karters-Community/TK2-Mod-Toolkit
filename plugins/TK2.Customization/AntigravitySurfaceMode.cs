using System;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    // Explicit free-driving mode: use real collision/support normals, including walls.
    // KCC still resolves collisions; only its game-specific wall-hit response is skipped.
    private static bool SurfaceMovementHit(PixelEasyCharMoveKartController __instance,
        Collider __0, Vector3 __1, ref HitStabilityReport __3)
    {
        var plugin = Plugin.Instance;
        if (plugin?.AntigravityEnabled.Value != true || !plugin.AntigravityAnySurface.Value ||
            !AirShiftFeature.Running(__instance) || !TryGet(__instance, out var entry) ||
            !entry.SurfaceMode || !EligibleSolidSurface(__0, __instance)) return true;
        try
        {
            if (!(__1.sqrMagnitude > .5f && __1.sqrMagnitude < 1.5f)) return true;
            Vector3 normal = __1.normalized;
            if (Vector3.Angle(entry.Policy.Up, normal) > 110f) return true;
            entry.MovementSurface = __0;
            entry.MovementNormal = normal;
            entry.MovementContactTime = Time.unscaledTime;
            __3.IsStable = true;
            return false;
        }
        catch (Exception ex) { Fail(__instance.GetInstanceID(), plugin, ex); return true; }
    }

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

        // Prefer an actual wall movement contact over last tick's floor grounding.
        if (!hop && entry?.MovementSurface != null &&
            Time.unscaledTime - entry.MovementContactTime <= .08f &&
            EligibleSolidSurface(entry.MovementSurface, controller))
        { surface = entry.MovementSurface; normal = entry.MovementNormal; directContact = true; }
        else if (!hop && grounding.FoundAnyGround && EligibleSolidSurface(grounding.GroundCollider, controller))
        { surface = grounding.GroundCollider; normal = grounding.GroundNormal; directContact = true; }
        if (entry != null) entry.MovementSurface = null;

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

        if (entry == null)
        {
            if (States.Count >= 4) ReleaseAll();
            entry = new KartState { Controller = controller, Motor = motor,
                KartSceneHandle = controller.gameObject.scene.handle,
                ActiveSceneHandle = SceneManager.GetActiveScene().handle };
            States[id] = entry;
        }
        if (!entry.LayersOverridden)
        { entry.OriginalStableLayers = motor.StableGroundLayers.value; entry.LayersOverridden = true; }
        int respawn = controller.wallRespawn_Flat_ColliderLayer.value | controller.wallRespawn_Always_ColliderLayer.value;
        motor.StableGroundLayers = (motor.CollidableLayers.value | entry.OriginalStableLayers) & ~respawn;

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
        if (directContact)
        { entry.AcceptedHop = false; entry.Policy.HopElapsed = entry.Policy.ContactGraceElapsed = 0f; }
        if (TryNativeYaw(physics.qSteeringOrientation, out float yaw))
        { entry.LastNativeYaw = yaw; entry.HasNativeYaw = true; }
        ApplyFrame(physics, motor, forward, normal, motor.Velocity, true);
        if (Plugin.Instance != null) Plugin.Instance.SessionModified = true;
        if (Time.unscaledTime >= NextAttachedStatusTime)
        {
            NextAttachedStatusTime = Time.unscaledTime + .2f;
            SetStatus($"Antigravity: solid surface {Vector3.Angle(Vector3.up, normal):0}° / layer {surface.gameObject.layer} / camera off");
        }
    }
}

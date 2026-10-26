using KinematicCharacterController;
using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static bool TryContinuityProbe(PixelEasyCharMoveKartController controller,
        KinematicCharacterMotor motor, Vector3 up, out Collider? surface, out Vector3 normal,
        float maxDistance = ContactGraceDistance)
    {
        surface = null;
        normal = Vector3.zero;
        Vector3 origin = WorldCapsuleBottom(motor) + up * .1f;
        float radius = Mathf.Clamp(motor.Capsule.radius * .5f, .08f, .4f);
        if (!Physics.SphereCast(origin, radius, -up, out var hit, maxDistance + radius,
            motor.CollidableLayers, QueryTriggerInteraction.Ignore) || !EligibleSolidSurface(hit.collider, controller))
            return false;
        surface = hit.collider;
        normal = hit.normal;
        return true;
    }

    private static Vector3 WorldCapsuleBottom(KinematicCharacterMotor motor) =>
        motor.TransientPosition + motor.TransientRotation * motor.CharacterTransformToCapsuleBottom;

    private static bool HasJumpContact(KartState entry)
    {
        var report = entry.Motor.GroundingStatus;
        if (report.FoundAnyGround && EligibleSolidSurface(report.GroundCollider, entry.Controller)) return true;
        Vector3 origin = WorldCapsuleBottom(entry.Motor) + entry.Up * .3f;
        return Physics.Raycast(origin, -entry.Up, out var hit, .45f,
            entry.Motor.CollidableLayers, QueryTriggerInteraction.Ignore) &&
            EligibleSolidSurface(hit.collider, entry.Controller);
    }

    internal static Vector3 ClipCamera(Ant_Player player, Vector3 pivot, Vector3 desired)
    {
        foreach (var entry in States.Values)
        {
            if (!entry.Attached || entry.Controller == null || entry.Controller.parentPlayer != player) continue;
            Vector3 offset = desired - pivot;
            float distance = offset.magnitude;
            if (distance > .01f && Physics.SphereCast(pivot, .35f, offset / distance,
                out var hit, distance, entry.Motor.CollidableLayers, QueryTriggerInteraction.Ignore) &&
                EligibleSolidSurface(hit.collider, entry.Controller))
                return pivot + offset / distance * Mathf.Max(0f, hit.distance - .1f);
            break;
        }
        return desired;
    }
}

using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityPolicy
{
    internal readonly struct ReleaseFrame
    {
        internal readonly Quaternion Rotation;
        internal readonly Vector3 Up, Forward, Right, Velocity;
        internal ReleaseFrame(Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right, Vector3 velocity)
        { Rotation = rotation; Up = up; Forward = forward; Right = right; Velocity = velocity; }
    }

    private static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        float length = value.magnitude;
        if (!Finite(value) || !Finite(length) || length < .0001f)
        { normalized = Vector3.zero; return false; }
        normalized = value / length;
        return true;
    }

    private static bool TryProjectForward(Vector3 value, Vector3 normal, out Vector3 forward)
    {
        Vector3 projected = Vector3.ProjectOnPlane(value, normal);
        float length = projected.magnitude;
        if (!Finite(projected) || !Finite(length) || length < .0001f)
        { forward = Vector3.zero; return false; }
        forward = projected / length;
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

    // Rotate only the native velocity/acceleration delta; any pre-existing outward or tangent
    // component stays untouched and the native delta magnitude is preserved.
    internal static Vector3 RedirectAddedDelta(Vector3 before, Vector3 after,
        Vector3 fromDirection, Vector3 toDirection)
    {
        Vector3 delta = after - before;
        if (!TryNormalize(fromDirection, out var from) || !TryNormalize(toDirection, out var to) ||
            !Finite(delta)) return after;
        return before + Quaternion.FromToRotation(from, to) * delta;
    }

    internal static Vector3 TransportVelocity(Vector3 previousNormal, Vector3 nextNormal, Vector3 velocity)
    {
        if (!TryNormalize(previousNormal, out var from) || !TryNormalize(nextNormal, out var to) || !Finite(velocity))
            return velocity;
        return Quaternion.FromToRotation(from, to) * velocity;
    }

    internal static Vector3 AlignVelocityTangent(Vector3 velocity, Vector3 surfaceNormal, Vector3 fallbackForward)
    {
        if (!Finite(velocity) || !TryNormalize(surfaceNormal, out var up)) return velocity;
        float speed = velocity.magnitude;
        if (!Finite(speed) || speed < .0001f) return velocity;
        Vector3 tangent = Vector3.ProjectOnPlane(velocity, up);
        if (!TryNormalize(tangent, out tangent) && !TryProjectForward(fallbackForward, up, out tangent))
            return velocity;
        return tangent * speed;
    }

    internal static Vector3 AccelerationForward(Vector3 velocity, Vector3 surfaceForward,
        Vector3 surfaceUp, bool blendVelocity, bool movingBackward)
    {
        if (!TryProjectForward(surfaceForward, surfaceUp, out var forward)) return Vector3.zero;
        if (!blendVelocity) return forward;
        Vector3 tangentVelocity = Vector3.ProjectOnPlane(velocity, surfaceUp);
        float speed = tangentVelocity.magnitude;
        if (!Finite(speed) || speed < .0001f) return forward;
        tangentVelocity /= speed;
        Vector3 signedForward = movingBackward ? -forward : forward;
        Vector3 blended = tangentVelocity + signedForward;
        if (!TryNormalize(blended, out var normalized)) return forward;
        return normalized;
    }

    internal static ReleaseFrame ReleaseAtCurrentHeading(Vector3 currentForward, Vector3 currentVelocity)
    {
        Vector3 up = Vector3.up;
        Vector3 forward;
        if (!TryProjectForward(currentForward, up, out forward))
        {
            if (!TryProjectForward(currentVelocity, up, out forward)) forward = Vector3.forward;
        }
        Vector3 right = Vector3.Cross(up, forward).normalized;
        return new ReleaseFrame(Quaternion.LookRotation(forward, up), up, forward, right, currentVelocity);
    }
}

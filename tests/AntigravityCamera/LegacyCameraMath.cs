// Historical camera experiment; excluded from the game plugin.
using UnityEngine;

namespace TK2.Customization;

// Camera framing and collision decisions are kept separate from the Unity physics adapter so
// their transition and obstruction behavior can be checked with a deterministic fake world.
// The approach is informed by gravity alignment and near-plane camera casting concepts from
// https://catlikecoding.com/unity/tutorials/movement/custom-gravity/ and
// https://catlikecoding.com/unity/tutorials/movement/orbit-camera/; this is an independent implementation.
internal static partial class AntigravityCameraMath
{
    internal interface ICollisionProbe
    {
        bool IsClear(Vector3 center, Vector3 halfExtents, Quaternion orientation);
        bool Cast(Vector3 origin, Vector3 direction, float distance, Vector3 halfExtents,
            Quaternion orientation, out float hitDistance);
    }

    internal struct State
    {
        internal bool Initialized;
        internal bool WasObstructed;
        internal Quaternion GravityFrame;
        internal Vector3 Position;
    }

    internal readonly struct Result
    {
        internal readonly Vector3 Position;
        internal readonly Quaternion Rotation;
        internal readonly bool Obstructed;
        internal readonly bool Safe;

        internal Result(Vector3 position, Quaternion rotation, bool obstructed, bool safe)
        { Position = position; Rotation = rotation; Obstructed = obstructed; Safe = safe; }
    }

    internal const float CollisionSkin = .035f;
    internal const float OriginStep = .04f;
    internal const float MaxOriginRecoveryDistance = .36f;
    internal const int MaxOriginSteps = 9;

    internal static Result Step(ref State state, Vector3 pivot, Vector3 nativePosition,
        Quaternion nativeRotation, Vector3 targetUp, Vector3 halfExtents, Vector3 volumeOffset,
        float deltaTime, float smoothingSeconds, ICollisionProbe collision)
    {
        if (!TryNormalize(targetUp, out targetUp)) targetUp = Vector3.up;
        float blend = BlendFactor(deltaTime, smoothingSeconds);

        Quaternion priorFrame = state.Initialized ? state.GravityFrame : Quaternion.identity;
        Vector3 priorUp = priorFrame * Vector3.up;
        Quaternion transportedFrame = Quaternion.FromToRotation(priorUp, targetUp) * priorFrame;
        Quaternion frame = Quaternion.Slerp(priorFrame, transportedFrame, blend);
        Quaternion rotation = frame * nativeRotation;

        // Rebuild from this frame's native output each time. The smoothed frame is applied to
        // the native camera offset around the kart focus; no previous output offset is reused.
        Vector3 unobstructedPosition = pivot + frame * (nativePosition - pivot);
        Vector3 safePosition = ResolveObstruction(pivot, unobstructedPosition, rotation,
            halfExtents, rotation * volumeOffset, collision, out bool obstructed, out bool hasSafeStart);
        if (!hasSafeStart) return new Result(nativePosition, nativeRotation, false, false);

        Vector3 position = safePosition;
        if (state.Initialized && state.WasObstructed && !obstructed)
        {
            // Let the camera return to its native framing gradually after it clears a wall.
            position = Vector3.Lerp(state.Position, unobstructedPosition, blend);
            // The smoothed return is checked again below; smoothing is never allowed to move
            // the near plane through a wall.
        }

        bool finalObstruction = false;
        position = ResolveObstruction(pivot, position, rotation, halfExtents,
            rotation * volumeOffset, collision, out finalObstruction, out bool finalHasSafeStart);
        if (!finalHasSafeStart) return new Result(nativePosition, nativeRotation, false, false);
        state.Initialized = true;
        state.WasObstructed = obstructed || finalObstruction;
        state.GravityFrame = frame;
        state.Position = position;
        return new Result(position, rotation, state.WasObstructed, true);
    }

    internal static Vector3 ResolveObstruction(Vector3 pivot, Vector3 desiredPosition,
        Quaternion orientation, Vector3 halfExtents, ICollisionProbe collision, out bool obstructed)
        => ResolveObstruction(pivot, desiredPosition, orientation, halfExtents, Vector3.zero,
            collision, out obstructed, out _);

    internal static Vector3 ResolveObstruction(Vector3 pivot, Vector3 desiredPosition,
        Quaternion orientation, Vector3 halfExtents, Vector3 volumeOffset,
        ICollisionProbe collision, out bool obstructed) => ResolveObstruction(pivot, desiredPosition,
            orientation, halfExtents, volumeOffset, collision, out obstructed, out _);

    private static Vector3 ResolveObstruction(Vector3 pivot, Vector3 desiredPosition,
        Quaternion orientation, Vector3 halfExtents, Vector3 volumeOffset,
        ICollisionProbe collision, out bool obstructed, out bool hasSafeStart)
    {
        obstructed = false;
        hasSafeStart = true;
        if (collision == null || !Finite(pivot) || !Finite(desiredPosition) || !Finite(volumeOffset) ||
            !Finite(halfExtents) || !Finite(orientation)) { hasSafeStart = false; return desiredPosition; }

        Vector3 offset = desiredPosition - pivot;
        float pathLength = offset.magnitude;
        if (!Finite(pathLength) || pathLength <= .0001f)
        {
            hasSafeStart = collision.IsClear(desiredPosition + volumeOffset, halfExtents, orientation);
            obstructed = !hasSafeStart;
            return obstructed ? pivot : desiredPosition;
        }

        Vector3 direction = offset / pathLength;
        float clearStartDistance = 0f;
        Vector3 clearStart = pivot;
        bool foundClearStart = false;
        float recoveryDistance = Mathf.Min(pathLength, MaxOriginRecoveryDistance);
        int steps = Mathf.Clamp(Mathf.CeilToInt(recoveryDistance / OriginStep), 1, MaxOriginSteps);
        for (int i = 0; i <= steps; i++)
        {
            float distance = recoveryDistance * i / steps;
            Vector3 candidate = pivot + direction * distance;
            if (!collision.IsClear(candidate + volumeOffset, halfExtents, orientation)) continue;
            clearStartDistance = distance;
            clearStart = candidate;
            foundClearStart = true;
            break;
        }

        if (!foundClearStart)
        {
            // No safe box origin exists within the bounded recovery distance. Fall back at
            // the pivot instead of advancing through an overlapping obstacle to reach the
            // potentially clear endpoint.
            obstructed = true;
            hasSafeStart = false;
            return pivot;
        }

        if (clearStartDistance > 0f) obstructed = true;
        float remaining = pathLength - clearStartDistance;
        if (remaining <= .0001f) return clearStart;
        if (!collision.Cast(clearStart + volumeOffset, direction, remaining, halfExtents, orientation, out float hitDistance))
            return desiredPosition;

        if (!Finite(hitDistance)) return clearStart;
        float safeDistance = Mathf.Clamp(hitDistance - CollisionSkin, 0f, remaining);
        obstructed = true;
        return clearStart + direction * safeDistance;
    }

    private static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        float magnitude = value.magnitude;
        if (!Finite(magnitude) || magnitude < .0001f) { normalized = Vector3.up; return false; }
        normalized = value / magnitude;
        return true;
    }

    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

}

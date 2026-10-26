using System;
using UnityEngine;

namespace TK2.Customization;

// Pure frame and transition policy. Contact classification is supplied by the feature adapter;
// this type never queries Unity physics or changes game objects.
internal static class AntigravityPolicy
{
    internal enum ContactKind { None, NativeStableRoad, ContinuousRoadProbe }

    internal enum DetachReason
    {
        None, Disabled, NotLocalOrOffline, InvalidContact, LaunchOrTeleport,
        LowSpeed, HopExpired, TooFar, InvalidNormal, CurvatureLimit
    }

    internal struct State
    {
        internal bool Attached;
        internal Vector3 Normal, Up, Forward;
        internal float HopElapsed;
    }

    internal readonly struct Settings
    {
        internal readonly float AttachSpeed, DetachSpeed, MaxTurnPerTick, MaxTurnPerSecond;
        internal readonly float MaxHopSeconds, MaxHopDistance, SteepAngle;
        internal static readonly Settings Default = new(12f, 8f, 20f, 720f, .45f, 2.5f, 70f);

        internal Settings(float attachSpeed, float detachSpeed, float maxTurnPerTick,
            float maxTurnPerSecond, float maxHopSeconds, float maxHopDistance, float steepAngle)
        {
            AttachSpeed = FiniteNonNegative(attachSpeed);
            DetachSpeed = Mathf.Min(FiniteNonNegative(detachSpeed), AttachSpeed);
            MaxTurnPerTick = Mathf.Clamp(FiniteNonNegative(maxTurnPerTick), .01f, 90f);
            MaxTurnPerSecond = Mathf.Clamp(FiniteNonNegative(maxTurnPerSecond), .01f, 1440f);
            MaxHopSeconds = Mathf.Clamp(FiniteNonNegative(maxHopSeconds), .01f, 2f);
            MaxHopDistance = Mathf.Clamp(FiniteNonNegative(maxHopDistance), .05f, 10f);
            SteepAngle = Mathf.Clamp(FiniteNonNegative(steepAngle), 1f, 89f);
        }

        private static float FiniteNonNegative(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    internal readonly struct Sample
    {
        internal readonly float DeltaTime, Speed, SteeringDeltaDegrees;
        internal readonly Vector3 Position, ContactNormal, ReferenceForward;
        internal readonly float ProbeSeparation;
        internal readonly ContactKind Contact;
        internal readonly bool Enabled, LocalHuman, OfflineAllowed, LayerInStableGroundMask;
        internal readonly bool IsTrigger, IsPlayerCollider, HasDynamicRigidbody, LaunchOrTeleport, DriftHopCandidate;

        internal Sample(float deltaTime, float speed, Vector3 position, Vector3 contactNormal,
            Vector3 referenceForward, float probeSeparation, float steeringDeltaDegrees, ContactKind contact, bool enabled, bool localHuman,
            bool offlineAllowed, bool layerInStableGroundMask, bool isTrigger,
            bool isPlayerCollider, bool hasDynamicRigidbody, bool launchOrTeleport, bool driftHopCandidate)
        {
            DeltaTime = deltaTime; Speed = speed; Position = position; ContactNormal = contactNormal;
            ReferenceForward = referenceForward; Contact = contact; Enabled = enabled;
            ProbeSeparation = probeSeparation;
            SteeringDeltaDegrees = steeringDeltaDegrees;
            LocalHuman = localHuman; OfflineAllowed = offlineAllowed;
            LayerInStableGroundMask = layerInStableGroundMask; IsTrigger = isTrigger;
            IsPlayerCollider = isPlayerCollider; HasDynamicRigidbody = hasDynamicRigidbody;
            LaunchOrTeleport = launchOrTeleport; DriftHopCandidate = driftHopCandidate;
        }
    }

    internal readonly struct Decision
    {
        internal readonly bool Attached;
        internal readonly DetachReason Reason;
        internal readonly Vector3 Normal, Up, Forward;
        internal readonly float HopElapsed;

        internal Decision(bool attached, DetachReason reason, Vector3 normal, Vector3 up,
            Vector3 forward, float hopElapsed)
        {
            Attached = attached; Reason = reason; Normal = normal; Up = up;
            Forward = forward; HopElapsed = hopElapsed;
        }
    }

    internal readonly struct ReleaseFrame
    {
        internal readonly Quaternion Rotation;
        internal readonly Vector3 Up, Forward, Right, Velocity;
        internal ReleaseFrame(Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right, Vector3 velocity)
        { Rotation = rotation; Up = up; Forward = forward; Right = right; Velocity = velocity; }
    }

    internal static Decision Step(ref State state, in Sample sample, in Settings settings)
    {
        if (!sample.Enabled) return Detach(ref state, DetachReason.Disabled);
        if (!sample.LocalHuman || !sample.OfflineAllowed)
            return Detach(ref state, DetachReason.NotLocalOrOffline);
        if (sample.LaunchOrTeleport) return Detach(ref state, DetachReason.LaunchOrTeleport);
        if (!Finite(sample.DeltaTime) || sample.DeltaTime <= 0f ||
            !Finite(sample.Speed) || !Finite(sample.Position))
            return Detach(ref state, DetachReason.InvalidNormal);

        bool eligible = sample.LayerInStableGroundMask && !sample.IsTrigger &&
            !sample.IsPlayerCollider && !sample.HasDynamicRigidbody;
        bool nativeRoad = eligible && sample.Contact == ContactKind.NativeStableRoad;
        bool continuityRoad = eligible && sample.Contact == ContactKind.ContinuousRoadProbe;

        if (!state.Attached && !nativeRoad) return Detach(ref state, DetachReason.InvalidContact);

        if (!nativeRoad)
        {
            if (!state.Attached || !continuityRoad || !sample.DriftHopCandidate)
                return Detach(ref state, DetachReason.InvalidContact);
            state.HopElapsed += sample.DeltaTime;
            float separation = sample.ProbeSeparation;
            if (state.HopElapsed > settings.MaxHopSeconds)
                return Detach(ref state, DetachReason.HopExpired);
            if (!Finite(separation) || separation > settings.MaxHopDistance)
                return Detach(ref state, DetachReason.TooFar);
            if (!Finite(sample.SteeringDeltaDegrees))
                return Detach(ref state, DetachReason.InvalidNormal);
            if (!TryNormalize(sample.ContactNormal, out var probeNormal))
                return Detach(ref state, DetachReason.InvalidNormal);
            float probeTurn = Vector3.Angle(state.Normal, probeNormal);
            float allowedProbeTurn = Mathf.Min(settings.MaxTurnPerTick,
                settings.MaxTurnPerSecond * Mathf.Min(sample.DeltaTime, .05f));
            if (!Finite(probeTurn) || probeTurn > allowedProbeTurn + .001f)
                return Detach(ref state, DetachReason.CurvatureLimit);
            if (Vector3.Angle(Vector3.up, probeNormal) >= settings.SteepAngle &&
                sample.Speed < settings.DetachSpeed)
                return Detach(ref state, DetachReason.LowSpeed);
            Quaternion hopTransport = Quaternion.FromToRotation(state.Normal, probeNormal);
            Vector3 hopForward = hopTransport * state.Forward;
            if (!TryProjectForward(hopForward, probeNormal, out hopForward))
                return Detach(ref state, DetachReason.InvalidNormal);
            hopForward = Quaternion.AngleAxis(sample.SteeringDeltaDegrees, probeNormal) * hopForward;
            if (!TryProjectForward(hopForward, probeNormal, out hopForward))
                return Detach(ref state, DetachReason.InvalidNormal);
            state.Normal = state.Up = probeNormal;
            state.Forward = hopForward;
            // The continuous probe keeps only the previous frame. It cannot refresh its timer
            // or acquire a kart that was already detached.
            return Current(ref state);
        }

        if (!TryNormalize(sample.ContactNormal, out var normal))
            return Detach(ref state, DetachReason.InvalidNormal);

        float angleFromWorldUp = Vector3.Angle(Vector3.up, normal);
        bool steep = angleFromWorldUp >= settings.SteepAngle;
        if (steep && ((!state.Attached && sample.Speed < settings.AttachSpeed) ||
                      (state.Attached && sample.Speed < settings.DetachSpeed)))
            return Detach(ref state, DetachReason.LowSpeed);

        if (!state.Attached)
        {
            if (!TryProjectForward(sample.ReferenceForward, normal, out var initialForward))
                return Detach(ref state, DetachReason.InvalidNormal);
            state.Attached = true;
            state.Normal = state.Up = normal;
            state.Forward = initialForward;
            state.HopElapsed = 0f;
            return Current(ref state);
        }

        float turn = Vector3.Angle(state.Normal, normal);
        float allowedTurn = Mathf.Min(settings.MaxTurnPerTick, settings.MaxTurnPerSecond * sample.DeltaTime);
        if (!Finite(turn) || turn > allowedTurn + .001f)
            return Detach(ref state, DetachReason.CurvatureLimit);
        Quaternion transport = Quaternion.FromToRotation(state.Normal, normal);
        Vector3 forward = transport * state.Forward;
        if (!TryProjectForward(forward, normal, out forward))
            return Detach(ref state, DetachReason.InvalidNormal);
        if (!Finite(sample.SteeringDeltaDegrees))
            return Detach(ref state, DetachReason.InvalidNormal);
        forward = Quaternion.AngleAxis(sample.SteeringDeltaDegrees, normal) * forward;
        if (!TryProjectForward(forward, normal, out forward))
            return Detach(ref state, DetachReason.InvalidNormal);

        state.Normal = state.Up = normal;
        state.Forward = forward;
        state.HopElapsed = 0f;
        return Current(ref state);
    }

    private static Decision Current(ref State state) =>
        new(true, DetachReason.None, state.Normal, state.Up, state.Forward, state.HopElapsed);

    private static Decision Detach(ref State state, DetachReason reason)
    {
        state = default;
        return new Decision(false, reason, Vector3.zero, Vector3.zero, Vector3.zero, 0f);
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

    internal static void ApplySteeringDelta(ref State state, float deltaDegrees)
    {
        if (!state.Attached || !Finite(deltaDegrees)) return;
        Vector3 forward = Quaternion.AngleAxis(deltaDegrees, state.Up) * state.Forward;
        if (TryProjectForward(forward, state.Up, out forward)) state.Forward = forward;
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

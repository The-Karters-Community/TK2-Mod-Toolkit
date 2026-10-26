// Historical curved-road experiment; excluded from the game plugin.
using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityPolicy
{
    internal enum ContactKind { None, NativeStableRoad, ContinuousRoadProbe }
    internal enum ArmBlockReason
    {
        None, LookaheadNotSampled, LookaheadMiss, LookaheadIneligible, LookaheadTrackMismatch,
        LookaheadHeight, LookaheadTurn, LookaheadNotIncreasing, LookaheadBelowTarget,
        StableContacts, ForwardTravel, CurveProgress, NormalProgress, CurrentSlope, LowSpeed
    }

    internal enum DetachReason
    {
        None, Disabled, NotLocalOrOffline, InvalidContact, LaunchOrTeleport,
        LowSpeed, HopExpired, TooFar, InvalidNormal, CurvatureLimit, UnsupportedSlopeLimit
    }

    internal struct State
    {
        internal bool Armed, Attached;
        internal Vector3 Normal, Up, Forward;
        internal float HopElapsed;
        internal float ContactGraceElapsed;
        internal Vector3 ArmNormal, ArmForward, LastPosition;
        internal float LastSlopeAngle, ArmForwardTravel, ArmCurveProgress, ArmNormalTravel;
        internal int StableSamples;
        internal string LastArmResetCause;
    }

    internal readonly struct Settings
    {
        internal readonly float AttachSpeed, DetachSpeed, MaxTurnPerTick, MaxTurnPerSecond;
        internal readonly float MaxHopSeconds, MaxHopDistance, SteepAngle;
        internal readonly float ActivationLeadDegrees, MinActivationAngle, MinForwardTravel, MinCurveProgress;
        internal readonly float MaxLookaheadNormalTurn, MaxLookaheadHeight, MaxContactGraceSeconds, MaxContactGraceDistance;
        internal readonly int MinStableSamples;
        internal static readonly Settings Default = new(12f, 8f, 20f, 720f, .45f, 2.5f, 70f,
            8f, 25f, 4, .5f, 2f, 20f, .55f, .08f, .25f);

        internal Settings(float attachSpeed, float detachSpeed, float maxTurnPerTick,
            float maxTurnPerSecond, float maxHopSeconds, float maxHopDistance, float steepAngle,
            float activationLeadDegrees = 8f, float minActivationAngle = 25f, int minStableSamples = 4,
            float minForwardTravel = .5f, float minCurveProgress = 2f,
            float maxLookaheadNormalTurn = 20f, float maxLookaheadHeight = .55f,
            float maxContactGraceSeconds = .08f, float maxContactGraceDistance = .25f)
        {
            AttachSpeed = FiniteNonNegative(attachSpeed);
            DetachSpeed = Mathf.Min(FiniteNonNegative(detachSpeed), AttachSpeed);
            MaxTurnPerTick = Mathf.Clamp(FiniteNonNegative(maxTurnPerTick), .01f, 90f);
            MaxTurnPerSecond = Mathf.Clamp(FiniteNonNegative(maxTurnPerSecond), .01f, 1440f);
            MaxHopSeconds = Mathf.Clamp(FiniteNonNegative(maxHopSeconds), .01f, 2f);
            MaxHopDistance = Mathf.Clamp(FiniteNonNegative(maxHopDistance), .05f, 10f);
            SteepAngle = Mathf.Clamp(FiniteNonNegative(steepAngle), 1f, 89f);
            ActivationLeadDegrees = Mathf.Clamp(FiniteNonNegative(activationLeadDegrees), 1f, 20f);
            MinActivationAngle = Mathf.Clamp(FiniteNonNegative(minActivationAngle), 1f, 60f);
            MinStableSamples = Math.Max(2, Math.Min(12, minStableSamples));
            MinForwardTravel = Mathf.Clamp(FiniteNonNegative(minForwardTravel), .05f, 3f);
            MinCurveProgress = Mathf.Clamp(FiniteNonNegative(minCurveProgress), .1f, 15f);
            MaxLookaheadNormalTurn = Mathf.Clamp(FiniteNonNegative(maxLookaheadNormalTurn), 1f, 45f);
            MaxLookaheadHeight = Mathf.Clamp(FiniteNonNegative(maxLookaheadHeight), .05f, 2f);
            MaxContactGraceSeconds = Mathf.Clamp(FiniteNonNegative(maxContactGraceSeconds), .01f, .2f);
            MaxContactGraceDistance = Mathf.Clamp(FiniteNonNegative(maxContactGraceDistance), .05f, 1f);
        }

        private static float FiniteNonNegative(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    internal readonly struct Sample
    {
        internal readonly float DeltaTime, Speed, SteeringDeltaDegrees;
        internal readonly Vector3 Position, ContactNormal, ReferenceForward;
        internal readonly float ProbeSeparation;
        internal readonly Vector3 NativeCharacterUp, LookaheadNormal;
        internal readonly float NativeMaxStableSlopeAngle, ForwardProgress, LookaheadHeightError;
        internal readonly ContactKind Contact;
        internal readonly bool Enabled, LocalHuman, OfflineAllowed, LayerInStableGroundMask;
        internal readonly bool IsTrigger, IsPlayerCollider, HasDynamicRigidbody, LaunchOrTeleport, DriftHopCandidate;
        internal readonly bool HasEligibleLookahead, SameEligibleTrack, ActiveContactCandidate;
        internal readonly bool LookaheadAttempted, LookaheadHit, LookaheadEligible, LookaheadSameTrack;
        internal readonly float LookaheadNormalTurnDegrees;

        internal Sample(float deltaTime, float speed, Vector3 position, Vector3 contactNormal,
            Vector3 referenceForward, float probeSeparation, float steeringDeltaDegrees, ContactKind contact, bool enabled, bool localHuman,
            bool offlineAllowed, bool layerInStableGroundMask, bool isTrigger,
            bool isPlayerCollider, bool hasDynamicRigidbody, bool launchOrTeleport, bool driftHopCandidate,
            Vector3 nativeCharacterUp = default, float nativeMaxStableSlopeAngle = 0f,
            float forwardProgress = 0f, bool hasEligibleLookahead = false, Vector3 lookaheadNormal = default,
            float lookaheadHeightError = 0f, bool sameEligibleTrack = false, bool activeContactCandidate = false,
            bool lookaheadAttempted = false, bool lookaheadHit = false, bool lookaheadEligible = false,
            bool lookaheadSameTrack = false, float lookaheadNormalTurnDegrees = 0f)
        {
            DeltaTime = deltaTime; Speed = speed; Position = position; ContactNormal = contactNormal;
            ReferenceForward = referenceForward; Contact = contact; Enabled = enabled;
            ProbeSeparation = probeSeparation;
            SteeringDeltaDegrees = steeringDeltaDegrees;
            LocalHuman = localHuman; OfflineAllowed = offlineAllowed;
            LayerInStableGroundMask = layerInStableGroundMask; IsTrigger = isTrigger;
            IsPlayerCollider = isPlayerCollider; HasDynamicRigidbody = hasDynamicRigidbody;
            LaunchOrTeleport = launchOrTeleport; DriftHopCandidate = driftHopCandidate;
            NativeCharacterUp = nativeCharacterUp; NativeMaxStableSlopeAngle = nativeMaxStableSlopeAngle;
            ForwardProgress = forwardProgress; HasEligibleLookahead = hasEligibleLookahead;
            LookaheadNormal = lookaheadNormal; LookaheadHeightError = lookaheadHeightError;
            SameEligibleTrack = sameEligibleTrack; ActiveContactCandidate = activeContactCandidate;
            LookaheadAttempted = lookaheadAttempted || hasEligibleLookahead;
            LookaheadHit = lookaheadHit || hasEligibleLookahead;
            LookaheadEligible = lookaheadEligible || hasEligibleLookahead;
            LookaheadSameTrack = lookaheadSameTrack || (hasEligibleLookahead && sameEligibleTrack);
            LookaheadNormalTurnDegrees = lookaheadNormalTurnDegrees;
        }
    }

    internal readonly struct Decision
    {
        internal readonly bool Attached;
        internal readonly bool Armed;
        internal readonly DetachReason Reason;
        internal readonly Vector3 Normal, Up, Forward;
        internal readonly float HopElapsed, ContactGraceElapsed, ArmSlopeAngle;

        internal Decision(bool attached, bool armed, DetachReason reason, Vector3 normal, Vector3 up,
            Vector3 forward, float hopElapsed, float contactGraceElapsed = 0f, float armSlopeAngle = 0f)
        {
            Attached = attached; Armed = armed; Reason = reason; Normal = normal; Up = up;
            Forward = forward; HopElapsed = hopElapsed; ContactGraceElapsed = contactGraceElapsed;
            ArmSlopeAngle = armSlopeAngle;
        }
    }

    // MaxStableSlopeAngle is measured from CharacterUp, not world-up. The 47° cap
    // is empirical calibration (8° below the observed 55° stock ceiling), not a
    // native world-angle limit or a guarantee for every road.
    internal static float WorldActivationThreshold(float nativeLimit, in Settings settings) =>
        Finite(nativeLimit) ? Mathf.Min(nativeLimit - settings.ActivationLeadDegrees, 47f) : float.NaN;

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

        if (!state.Attached)
        {
            if (!nativeRoad) return Detach(ref state, DetachReason.InvalidContact);
            if (!TryNormalize(sample.ContactNormal, out var candidateNormal) ||
                !TryNormalize(sample.NativeCharacterUp, out var nativeUp) ||
                !Finite(sample.NativeMaxStableSlopeAngle) || sample.NativeMaxStableSlopeAngle <= 0f)
                return Detach(ref state, DetachReason.InvalidNormal);

            float nativeLimit = sample.NativeMaxStableSlopeAngle;
            float activationAngle = WorldActivationThreshold(nativeLimit, in settings);
            if (activationAngle < settings.MinActivationAngle)
                return Detach(ref state, DetachReason.UnsupportedSlopeLimit);

            float nativeRelativeAngle = Vector3.Angle(nativeUp, candidateNormal);
            float slopeAngle = Vector3.Angle(Vector3.up, candidateNormal);
            if (!Finite(slopeAngle) || !Finite(nativeRelativeAngle) || nativeRelativeAngle >= nativeLimit)
                return Detach(ref state, DetachReason.InvalidContact);

            if (!state.Armed)
            {
                if (!TryProjectForward(sample.ReferenceForward, candidateNormal, out var armForward))
                    return Detach(ref state, DetachReason.InvalidNormal);
                state = new State {
                    Armed = true,
                    ArmNormal = candidateNormal,
                    ArmForward = armForward,
                    LastPosition = sample.Position,
                    LastSlopeAngle = slopeAngle,
                    StableSamples = 1
                };
                return ArmedCurrent(ref state, slopeAngle);
            }

            float armTurn = Vector3.Angle(state.ArmNormal, candidateNormal);
            // Road meshes can be faceted at the physics tick rate. Bound each accepted
            // contact step by geometry, not elapsed time; same-track/lookahead checks
            // still reject discontinuous wall transitions.
            float allowedArmTurn = settings.MaxTurnPerTick;
            if (!Finite(armTurn) || armTurn > allowedArmTurn + .001f ||
                slopeAngle + .5f < state.LastSlopeAngle)
            {
                string resetCause = !Finite(armTurn) || armTurn > allowedArmTurn + .001f
                    ? "normal-step" : "slope-regression";
                if (!TryProjectForward(sample.ReferenceForward, candidateNormal, out var resetForward))
                    return Detach(ref state, DetachReason.InvalidNormal);
                state = new State {
                    Armed = true,
                    ArmNormal = candidateNormal,
                    ArmForward = resetForward,
                    LastPosition = sample.Position,
                    LastSlopeAngle = slopeAngle,
                    StableSamples = 1,
                    LastArmResetCause = resetCause
                };
                return ArmedCurrent(ref state, slopeAngle);
            }

            float progress = Mathf.Max(0f, sample.ForwardProgress);
            if (!Finite(progress)) progress = 0f;
            float slopeProgress = Mathf.Max(0f, slopeAngle - state.LastSlopeAngle);
            state.StableSamples++;
            state.ArmForwardTravel += progress;
            state.ArmCurveProgress += slopeProgress;
            state.ArmNormalTravel += armTurn;
            state.ArmNormal = candidateNormal;
            state.LastPosition = sample.Position;
            state.LastSlopeAngle = slopeAngle;
            if (TryProjectForward(sample.ReferenceForward, candidateNormal, out var updatedArmForward))
                state.ArmForward = updatedArmForward;

            bool lookahead = sample.HasEligibleLookahead && sample.LookaheadSameTrack &&
                Finite(sample.LookaheadHeightError) && sample.LookaheadHeightError <= settings.MaxLookaheadHeight &&
                TryNormalize(sample.LookaheadNormal, out var lookaheadNormal) &&
                Vector3.Angle(candidateNormal, lookaheadNormal) <= settings.MaxLookaheadNormalTurn &&
                Vector3.Angle(Vector3.up, lookaheadNormal) + .01f >= slopeAngle &&
                Vector3.Angle(Vector3.up, lookaheadNormal) >= activationAngle;
            bool ready = state.StableSamples >= settings.MinStableSamples &&
                state.ArmForwardTravel >= settings.MinForwardTravel &&
                state.ArmCurveProgress >= settings.MinCurveProgress &&
                state.ArmNormalTravel >= settings.MinCurveProgress &&
                slopeAngle >= settings.MinActivationAngle && lookahead;
            if (!ready) return ArmedCurrent(ref state, slopeAngle);

            if (sample.Speed < settings.AttachSpeed)
                return ArmedCurrent(ref state, slopeAngle);
            if (!TryProjectForward(sample.ReferenceForward, candidateNormal, out var initialForward))
                return Detach(ref state, DetachReason.InvalidNormal);
            state.Attached = true;
            state.Armed = false;
            state.Normal = state.Up = candidateNormal;
            state.Forward = initialForward;
            state.HopElapsed = 0f;
            state.ContactGraceElapsed = 0f;
            return Current(ref state);
        }

        if (!nativeRoad)
        {
            if (!continuityRoad || !sample.SameEligibleTrack ||
                (!sample.DriftHopCandidate && !sample.ActiveContactCandidate))
                return Detach(ref state, DetachReason.InvalidContact);
            bool hop = sample.DriftHopCandidate;
            float separation = sample.ProbeSeparation;
            if (hop)
            {
                state.HopElapsed += sample.DeltaTime;
                if (state.HopElapsed > settings.MaxHopSeconds)
                    return Detach(ref state, DetachReason.HopExpired);
                if (!Finite(separation) || separation > settings.MaxHopDistance)
                    return Detach(ref state, DetachReason.TooFar);
            }
            else
            {
                state.ContactGraceElapsed += sample.DeltaTime;
                if (state.ContactGraceElapsed > settings.MaxContactGraceSeconds)
                    return Detach(ref state, DetachReason.HopExpired);
                if (!Finite(separation) || separation > settings.MaxContactGraceDistance)
                    return Detach(ref state, DetachReason.TooFar);
            }

            if (!Finite(sample.SteeringDeltaDegrees) ||
                !TryNormalize(sample.ContactNormal, out var probeNormal))
                return Detach(ref state, DetachReason.InvalidNormal);
            float probeTurn = Vector3.Angle(state.Normal, probeNormal);
            float allowedProbeTurn = settings.MaxTurnPerTick;
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
            return Current(ref state);
        }

        state.HopElapsed = 0f;
        state.ContactGraceElapsed = 0f;
        if (!TryNormalize(sample.ContactNormal, out var normal))
            return Detach(ref state, DetachReason.InvalidNormal);

        float angleFromWorldUp = Vector3.Angle(Vector3.up, normal);
        if (!Finite(sample.NativeMaxStableSlopeAngle) || sample.NativeMaxStableSlopeAngle <= 0f)
            return Detach(ref state, DetachReason.InvalidNormal);
        float activationThreshold = WorldActivationThreshold(sample.NativeMaxStableSlopeAngle, in settings);
        if (!Finite(activationThreshold))
            return Detach(ref state, DetachReason.InvalidNormal);
        float passiveReleaseAngle = Mathf.Max(settings.MinActivationAngle - 5f, activationThreshold - 12f);
        if (angleFromWorldUp <= passiveReleaseAngle)
        {
            if (!TryProjectForward(sample.ReferenceForward, normal, out var passiveForward))
                return Detach(ref state, DetachReason.InvalidNormal);
            state = new State {
                Armed = true,
                ArmNormal = normal,
                ArmForward = passiveForward,
                LastPosition = sample.Position,
                LastSlopeAngle = angleFromWorldUp,
                StableSamples = 1
            };
            return ArmedCurrent(ref state, angleFromWorldUp);
        }
        bool steep = angleFromWorldUp >= settings.SteepAngle;
        if (steep && sample.Speed < settings.DetachSpeed)
            return Detach(ref state, DetachReason.LowSpeed);

        float turn = Vector3.Angle(state.Normal, normal);
        float allowedTurn = settings.MaxTurnPerTick;
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
        new(true, false, DetachReason.None, state.Normal, state.Up, state.Forward, state.HopElapsed, state.ContactGraceElapsed);

    private static Decision ArmedCurrent(ref State state, float slopeAngle) =>
        new(false, true, DetachReason.None, state.ArmNormal, Vector3.zero, state.ArmForward, 0f, 0f, slopeAngle);

    internal static ArmBlockReason GetArmBlockReason(in State state, in Sample sample, in Settings settings)
    {
        float target = WorldActivationThreshold(sample.NativeMaxStableSlopeAngle, in settings);
        float currentSlope = Vector3.Angle(Vector3.up, sample.ContactNormal);
        if (!sample.LookaheadAttempted) return ArmBlockReason.LookaheadNotSampled;
        if (!sample.LookaheadHit) return ArmBlockReason.LookaheadMiss;
        if (!sample.LookaheadEligible) return ArmBlockReason.LookaheadIneligible;
        if (!sample.LookaheadSameTrack) return ArmBlockReason.LookaheadTrackMismatch;
        if (!Finite(sample.LookaheadHeightError) || sample.LookaheadHeightError > settings.MaxLookaheadHeight)
            return ArmBlockReason.LookaheadHeight;
        if (!Finite(sample.LookaheadNormalTurnDegrees) || sample.LookaheadNormalTurnDegrees > settings.MaxLookaheadNormalTurn)
            return ArmBlockReason.LookaheadTurn;
        if (!Finite(sample.LookaheadNormal) || Vector3.Angle(Vector3.up, sample.LookaheadNormal) + .01f < currentSlope)
            return ArmBlockReason.LookaheadNotIncreasing;
        if (Vector3.Angle(Vector3.up, sample.LookaheadNormal) < target)
            return ArmBlockReason.LookaheadBelowTarget;
        if (state.StableSamples < settings.MinStableSamples) return ArmBlockReason.StableContacts;
        if (state.ArmForwardTravel < settings.MinForwardTravel) return ArmBlockReason.ForwardTravel;
        if (state.ArmCurveProgress < settings.MinCurveProgress) return ArmBlockReason.CurveProgress;
        if (state.ArmNormalTravel < settings.MinCurveProgress) return ArmBlockReason.NormalProgress;
        if (currentSlope < settings.MinActivationAngle) return ArmBlockReason.CurrentSlope;
        if (sample.Speed < settings.AttachSpeed) return ArmBlockReason.LowSpeed;
        return ArmBlockReason.None;
    }

    private static Decision Detach(ref State state, DetachReason reason)
    {
        state = default;
        return new Decision(false, false, reason, Vector3.zero, Vector3.zero, Vector3.zero, 0f);
    }

    internal static float ForwardProgress(Vector3 displacement, Vector3 previousForward, Vector3 previousNormal)
    {
        if (!Finite(displacement) || !TryNormalize(previousNormal, out var normal) ||
            !TryProjectForward(previousForward, normal, out var forward)) return 0f;
        Vector3 tangentDisplacement = Vector3.ProjectOnPlane(displacement, normal);
        if (!Finite(tangentDisplacement)) return 0f;
        return Vector3.Dot(tangentDisplacement, forward);
    }

    internal static void ApplySteeringDelta(ref State state, float deltaDegrees)
    {
        if (!state.Attached || !Finite(deltaDegrees)) return;
        Vector3 forward = Quaternion.AngleAxis(deltaDegrees, state.Up) * state.Forward;
        if (TryProjectForward(forward, state.Up, out forward)) state.Forward = forward;
    }

}

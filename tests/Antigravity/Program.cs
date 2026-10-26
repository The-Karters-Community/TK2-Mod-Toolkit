using System;
using System.Linq;
using TK2.Customization;
using UnityEngine;

int checks = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception(message);
}
bool Near(float a, float b, float epsilon = .02f) => MathF.Abs(a - b) <= epsilon;
Vector3 Normal(float degrees) => new(MathF.Sin(degrees * Mathf.Deg2Rad), MathF.Cos(degrees * Mathf.Deg2Rad), 0);
Vector3 LoopNormal(float degrees) => new(0, MathF.Cos(degrees * Mathf.Deg2Rad), MathF.Sin(degrees * Mathf.Deg2Rad));

AntigravityPolicy.Sample Sample(float degrees = 0, float speed = 20,
    AntigravityPolicy.ContactKind contact = AntigravityPolicy.ContactKind.NativeStableRoad,
    float dt = 1f / 60, Vector3? position = null, float probeSeparation = 0,
    float steeringDeltaDegrees = 0, Vector3? contactNormal = null, Vector3? referenceForward = null,
    Vector3? nativeUp = null, float nativeMaxSlope = 45, float forwardProgress = 0,
    bool hasLookahead = false, Vector3? lookaheadNormal = null, float lookaheadHeight = 0,
    bool sameTrack = false, bool activeContactCandidate = false,
    bool enabled = true, bool local = true, bool offline = true, bool groundMask = true,
    bool trigger = false, bool player = false, bool dynamicBody = false,
    bool launch = false, bool hop = false) => new(dt, speed, position ?? Vector3.zero,
        contactNormal ?? Normal(degrees), referenceForward ?? Vector3.forward, probeSeparation,
        steeringDeltaDegrees, contact, enabled, local, offline, groundMask, trigger, player,
        dynamicBody, launch, hop, nativeUp ?? Vector3.up, nativeMaxSlope, forwardProgress,
        hasLookahead, lookaheadNormal ?? Vector3.zero, lookaheadHeight, sameTrack,
        activeContactCandidate);

AntigravityPolicy.Decision Step(ref AntigravityPolicy.State state,
    AntigravityPolicy.Sample sample, AntigravityPolicy.Settings? settings = null)
{
    var selected = settings ?? AntigravityPolicy.Settings.Default;
    return AntigravityPolicy.Step(ref state, in sample, in selected);
}

AntigravityPolicy.Decision Activate(ref AntigravityPolicy.State state, float speed = 20,
    bool passivelyTiltedMotor = false, float startAngle = 30, float finalAngle = 38,
    bool validLookahead = true, bool sameTrack = true, float lookaheadHeight = 0,
    Vector3? referenceForward = null, Vector3? previewNormal = null,
    float nativeLimit = 45, float progress = .13f)
{
    AntigravityPolicy.Decision result = default;
    int sampleIndex = 0;
    Vector3 forward = referenceForward ?? Vector3.forward;
    for (float angle = startAngle; angle <= finalAngle + .001f; angle += 1f)
    {
        sampleIndex++;
        Vector3 normal = forward.x > .9f ? LoopNormal(angle) : Normal(angle);
        Vector3 motorUp = passivelyTiltedMotor ? normal : Vector3.up;
        Vector3 lookahead = previewNormal ?? (forward.x > .9f ? LoopNormal(42) : Normal(42));
        result = Step(ref state, Sample(speed: speed, dt: .05f,
            position: new Vector3(0, 0, (sampleIndex - 1) * progress), contactNormal: normal,
            referenceForward: referenceForward, nativeUp: motorUp, nativeMaxSlope: nativeLimit,
            forwardProgress: sampleIndex == 1 ? 0 : progress,
            hasLookahead: validLookahead, lookaheadNormal: lookahead,
            lookaheadHeight: lookaheadHeight, sameTrack: sameTrack));
    }
    return result;
}

// Arming is passive. It requires continuous stable-ground samples and forward travel.
{
    Check(!OnlineProtectionPolicy.IsWhitelistedModule("Antigravity"),
        "antigravity remains outside the gameplay allowlist");
    Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("Antigravity", "Enabled", true) }, Array.Empty<string>()),
        "online-protection policy blocks submissions/rooms for enabled antigravity");

    var state = new AntigravityPolicy.State();
    var result = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.NativeStableRoad,
        degrees: 0, nativeUp: Vector3.up));
    Check(!result.Attached && result.Armed && state.Armed,
        "flat eligible road arms without activating a surface frame");
    Check(result.Up.sqrMagnitude == 0 && !state.Attached,
        "armed state emits no frame that could alter native orientation or axes");

    state = new AntigravityPolicy.State();
    result = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe));
    Check(!result.Armed && result.Reason == AntigravityPolicy.DetachReason.InvalidContact,
        "continuous probe cannot arm or acquire when detached");
    result = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.None));
    Check(result.Reason == AntigravityPolicy.DetachReason.InvalidContact,
        "airborne, wall, respawn, and trigger categories cannot arm");
}

// A shallow 14-degree road remains passive even with repeated progress and a same-track preview.
{
    var state = new AntigravityPolicy.State();
    AntigravityPolicy.Decision latest = default;
    for (int angle = 10; angle <= 14; angle++)
    {
        latest = Step(ref state, Sample(angle, dt: .05f,
            position: new Vector3(0, 0, angle * .2f), forwardProgress: angle == 10 ? 0 : .2f,
            hasLookahead: true, lookaheadNormal: Normal(18), lookaheadHeight: .1f, sameTrack: true));
        Check(!latest.Attached && latest.Armed,
            $"shallow {angle}° road stays armed and passive");
    }
    Check(!state.Attached && state.StableSamples >= 4,
        "sufficient flat-road travel and stable samples do not cause shallow activation");

    state = new AntigravityPolicy.State();
    latest = Activate(ref state, validLookahead: false);
    Check(!latest.Attached && latest.Armed,
        "near-slope stable history remains passive when no eligible lookahead exists");
    state = new AntigravityPolicy.State();
    latest = Activate(ref state, sameTrack: false);
    Check(!latest.Attached && latest.Armed,
        "lookahead on a different road collider cannot activate the frame");
    state = new AntigravityPolicy.State();
    latest = Activate(ref state, lookaheadHeight: .56f);
    Check(!latest.Attached && latest.Armed,
        "lookahead outside the bounded road-height tolerance cannot activate");
    state = new AntigravityPolicy.State();
    latest = Activate(ref state, startAngle: 30, finalAngle: 36, previewNormal: Normal(45));
    Check(!latest.Attached && latest.Armed,
        "lookahead at the activation band cannot engage before the current road reaches that band");
    state = new AntigravityPolicy.State();
    latest = Activate(ref state, referenceForward: Vector3.right, previewNormal: Vector3.right);
    Check(!latest.Attached && latest.Armed,
        "a same-track lookahead with an abrupt wall normal cannot activate");
}

// Smooth uphill progression activates only near the actual native slope boundary.
{
    var state = new AntigravityPolicy.State();
    var result = Activate(ref state, passivelyTiltedMotor: true);
    Check(result.Attached && state.Attached && !state.Armed,
        "gradual near-limit curve activates after stable history, actual travel, and same-road lookahead");
    Check(Vector3.Angle(result.Up, Normal(38)) < 1,
        "active frame follows the current road normal even when native motor up passively tracks the road");
    Check(state.StableSamples >= 4 && state.ArmForwardTravel >= .5f && state.ArmCurveProgress >= 2f,
        "activation records minimum stable samples, forward travel, and curve progress");

    state = new AntigravityPolicy.State();
    result = Activate(ref state, speed: 8);
    Check(!result.Attached && result.Armed,
        "near-limit lookahead cannot activate below the attach-speed threshold");

    state = new AntigravityPolicy.State();
    result = Activate(ref state, nativeLimit: 32, startAngle: 26, finalAngle: 30);
    Check(!result.Attached && !result.Armed && result.Reason == AntigravityPolicy.DetachReason.UnsupportedSlopeLimit,
        "native slope limits too low for a safe lead band are rejected explicitly");

    var settings = AntigravityPolicy.Settings.Default;
    Check(Near(AntigravityPolicy.WorldActivationThreshold(45, in settings), 37) &&
        Near(AntigravityPolicy.WorldActivationThreshold(85, in settings), 47) &&
        Near(AntigravityPolicy.WorldActivationThreshold(90, in settings), 47),
        "world activation threshold uses native limit minus lead, capped at the empirical 47° world calibration");
    foreach (float nativeLimit in new[] { 85f, 90f })
    {
        state = new AntigravityPolicy.State();
        result = Activate(ref state, nativeLimit: nativeLimit, startAngle: 40, finalAngle: 46,
            previewNormal: Normal(53));
        Check(!result.Attached && result.Armed,
            $"native limit {nativeLimit:0}° cannot activate before 47° world slope even with forward lookahead");
        result = Step(ref state, Sample(48, dt: .05f, position: new Vector3(0, 0, 7 * .13f),
            forwardProgress: .13f, nativeMaxSlope: nativeLimit,
            hasLookahead: true, lookaheadNormal: Normal(53), sameTrack: true));
        Check(result.Attached && Vector3.Angle(result.Normal, Normal(48)) < 1,
            $"native limit {nativeLimit:0}° activates at the empirical 47° world calibration, not nativeLimit−8°");
    }

    state = new AntigravityPolicy.State();
    result = Activate(ref state, startAngle: 30, finalAngle: 34, progress: .1f);
    Check(!result.Attached && result.Armed,
        "stable sample count without the required half metre of actual forward travel stays passive");
}

// Broken continuity or a floor-to-wall jump cannot turn an armed kart into an active one.
{
    var state = new AntigravityPolicy.State();
    Step(ref state, Sample(0));
    var abrupt = Step(ref state, Sample(90, contact: AntigravityPolicy.ContactKind.None));
    Check(!abrupt.Attached && !abrupt.Armed && !state.Armed,
        "abrupt floor-to-wall loss clears a passive arm without attaching");

    state = new AntigravityPolicy.State();
    var result = Step(ref state, Sample(90, nativeUp: Vector3.up));
    Check(!result.Attached && result.Reason == AntigravityPolicy.DetachReason.InvalidContact,
        "a wall contact outside native stable support cannot arm in midair");

    state = new AntigravityPolicy.State();
    result = Step(ref state, Sample(10, launch: true));
    Check(!result.Attached && !result.Armed && result.Reason == AntigravityPolicy.DetachReason.LaunchOrTeleport,
        "launch and teleport clear passive arming");
}

// Road-facet normal changes are bounded geometrically, not by fixedDeltaTime.
{
    foreach (float dt in new[] { 1f / 120f, 1f / 60f })
    {
        var armed = new AntigravityPolicy.State();
        var first = Step(ref armed, Sample(20, dt: dt, nativeUp: Normal(20)));
        var facet = Step(ref armed, Sample(35, dt: dt, nativeUp: Normal(35),
            position: Vector3.forward * .2f, forwardProgress: .2f));
        Check(first.Armed && facet.Armed && !facet.Attached && armed.StableSamples == 2,
            $"15° same-road facet remains a valid passive arm step at {1f / dt:0} Hz");

        armed = new AntigravityPolicy.State();
        Activate(ref armed);
        Vector3 previousNormal = armed.Normal;
        var activeFacet = Step(ref armed, Sample(dt: dt, contactNormal: Normal(53),
            nativeUp: previousNormal, referenceForward: armed.Forward));
        Check(activeFacet.Attached && Vector3.Angle(activeFacet.Normal, Normal(53)) < 1,
            $"15° native-stable facet remains attached at {1f / dt:0} Hz");

        armed = new AntigravityPolicy.State();
        Activate(ref armed);
        var probeFacet = Step(ref armed, Sample(dt: dt,
            contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
            contactNormal: Normal(53), nativeUp: armed.Up, referenceForward: armed.Forward,
            probeSeparation: .1f, sameTrack: true, activeContactCandidate: true));
        Check(probeFacet.Attached && Vector3.Angle(probeFacet.Normal, Normal(53)) < 1,
            $"15° same-track contact facet remains supported at {1f / dt:0} Hz");
    }

    var active = new AntigravityPolicy.State();
    Activate(ref active);
    var abruptWall = Step(ref active, Sample(dt: 1f / 120,
        contactNormal: Vector3.right, nativeUp: active.Normal, referenceForward: active.Forward));
    Check(!abruptWall.Attached && abruptWall.Reason == AntigravityPolicy.DetachReason.CurvatureLimit,
        "a 90° wall normal is still rejected independent of the tick rate");
}

// Once active, transported forward remains continuous around a complete loop.
{
    var state = new AntigravityPolicy.State();
    var current = Activate(ref state, referenceForward: Vector3.right);
    Check(current.Attached, "loop fixture first reaches near-slope active state");
    var previousForward = current.Forward;
    Vector3 previousNormal = current.Normal;
    for (int angle = 43; angle <= 333; angle += 5)
    {
        var normal = LoopNormal(angle);
        current = Step(ref state, Sample(speed: 20, dt: 1f / 60,
            contactNormal: normal, nativeUp: previousNormal,
            referenceForward: Vector3.right));
        Check(current.Attached, $"smooth native support follows loop at {angle}°");
        Check(Near(current.Up.magnitude, 1) && MathF.Abs(Vector3.Dot(current.Up, current.Forward)) < .002f,
            "transported up stays normalized and forward stays tangent");
        Check(Vector3.Dot(previousForward, current.Forward) > .99f,
            $"forward remains continuous at {angle}° without a vertical-frame flip");
        previousForward = current.Forward;
        previousNormal = normal;
    }
    Check(Vector3.Angle(current.Up, LoopNormal(333)) < 1,
        "transported frame remains aligned through the inverted portion of the loop");
    foreach (int angle in new[] { 338, 343, 348, 353, 358, 360 })
    {
        var normal = LoopNormal(angle);
        current = Step(ref state, Sample(speed: 20, dt: 1f / 60,
            contactNormal: normal, nativeUp: previousNormal, referenceForward: Vector3.right));
        Check(!current.Attached && current.Armed,
            $"active frame returns to passive arming as the road flattens at {angle}°");
        previousNormal = normal;
    }
    for (int angle = 1; angle <= 14; angle++)
    {
        var normal = LoopNormal(angle);
        current = Step(ref state, Sample(speed: 20, dt: 1f / 60,
            contactNormal: normal, nativeUp: previousNormal, referenceForward: Vector3.right));
        previousNormal = normal;
    }
    Check(!current.Attached && current.Armed && !state.Attached,
        "a full loop back to a 14° road settles passive instead of remaining activated");
}

// Native contacts use normal hysteresis; short same-track support loss is separately bounded.
{
    var state = new AntigravityPolicy.State();
    Activate(ref state);
    var grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .04f, probeSeparation: .2f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: true, activeContactCandidate: true));
    Check(grace.Attached && Near(grace.ContactGraceElapsed, .04f) && Near(grace.HopElapsed, 0),
        "short high-speed contact loss preserves an active frame with separate contact grace");
    grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .04f, probeSeparation: .2f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: true, activeContactCandidate: true));
    Check(grace.Attached && Near(grace.ContactGraceElapsed, .08f),
        "active-contact grace reaches its bounded time edge");
    grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .005f, probeSeparation: .2f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: true, activeContactCandidate: true));
    Check(!grace.Attached && grace.Reason == AntigravityPolicy.DetachReason.HopExpired,
        "active-contact grace expires after 0.08 seconds without a jump");

    state = new AntigravityPolicy.State();
    Activate(ref state);
    grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .01f, probeSeparation: .251f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: true, activeContactCandidate: true));
    Check(!grace.Attached && grace.Reason == AntigravityPolicy.DetachReason.TooFar,
        "active-contact grace cannot retain support beyond 0.25 metres");

    state = new AntigravityPolicy.State();
    Activate(ref state);
    grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .01f, probeSeparation: .1f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: false, activeContactCandidate: true));
    Check(!grace.Attached && grace.Reason == AntigravityPolicy.DetachReason.InvalidContact,
        "active-contact grace cannot bridge to another track");

    state = new AntigravityPolicy.State();
    Activate(ref state);
    grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .01f, probeSeparation: .1f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: true, hop: true));
    Check(grace.Attached && Near(grace.HopElapsed, .01f) && Near(grace.ContactGraceElapsed, 0),
        "native jump or drift candidate uses its separate drift-hop timer");
    for (int i = 1; i < 9; i++)
        grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
            dt: .05f, probeSeparation: 1f, contactNormal: state.Normal, referenceForward: state.Forward,
            nativeUp: state.Up, sameTrack: true, hop: true));
    Check(grace.Attached && Near(grace.HopElapsed, .41f),
        "drift-hop grace retains an active frame for its independent bounded duration");
    grace = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .05f, probeSeparation: 1f, contactNormal: state.Normal, referenceForward: state.Forward,
        nativeUp: state.Up, sameTrack: true, hop: true));
    Check(!grace.Attached && grace.Reason == AntigravityPolicy.DetachReason.HopExpired,
        "drift-hop grace expires after 0.45 seconds even if the button remains held");
}

// All ownership, collider, launch, and finite-value guards apply to both armed and active state.
{
    foreach (var invalid in new[] {
        Sample(groundMask: false), Sample(trigger: true), Sample(player: true), Sample(dynamicBody: true),
        Sample(local: false), Sample(offline: false), Sample(enabled: false), Sample(launch: true),
    })
    {
        var state = new AntigravityPolicy.State();
        var result = Step(ref state, invalid);
        Check(!result.Attached && !result.Armed, "guard rejects an ineligible passive acquisition");
        state = new AntigravityPolicy.State();
        Activate(ref state);
        result = Step(ref state, invalid);
        Check(!result.Attached && !result.Armed && !state.Attached,
            "guard detaches and clears an active frame");
    }
}

// Settings and malformed inputs remain finite and release without rewinding movement.
{
    var settings = new AntigravityPolicy.Settings(float.NaN, float.PositiveInfinity,
        float.NaN, float.NegativeInfinity, float.PositiveInfinity, float.NaN, float.NegativeInfinity);
    foreach (var value in new[] { settings.AttachSpeed, settings.DetachSpeed, settings.MaxTurnPerTick,
        settings.MaxTurnPerSecond, settings.MaxHopSeconds, settings.MaxHopDistance, settings.SteepAngle,
        settings.ActivationLeadDegrees, settings.MinActivationAngle, settings.MinForwardTravel,
        settings.MinCurveProgress, settings.MaxLookaheadNormalTurn, settings.MaxLookaheadHeight,
        settings.MaxContactGraceSeconds, settings.MaxContactGraceDistance })
        Check(float.IsFinite(value), "settings constructor clamps non-finite inputs to finite values");
    Check(settings.DetachSpeed <= settings.AttachSpeed, "detach threshold cannot exceed attach threshold");

    var state = new AntigravityPolicy.State();
    var invalid = Step(ref state, Sample(degrees: float.NaN));
    Check(!invalid.Attached && !invalid.Armed, "non-finite normal cannot poison arming or frame state");
    state = new AntigravityPolicy.State();
    invalid = Step(ref state, Sample(dt: float.NaN));
    Check(!invalid.Attached && !invalid.Armed, "non-finite timestep cannot poison arming or frame state");

    var currentForward = Vector3.right;
    var currentVelocity = new Vector3(30, 0, 40);
    var release = AntigravityPolicy.ReleaseAtCurrentHeading(currentForward, currentVelocity);
    Check(Near(release.Up.y, 1) && Near(release.Forward.x, 1),
        "release returns upright while retaining the current heading");
    Check(Near(release.Velocity.magnitude, currentVelocity.magnitude) &&
        Near(release.Velocity.z, currentVelocity.z),
        "release preserves current velocity without storing or rewinding position");
    Check(!typeof(AntigravityPolicy.ReleaseFrame).GetFields().Any(field => field.Name.Contains("Position")),
        "release frame contains no prior position that could rewind the kart");
}

// Native steering and jump redirection preserve their intended frame and magnitude.
{
    var state = new AntigravityPolicy.State();
    var attached = Activate(ref state, referenceForward: Vector3.right);
    var steered = Step(ref state, Sample(speed: 20, dt: .05f,
        contactNormal: attached.Normal, nativeUp: attached.Normal,
        referenceForward: Vector3.right, steeringDeltaDegrees: 15));
    Check(steered.Attached && Near(Vector3.Angle(attached.Forward, steered.Forward), 15),
        "native yaw applies around the local transported up axis");

    var before = new Vector3(0, 0, 12);
    var nativeDelta = new Vector3(0, 10, 0);
    var redirected = AntigravityPolicy.RedirectAddedDelta(before, before + nativeDelta,
        Vector3.up, Vector3.right);
    var redirectedDelta = redirected - before;
    Check(Near(redirectedDelta.magnitude, nativeDelta.magnitude) &&
        Near(redirectedDelta.x, 10) && Near(redirected.z, before.z),
        "jump redirect rotates only the native added delta and preserves its magnitude");
}

Console.WriteLine($"Antigravity policy: {checks} assertions passed.");

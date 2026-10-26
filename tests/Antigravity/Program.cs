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
    bool enabled = true, bool local = true, bool offline = true, bool groundMask = true,
    bool trigger = false, bool player = false, bool dynamicBody = false,
    bool launch = false, bool hop = false) => new(dt, speed, position ?? Vector3.zero,
        contactNormal ?? Normal(degrees), referenceForward ?? new Vector3(0, 0, 1), probeSeparation,
        steeringDeltaDegrees, contact, enabled, local, offline,
        groundMask, trigger, player, dynamicBody, launch, hop);
AntigravityPolicy.Decision Step(ref AntigravityPolicy.State state,
    AntigravityPolicy.Sample sample, AntigravityPolicy.Settings? settings = null)
{
    var selected = settings ?? AntigravityPolicy.Settings.Default;
    return AntigravityPolicy.Step(ref state, in sample, in selected);
}
AntigravityPolicy.State AttachedAtFlat()
{
    var state = new AntigravityPolicy.State();
    var attached = Step(ref state, Sample());
    Check(attached.Attached, "stable eligible native road acquires the frame");
    Check(Near(attached.Up.y, 1), "acquired frame begins aligned with the stable road normal");
    return state;
}

// Acquisition is limited to stable native road contacts.
{
    Check(!OnlineProtectionPolicy.IsWhitelistedModule("Antigravity"),
        "antigravity remains outside the gameplay allowlist");
    Check(OnlineProtectionPolicy.RequiresBlocking(new[] { ("Antigravity", "Enabled", true) }, Array.Empty<string>()),
        "online-protection policy blocks submissions/rooms for enabled antigravity");
    var state = new AntigravityPolicy.State();
    Check(Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe)).Reason ==
        AntigravityPolicy.DetachReason.InvalidContact, "continuous probe cannot acquire while detached");
    Check(Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.None)).Reason ==
        AntigravityPolicy.DetachReason.InvalidContact, "wall/respawn/trigger categories cannot acquire");
}

// Stable normals follow a complete continuous loop at ordinary per-frame changes.
{
    var state = new AntigravityPolicy.State();
    var current = Step(ref state, Sample());
    AntigravityPolicy.Decision latest = default;
    var previousForward = current.Forward;
    for (int angle = 5; angle <= 360; angle += 5)
    {
        latest = Step(ref state, Sample(contactNormal: LoopNormal(angle)));
        Check(latest.Attached, $"continuous loop normal at {angle} degrees remains attached");
        Check(Near(latest.Up.magnitude, 1), "transported up remains normalized");
        Check(MathF.Abs(Vector3.Dot(latest.Up, latest.Forward)) < .002f,
            "transported forward remains tangent to the road");
        Check(Vector3.Dot(previousForward, latest.Forward) > .99f,
            $"forward remains continuous at {angle} degrees instead of flipping from world projection");
        previousForward = latest.Forward;
    }
    Check(Vector3.Angle(latest.Up, Vector3.up) < 1, "full loop returns the transported frame to its starting normal");
}

// Native steering yaw is applied around the transported local up axis, including at vertical and inverted poses.
{
    foreach (var angle in new[] { 90, 180, 270 })
    {
        var state = new AntigravityPolicy.State();
        var start = Step(ref state, Sample(contactNormal: LoopNormal(angle), referenceForward: Vector3.right));
        Check(start.Attached, $"frame acquires at {angle} degree wall/ceiling pose with a valid tangent heading");
        var steered = Step(ref state, Sample(contactNormal: LoopNormal(angle), referenceForward: Vector3.right,
            steeringDeltaDegrees: 15));
        Check(steered.Attached && Near(Vector3.Angle(start.Forward, steered.Forward), 15),
            $"native yaw applies at {angle} degrees without world-up projection");
        Check(MathF.Abs(Vector3.Dot(steered.Up, steered.Forward)) < .002f,
            "steering keeps forward tangent to the current surface");
    }
}

// Curvature is rate bounded, and the per-tick cap also applies at long timesteps.
{
    var state = AttachedAtFlat();
    Check(Step(ref state, Sample(90)).Reason == AntigravityPolicy.DetachReason.CurvatureLimit,
        "abrupt floor-to-wall normal transition is rejected");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(13, dt: 1f / 60)).Reason == AntigravityPolicy.DetachReason.CurvatureLimit,
        "turn over 720 degrees per second is rejected at 60 Hz");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(21, dt: 1)).Reason == AntigravityPolicy.DetachReason.CurvatureLimit,
        "long timestep cannot exceed the 20 degree per tick cap");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(10, dt: 1f / 60)).Attached,
        "10 degree per tick loop sample passes the configured 20 degree tick cap");
}

// Steep surfaces require the attach threshold and use a lower detach threshold.
{
    var state = new AntigravityPolicy.State();
    Check(Step(ref state, Sample(90, speed: 11.99f)).Reason == AntigravityPolicy.DetachReason.LowSpeed,
        "steep surface cannot attach below 12 m/s");
    Check(Step(ref state, Sample(90, speed: 12)).Attached, "steep surface can attach at 12 m/s");
    Check(Step(ref state, Sample(90, speed: 8)).Attached, "attached steep surface remains at the 8 m/s detach boundary");
    Check(Step(ref state, Sample(90, speed: 7.99f)).Reason == AntigravityPolicy.DetachReason.LowSpeed,
        "steep surface detaches below 8 m/s");
    state = new AntigravityPolicy.State();
    Check(Step(ref state, Sample(speed: 0)).Attached,
        "flat road does not inherit the steep-surface speed gate");
}

// Jump is a bounded drift-hop grace, not an unconditional detach or a way to acquire in air.
{
    var state = AttachedAtFlat();
    var hop = default(AntigravityPolicy.Decision);
    for (int i = 1; i <= 24; i++)
        hop = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
            position: new Vector3(i * (20f / 24), 0, 0), probeSeparation: 1, hop: true));
    Check(hop.Attached && Near(hop.HopElapsed, .4f),
        "drift-hop keeps the acquired frame through 20 metres of tangent travel in 0.4 seconds");
    for (int i = 25; i <= 28 && hop.Attached; i++)
        hop = Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
            position: new Vector3(i * (20f / 24), 0, 0), probeSeparation: 1, hop: true));
    Check(!hop.Attached && hop.Reason == AntigravityPolicy.DetachReason.HopExpired,
        $"hop grace expires after 0.45 seconds even while drift remains held ({hop.Attached}, {hop.Reason}, {hop.HopElapsed})");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        dt: .5f, probeSeparation: 1, hop: true)).Reason == AntigravityPolicy.DetachReason.HopExpired,
        "large valid timestep contributes its full duration and cannot prolong hop grace");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        position: new Vector3(20, 0, 0), probeSeparation: 2.51f, hop: true)).Reason == AntigravityPolicy.DetachReason.TooFar,
        "hop grace ends when normal probe support separation exceeds 2.5 metres");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        probeSeparation: 1, hop: false)).Reason == AntigravityPolicy.DetachReason.InvalidContact,
        "contact loss without an active drift-hop candidate detaches");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(90, contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        probeSeparation: 1, hop: true)).Reason == AntigravityPolicy.DetachReason.CurvatureLimit,
        "abrupt probe normal cannot preserve the cached frame during a hop");
    state = AttachedAtFlat();
    Check(Step(ref state, Sample(180, contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
        probeSeparation: 1, hop: true)).Reason == AntigravityPolicy.DetachReason.CurvatureLimit,
        "opposite probe normal cannot preserve the cached frame during a hop");
    state = AttachedAtFlat();
    for (int angle = 5; angle <= 90; angle += 5)
        hop = Step(ref state, Sample(angle, contact: AntigravityPolicy.ContactKind.ContinuousRoadProbe,
            dt: 1f / 60, probeSeparation: 1, hop: true));
    Check(hop.Attached && Near(hop.HopElapsed, .3f),
        "valid curved probe normals transport during hop without refreshing its deadline");
    Check(Vector3.Angle(hop.Up, Normal(90)) < 1,
        "hop frame follows the current continuous probe normal through the curve");
}

// Every classification/ownership guard applies both to acquisition and an attached frame.
{
    foreach (var invalid in new[] {
        Sample(groundMask: false), Sample(trigger: true), Sample(player: true), Sample(dynamicBody: true),
        Sample(local: false), Sample(offline: false), Sample(enabled: false), Sample(launch: true),
    })
    {
        var state = new AntigravityPolicy.State();
        Check(!Step(ref state, invalid).Attached, "guard rejects ineligible acquisition");
        state = AttachedAtFlat();
        var result = Step(ref state, invalid);
        Check(!result.Attached && !state.Attached, "guard detaches and clears an active frame");
    }
}

// Finite settings and sample validation prevent NaN/infinity from poisoning the frame.
{
    var settings = new AntigravityPolicy.Settings(float.NaN, float.PositiveInfinity,
        float.NaN, float.NegativeInfinity, float.PositiveInfinity, float.NaN, float.NegativeInfinity);
    foreach (var value in new[] { settings.AttachSpeed, settings.DetachSpeed, settings.MaxTurnPerTick,
        settings.MaxTurnPerSecond, settings.MaxHopSeconds, settings.MaxHopDistance, settings.SteepAngle })
        Check(float.IsFinite(value), "settings constructor clamps non-finite inputs to finite values");
    Check(settings.DetachSpeed <= settings.AttachSpeed, "detach threshold cannot exceed attach threshold");
    var defaults = AntigravityPolicy.Settings.Default;
    Check(Near(defaults.AttachSpeed, 12) && Near(defaults.DetachSpeed, 8) &&
        Near(defaults.MaxTurnPerTick, 20) && Near(defaults.MaxTurnPerSecond, 720) &&
        Near(defaults.MaxHopSeconds, .45f) && Near(defaults.MaxHopDistance, 2.5f),
        "default thresholds match the reviewed attach, curvature and hop limits");
    var state = new AntigravityPolicy.State();
    Check(Step(ref state, Sample(degrees: float.NaN)).Reason == AntigravityPolicy.DetachReason.InvalidNormal,
        "non-finite normal detaches safely");
    state = new AntigravityPolicy.State();
    var invalidTime = Sample(dt: float.NaN);
    Check(Step(ref state, invalidTime).Reason == AntigravityPolicy.DetachReason.InvalidNormal,
        "non-finite timestep detaches safely");
}

// Detach releases from the current heading, not the pre-attachment heading or position.
{
    var currentForward = new Vector3(1, 0, 0);
    var currentVelocity = new Vector3(30, 0, 40);
    var release = AntigravityPolicy.ReleaseAtCurrentHeading(currentForward, currentVelocity);
    Check(Near(release.Up.x, 0) && Near(release.Up.y, 1) && Near(release.Up.z, 0),
        "release returns to world-up orientation");
    Check(Near(release.Forward.x, 1) && Near(release.Forward.z, 0),
        "release retains the current heading after the loop");
    Check(Near(release.Right.magnitude, 1) && MathF.Abs(Vector3.Dot(release.Right, release.Forward)) < .001f,
        "release produces a normalized perpendicular right axis");
    Check(Near(release.Velocity.x, currentVelocity.x) && Near(release.Velocity.y, currentVelocity.y) &&
        Near(release.Velocity.z, currentVelocity.z) && Near(release.Velocity.magnitude, currentVelocity.magnitude),
        "release leaves current velocity and its magnitude unchanged");
    Check(!typeof(AntigravityPolicy.ReleaseFrame).GetFields().Any(field => field.Name.Contains("Position")),
        "release frame carries no old position that could rewind the kart");
}

// Surface redirection changes only the native added delta, preserving its magnitude and prior velocity.
{
    var before = new Vector3(0, 0, 12);
    var nativeDelta = new Vector3(0, 10, 0);
    var redirected = AntigravityPolicy.RedirectAddedDelta(before, before + nativeDelta,
        Vector3.up, new Vector3(1, 0, 0));
    var redirectedDelta = redirected - before;
    Check(Near(redirectedDelta.magnitude, nativeDelta.magnitude),
        "redirect preserves native jump/gravity delta magnitude");
    Check(Near(redirectedDelta.x, 10) && Near(redirectedDelta.y, 0),
        "redirect rotates native delta into the acquired frame");
    Check(Near(redirected.z, before.z), "redirect leaves pre-existing velocity unchanged");
}

Console.WriteLine($"Antigravity policy: {checks} assertions passed.");

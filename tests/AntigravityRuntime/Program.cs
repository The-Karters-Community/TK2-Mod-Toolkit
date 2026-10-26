using System;
using System.Collections;
using System.Reflection;
using KinematicCharacterController;
using TK2.Customization;
using UnityEngine;
using UnityEngine.SceneManagement;

const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
const BindingFlags HiddenInstance = BindingFlags.NonPublic | BindingFlags.Instance;
int checks = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception(message);
}
bool Near(float a, float b, float epsilon = .03f) => MathF.Abs(a - b) <= epsilon;
bool NearVector(Vector3 a, Vector3 b, float epsilon = .02f) => (a - b).magnitude <= epsilon;
Vector3 Normal(float degrees) => new(MathF.Sin(degrees * Mathf.Deg2Rad), MathF.Cos(degrees * Mathf.Deg2Rad), 0);
Vector3 RoadNormal(float degrees) => new(0, MathF.Cos(degrees * Mathf.Deg2Rad), -MathF.Sin(degrees * Mathf.Deg2Rad));
Vector3 RoadForward(float degrees) => new(0, MathF.Sin(degrees * Mathf.Deg2Rad), MathF.Cos(degrees * Mathf.Deg2Rad));

FieldInfo StaticField(string name) => typeof(AntigravityFeature).GetField(name, HiddenStatic)!;
IDictionary States() => (IDictionary)StaticField("States").GetValue(null)!;
object? Invoke(string method, params object?[] args) => typeof(AntigravityFeature)
    .GetMethod(method, HiddenStatic)!.Invoke(null, args);
void ClearState()
{
    States().Clear();
    ((IDictionary)StaticField("PendingReleaseRotation").GetValue(null)!).Clear();
}
object? KartState(Fixture f) => States().Contains(f.Kart.InstanceId) ? States()[f.Kart.InstanceId] : null;
AntigravityPolicy.State Policy(Fixture f)
{
    object entry = KartState(f) ?? throw new Exception("expected adapter state");
    return (AntigravityPolicy.State)entry.GetType().GetField("Policy", HiddenInstance)!.GetValue(entry)!;
}
bool IsAttached(Fixture f) => KartState(f) is not null && Policy(f).Attached;
bool IsArmed(Fixture f) => KartState(f) is not null && Policy(f).Armed;
float ContactGrace(Fixture f) => Policy(f).ContactGraceElapsed;

Fixture NewFixture(bool enabled = true, Vector3? worldPosition = null)
{
    ClearState();
    Plugin.OfflineLabAllowed = true;
    Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet = false;
    AirShiftFeature.CanRun = true;
    AirShiftFeature.Mode = AirShiftFeature.RunMode.Live;
    AirShiftFeature.Calls = 0;
    Time.fixedDeltaTime = 1f / 60f;
    Time.unscaledTime = 0;
    SceneManager.ActiveScene = new Scene(1);
    Physics.RaycastHandler = null;

    var f = new Fixture { Plugin = new Plugin(), Kart = new PixelEasyCharMoveKartController() };
    Plugin.Instance = f.Plugin;
    f.Plugin.AntigravityEnabled.Value = enabled;
    f.Kart.gameObject.scene = new Scene(1);
    if (worldPosition is Vector3 initialPosition) f.Kart.transform.position = initialPosition;
    f.Physics = new PixelKartPhysics { kartController = f.Kart, vLastVelocity = Vector3.zero };
    f.Kart.kartPhysics = f.Physics;
    f.Motor = new KinematicCharacterMotor {
        Transform = f.Kart.transform,
        Velocity = new Vector3(0, 0, 20),
        BaseVelocity = new Vector3(0, 0, 20),
        TransientPosition = f.Kart.transform.position,
        CharacterTransformToCapsuleBottom = new Vector3(0, -.8f, 0),
        MaxStableSlopeAngle = 45f
    };
    f.Kart.Motor = f.Motor;
    f.Road = new Collider { gameObject = new GameObject { layer = 8 } };
    f.Motor.StableGroundLayers = 1 << 8;
    ReportGround(f, Vector3.up, found: true);
    return f;
}

void ReportGround(Fixture f, Vector3 normal, bool found, Collider? collider = null)
{
    bool stable = found && Vector3.Angle(f.Motor.CharacterUp, normal) < f.Motor.MaxStableSlopeAngle;
    f.Motor.GroundingStatus = new GroundingStatus {
        FoundAnyGround = found, IsStableOnGround = stable,
        GroundCollider = collider ?? f.Road, GroundNormal = normal
    };
}

Vector3 WorldBottom(Fixture f) => f.Motor.TransientPosition +
    f.Motor.TransientRotation * f.Motor.CharacterTransformToCapsuleBottom;
void KccRotationPhase(Fixture f)
{
    object?[] rotationCall = { f.Kart, f.Motor.TransientRotation };
    Invoke("UpdateRotationPostfix", rotationCall);
    f.Motor.TransientRotation = (Quaternion)rotationCall[1]!;
}
void After(Fixture f) => Invoke("AfterCharacterUpdate", f.Kart, Time.fixedDeltaTime);
void SimulateCommit(Fixture f) => f.Motor.SimulateCommit();
void KccRoadFrame(Fixture f, Vector3 normal, bool nativePassiveAlignment = true)
{
    KccRotationPhase(f);
    if (nativePassiveAlignment)
    {
        float slope = Vector3.Angle(Vector3.up, normal) * Mathf.Deg2Rad;
        Vector3 nativeForward = MathF.Abs(normal.x) > .001f
            ? new Vector3(-MathF.Cos(slope), MathF.Sin(slope), 0)
            : new Vector3(0, MathF.Sin(slope), MathF.Cos(slope));
        Quaternion nativeRotation = Quaternion.LookRotation(nativeForward, normal);
        f.Physics.qSteeringOrientation = nativeRotation;
        f.Motor.SetRotation(nativeRotation, true);
    }
    ReportGround(f, normal, found: true);
    After(f);
    SimulateCommit(f);
}

void SetForwardLookahead(Fixture f, Vector3 previewNormal, Action<Vector3, Vector3, Vector3>? inspect = null,
    float targetRise = 0)
{
    Physics.RaycastHandler = (origin, direction, maxDistance, layerMask, query) => {
        Vector3 normal = f.Motor.GroundingStatus.GroundNormal;
        Vector3 forward = Vector3.ProjectOnPlane(f.Physics.qSteeringOrientation * Vector3.forward, normal).normalized;
        float speed = Vector3.ProjectOnPlane(f.Motor.Velocity, normal).magnitude;
        float distance = Mathf.Clamp(speed * Time.fixedDeltaTime * 2f, .25f, 1.5f);
        Vector3 expectedPoint = WorldBottom(f) + forward * distance;
        // Model the next mesh facet as a real plane raised in world space. The
        // returned hit is the ray/plane intersection, not a fabricated hit at
        // the expected point.
        Vector3 targetPoint = expectedPoint + Vector3.up * targetRise;
        if (Vector3.Dot(direction, -normal) < .99f || maxDistance < .5f ||
            (layerMask.value & (1 << 8)) == 0 || query != QueryTriggerInteraction.Ignore)
            return (false, default);
        float denominator = Vector3.Dot(previewNormal, direction);
        if (MathF.Abs(denominator) < .0001f) return (false, default);
        float hitDistance = Vector3.Dot(previewNormal, targetPoint - origin) / denominator;
        if (hitDistance < 0 || hitDistance > maxDistance) return (false, default);
        Vector3 hitPoint = origin + direction * hitDistance;
        Vector3 nextForward = new(-previewNormal.y, previewNormal.x, 0);
        float alongFacet = Vector3.Dot(hitPoint - targetPoint, nextForward);
        if (alongFacet < -.36f || alongFacet > 1.5f || MathF.Abs(hitPoint.z - targetPoint.z) > 2f)
            return (false, default);
        inspect?.Invoke(origin, targetPoint, hitPoint);
        return (true, new RaycastHit(f.Road, previewNormal, hitDistance, hitPoint));
    };
}

void RoadStep(Fixture f, float degrees, Vector3 previewNormal, Action<Vector3, Vector3, Vector3>? inspect = null,
    float forwardTravel = .13f, float targetRise = 0)
{
    Vector3 normal = RoadNormal(degrees);
    f.Motor.TransientPosition += RoadForward(degrees) * forwardTravel;
    SetForwardLookahead(f, previewNormal, inspect, targetRise);
    KccRoadFrame(f, normal, nativePassiveAlignment: !IsAttached(f));
}

void DriveToActive(Fixture f, Action<Vector3, Vector3, Vector3>? inspect = null, bool studioTickEachStep = false)
{
    f.Physics.qSteeringOrientation = Quaternion.LookRotation(-Vector3.right, Vector3.up);
    f.Motor.SetRotation(f.Physics.qSteeringOrientation, true);
    f.Motor.Velocity = -Vector3.right * 80f;
    f.Motor.BaseVelocity = f.Motor.Velocity;
    foreach (float angle in new[] { 0f, 0f, 18f, 36f })
    {
        RoadStep(f, angle, RoadNormal(55), inspect, forwardTravel: .18f, targetRise: .52f);
        if (studioTickEachStep) Invoke("Tick");
        if (angle < 36f)
            Check(IsArmed(f) && !IsAttached(f) && !f.Plugin.SessionModified,
                $"{angle:0}° facet remains passive while the ramp history is collected");
    }
    var state = KartState(f) is null ? default : Policy(f);
    var diagnostic = $"state={state.Attached}/{state.Armed}; samples={state.StableSamples}; travel={state.ArmForwardTravel:0.00}; curve={state.ArmCurveProgress:0.0}; slope={state.LastSlopeAngle:0.0}; motor={Vector3.Angle(Vector3.up, f.Motor.CharacterUp):0.0}°; modified={f.Plugin.SessionModified}";
    Check(IsAttached(f) && Vector3.Angle(Policy(f).Normal, RoadNormal(36)) < 1,
        $"adapter predicts the 55° facet and activates on the 36° facet before contact reaches 55° ({diagnostic})");
}

// Studio's render/update tick may run between the KCC callbacks while the
// kart object belongs to an additive scene different from the active scene.
{
    var f = NewFixture();
    f.Kart.gameObject.scene = new Scene(2);
    DriveToActive(f, studioTickEachStep: true);
    Check(IsAttached(f) && Policy(f).StableSamples >= 4 && Policy(f).ArmForwardTravel >= .5f,
        "AfterCharacterUpdate → StudioTick → AfterCharacterUpdate retains arm history for a kart in another scene");
    SceneManager.ActiveScene = new Scene(3);
    Invoke("Tick");
    Check(KartState(f) is null &&
        Vector3.Angle(f.Physics.vUP_CharacterMotorAligned, Vector3.up) < 1,
        "an actual active-scene transition clears attached state and restores upright native axes");
}

Vector3 RunNativeMainUpdate(Fixture f, Vector3 velocity, float dt = 1f / 60f)
{
    object?[] prefix = { f.Physics, velocity, null };
    Invoke("MainVelocityPrefix", prefix);
    Vector3 changed = (Vector3)prefix[1]!;
    f.Physics.UpdateVelocity(ref changed, dt); // Test body includes the native inline Y cap; it never calls the gravity helper.
    object?[] postfix = { f.Physics, changed, dt, prefix[2] };
    Invoke("MainVelocityPostfix", postfix);
    changed = (Vector3)postfix[1]!;
    object?[] finalizer = { f.Physics, postfix[3], null };
    Invoke("MainVelocityFinalizer", finalizer);
    return changed;
}

// Disabled path exits before kart identity/native physics access.
{
    var f = NewFixture(enabled: false);
    f.Physics.ThrowOnNativeAccess = true;
    After(f);
    Check(States().Count == 0 && f.Kart.GetInstanceIDCalls == 0 && f.Kart.NativePropertyReads == 0,
        "disabled feature performs no kart-id or native-property access");
    Check(!f.Plugin.SessionModified && AirShiftFeature.Calls == 0,
        "disabled callback changes no session state and exits before the offline guard");
}

// Passive arming on flat/shallow road does not write frame, axes, or velocity.
{
    var f = NewFixture();
    f.Physics.vAccelerationForwardDirection = Vector3.right;
    var rotation = f.Motor.TransientRotation;
    var axes = f.Physics.vForward_SteeringChar_AlignedToSurfacce_Normalized;
    var acceleration = f.Physics.vAccelerationForwardDirection;
    var velocity = f.Motor.BaseVelocity;
    for (int i = 0; i < 5; i++)
    {
        f.Motor.TransientPosition += Vector3.forward * .13f;
        KccRoadFrame(f, Vector3.up);
        Check(IsArmed(f) && !IsAttached(f), "flat stable road records passive armed state");
    }
    Check(!f.Plugin.SessionModified, "passive arming does not mark the session modified");
    Check(Quaternion.Angle(rotation, f.Motor.TransientRotation) < .01f &&
        NearVector(axes, f.Physics.vForward_SteeringChar_AlignedToSurfacce_Normalized) &&
        NearVector(acceleration, f.Physics.vAccelerationForwardDirection) &&
        NearVector(velocity, f.Motor.BaseVelocity),
        "passive arming leaves native frame, axes, acceleration cache, and velocity unchanged");

    f = NewFixture(worldPosition: new Vector3(100, 25, -300));
    for (int angle = 10; angle <= 14; angle++)
    {
        f.Motor.TransientPosition += Vector3.forward * .2f;
        KccRoadFrame(f, Normal(angle));
        Check(IsArmed(f) && !IsAttached(f), $"native passive motor frame at {angle}° remains unmodified by feature");
    }
    Check(!f.Plugin.SessionModified, "14 degree road remains passive after ample forward travel");
}

// The adapter uses world-space capsule geometry for both lookahead and active support continuity.
{
    var f = NewFixture(worldPosition: new Vector3(100, 25, -300));
    int lookaheadCasts = 0;
    bool translatedOriginVerified = true;
    bool raisedFacetIntersectionVerified = false;
    DriveToActive(f, (actual, target, hit) => {
        lookaheadCasts++;
        Vector3 expectedOrigin = WorldBottom(f) +
            Vector3.ProjectOnPlane(f.Physics.qSteeringOrientation * Vector3.forward,
                f.Motor.GroundingStatus.GroundNormal).normalized * 1.5f +
            f.Motor.GroundingStatus.GroundNormal * .35f;
        if (!NearVector(actual, expectedOrigin, .025f) || actual.magnitude < 100) translatedOriginVerified = false;
        Vector3 ray = (-f.Motor.GroundingStatus.GroundNormal).normalized;
        bool onPlane = MathF.Abs(Vector3.Dot(RoadNormal(55), hit - target)) < .01f;
        float rayDistance = Vector3.Dot(hit - actual, ray);
        bool onRay = rayDistance >= 0 && rayDistance <= .9f &&
            NearVector(actual + ray * rayDistance, hit, .01f);
        if (onPlane && onRay && target.y - WorldBottom(f).y > .45f)
            raisedFacetIntersectionVerified = true;
    });
    Check(lookaheadCasts == 1 && translatedOriginVerified,
        "one successful forward-lookahead cast at36° originates at translated world capsule bottom plus the bounded lead");
    Check(raisedFacetIntersectionVerified,
        "36° lookahead reaches the actual ray/plane intersection on the raised 55° facet within the native cast range");
    Check(Vector3.Dot(f.Motor.CharacterUp, RoadNormal(36)) > .99f &&
        Vector3.Dot(f.Motor.Transform.rotation * Vector3.up, RoadNormal(36)) > .99f,
        "adapter engages on the36° facet before the55° contact and commits the KCC frame");
    Check(f.Plugin.SessionModified, "first active frame records actual physics modification");

    KccRoadFrame(f, RoadNormal(55), nativePassiveAlignment: false);
    Check(IsAttached(f) && Vector3.Angle(Policy(f).Normal, RoadNormal(55)) < 1,
        "the 19° transition from predictive36° engagement to actual55° facet remains attached");

    Vector3 activeNormal = Policy(f).Normal;
    Vector3 bottom = WorldBottom(f);
    bool contactOriginVerified = false;
    f.Motor.GroundingStatus = new GroundingStatus {
        FoundAnyGround = true, IsStableOnGround = false, GroundCollider = f.Road,
        GroundNormal = activeNormal
    };
    Physics.RaycastHandler = (origin, direction, maxDistance, mask, query) => {
        var expected = bottom + activeNormal * .1f;
        contactOriginVerified = NearVector(origin, expected, .02f) && origin.magnitude > 100 &&
            Vector3.Dot(direction, -activeNormal) > .99f && maxDistance < .36f && query == QueryTriggerInteraction.Ignore;
        return (true, new RaycastHit(f.Road, activeNormal, .3f, bottom - activeNormal * .2f));
    };
    KccRotationPhase(f);
    After(f);
    SimulateCommit(f);
    Check(IsAttached(f) && ContactGrace(f) > 0,
        "high-speed active kart with very-close same-track support survives one contact-loss tick without jump");
    Check(contactOriginVerified, "contact continuity ray uses translated world capsule bottom and local surface up");

    foreach (float tick in new[] { 1f / 120f, 1f / 60f })
    {
        var faceted = NewFixture();
        DriveToActive(faceted);
        Time.fixedDeltaTime = tick;
        KccRoadFrame(faceted, RoadNormal(51), nativePassiveAlignment: false);
        Check(IsAttached(faceted) && Vector3.Angle(Policy(faceted).Normal, RoadNormal(51)) < 1,
            $"active adapter follows a 15° stable mesh facet at {1f / tick:0} Hz");

        var supportLoss = NewFixture();
        DriveToActive(supportLoss);
        Time.fixedDeltaTime = tick;
        supportLoss.Motor.GroundingStatus = new GroundingStatus {
            FoundAnyGround = true, IsStableOnGround = false, GroundCollider = supportLoss.Road,
            GroundNormal = Policy(supportLoss).Normal
        };
        Physics.RaycastHandler = (_, _, _, _, _) =>
            (true, new RaycastHit(supportLoss.Road, RoadNormal(51), .2f, WorldBottom(supportLoss)));
        KccRotationPhase(supportLoss);
        After(supportLoss);
        SimulateCommit(supportLoss);
        Check(IsAttached(supportLoss) && Vector3.Angle(Policy(supportLoss).Normal, RoadNormal(51)) < 1,
            $"same-road 15° continuity facet is accepted at {1f / tick:0} Hz");
    }

    var disconnected = NewFixture(worldPosition: new Vector3(100, 25, -300));
    DriveToActive(disconnected);
    var otherRoad = new Collider { gameObject = new GameObject { layer = 8 } };
    Vector3 otherNormal = Policy(disconnected).Normal;
    disconnected.Motor.GroundingStatus = new GroundingStatus {
        FoundAnyGround = true, IsStableOnGround = false, GroundCollider = otherRoad,
        GroundNormal = otherNormal
    };
    Physics.RaycastHandler = (_, _, _, _, _) =>
        (true, new RaycastHit(otherRoad, otherNormal, .2f, WorldBottom(disconnected)));
    KccRotationPhase(disconnected);
    After(disconnected);
    SimulateCommit(disconnected);
    Check(!IsAttached(disconnected),
        "active contact probe on a different eligible collider cannot preserve the old road frame");
}

// Wrong track, walls, respawn layers, triggers, dynamic bodies, online, and airborne states cannot attach.
{
    var f = NewFixture();
    for (int angle = 30; angle <= 34; angle++)
    {
        f.Motor.TransientPosition += Vector3.forward * .13f;
        SetForwardLookahead(f, Normal(90));
        KccRoadFrame(f, Normal(angle));
    }
    Check(!IsAttached(f) && IsArmed(f), "abrupt wall lookahead cannot activate an otherwise armed kart");

    foreach (var invalid in new Action<Fixture>[] {
        x => x.Road.gameObject.layer = 9,
        x => x.Kart.wallLayerToCollDetect = 1 << 8,
        x => x.Kart.wallRespawn_Flat_ColliderLayer = 1 << 8,
        x => x.Kart.wallRespawn_Always_ColliderLayer = 1 << 8,
        x => x.Road.isTrigger = true,
        x => x.Road.attachedRigidbody = new Rigidbody { isKinematic = false },
        x => x.Road.PlayerOwned = true,
    })
    {
        var rejected = NewFixture();
        invalid(rejected);
        After(rejected);
        Check(!IsAttached(rejected), "ineligible road collider does not activate surface alignment");
    }

    var ai = NewFixture();
    ai.Kart.parentPlayer!.ePlayerType = Ant_Player.EPlayerType.E_AI;
    After(ai);
    Check(States().Count == 0 && ai.Kart.GetInstanceIDCalls == 0, "AI kart cannot arm");

    var online = NewFixture();
    Plugin.OfflineLabAllowed = false;
    Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet = true;
    After(online);
    Check(States().Count == 0, "online race cannot arm or attach");

    var airborne = NewFixture();
    airborne.Motor.GroundingStatus = new GroundingStatus();
    int probes = 0;
    Physics.RaycastHandler = (_, _, _, _, _) => { probes++; return (true, new RaycastHit(airborne.Road, Vector3.right, .1f)); };
    After(airborne);
    Check(States().Count == 0 && probes == 0, "detached airborne kart cannot acquire from any wall probe");
}

// Main PixelKartPhysics.UpdateVelocity is the actual inline cap boundary before KCC movement.
{
    var passive = NewFixture();
    for (int i = 0; i < 5; i++)
    {
        passive.Motor.TransientPosition += Vector3.forward * .13f;
        KccRoadFrame(passive, Vector3.up);
    }
    Check(!IsAttached(passive), "cap test passive fixture is armed but not active");
    passive.Physics.fKartMaximum_Y_Velocity = 10;
    passive.Physics.vLastVelocity = Vector3.zero;
    passive.Physics.NativeAddedVelocity = new Vector3(0, 30, 0);
    Vector3 vanilla = RunNativeMainUpdate(passive, new Vector3(0, 0, 20));
    Check(Near(vanilla.y, 10) && passive.Physics.bLimitVerticalUpVelocity,
        "passive frame leaves the native inline world-Y cap enabled and unchanged");
    Check(!passive.Plugin.SessionModified && passive.Physics.NativeGravityHelperCalls == 0,
        "passive vanilla cap path does not modify session or call the gravity helper");

    var active = NewFixture();
    DriveToActive(active);
    active.Physics.fKartMaximum_Y_Velocity = 10;
    active.Physics.vLastVelocity = Vector3.zero;
    active.Physics.NativeAddedVelocity = new Vector3(0, 30, 0);
    Vector3 before = new(0, 0, 20);
    Vector3 afterMain = RunNativeMainUpdate(active, before);
    Check(afterMain.y > active.Physics.fKartMaximum_Y_Velocity && active.Physics.bLimitVerticalUpVelocity,
        "active uphill frame bypasses the inline world-Y cap before KCC movement and restores the flag afterward");
    Check(active.Physics.NativeUpdateVelocityCalls == 1 && active.Physics.NativeGravityHelperCalls == 0,
        "main native body executes once and does not invoke the separate gravity helper");

    object?[] characterVelocity = { active.Kart, afterMain };
    Invoke("CharacterVelocityPostfix", characterVelocity);
    var beforeMove = (Vector3)characterVelocity[1]!;
    Check(MathF.Abs(Vector3.Dot(beforeMove, Policy(active).Normal)) < .02f &&
        Near(beforeMove.magnitude, afterMain.magnitude),
        "pre-movement KCC velocity callback aligns active ground velocity tangent without adding speed");
    Vector3 beforeMovement = active.Motor.Transform.position;
    active.Motor.TransientPosition += beforeMove * Time.fixedDeltaTime;
    SimulateCommit(active);
    Check(active.Motor.Transform.position.y > beforeMovement.y,
        "native KCC movement receives the redirected velocity before its position commit");

    active.Physics.bLimitVerticalUpVelocity = false;
    RunNativeMainUpdate(active, new Vector3(0, 0, 20));
    Check(!active.Physics.bLimitVerticalUpVelocity,
        "active cap guard restores an originally false native flag exactly");
}

// The main-update finalizer restores the cap on exceptions and nested reentry.
{
    var f = NewFixture();
    DriveToActive(f);
    object?[] outer = { f.Physics, new Vector3(0, 0, 20), null };
    Invoke("MainVelocityPrefix", outer);
    Check(!f.Physics.bLimitVerticalUpVelocity, "outer active velocity prefix temporarily disables the native cap");
    object?[] inner = { f.Physics, new Vector3(0, 0, 20), null };
    Invoke("MainVelocityPrefix", inner);
    object?[] innerFinalizer = { f.Physics, inner[2], null };
    Invoke("MainVelocityFinalizer", innerFinalizer);
    Check(!f.Physics.bLimitVerticalUpVelocity, "nested finalizer restores the exact inner prior false value");
    object?[] outerFinalizer = { f.Physics, outer[2], null };
    Invoke("MainVelocityFinalizer", outerFinalizer);
    Check(f.Physics.bLimitVerticalUpVelocity, "outer finalizer restores the original true value");

    object?[] throwingPrefix = { f.Physics, new Vector3(0, 0, 20), null };
    Invoke("MainVelocityPrefix", throwingPrefix);
    f.Physics.ThrowOnNativeUpdateVelocity = true;
    Exception? thrown = null;
    try
    {
        var velocity = (Vector3)throwingPrefix[1]!;
        f.Physics.UpdateVelocity(ref velocity, Time.fixedDeltaTime);
    }
    catch (Exception ex) { thrown = ex; }
    finally { f.Physics.ThrowOnNativeUpdateVelocity = false; }
    var exception = thrown ?? throw new Exception("expected fake native exception");
    object?[] exceptionFinalizer = { f.Physics, throwingPrefix[2], exception };
    object? returned = Invoke("MainVelocityFinalizer", exceptionFinalizer);
    Check(ReferenceEquals(returned, exception) && f.Physics.bLimitVerticalUpVelocity,
        "Harmony finalizer preserves the thrown exception and restores the original cap flag");
    Check(thrown is not null, "fake native inline update actually exercised the exception path");
}

// Inlined main-update gravity is reoriented only for a latched native hop; helpers are used only as a native probe.
{
    var f = NewFixture();
    DriveToActive(f);
    f.Motor.GroundingStatus = new GroundingStatus();
    f.Physics.NativeAddedVelocity = Vector3.down * 2;
    f.Physics.NativeGravityDelta = Vector3.down * 2;
    Vector3 beforeJump = new(0, 0, 20);
    Vector3 firstMain = RunNativeMainUpdate(f, beforeJump);
    Check(Near((firstMain - beforeJump).y, -2) && f.Physics.NativeGravityHelperCalls == 0,
        "main update before the native jump uses its inline world gravity and does not probe the helper");
    beforeJump = firstMain;
    object?[] jumpPrefix = { f.Physics, beforeJump, null };
    Invoke("JumpPrefix", jumpPrefix);
    Vector3 afterJump = beforeJump + Vector3.up * 5;
    object?[] jumpPostfix = { f.Physics, afterJump, jumpPrefix[2] };
    Invoke("JumpPostfix", jumpPostfix);
    var redirectedJump = (Vector3)jumpPostfix[1]!;
    Check(MathF.Abs((redirectedJump - beforeJump).magnitude - 5) < .03f,
        "native jump impulse is redirected after the main physics update without changing impulse magnitude");

    var afterGravity = RunNativeMainUpdate(f, redirectedJump);
    var added = afterGravity - redirectedJump;
    Check(Near(added.magnitude, 2) && Vector3.Dot(added, -Policy(f).Up) > 1.9f,
        "main-update inline world-down gravity delta is reoriented along the active surface only during hop grace");
    Check(f.Physics.NativeGravityHelperCalls == 1,
        "gravity helper runs once as a scratch native-delta probe, not from the simulated native main body");
}

// Pause, replay, respawn, disable, and runtime faults release active state safely.
{
    foreach (var mode in new[] { "online", "paused", "respawning", "replay", "disabled" })
    {
        var f = NewFixture();
        DriveToActive(f);
        var currentHeading = new Vector3(1, 0, 0);
        f.Motor.SetRotation(Quaternion.LookRotation(currentHeading, Vector3.up), true);
        f.Motor.Velocity = new Vector3(30, 2, 40);
        f.Motor.BaseVelocity = f.Motor.Velocity;
        var velocity = f.Motor.BaseVelocity;
        if (mode == "online")
        {
            Plugin.OfflineLabAllowed = false;
            Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet = true;
        }
        else if (mode == "disabled") f.Plugin.AntigravityEnabled.Value = false;
        else AirShiftFeature.Mode = mode == "paused" ? AirShiftFeature.RunMode.Paused :
            mode == "respawning" ? AirShiftFeature.RunMode.Respawning : AirShiftFeature.RunMode.Replay;
        After(f);
        Check(!IsAttached(f), $"{mode} mode releases the active surface frame");
        Check(NearVector(f.Motor.BaseVelocity, velocity), $"{mode} release preserves current velocity");
        object?[] rotationCall = { f.Kart, Quaternion.LookRotation(Vector3.up, Vector3.forward) };
        Invoke("UpdateRotationPostfix", rotationCall);
        var releasedRotation = (Quaternion)rotationCall[1]!;
        Check(Vector3.Angle(releasedRotation * Vector3.up, Vector3.up) < 1,
            $"{mode} release is upright at the next KCC rotation boundary");
        Check(Vector3.Dot(releasedRotation * Vector3.forward, currentHeading) > .99f,
            $"{mode} release keeps the current direction");
    }

    var fault = NewFixture();
    DriveToActive(fault);
    fault.Motor.ThrowOnGroundingRead = true;
    After(fault);
    Check(!IsAttached(fault) && !fault.Plugin.AntigravityEnabled.Value && fault.Plugin.Log.Errors.Count == 1,
        "adapter exception disables the feature and clears active state");
}

Console.WriteLine($"Antigravity runtime adapter: {checks} assertions passed.");

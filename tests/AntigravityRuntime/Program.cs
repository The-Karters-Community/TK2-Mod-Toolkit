using System;
using System.Collections;
using System.Reflection;
using KinematicCharacterController;
using TK2.Customization;
using UnityEngine;
using UnityEngine.SceneManagement;

const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
int checks = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception(message);
}

FieldInfo StaticField(string name) => typeof(AntigravityFeature).GetField(name, HiddenStatic)!;
IDictionary Dictionary(string name) => (IDictionary)StaticField(name).GetValue(null)!;
int StateCount() => Dictionary("States").Count;
void ClearState()
{
    Dictionary("States").Clear();
    Dictionary("PendingReleaseRotation").Clear();
}
void Invoke(string method, params object?[] args) => typeof(AntigravityFeature)
    .GetMethod(method, HiddenStatic)!.Invoke(null, args);
Vector3 LoopNormal(float degrees) => new(0, MathF.Cos(degrees * MathF.PI / 180f), MathF.Sin(degrees * MathF.PI / 180f));

Fixture NewFixture(bool enabled = true)
{
    ClearState();
    Plugin.OfflineLabAllowed = true;
    Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet = false;
    AirShiftFeature.CanRun = true;
    AirShiftFeature.Mode = AirShiftFeature.RunMode.Live;
    AirShiftFeature.Calls = 0;
    Time.fixedDeltaTime = 1f / 60f;
    SceneManager.ActiveScene = new Scene(1);
    Physics.RaycastHandler = null;

    var f = new Fixture { Plugin = new Plugin(), Kart = new PixelEasyCharMoveKartController() };
    Plugin.Instance = f.Plugin;
    f.Plugin.AntigravityEnabled.Value = enabled;
    f.Kart.gameObject.scene = new Scene(1);
    f.Physics = new PixelKartPhysics { kartController = f.Kart };
    f.Kart.kartPhysics = f.Physics;
    f.Motor = new KinematicCharacterMotor { Transform = f.Kart.transform, Velocity = new Vector3(0, 0, 20), BaseVelocity = new Vector3(0, 0, 20) };
    f.Kart.Motor = f.Motor;
    f.Road = new Collider { gameObject = new GameObject { layer = 8 } };
    f.Motor.StableGroundLayers = 1 << 8;
    f.Motor.GroundingStatus = new GroundingStatus { FoundAnyGround = true, IsStableOnGround = true, GroundCollider = f.Road, GroundNormal = Vector3.up };
    return f;
}

void Update(Fixture f) => Invoke("AfterCharacterUpdate", f.Kart, Time.fixedDeltaTime);
void Acquire(Fixture f) { Update(f); Check(StateCount() == 1, "eligible local offline native stable road acquires adapter state"); }
Vector3 Vec(Quaternion q, Vector3 v) => q * v;

// The default-off callback exits before instance lookup, kart properties, or game physics access.
{
    var f = NewFixture(enabled: false);
    f.Physics.ThrowOnNativeAccess = true;
    Update(f);
    Check(StateCount() == 0, "disabled module creates no adapter state");
    Check(f.Kart.GetInstanceIDCalls == 0 && f.Kart.NativePropertyReads == 0,
        "disabled callback does not look up the kart id or read its native physics properties");
    Check(AirShiftFeature.Calls == 0, "disabled callback exits before mode guard or scene work");
    Check(!f.Plugin.SessionModified && f.Plugin.AntigravityEnabled.Value == false,
        "disabled callback does not mark the session or alter configuration");
}

// Local stable-road acquisition and ownership/contact exclusions use the real feature callback.
{
    var f = NewFixture();
    f.Physics.vAccelerationForwardDirection = Vector3.right;
    Acquire(f);
    Check(f.Plugin.SessionModified, "first adapter frame records that local physics changed");

    foreach (var invalid in new Action<Fixture>[] {
        x => x.Road.gameObject.layer = 9, // wall/respawn layer outside the native stable-ground mask
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
        Update(rejected);
        Check(StateCount() == 0, "wall/respawn, trigger, dynamic, or player collider cannot acquire the feature");
    }

    var nonlocal = NewFixture();
    nonlocal.Kart.parentPlayer!.ePlayerType = Ant_Player.EPlayerType.E_AI;
    Update(nonlocal);
    Check(StateCount() == 0 && nonlocal.Kart.GetInstanceIDCalls == 0,
        "AI kart cannot acquire local surface adhesion");

    var online = NewFixture();
    Plugin.OfflineLabAllowed = false;
    Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet = true;
    Update(online);
    Check(StateCount() == 0, "online race cannot acquire local adhesion");
}

// Stale native acceleration caches cannot reverse the transported forward at a >90 degree normal.
{
    var f = NewFixture();
    var normal = LoopNormal(100);
    var expectedForward = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
    f.Motor.GroundingStatus.GroundNormal = normal;
    f.Motor.Velocity = expectedForward * 20;
    f.Motor.BaseVelocity = f.Motor.Velocity;
    f.Physics.vAccelerationForwardDirection = -expectedForward;
    Acquire(f);
    Check(Vector3.Dot(f.Physics.vAccelerationForwardDirection.normalized, expectedForward) > .99f,
        "adapter rebuilds acceleration from current local forward instead of a reversed world-projected cache");
}

// Stable-contact velocity already projected onto the new road normal is not transported twice.
{
    var f = NewFixture();
    Acquire(f);
    var nextNormal = LoopNormal(10).normalized;
    var alreadyTangent = Vector3.ProjectOnPlane(Vector3.forward, nextNormal).normalized * 25;
    f.Motor.GroundingStatus.GroundNormal = nextNormal;
    f.Motor.Velocity = alreadyTangent;
    f.Motor.BaseVelocity = alreadyTangent;
    Update(f);
    Check(MathF.Abs(Vector3.Dot(f.Motor.BaseVelocity, nextNormal)) < .02f,
        "stable normal change keeps native base velocity tangent to the new surface");
    Check(MathF.Abs(f.Motor.BaseVelocity.magnitude - alreadyTangent.magnitude) < .02f,
        "stable normal correction preserves existing velocity magnitude");
}

// Airborne state alone never raycasts or acquires an arbitrary wall.
{
    var f = NewFixture();
    f.Motor.GroundingStatus = new GroundingStatus();
    int raycasts = 0;
    Physics.RaycastHandler = (_, _, _, _, _) => { raycasts++; return (true, new RaycastHit(f.Road, Vector3.right, 1)); };
    Update(f);
    Check(StateCount() == 0 && raycasts == 0,
        "detached airborne kart cannot use the continuity probe to pick up a wall or road");
}

// A native jump starts finite grace even when drift state is false, and redirects only its added delta.
{
    var f = NewFixture();
    f.Motor.GroundingStatus.GroundNormal = Vector3.right;
    f.Motor.Velocity = new Vector3(0, 0, 20);
    f.Motor.BaseVelocity = f.Motor.Velocity;
    f.Physics.qSteeringOrientation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    Acquire(f);
    f.Physics.bIsDrifting = false;

    Vector3 velocity = new(0, 0, 20);
    object?[] prefix = { f.Physics, velocity, null };
    Invoke("JumpPrefix", prefix);
    var nativeBefore = (Vector3)prefix[2]!;
    velocity += Vector3.up * 5f;
    object?[] postfix = { f.Physics, velocity, nativeBefore };
    Invoke("JumpPostfix", postfix);
    var redirected = (Vector3)postfix[1]!;
    Check(MathF.Abs((redirected - nativeBefore).magnitude - 5f) < .02f,
        "native jump redirect preserves the game-produced impulse magnitude");
    Check(((Vector3)redirected).x > 4.9f,
        "native jump added impulse is redirected along the attached wall normal");

    // KCC can retain the last stable report for the force-unground jump phase.
    f.Motor.MustUngroundValue = true;
    Physics.RaycastHandler = (_, _, _, _, _) => (true, new RaycastHit(f.Road, Vector3.right, 1.1f));
    Update(f);
    Check(StateCount() == 1 && f.Physics.bIsDrifting == false,
        "post-jump continuity remains attached through the latched native hop without drift held");
    var stateEntries = Dictionary("States").Values.GetEnumerator(); stateEntries.MoveNext();
    var kartState = stateEntries.Current!;
    var policy = (AntigravityPolicy.State)kartState.GetType().GetField("Policy", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(kartState)!;
    Check((bool)kartState.GetType().GetField("AcceptedHop", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(kartState)!,
        "stale stable status cannot clear the native jump latch while KCC must unground");
    Check(policy.HopElapsed > 0 && policy.Attached,
        "native jump latch starts the bounded hop clock on a valid nearby road probe");

    f.Motor.MustUngroundValue = false;
    Update(f); // Fresh native stable contact confirms landing and consumes the latch.
    var landedEntries = Dictionary("States").Values.GetEnumerator(); landedEntries.MoveNext();
    var landed = landedEntries.Current!;
    Check(!(bool)landed.GetType().GetField("AcceptedHop", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(landed)!,
        "a fresh native stable contact consumes the hop latch");
}

// Online, pause, and recovery guards release to current heading without rewinding position or velocity.
{
    foreach (var mode in new[] { "online", "paused", "respawning", "replay" })
    {
        var f = NewFixture();
        Acquire(f);
        var currentHeading = new Vector3(1, 0, 0);
        f.Kart.transform.rotation = Quaternion.LookRotation(currentHeading, Vector3.up);
        f.Kart.transform.position = new Vector3(7, 8, 9);
        var position = f.Kart.transform.position;
        f.Motor.Velocity = new Vector3(30, 2, 40);
        f.Motor.BaseVelocity = f.Motor.Velocity;
        var velocity = f.Motor.BaseVelocity;
        if (mode == "online")
        {
            Plugin.OfflineLabAllowed = false;
            Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet = true;
        }
        else
        {
            AirShiftFeature.Mode = mode == "paused" ? AirShiftFeature.RunMode.Paused :
                mode == "respawning" ? AirShiftFeature.RunMode.Respawning : AirShiftFeature.RunMode.Replay;
        }
        Update(f);
        Check(StateCount() == 0, $"{mode} guard releases adapter state");
        Check(f.Motor.BaseVelocity.Equals(velocity), $"{mode} release preserves the current velocity vector");
        Check(f.Kart.transform.position.Equals(position), $"{mode} release does not move or rewind kart position");

        object?[] rotationCall = { f.Kart, Quaternion.LookRotation(Vector3.up, Vector3.forward) };
        Invoke("UpdateRotationPostfix", rotationCall);
        var releasedRotation = (Quaternion)rotationCall[1]!;
        Check(Vector3.Angle(Vec(releasedRotation, Vector3.up), Vector3.up) < 1,
            $"{mode} release returns the current heading to upright after the safe rotation boundary");
        Check(Vector3.Dot(Vec(releasedRotation, Vector3.forward), currentHeading) > .99f,
            $"{mode} release keeps the current heading rather than a pre-attachment heading");
    }
}

// Runtime exceptions disable the feature and release all tracked karts.
{
    var f = NewFixture();
    Acquire(f);
    f.Motor.ThrowOnGroundingRead = true;
    Update(f);
    Check(StateCount() == 0 && !f.Plugin.AntigravityEnabled.Value,
        "fault disables the feature and releases tracked state");
    Check(f.Plugin.Log.Errors.Count == 1, "fault is recorded for diagnosis");
}

// Config disable and native race reset both release tracked frame state.
{
    var f = NewFixture();
    Acquire(f);
    f.Plugin.AntigravityEnabled.Value = false;
    Update(f);
    Check(StateCount() == 0, "turning the feature off releases the attached frame");

    f = NewFixture();
    Acquire(f);
    Invoke("RaceResetPostfix");
    Check(StateCount() == 0, "native race reset releases the attached frame");
}

Console.WriteLine($"Antigravity runtime adapter: {checks} assertions passed.");

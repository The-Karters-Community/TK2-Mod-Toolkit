# Antigravity: current implementation

Offline only. Applies to local human and AI racers; excludes remote players,
replays, respawns, teleports and tank form. Default off.

## What has been tested

- The user confirmed normal driving, full loops, steering, jump-drift and
  character pose in their tested track. Human checkpoint: `f6b69ee`.
- The user confirmed the camera transition is now perfect. Camera/collision
  checkpoint: `c7634d3`. Collision behavior has not been independently confirmed.
- AI remain experimental: only two CPUs completed the latest tested loop.
  The new lateral-velocity correction below is built but not yet tested in game.

## Engine changes needed

1. Use the contacted road normal as the kart's up direction. Transport its
   previous forward direction into that frame instead of rebuilding from world up.
2. Feed that frame to the character motor before its stable-slope check.
   Preserve collision sweeps and exclude triggers/respawn volumes as road.
3. Apply gravity along negative surface up. Remove the world-Y velocity cap
   only on steep surfaces. Keep a bounded road-contact recovery probe.
4. Run the existing steering/centripetal calculation in a temporary flat frame,
   then transform its velocity/acceleration outputs back. Apply each steering
   delta once around surface up; do not invert controls at the loop top.
5. Preserve the native human hop impulse and rotate it with the road during
   the hop. Contact recovery must not cancel a real jump or pretend it landed.
6. Let animation consumers see grounded while touching the road, except during
   hops. Restore temporary visual flags immediately afterward.

This is an independent adaptation, not recovered original game C#.

## AI correction under test

Native AI steering assumes world-XZ direction and world-yaw rotation. Attached
AI instead use a nearby route tangent and lateral centre error in the road frame.
They get one bounded heading update, not both native and replacement turns.
Unreliable distant route matches are rejected. Native AI resumes on release.

The previous adapter turned the heading but retained sideways drift velocity.
The common tangent projection also preserves that velocity even after drifting
stops. Latest logs show AI leaving the route with `drift=False`; this supports
residual side-slip as a remaining cause, not proof of every death's cause.

The new correction changes only AI lateral velocity while in road contact:

```text
requestedSideSpeed = clamp(2 * centreError, -6, 6)
sideSpeed = clamp(moveTowards(sideSpeed, requestedSideSpeed, 35 * deltaTime), -8, 8)
```

With no reliable route, requested side speed is zero. The correction uses the
road's right vector, preserves route-forward/normal components and does not
snap position or add adhesion. It does not apply during hops, forced ungrounding
or lost contact. Human velocity is unchanged. Logs report actual/target side speed.

The next user test still showed frequent falls. Samples captured lateral speeds
of 55-84 units/second against targets of 0-6, which the previous acceleration
limit could not arrest before reaching an edge. The 8-unit lateral ceiling above
now handles those spikes while in contact. AI handling is deliberately assisted;
this is not a change to the human driver's drift physics.

Native `AddCounterSteerForceAtTheStartOfTheDrift` (`0x1805fad10`, local evidence
`ai-drift-kick`) adds lateral acceleration when steering and drift signs oppose,
guarded by `bAddLateralForceAtStartOfTheDriftToTweakVel`. The flag is now disabled
only within attached-AI velocity calculations, then restored in postfix/finalizer.
This avoids relying on a helper hook that a native caller could inline. The
samples support excessive side-slip; they do not prove this force caused every
captured fall. This revision still needs an in-game test.

AI hops are suppressed while attached. Drifts remain allowed near the route
when aligned; conflicting/recovery drifts are canceled. Boosts still use native
earned drift bars and thresholds. Missing physical loop routes still need track
changes; no generic adapter can guarantee every Workshop track is navigable.

## Camera and racer contact

The road-frame chase camera fades its complete safe pose in over 0.45 seconds
and out over 0.8 seconds, including height and obstruction clearance.

While attached, racer-to-racer KCC contacts are rejected. The independent native
proximity-bounce effect is also suppressed for attached racers and immediate
neighbors; this can suppress an ordinary pair's bounce near an attached racer.
Track collisions/item forces remain native. No persistent collision flags change.

## Source map

- `AntigravityFeature.cs`: state and exact hook signatures.
- `AntigravityLifecycle.cs`, `AntigravitySurfaceMode.cs`, `AntigravityCleanup.cs`:
  motor integration, road contact and restoration.
- `AntigravitySteering.cs`, `AntigravityVelocity.cs`, `AntigravityPolicy.cs`:
  steering frame, velocity and retained pure vector math.
- `AntigravityAi.cs`: CPU guidance and lateral correction.
- `AntigravityCamera.cs`: tested chase camera; `AntigravityCameraMath.cs`: smoothing.
- `AntigravityCollisions.cs`: scoped racer contact/bounce suppression.
- `AntigravityProbes.cs`, `AntigravityVisualGrounding.cs`: contact and animation.
- `AntigravitySplineTeleport.cs`: separate optional all-racer spline test switch.

Unused curved-road arming and old camera experiments were removed from the
production sources. Existing historical harnesses retain those algorithms under
`tests/`; they do not establish current in-game behavior. Earlier investigation
notes are preserved in Git history, especially `c7634d3`.

## Native evidence and verification

Current local native slices (ignored, not distributed) are under
`local/antigravity-evidence/`, with binary hashes in their provenance manifests:
AI guidance `0x1804e2de0`, input consumption `0x1804f70b0`, drift boosts
`0x1804e4890`, spline point/tangent `0x180d88a80`/`0x180d88d80`, proximity
bounce `0x1805f9a20`, and drift cancellation `0x1805fcc90`.
Dummy declarations establish signatures only; native pseudocode is not original C#.

Current plugin build passes with zero warnings/errors. Retained production math
method bodies were checked unchanged during cleanup. No new test suite or game
launch was performed; AI behavior still requires the user's offline test.

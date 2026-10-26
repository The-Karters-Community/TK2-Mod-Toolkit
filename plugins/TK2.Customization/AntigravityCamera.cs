using System.Collections.Generic;
using UnityEngine;

namespace TK2.Customization;

// A local road-frame chase pose, blended with the game's normal racing camera.
internal static class AntigravityCamera
{
    private struct State
    {
        internal Quaternion Frame;
        internal Vector3 Up, Forward, SmoothedForward;
        internal float Distance, Weight, ClearDistance;
        internal bool HasClearance;
    }
    private static readonly Dictionary<int, State> States = new();

    internal static void Apply(PixelGameKartCamera camera, Ant_Player player,
        ref Vector3 position, ref Quaternion rotation, float fov)
    {
        if (camera == null || player == null) return;
        int id = camera.GetInstanceID();
        if (Plugin.Instance?.AntigravityCameraEnabled.Value != true || !Plugin.OfflineLabAllowed ||
            camera.bIsSpectatorCamera || camera.fRacingCameraStrength < .95f ||
            player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL)
        { States.Remove(id); return; }

        bool active = AntigravityFeature.TryGetActiveFrame(player, out var up, out var forward);
        bool existing = States.TryGetValue(id, out var state);
        if (!active && !existing) return;
        if (player.visualInstanceSyncedParams == null || !Finite(position) || !Finite(rotation))
        { States.Remove(id); return; }
        Vector3 kart = player.visualInstanceSyncedParams.vCurPos;
        if (!Finite(kart)) { States.Remove(id); return; }
        if (!existing)
        {
            if (States.Count >= 16) States.Clear();
            state.Frame = Quaternion.identity;
            state.Distance = Mathf.Max(3f, (position - kart).magnitude);
        }

        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        float blend = AntigravityCameraMath.BlendFactor(dt, .08f);
        if (active)
        {
            state.Up = up.normalized;
            Quaternion target = Quaternion.FromToRotation(state.Frame * Vector3.up, state.Up) * state.Frame;
            state.Frame = Quaternion.Slerp(state.Frame, target, blend);
            state.Forward = forward;
        }
        state.Weight = Mathf.MoveTowards(state.Weight, active ? 1f : 0f, dt / .18f);
        if (state.Weight <= 0f) { States.Remove(id); return; }

        Vector3 cameraUp = state.Frame * Vector3.up;
        Vector3 cameraForward = Vector3.ProjectOnPlane(state.Forward, cameraUp).normalized;
        if (cameraForward.sqrMagnitude < .5f) { States.Remove(id); return; }
        Vector3 priorForward = Vector3.ProjectOnPlane(state.SmoothedForward, cameraUp).normalized;
        if (priorForward.sqrMagnitude > .5f)
            cameraForward = Vector3.Slerp(priorForward, cameraForward,
                AntigravityCameraMath.BlendFactor(dt, .035f)).normalized;
        state.SmoothedForward = cameraForward;
        // Build a rear/above offset directly from the road basis. Rotating the
        // stock world-up offset can put the camera beside or ahead of the kart.
        Vector3 chase = kart - cameraForward * (state.Distance * .94f)
            + cameraUp * (state.Distance * .35f);
        position = Vector3.Lerp(position, chase, state.Weight);
        float height = Vector3.Dot(position - kart, state.Up);
        position += state.Up * Mathf.Max(0f, 1.25f - height);
        Vector3 pivot = kart + state.Up * 1.25f;
        Vector3 safe = AntigravityFeature.ClipCamera(player, pivot, position);
        Vector3 offset = safe - pivot;
        float clearDistance = offset.magnitude;
        // Retract immediately for safety, then ease outward after an obstruction
        // clears. This prevents one-frame collision/no-collision camera pumping.
        state.ClearDistance = !state.HasClearance || clearDistance < state.ClearDistance
            ? clearDistance
            : Mathf.Lerp(state.ClearDistance, clearDistance, blend);
        state.HasClearance = true;
        position = clearDistance > .001f ? pivot + offset / clearDistance * state.ClearDistance : safe;

        Vector3 focus = kart + cameraForward + cameraUp * .65f;
        Vector3 view = focus - position;
        if (view.sqrMagnitude > .0001f && Vector3.Cross(view, cameraUp).sqrMagnitude > .0001f)
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(view, cameraUp), state.Weight);
        States[id] = state;
    }

    internal static void Reset(int instanceId) => States.Remove(instanceId);
    internal static void ResetAll() => States.Clear();
    private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    private static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w);
    private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}

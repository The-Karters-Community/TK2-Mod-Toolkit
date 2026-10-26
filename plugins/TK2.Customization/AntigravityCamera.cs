using System.Collections.Generic;
using UnityEngine;

namespace TK2.Customization;

// Optional local-race camera alignment. It transforms only the native racing
// camera output for the current frame; it does not write camera internals or
// issue physics collision queries.
internal static class AntigravityCamera
{
    private const float SmoothingSeconds = .16f;
    private const float FocusHeight = .65f;
    private struct State { internal bool Initialized; internal Quaternion Frame; }
    private static readonly Dictionary<int, State> States = new();

    internal static void Apply(PixelGameKartCamera camera, Ant_Player player,
        ref Vector3 position, ref Quaternion rotation, float fov)
    {
        if (camera == null || player == null) return;
        var plugin = Plugin.Instance;
        if (plugin == null) return;
        int id = camera.GetInstanceID();
        if (!plugin.AntigravityCameraEnabled.Value)
        {
            States.Remove(id);
            return;
        }
        if (!Plugin.OfflineLabAllowed)
        {
            States.Remove(id);
            return;
        }

        if (camera.bIsSpectatorCamera ||
            camera.fRacingCameraStrength < .95f || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL)
        {
            States.Remove(id);
            return;
        }

        bool active = AntigravityFeature.TryGetActiveFrame(player, out Vector3 surfaceUp, out _);
        if (!active && !States.ContainsKey(id)) return;

        if (player.visualInstanceSyncedParams == null)
        {
            States.Remove(id);
            return;
        }

        if (!States.TryGetValue(id, out var state))
        {
            if (States.Count >= 16) States.Clear();
            state = default;
        }

        Vector3 targetUp = active && surfaceUp.sqrMagnitude > .5f ? surfaceUp.normalized : Vector3.up;
        // Transport the previous frame onto the new surface normal instead of
        // rebuilding from world-up every frame. FromToRotation(Vector3.up,
        // targetUp) has an arbitrary roll near the top/bottom of a loop and
        // was the source of the visible camera snap/jitter there.
        Quaternion priorFrame = state.Initialized ? state.Frame : Quaternion.identity;
        Vector3 priorUp = priorFrame * Vector3.up;
        Quaternion desiredFrame = Quaternion.FromToRotation(priorUp, targetUp) * priorFrame;
        float blend = AntigravityCameraMath.BlendFactor(Time.unscaledDeltaTime, SmoothingSeconds);
        Quaternion frame = state.Initialized ? Quaternion.Slerp(priorFrame, desiredFrame, blend) : desiredFrame;
        Vector3 pivotUp = frame * Vector3.up;
        Vector3 pivot = player.visualInstanceSyncedParams.vCurPos + pivotUp * FocusHeight;
        Vector3 nativeOffset = position - pivot;
        if (!Finite(pivot) || !Finite(nativeOffset) || !Finite(rotation)) { States.Remove(id); return; }

        state.Initialized = true;
        state.Frame = frame;
        States[id] = state;
        position = pivot + frame * nativeOffset;
        rotation = frame * rotation;

        if (!active && Quaternion.Angle(frame, Quaternion.identity) < .5f)
            States.Remove(id);
    }

    internal static void Reset(int instanceId) => States.Remove(instanceId);
    internal static void ResetAll() => States.Clear();

    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

}

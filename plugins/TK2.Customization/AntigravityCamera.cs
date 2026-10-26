using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK2.Customization;

// Optional local-race camera alignment. It only consumes the already-attached antigravity
// state and modifies the native camera's by-ref outputs for this frame.
internal static class AntigravityCamera
{
    private const float SmoothingSeconds = .18f;
    private const float FocusHeight = .65f;
    private const float VolumePadding = .025f;
    private static readonly Dictionary<int, AntigravityCameraMath.State> States = new();
    private static readonly UnityCollisionProbe Collision = new();

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

        var sdkCamera = ReadableGame.GetCamera(camera)?.unityCamera;
        if (sdkCamera == null || player.visualInstanceSyncedParams == null)
        {
            States.Remove(id);
            return;
        }

        float near = Mathf.Clamp(sdkCamera.nearClipPlane, .03f, .5f);
        float aspect = Mathf.Clamp(sdkCamera.aspect, .5f, 3f);
        float safeFov = Mathf.Clamp(fov, 1f, 175f);
        float halfHeight = near * Mathf.Tan(safeFov * Mathf.Deg2Rad * .5f) + VolumePadding;
        float halfWidth = halfHeight * aspect + VolumePadding;
        var halfExtents = new Vector3(halfWidth, halfHeight, VolumePadding);

        if (!States.TryGetValue(id, out var state))
        {
            if (States.Count >= 16) States.Clear();
            state = default;
        }

        Vector3 targetUp = active ? surfaceUp.normalized : Vector3.up;
        Vector3 pivotUp = active ? targetUp :
            state.Initialized ? state.GravityFrame * Vector3.up : Vector3.up;
        Vector3 pivot = player.visualInstanceSyncedParams.vCurPos + pivotUp * FocusHeight;
        if (!Finite(pivot) || !Finite(position) || !Finite(rotation)) { States.Remove(id); return; }
        Vector3 nativePosition = position;

        Collision.SetIgnoredPlayer(player);
        var result = AntigravityCameraMath.Step(ref state, pivot, position, rotation,
            targetUp, halfExtents, Vector3.forward * near, Time.unscaledDeltaTime, SmoothingSeconds, Collision);
        if (!result.Safe) { States.Remove(id); return; }
        States[id] = state;
        position = result.Position;
        rotation = result.Rotation;

        if (!active && !result.Obstructed && Quaternion.Angle(state.GravityFrame, Quaternion.identity) < .5f &&
            Vector3.Distance(result.Position, nativePosition) < .05f)
            States.Remove(id);
    }

    internal static void Reset(int instanceId) => States.Remove(instanceId);
    internal static void ResetAll() => States.Clear();

    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private sealed class UnityCollisionProbe : AntigravityCameraMath.ICollisionProbe
    {
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];
        private Ant_Player? _ignoredPlayer;

        internal void SetIgnoredPlayer(Ant_Player player) => _ignoredPlayer = player;

        public bool IsClear(Vector3 center, Vector3 halfExtents, Quaternion orientation)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlaps, orientation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= _overlaps.Length) return false; // Saturation may hide an unreturned wall.
            for (int i = 0; i < count; i++)
                if (!Ignore(_overlaps[i])) return false;
            return true;
        }

        public bool Cast(Vector3 origin, Vector3 direction, float distance, Vector3 halfExtents,
            Quaternion orientation, out float hitDistance)
        {
            int count = Physics.BoxCastNonAlloc(origin, halfExtents, direction, _hits, orientation,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= _hits.Length) { hitDistance = 0f; return true; }
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || Ignore(hit.collider) || !Finite(hit.distance) || hit.distance < 0f) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }
            hitDistance = nearest;
            return !float.IsInfinity(nearest);
        }

        private bool Ignore(Collider? collider)
        {
            if (collider == null || collider.isTrigger) return true;
            var kart = collider.GetComponentInParent<PixelEasyCharMoveKartController>();
            if (kart != null && kart.parentPlayer == _ignoredPlayer) return true;
            return collider.GetComponentInParent<Ant_Player>() == _ignoredPlayer;
        }
    }
}

using System;

namespace TK2.Customization;

// Limit airborne loss above the soft cap, in game speed units per second.
internal static class CtrSpeedGhostMath
{
    internal static float DownhillFactor(float nx, float ny, float nz, float vx, float vz)
    {
        float n = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        float v = MathF.Sqrt(vx * vx + vz * vz);
        if (!float.IsFinite(n) || !float.IsFinite(v) || n < .01f || v < 1f || ny / n < .4f) return 0;
        return Math.Clamp((nx * vx + nz * vz) * ny / (n * n * v), 0f, 1f);
    }
    internal static float DownhillSpeed(float before, float after, float softCap, float factor,
        float acceleration, float groundLoss, float maximumRatio, float dt)
    {
        if (!float.IsFinite(factor) || !float.IsFinite(acceleration) || !float.IsFinite(maximumRatio) ||
            factor <= 0 || acceleration <= 0 || maximumRatio < 1 || dt <= 0 || !float.IsFinite(dt)) return after;
        float kept = RetainedSpeed(before, after, softCap, groundLoss, dt);
        float limit = Math.Max(softCap, softCap * maximumRatio);
        return kept + Math.Min(Math.Max(0f, limit - kept), factor * acceleration * dt);
    }
    internal static float RetainedSpeed(float before, float after, float cap, float lossPerSecond, float dt)
    {
        if (!float.IsFinite(before) || !float.IsFinite(after) || !float.IsFinite(cap) ||
            !float.IsFinite(lossPerSecond) || !float.IsFinite(dt) || before <= cap || after >= before ||
            dt <= 0 || lossPerSecond < 0 || before - after > Math.Max(1f, before * .15f)) return after;
        float retained = Math.Max(cap, before - lossPerSecond * dt);
        return Math.Max(after, Math.Min(before, retained));
    }
}

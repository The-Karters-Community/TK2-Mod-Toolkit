using System;

namespace TK2.Customization;

// Limit airborne loss above the soft cap, in game speed units per second.
internal static class CtrSpeedGhostMath
{
    internal static float RetainedSpeed(float before, float after, float cap, float lossPerSecond, float dt)
    {
        if (!float.IsFinite(before) || !float.IsFinite(after) || !float.IsFinite(cap) ||
            !float.IsFinite(lossPerSecond) || !float.IsFinite(dt) || before <= cap || after >= before ||
            dt <= 0 || lossPerSecond < 0 || before - after > Math.Max(1f, before * .15f)) return after;
        float retained = Math.Max(cap, before - lossPerSecond * dt);
        return Math.Max(after, Math.Min(before, retained));
    }
}

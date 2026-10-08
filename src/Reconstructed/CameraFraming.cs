using System;

namespace TK2.Reconstructed;

// New mod geometry, separate from the recovered game behavior.
public static class CameraFraming
{
    public static float DistanceScale(float originalFov, float requestedFov, bool preserveKartSize, float distanceMultiplier)
    {
        if (!float.IsFinite(originalFov) || !float.IsFinite(requestedFov) || !float.IsFinite(distanceMultiplier) ||
            originalFov <= 0 || originalFov >= 180 || requestedFov <= 0 || requestedFov >= 180 || distanceMultiplier <= 0)
            throw new ArgumentOutOfRangeException("Camera framing requires finite positive distance and FOV between 0 and 180 degrees.");
        if (!preserveKartSize) return distanceMultiplier;
        double radians = Math.PI / 360.0;
        double factor = Math.Tan(originalFov * radians) / Math.Tan(requestedFov * radians);
        return (float)Math.Clamp(factor * distanceMultiplier, 0.2, 6.0);
    }
}

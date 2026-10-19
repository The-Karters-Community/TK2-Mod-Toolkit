using System;

namespace TK2.Customization;

internal static class AirDashMath
{
    internal static float TargetLateral(float current, int direction, float impulse)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!float.IsFinite(impulse) || impulse < 0)
            throw new ArgumentOutOfRangeException(nameof(impulse));
        return current + direction * impulse;
    }

    internal static (float X, float Z) Hold(float x, float z, float rightX, float rightZ, float target, float speedBudget)
    {
        double length = Math.Sqrt((double)rightX * rightX + (double)rightZ * rightZ);
        if (!double.IsFinite(length) || length < .001) throw new ArgumentException("Kart lateral direction is unavailable.");
        if (!float.IsFinite(speedBudget) || speedBudget < 0) throw new ArgumentOutOfRangeException(nameof(speedBudget));
        double rx = rightX / length, rz = rightZ / length;
        double lateral = x * rx + z * rz;
        double nx = x + rx * (target - lateral), nz = z + rz * (target - lateral);
        double magnitude = Math.Sqrt(nx * nx + nz * nz);
        if (magnitude > speedBudget && magnitude > 0) { nx *= speedBudget / magnitude; nz *= speedBudget / magnitude; }
        return ((float)nx, (float)nz);
    }
}

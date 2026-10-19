using System;

namespace TK2.Customization;

// Project the impulse onto the original horizontal-speed disk. No heading or Y changes.
internal static class AirShiftMath
{
    internal static (float X, float Z) Adjust(float x, float z, float rightX, float rightZ, float impulse, float retention)
    {
        double speed = Math.Sqrt((double)x * x + (double)z * z);
        double rightLength = Math.Sqrt((double)rightX * rightX + (double)rightZ * rightZ);
        if (speed < .001 || rightLength < .001) return (x, z);
        double nx = x + rightX / rightLength * impulse, nz = z + rightZ / rightLength * impulse;
        double length = Math.Sqrt(nx * nx + nz * nz);
        double limit = speed * Math.Clamp(retention, .9f, 1f);
        if (length > limit) { nx *= limit / length; nz *= limit / length; }
        return ((float)nx, (float)nz);
    }
}

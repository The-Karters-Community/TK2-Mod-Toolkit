using System;

namespace TK2.Customization;

// Project the impulse onto the original horizontal-speed disk. No heading or Y changes.
internal static class AirShiftMath
{
    internal static (float X, float Z) Hold(float x, float z, float rightX, float rightZ, float target, float budget, bool limited)
    {
        double rightLength = Math.Sqrt((double)rightX * rightX + (double)rightZ * rightZ);
        if (rightLength < .001) return (x, z);
        double rx = rightX / rightLength, rz = rightZ / rightLength;
        double difference = target - (x * rx + z * rz);
        double nx = x + rx * difference, nz = z + rz * difference;
        double length = Math.Sqrt(nx * nx + nz * nz);
        if (limited && length > budget) { nx *= budget / length; nz *= budget / length; }
        return ((float)nx, (float)nz);
    }
}

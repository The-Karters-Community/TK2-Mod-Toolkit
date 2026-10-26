using System;

namespace UnityEngine;

internal static class Mathf
{
    internal const float Deg2Rad = MathF.PI / 180f;
    internal const float Rad2Deg = 180f / MathF.PI;
    internal static float Min(float a, float b) => MathF.Min(a, b);
    internal static float Max(float a, float b) => MathF.Max(a, b);
    internal static float Abs(float value) => MathF.Abs(value);
    internal static float Atan2(float y, float x) => MathF.Atan2(y, x);
    internal static float DeltaAngle(float current, float target)
    {
        float delta = (target - current) % 360f;
        if (delta > 180f) delta -= 360f;
        if (delta < -180f) delta += 360f;
        return delta;
    }
    internal static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
}

internal struct Vector3
{
    internal float x, y, z;
    internal Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    internal static Vector3 zero => new(0, 0, 0);
    internal static Vector3 up => new(0, 1, 0);
    internal static Vector3 down => new(0, -1, 0);
    internal static Vector3 right => new(1, 0, 0);
    internal static Vector3 forward => new(0, 0, 1);
    internal float magnitude => MathF.Sqrt(x * x + y * y + z * z);
    internal float sqrMagnitude => x * x + y * y + z * z;
    internal Vector3 normalized => magnitude > 0 ? this / magnitude : zero;
    internal void Normalize() { float length = magnitude; if (length > 0) { x /= length; y /= length; z /= length; } }
    internal static float SqrMagnitude(Vector3 value) => value.sqrMagnitude;
    internal static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    internal static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    internal static float Angle(Vector3 a, Vector3 b)
    {
        var denom = a.magnitude * b.magnitude;
        if (denom == 0) return 0;
        return MathF.Acos(Math.Clamp(Dot(a, b) / denom, -1, 1)) * Mathf.Rad2Deg;
    }
    internal static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
    internal static Vector3 ProjectOnPlane(Vector3 value, Vector3 normal) => value - normal * Dot(value, normal);
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
    public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
    public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
}

internal readonly struct Quaternion
{
    private readonly float x, y, z, w;
    internal Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    internal static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        forward = forward.normalized;
        Vector3 right = Vector3.Cross(up.normalized, forward).normalized;
        up = Vector3.Cross(forward, right).normalized;
        float m00 = right.x, m01 = up.x, m02 = forward.x;
        float m10 = right.y, m11 = up.y, m12 = forward.y;
        float m20 = right.z, m21 = up.z, m22 = forward.z;
        float trace = m00 + m11 + m22;
        if (trace > 0)
        {
            float s = MathF.Sqrt(trace + 1f) * 2f;
            return new((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, .25f * s);
        }
        if (m00 > m11 && m00 > m22)
        {
            float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
            return new(.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
        }
        if (m11 > m22)
        {
            float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
            return new((m01 + m10) / s, .25f * s, (m12 + m21) / s, (m02 - m20) / s);
        }
        float last = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
        return new((m02 + m20) / last, (m12 + m21) / last, .25f * last, (m10 - m01) / last);
    }
    internal static Quaternion AngleAxis(float degrees, Vector3 axis)
    {
        axis = axis.normalized;
        float half = degrees * Mathf.Deg2Rad * .5f;
        float sine = MathF.Sin(half);
        return new(axis.x * sine, axis.y * sine, axis.z * sine, MathF.Cos(half));
    }
    internal static float Angle(Quaternion a, Quaternion b)
    {
        float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        return MathF.Acos(Math.Clamp(MathF.Abs(dot), -1, 1)) * 2 * Mathf.Rad2Deg;
    }
    internal static Quaternion FromToRotation(Vector3 from, Vector3 to)
    {
        from /= from.magnitude;
        to /= to.magnitude;
        var dot = Vector3.Dot(from, to);
        if (dot < -0.999999f)
        {
            var axis = Vector3.Cross(from, new Vector3(1, 0, 0));
            if (axis.magnitude < .0001f) axis = Vector3.Cross(from, new Vector3(0, 0, 1));
            axis /= axis.magnitude;
            return new(axis.x, axis.y, axis.z, 0);
        }
        var cross = Vector3.Cross(from, to);
        var inverse = 1 / MathF.Sqrt(2 * (1 + dot));
        return new(cross.x * inverse, cross.y * inverse, cross.z * inverse, MathF.Sqrt((1 + dot) / 2));
    }
    public static Vector3 operator *(Quaternion q, Vector3 v)
    {
        var u = new Vector3(q.x, q.y, q.z);
        return u * (2 * Vector3.Dot(u, v)) + v * (q.w * q.w - Vector3.Dot(u, u)) + Vector3.Cross(u, v) * (2 * q.w);
    }
}

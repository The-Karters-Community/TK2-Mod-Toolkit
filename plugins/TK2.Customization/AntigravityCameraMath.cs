using UnityEngine;

namespace TK2.Customization;

internal static partial class AntigravityCameraMath
{
    internal static float BlendFactor(float deltaTime, float smoothingSeconds)
    {
        if (!Finite(deltaTime) || deltaTime <= 0f) return 0f;
        if (!Finite(smoothingSeconds) || smoothingSeconds <= 0f) return 1f;
        return 1f - Mathf.Exp(-Mathf.Min(deltaTime, .1f) / smoothingSeconds);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

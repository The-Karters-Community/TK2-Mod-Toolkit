using System;

namespace TK2.Customization;

internal readonly struct InputOverlayModel
{
    internal readonly float Steering;
    internal readonly float Acceleration;
    internal readonly bool JumpDrift, Boost, Accelerate, Reverse, Weapon, Brake;
    internal readonly bool IsReplay;
    internal readonly string Device;

    private InputOverlayModel(float steering, float acceleration, bool jumpDrift, bool boost,
        bool weapon, bool brake, bool isReplay, string device)
    {
        Steering = ClampAxis(steering);
        Acceleration = ClampAxis(acceleration);
        JumpDrift = jumpDrift; Boost = boost; Accelerate = Acceleration > 0.05f; Reverse = Acceleration < -0.05f;
        Weapon = weapon; Brake = brake;
        IsReplay = isReplay;
        Device = string.IsNullOrWhiteSpace(device) ? "INPUT" : device;
    }

    internal static InputOverlayModel CreateLive(float steering, float acceleration, bool jumpDrift,
        bool boost, bool weapon, bool brake, string device) => new(steering, acceleration, jumpDrift, boost,
            weapon, brake, false, device);

    internal static InputOverlayModel CreateReplay(float steering, float acceleration, bool jumpDrift,
        bool boost, bool brake) => new(steering, acceleration, jumpDrift, boost, false, brake, true, "RECORDED");

    internal static float ClampAxis(float value) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(-1, Math.Min(1, value));
}

internal static class InputOverlayPlacement
{
    internal static (float X, float Y) Resolve(string preset, float screenWidth, float screenHeight,
        float overlayWidth, float overlayHeight, float margin, float positionX, float positionY)
    {
        float minX = margin, minY = margin;
        float maxX = Math.Max(margin, screenWidth - overlayWidth - margin);
        float maxY = Math.Max(margin, screenHeight - overlayHeight - margin);
        float x = preset == "Custom" ? Lerp(minX, maxX, positionX / 100f) :
            preset.EndsWith("Right", StringComparison.OrdinalIgnoreCase) ? maxX :
            preset.EndsWith("Center", StringComparison.OrdinalIgnoreCase) ? (screenWidth - overlayWidth) * .5f : minX;
        float y = preset == "Custom" ? Lerp(minY, maxY, positionY / 100f) :
            preset.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase) ? maxY :
            preset.StartsWith("Middle", StringComparison.OrdinalIgnoreCase) ? (screenHeight - overlayHeight) * .5f : minY;
        return (Math.Clamp(x, minX, maxX), Math.Clamp(y, minY, maxY));
    }

    private static float Lerp(float start, float end, float fraction) => start + (end - start) * Math.Clamp(fraction, 0, 1);
}

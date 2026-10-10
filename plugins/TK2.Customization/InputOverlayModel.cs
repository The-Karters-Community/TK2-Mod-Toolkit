using System;

namespace TK2.Customization;

internal readonly struct InputOverlayModel
{
    internal readonly float Steering;
    internal readonly float Acceleration;
    internal readonly bool JumpDrift, Boost, Accelerate, Reverse, Weapon, Brake, LookBack, TargetUp, TargetDown, Pickup, Menu, PlayerList;
    internal readonly bool IsReplay;
    internal readonly string Device;

    private InputOverlayModel(float steering, float acceleration, bool jumpDrift, bool boost,
        bool weapon, bool brake, bool lookBack, bool targetUp, bool targetDown, bool pickup,
        bool menu, bool playerList, bool isReplay, string device)
    {
        Steering = ClampAxis(steering);
        Acceleration = ClampAxis(acceleration);
        JumpDrift = jumpDrift; Boost = boost; Accelerate = Acceleration > 0.05f; Reverse = Acceleration < -0.05f;
        Weapon = weapon; Brake = brake;
        LookBack = lookBack; TargetUp = targetUp; TargetDown = targetDown; Pickup = pickup;
        Menu = menu; PlayerList = playerList;
        IsReplay = isReplay;
        Device = string.IsNullOrWhiteSpace(device) ? "INPUT" : device;
    }

    internal static InputOverlayModel CreateLive(float steering, float acceleration, bool jumpDrift,
        bool boost, bool weapon, bool brake, bool lookBack, bool targetUp, bool targetDown, bool pickup,
        bool menu, bool playerList, string device) => new(steering, acceleration, jumpDrift, boost,
            weapon, brake, lookBack, targetUp, targetDown, pickup, menu, playerList, false, device);

    internal static InputOverlayModel CreateReplay(float steering, float acceleration, bool jump, bool drift,
        bool boost, bool weapon, bool brake, bool lookBack, bool targetUp, bool targetDown, bool pickup) =>
        new(steering, acceleration, jump || drift, boost, weapon, brake, lookBack, targetUp, targetDown,
            pickup, false, false, true, "RECORDED");

    internal static float ClampAxis(float value) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(-1, Math.Min(1, value));
}

using System;

namespace TK2.Customization;

// Input timing is in seconds, independent of rendering and physics frame rates.
internal sealed class MidairDriftState
{
    internal const float BufferSeconds = 10f / 60f, CoyoteSeconds = 1f / 6f;
    private float RequestedAt = float.NegativeInfinity;
    private int LastPressFrame = -1;
    internal string Button = "";
    internal bool Active;

    internal void Press(float now, int frame, string button, bool drifting)
    {
        if (frame == LastPressFrame || drifting) return;
        LastPressFrame = frame;
        RequestedAt = now;
        Button = button;
    }

    internal bool Pending(float now, bool held) => held && now >= RequestedAt && now - RequestedAt <= BufferSeconds;

    internal static bool CanDrift(float steering, float speed, float minimumSpeed) =>
        float.IsFinite(steering) && Math.Abs(steering) > .1f && float.IsFinite(speed) && speed >= minimumSpeed;

    internal bool CanJump(float now, bool held, bool grounded, bool waitingForLanding, float timeOffGround) =>
        Pending(now, held) && (grounded || !waitingForLanding && timeOffGround >= 0 && timeOffGround <= CoyoteSeconds);

    internal bool Accept(bool eligible, float now, bool held, float steering, float speed, float minimumSpeed)
    {
        // Native can set its jump trigger for other airborne actions. Only a hop
        // eligible at the start of this physics call may start our drift.
        if (!eligible) return false;
        bool start = Pending(now, held) && CanDrift(steering, speed, minimumSpeed);
        RequestedAt = float.NegativeInfinity;
        Active = start;
        return start;
    }

    internal void Clear() { RequestedAt = float.NegativeInfinity; Button = ""; Active = false; }
    internal void Land() => Active = false;
}

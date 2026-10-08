// Hand-reconstructed from the matching 0.1.4.18 native bodies, not original developer source.
// Normal, initialized objects are assumed. Native IL2CPP metadata initialization is omitted.
using System.Collections.Generic;

namespace TK2.Reconstructed;

public struct Velocity
{
    public float X, Y, Z;
    public Velocity(float x, float y, float z) { X = x; Y = y; Z = z; }
}

public sealed class JumpState
{
    public bool ExtraJumpAllowsTrick, PendingTrick, RunJump;
    public float JumpedAt, TimeSinceNotGrounded, GracePeriod;
    public readonly List<float> PressesBeforeGround = new();
}

public static class KartLogic
{
    // PixelKartPhysics.AddVelocity @ 0x1805dd8a0: all three components accumulate.
    public static Velocity AddVelocity(Velocity pending, Velocity added) =>
        new(pending.X + added.X, pending.Y + added.Y, pending.Z + added.Z);

    // PixelKartPhysics.JumpInput(bool) @ 0x1805e0270.
    // This requests a jump; UpdateJumping applies motion later. It does not directly set velocity.
    public static void JumpInput(JumpState state, bool pressed, float now,
        bool foundGround, bool replayPlaying, bool replayGrounded)
    {
        if (!pressed) return;
        if (state.ExtraJumpAllowsTrick)
        {
            state.JumpedAt = now;
            state.PendingTrick = true;
        }
        state.PressesBeforeGround.Clear();
        bool grounded = foundGround || (replayPlaying && replayGrounded);
        if (!grounded) state.PressesBeforeGround.Insert(0, now);
        state.RunJump = grounded || state.TimeSinceNotGrounded <= state.GracePeriod;
    }
}

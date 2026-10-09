using System;

namespace TK2.Customization;

// Fixed-size counters; inclusive method spans can overlap.
internal sealed class PerformanceSamples
{
    internal readonly long[] Calls, Ticks, MaxTicks;
    private readonly int[] _frameBins = new int[801]; // .25 ms bins; last is >=200 ms.
    internal int Frames { get; private set; }
    internal double FrameSeconds { get; private set; }
    internal double MaxFrameMs { get; private set; }
    internal PerformanceSamples(int targets) { Calls = new long[targets]; Ticks = new long[targets]; MaxTicks = new long[targets]; }
    internal void Method(int slot, long elapsed)
    {
        if (elapsed < 0) return;
        Calls[slot]++; Ticks[slot] += elapsed; MaxTicks[slot] = Math.Max(MaxTicks[slot], elapsed);
    }
    internal void Frame(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        Frames++; FrameSeconds += seconds;
        double ms = seconds * 1000;
        MaxFrameMs = Math.Max(MaxFrameMs, ms);
        _frameBins[(int)Math.Min(800, Math.Floor(ms * 4))]++;
    }
    internal double? FrameP95Ms()
    {
        if (Frames == 0) return null;
        int required = (int)Math.Ceiling(Frames * .95), count = 0;
        for (int i = 0; i < _frameBins.Length; i++)
        { count += _frameBins[i]; if (count >= required) return i == 800 ? null : (i + 1) * .25; }
        return null;
    }
    internal void Reset()
    {
        Array.Clear(Calls, 0, Calls.Length); Array.Clear(Ticks, 0, Ticks.Length); Array.Clear(MaxTicks, 0, MaxTicks.Length);
        Array.Clear(_frameBins, 0, _frameBins.Length); Frames = 0; FrameSeconds = MaxFrameMs = 0;
    }
}

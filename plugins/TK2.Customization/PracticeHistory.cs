using System.Collections.Generic;

namespace TK2.Customization;

internal sealed class PracticeHistory<T>
{
    private readonly List<(float Time, T Value)> Samples = new();
    internal int Count => Samples.Count;
    internal float Oldest => Samples.Count == 0 ? 0 : Samples[0].Time;
    internal float Newest => Samples.Count == 0 ? 0 : Samples[^1].Time;
    internal void Add(float time, T value, float seconds)
    {
        Branch(time);
        Samples.Add((time, value));
        Trim(time, seconds);
    }
    internal void Trim(float time, float seconds)
    {
        while (Samples.Count > 0 && (Samples[0].Time < time - seconds || Samples.Count > 401)) Samples.RemoveAt(0);
    }
    internal bool TryAt(float time, out T value, out float capturedAt)
    {
        for (int i = Samples.Count - 1; i >= 0; i--)
            if (Samples[i].Time <= time) { (capturedAt, value) = Samples[i]; return true; }
        value = default!; capturedAt = 0; return false;
    }
    internal void Branch(float time) => Samples.RemoveAll(sample => sample.Time > time);
    internal void Clear() => Samples.Clear();
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TK2.Customization;

// Deterministic completed native buffers; none of these are actual game timings.
internal sealed class EngineApiStub : PerformanceEngineSamples.IApi
{
    internal readonly Dictionary<string, ulong> Names = new() { ["GPU Frame Time"] = 1, ["Draw Calls Count"] = 2, ["Physics.Simulate"] = 3 };
    internal int Released, Copies;
    internal bool ThrowCopy, ThrowFind, ThrowRelease;
    private readonly HashSet<ulong> _consumed = new();
    public ulong Find(string name)
    {
        if (ThrowFind && name == "Physics.Simulate") throw new InvalidOperationException("find failed");
        return Names.TryGetValue(name, out var id) ? id : 0;
    }
    public PerformanceEngineSamples.Description Describe(ref ulong handle) => new() { DataType = 4, Unit = (byte)(handle == 2 ? 3 : 1) };
    public ulong Create(ref ulong handle) => handle;
    public int Copy(ref ulong recorder, IntPtr buffer)
    {
        Copies++;
        if (ThrowCopy) throw new InvalidOperationException("copy failed");
        if (!_consumed.Add(recorder)) return 0;
        long value = recorder switch { 1 => 2_000_000, 2 => 40, _ => 0 };
        Marshal.WriteInt64(buffer, 0, value); Marshal.WriteInt64(buffer, 8, 1);
        if (recorder == 1) { Marshal.WriteInt64(buffer, 24, 0); Marshal.WriteInt64(buffer, 32, 1); return 2; }
        return 1;
    }
    public void Release(ref ulong recorder)
    {
        Released++;
        if (ThrowRelease) throw new InvalidOperationException("release failed");
    }
}

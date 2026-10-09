using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Il2CppInterop.Runtime;

namespace TK2.Customization;

// Unity 6 native ProfilerRecorder bridge: these managed types were stripped from this
// game's interop assembly. Exact shipped icall ABI and source layouts are documented.
internal sealed class PerformanceEngineSamples : IDisposable
{
    internal const int Capacity = 8;
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct Description
    {
        [FieldOffset(0)] public ushort Category;
        [FieldOffset(2)] public ushort Flags;
        [FieldOffset(4)] public byte DataType;
        [FieldOffset(5)] public byte Unit;
        [FieldOffset(12)] public int NameLength;
        [FieldOffset(16)] public IntPtr Name;
    }
    internal interface IApi
    {
        ulong Find(string name);
        Description Describe(ref ulong handle);
        ulong Create(ref ulong handle);
        int Copy(ref ulong recorder, IntPtr buffer);
        void Release(ref ulong recorder);
    }
    private sealed class Metric
    {
        internal readonly string Name;
        internal readonly bool PositiveOnly;
        internal ulong Recorder;
        internal Description Description;
        internal string Status = "not_started";
        internal long Samples, InvalidSamples, ZeroSamples, FullBuffers;
        internal double Total, Max;
        internal Metric(string name, bool positiveOnly = false) { Name = name; PositiveOnly = positiveOnly; }
        internal void Reset() { Samples = InvalidSamples = ZeroSamples = FullBuffers = 0; Total = Max = 0; }
    }
    private readonly Metric[] _metrics = {
        new("CPU Total Frame Time", true), new("CPU Main Thread Frame Time", true),
        new("CPU Render Thread Frame Time", true), new("GPU Frame Time", true),
        new("Main Thread"), new("Render Thread"), new("PlayerLoop"),
        new("Physics.Simulate"), new("Physics.Processing"), new("Animator.Update"),
        new("Animation.Update"), new("Skinning.Update"), new("Skinning"),
        new("Gfx.WaitForPresentOnGfxThread"), new("Gfx.WaitForRenderThread"),
        new("Draw Calls Count"), new("Batches Count"), new("SetPass Calls Count"),
        new("Triangles Count"), new("Vertices Count") };
    private readonly IApi? _providedApi;
    private IApi? _api;
    private IntPtr _buffer;
    private string _status = "not_started";
    private string? _error;

    internal PerformanceEngineSamples(IApi? api = null) => _providedApi = api;
    internal void Start()
    {
        Dispose(); _error = null;
        foreach (var m in _metrics) { m.Reset(); m.Status = "unavailable"; m.Description = default; }
        try
        {
            _api = _providedApi ?? NativeApi.Bind();
            _buffer = Marshal.AllocHGlobal(Capacity * 24);
            _status = "available";
            foreach (var m in _metrics)
            {
                ulong handle = _api.Find(m.Name);
                if (handle == 0 || handle == ulong.MaxValue) { m.Status = "metric_not_registered"; continue; }
                m.Description = _api.Describe(ref handle);
                // Last sample storage is a signed 64-bit integer. Preserve its unit;
                // float/double or unknown metrics must not be reinterpreted as integers.
                if (m.Description.DataType != 4 || m.Description.Unit is not (1 or 2 or 3))
                { m.Status = "unsupported_data_type_or_unit"; continue; }
                m.Recorder = _api.Create(ref handle);
                m.Status = m.Recorder == 0 ? "recorder_unavailable" : "recording";
            }
        }
        catch (Exception ex) { _error = ex.Message; _status = "unavailable"; Dispose(); }
    }
    internal void Tick()
    {
        if (_api == null || _buffer == IntPtr.Zero) return;
        try
        {
            foreach (var m in _metrics)
            {
                if (m.Recorder == 0) continue;
                int count = _api.Copy(ref m.Recorder, _buffer);
                if (count < 0 || count > Capacity) throw new InvalidOperationException("Unexpected native profiler sample count.");
                if (count == Capacity) m.FullBuffers++;
                for (int i = 0; i < count; i++)
                {
                    long value = Marshal.ReadInt64(_buffer, i * 24);
                    // Copy(reset:true) consumes each completed sample exactly once;
                    // reading LastValue repeatedly would count stale GPU frames.
                    if (value < 0 || (m.PositiveOnly && value == 0)) { m.InvalidSamples++; continue; }
                    m.Samples++; if (value == 0) m.ZeroSamples++;
                    m.Total += value; if (value > m.Max) m.Max = value;
                }
            }
        }
        catch (Exception ex) { _error = ex.Message; _status = "read_failed"; Dispose(); }
    }
    internal object Report()
    {
        var metrics = new object[_metrics.Length];
        for (int i = 0; i < metrics.Length; i++)
        {
            var m = _metrics[i]; string unit = m.Description.Unit switch { 1 => "milliseconds", 2 => "bytes", 3 => "count", _ => "unknown" };
            double scale = m.Description.Unit == 1 ? 1e-6 : 1;
            metrics[i] = new { name = m.Name, status = m.Status == "recording" ? (m.Samples == 0 ? "no_valid_samples" : "sampled") : m.Status,
                category = m.Description.Category, nativeDataType = m.Description.DataType, nativeUnit = m.Description.Unit, unit,
                samples = m.Samples, invalidSamples = m.InvalidSamples, zeroSamples = m.ZeroSamples,
                fullBuffers = m.FullBuffers, average = m.Samples == 0 ? (double?)null : m.Total * scale / m.Samples,
                maximum = m.Samples == 0 ? (double?)null : m.Max * scale };
        }
        return new { source = "Unity native ProfilerRecorder", status = _status, error = _error, metrics,
            limitation = "Completed samples can lag by four frames. Timers overlap and may include waits; do not sum them. Unregistered or stripped release markers stay unavailable. Zero CPU/GPU frame timings are invalid, not evidence of no workload. Full buffers can lose samples. Observation has overhead." };
    }
    internal void ResetWindow() { foreach (var m in _metrics) m.Reset(); }
    public void Dispose()
    {
        if (_api != null)
            foreach (var m in _metrics)
                if (m.Recorder != 0)
                {
                    try { _api.Release(ref m.Recorder); }
                    catch (Exception ex) { _error ??= "Recorder release failed: " + ex.Message; }
                    finally { m.Recorder = 0; }
                }
        if (_buffer != IntPtr.Zero) { Marshal.FreeHGlobal(_buffer); _buffer = IntPtr.Zero; }
        _api = null;
    }
    private sealed class NativeApi : IApi
    {
        private const string PlayerHash = "5da2e6c1050924f370b8143256a1705ac3758bb78eb07013a268058edeb6709a";
        private static bool _verified;
        // Observed Win64 icall ABI: ref category/handle, UTF-16 pointer, length,
        // explicit out return for structures. No object/string marshalling involved.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FindCall(ref ushort category, IntPtr name, int length, out ulong handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DescribeCall(ref ulong handle, out Description description);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void CreateCall(ref ulong handle, int capacity, int options, out ulong recorder);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CopyCall(ref ulong recorder, IntPtr buffer, int capacity, [MarshalAs(UnmanagedType.I1)] bool reset);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ControlCall(ref ulong recorder, int option);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool ValidCall(ref ulong recorder);
        private readonly FindCall _find;
        private readonly DescribeCall _describe;
        private readonly CreateCall _create;
        private readonly CopyCall _copy;
        private readonly ControlCall _control;
        private readonly ValidCall _valid;
        private NativeApi()
        {
            _find = Resolve<FindCall>("Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle::GetByName_Unsafe_Injected");
            _describe = Resolve<DescribeCall>("Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle::GetDescriptionInternal_Injected");
            _create = Resolve<CreateCall>("Unity.Profiling.ProfilerRecorder::Create_Injected");
            _copy = Resolve<CopyCall>("Unity.Profiling.ProfilerRecorder::CopyTo_Pointer_Injected");
            _control = Resolve<ControlCall>("Unity.Profiling.ProfilerRecorder::Control_Injected");
            _valid = Resolve<ValidCall>("Unity.Profiling.ProfilerRecorder::GetValid_Injected");
        }
        internal static NativeApi Bind()
        {
            if (!_verified)
            {
                if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new NotSupportedException("Native profiler ABI requires verified Win64 player.");
                using var file = File.OpenRead(Path.Combine(BepInEx.Paths.GameRootPath, "UnityPlayer.dll"));
                using var sha = SHA256.Create();
                if (!Convert.ToHexString(sha.ComputeHash(file)).Equals(PlayerHash, StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("UnityPlayer binary changed; native profiler bridge disabled.");
                _verified = true;
            }
            return new NativeApi();
        }
        private static T Resolve<T>(string name) where T : Delegate
        {
            IntPtr pointer = IL2CPP.il2cpp_resolve_icall(name);
            if (pointer == IntPtr.Zero) throw new MissingMethodException("Native profiler icall unavailable: " + name);
            return Marshal.GetDelegateForFunctionPointer<T>(pointer);
        }
        public ulong Find(string name)
        {
            IntPtr text = Marshal.StringToHGlobalUni(name);
            try { ushort category = ushort.MaxValue; _find(ref category, text, name.Length, out ulong handle); return handle; }
            finally { Marshal.FreeHGlobal(text); }
        }
        public Description Describe(ref ulong handle) { _describe(ref handle, out var result); return result; }
        public ulong Create(ref ulong handle)
        {
            // StartImmediately | WrapAroundWhenCapacityReached | SumAllSamplesInFrame.
            _create(ref handle, Capacity, 25, out ulong recorder);
            if (recorder != 0 && !_valid(ref recorder)) { _control(ref recorder, 4); return 0; }
            return recorder;
        }
        public int Copy(ref ulong recorder, IntPtr buffer) => _copy(ref recorder, buffer, Capacity, true);
        public void Release(ref ulong recorder) => _control(ref recorder, 4);
    }
}

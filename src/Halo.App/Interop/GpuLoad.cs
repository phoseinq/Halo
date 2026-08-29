using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Halo.Interop;

internal static class GpuLoad
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;

    private static volatile float _last = -1f;
    private static long _startedAt;
    private static int _busy;

    internal static float Last => _last;

    internal static void Refresh(int minGapMs = 4000)
    {
        long now = Environment.TickCount64;
        if (now - _startedAt < minGapMs) return;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        _startedAt = now;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try { _last = Sample(); }
            catch { }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });
    }

    internal static string EngineType(string instance)
    {
        if (string.IsNullOrEmpty(instance)) return "";
        const string marker = "engtype_";
        int at = instance.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? "" : instance[(at + marker.Length)..];
    }

    internal static float Busiest(IEnumerable<(string Instance, double Value)> samples)
    {
        var byType = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instance, value) in samples)
        {
            if (value <= 0) continue;
            string type = EngineType(instance);
            if (type.Length == 0) continue;
            byType[type] = byType.TryGetValue(type, out double sum) ? sum + value : value;
        }
        double best = 0;
        foreach (var v in byType.Values) if (v > best) best = v;
        return (float)Math.Clamp(best / 100.0, 0.0, 1.0);
    }

    internal static float SampleBlocking()
    {
        try { _last = Sample(); } catch { }
        return _last;
    }

    private static float Sample()
    {
        IntPtr query = IntPtr.Zero;
        try
        {
            if (PdhOpenQueryW(null, IntPtr.Zero, out query) != 0) return -1f;
            if (PdhAddEnglishCounterW(query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero,
                                      out IntPtr counter) != 0) return -1f;

            if (PdhCollectQueryData(query) != 0) return -1f;
            System.Threading.Thread.Sleep(300);
            if (PdhCollectQueryData(query) != 0) return -1f;

            uint size = 0, count = 0;
            uint rc = PdhGetFormattedCounterArrayW(counter, PdhFmtDouble, ref size, out count, IntPtr.Zero);
            if (rc != PdhMoreData || size == 0) return -1f;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (PdhGetFormattedCounterArrayW(counter, PdhFmtDouble, ref size, out count, buffer) != 0)
                    return -1f;

                var samples = new List<(string, double)>((int)count);
                int stride = Marshal.SizeOf<PdhFmtCounterValueItem>();
                for (int i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buffer + i * stride);
                    string name = item.NamePtr == IntPtr.Zero ? "" : Marshal.PtrToStringUni(item.NamePtr) ?? "";
                    samples.Add((name, item.Value.DoubleValue));
                }
                return Busiest(samples);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        catch { return -1f; }
        finally { if (query != IntPtr.Zero) PdhCloseQuery(query); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValue
    {
        public uint CStatus;

        private readonly uint _pad;
        public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValueItem
    {
        public IntPtr NamePtr;
        public PdhFmtCounterValue Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize,
                                                            out uint itemCount, IntPtr buffer);
    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}

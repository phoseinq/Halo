namespace Halo.Interop;

internal static class CpuLoad
{
    private static volatile float _last = -1f;

    internal static float Last => _last;

    internal static void Observe(float busy) => _last = busy < 0f ? -1f : (busy > 1f ? 1f : busy);

    internal static float SampleBlocking(int ms = 120)
    {
        try
        {
            if (!Win32.GetSystemTimes(out long idle0, out long kern0, out long user0)) return -1f;
            System.Threading.Thread.Sleep(ms);
            if (!Win32.GetSystemTimes(out long idle1, out long kern1, out long user1)) return -1f;
            long total = (kern1 + user1) - (kern0 + user0);
            if (total <= 0) return -1f;
            float busy = 1f - (float)(idle1 - idle0) / total;
            Observe(busy);
            return _last;
        }
        catch { return -1f; }
    }
}

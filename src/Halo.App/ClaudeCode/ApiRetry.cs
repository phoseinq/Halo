using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace Halo.ClaudeCode;

internal static class ApiRetry
{

    public static volatile int Seconds = -1;
    public static int Version;

    public static volatile int Pid;

    private const int GoodForMs = 4000;

    private static int _busy;
    private static long _polledAt;
    private static long _readAt;

        public static bool LiveFor(int pid)
        => pid > 0 && Pid == pid && Seconds >= 0 && Environment.TickCount64 - Volatile.Read(ref _readAt) < GoodForMs;

    public static void Poke(int pid)
    {
        if (pid <= 0) return;
        if (pid != Pid) Track(pid);
        long now = Environment.TickCount64;
        if (now - _polledAt < 900) return;
        _polledAt = now;
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { Sample(pid); } catch { } finally { Volatile.Write(ref _busy, 0); }
        });
    }

    public static void Done()
    {
        if (Pid == 0 && Seconds < 0) return;
        Track(0);
    }

    internal static void Track(int pid)
    {
        Pid = pid;
        Seconds = -1;
        Volatile.Write(ref _readAt, 0);
        Interlocked.Increment(ref Version);
    }

    private static void Sample(int pid)
    {

        var rows = Interop.ConsoleRead.Tail(pid, 14, below: 2);
        if (rows is null) return;
        if (pid != Pid) return;

        int? found = null;
        foreach (var row in rows)
            if (RetryIn(row) is { } s) found = s;

        if (found is { } secs)
        {
            Seconds = secs;
            Volatile.Write(ref _readAt, Environment.TickCount64);
            Interlocked.Increment(ref Version);
        }
        else if (Seconds >= 0)
        {

            Seconds = -1;
            Interlocked.Increment(ref Version);
        }
    }

    private static readonly Regex Wait = new(
        @"\bretry(?:ing)?\s+in\s+(?:(\d+)\s*m(?:in(?:ute)?s?)?\s*)?(?:(\d+)\s*s(?:ec(?:ond)?s?)?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static int? RetryIn(string? line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        var m = Wait.Match(line);
        if (!m.Success) return null;
        bool hasM = m.Groups[1].Success, hasS = m.Groups[2].Success;
        if (!hasM && !hasS) return null;
        long total = 0;
        if (hasM) total += long.Parse(m.Groups[1].Value) * 60;
        if (hasS) total += long.Parse(m.Groups[2].Value);

        return total is > 0 and <= 3600 ? (int)total : null;
    }

    internal static string Caption(int seconds)
        => seconds < 0 ? ""
         : seconds < 60 ? seconds + "s"
         : seconds % 60 == 0 ? seconds / 60 + "m"
         : $"{seconds / 60}m {seconds % 60}s";

    public static string Caption() => Caption(Seconds);
}

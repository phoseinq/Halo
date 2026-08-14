using System;

namespace Halo.Widgets;

internal static class NetRate
{

    internal const long OnBytesPerSec = 125_000;

    internal const double OffHoldSeconds = 3.0;

        internal static long Delta(long previous, long current)
    {

        if (current < previous) return 0;
        return current - previous;
    }

        internal static double PerSecond(long bytes, double seconds)

        => seconds <= 0.05 ? 0 : bytes / seconds;

        internal static double Smooth(double previous, double sample, double alpha = 0.35)
    {

        alpha = Math.Clamp(alpha, 0.01, 1.0);
        if (previous <= 0) return sample;
        return previous + (sample - previous) * alpha;
    }

        internal static (bool On, double QuietFor) Latch(bool on, double rate, double quietFor, double dt)
        => Latch(on, rate, quietFor, dt, OnBytesPerSec);

    internal static (bool On, double QuietFor) Latch(bool on, double rate, double quietFor, double dt,
                                                     long onAt)
    {
        if (rate >= onAt) return (true, 0);
        if (!on) return (false, 0);
        quietFor += dt;
        return quietFor >= OffHoldSeconds ? (false, 0) : (true, quietFor);
    }

        internal static string Format(double bytesPerSec)
    {

        if (bytesPerSec <= 0) return "0 KB/s";
        double kb = bytesPerSec / 1024.0;
        if (kb < 1024) return $"{Math.Max(1, Math.Round(kb)):0} KB/s";
        double mb = kb / 1024.0;
        if (mb < 100) return $"{mb:0.0} MB/s";
        return $"{mb:0} MB/s";
    }

    internal static string LinkSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0) return "";
        double mbit = bitsPerSecond / 1_000_000.0;
        if (mbit < 1000) return $"{mbit:0} Mb/s";
        double gbit = mbit / 1000.0;
        return gbit < 10 ? $"{gbit:0.0} Gb/s" : $"{gbit:0} Gb/s";
    }

        internal static string Size(long bytes)
    {
        if (bytes < 0) bytes = 0;
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return mb < 100 ? $"{mb:0.0} MB" : $"{mb:0} MB";
        double gb = mb / 1024.0;
        return gb < 100 ? $"{gb:0.00} GB" : $"{gb:0} GB";
    }
}

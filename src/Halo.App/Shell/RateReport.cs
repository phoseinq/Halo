using System;
using System.IO;

namespace Halo.Shell;

internal struct MorphRate
{

    internal const int MinFrames = 8;
    internal const double MinSeconds = 0.06;

    private int _frames;
    private double _seconds;

    internal int Measured { get; private set; }

    internal bool Step(bool morphing, double dt)
    {
        if (morphing)
        {
            _frames++;
            _seconds += dt;
            return false;
        }
        if (_frames == 0) return false;
        int frames = _frames;
        double seconds = _seconds;
        _frames = 0;
        _seconds = 0;
        if (frames < MinFrames || seconds < MinSeconds) return false;
        int fps = (int)Math.Round(frames / seconds);
        if (fps == Measured) return false;
        Measured = fps;
        return true;
    }
}

internal struct SteadyRate
{
    private int _frames;
    private double _seconds;

    internal int Measured { get; private set; }

    internal const double WindowSeconds = 1.0;

    internal bool Step(bool on, double dt)
    {
        if (!on)
        {
            _frames = 0;
            _seconds = 0;
            if (Measured == 0) return false;
            Measured = 0;
            return true;
        }
        _frames++;
        _seconds += dt;
        if (_seconds < WindowSeconds) return false;
        int fps = (int)Math.Round(_frames / _seconds);
        _frames = 0;
        _seconds = 0;
        if (fps == Measured) return false;
        Measured = fps;
        return true;
    }
}

internal static class RateReport
{

    internal static string Format(int measured, int hz, int settled) => $"{measured} {hz} {settled}";

    private static string _written = "";

    internal static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "fps");

    internal static void Write(int measured, int hz, int settled)
    {
        string line = Format(measured, hz, settled);
        if (line == _written) return;
        _written = line;
        try
        {
            string path = Path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, line);
        }
        catch { }
    }
}

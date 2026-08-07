using System;
using System.Globalization;
using System.IO;

namespace Halo.Shell;

internal enum GreetingKind
{
    None,
    Install,
    Login,
}

internal readonly record struct GreetingMark(string Version, DateOnly? Last);

internal static class GreetingArm
{
    internal const float SettleSeconds = 1.2f;
    internal const float GiveUpSeconds = 45f;

    internal static (float held, float waited, bool armed) Step(bool watchable, float held, float waited, float dt)
    {
        waited += dt;
        held = watchable ? held + dt : 0f;
        return (held, waited, held >= SettleSeconds || waited >= GiveUpSeconds);
    }
}

internal static class GreetingGate
{
    internal static GreetingKind Decide(GreetingMark mark, string version, DateOnly today, bool enabled)
    {
        if (!enabled) return GreetingKind.None;
        if (!string.Equals(mark.Version, version, StringComparison.Ordinal)) return GreetingKind.Install;
        return mark.Last == today ? GreetingKind.None : GreetingKind.Login;
    }

    internal static string Version =>
        typeof(GreetingGate).Assembly.GetName().Version?.ToString() ?? "0";

    internal static GreetingMark Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new GreetingMark("", null);
        var lines = text.Split('\n');
        DateOnly? last = lines.Length > 1
            && DateOnly.TryParseExact(lines[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day)
            ? day : null;
        return new GreetingMark(lines[0].Trim(), last);
    }

    internal static string Format(GreetingMark mark) => mark.Last is { } day
        ? mark.Version + "\n" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : mark.Version;

    internal static GreetingMark Read(string path)
    {
        try { return Parse(File.Exists(path) ? File.ReadAllText(path) : null); }
        catch { return new GreetingMark("", null); }
    }

    internal static void Write(string path, GreetingMark mark)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Format(mark));
        }
        catch { }
    }

    internal static GreetingKind Take(string path, DateOnly today, bool enabled)
    {
        var mark = Read(path);
        var kind = Decide(mark, Version, today, enabled);
        Write(path, new GreetingMark(Version, kind == GreetingKind.None ? mark.Last : today));
        return kind;
    }
}

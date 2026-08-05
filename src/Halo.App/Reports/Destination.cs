using System;

namespace Halo.Reports;

internal static class Destination
{
    internal const string EndpointKey = "report.endpoint";
    internal const string KeyKey = "report.key";

    internal enum Kind
    {
        BuiltIn,
        Custom,
        Off,
    }

    internal static Kind Decide(string? rawEndpoint, string builtIn)
    {
        if (rawEndpoint is null) return Kind.BuiltIn;
        string trimmed = rawEndpoint.Trim();
        if (trimmed.Length == 0) return Kind.Off;
        return SameEndpoint(trimmed, builtIn) ? Kind.BuiltIn : Kind.Custom;
    }

    internal static string? Key(Kind kind, string builtIn, string? rawKey)
    {
        if (kind != Kind.Custom) return kind == Kind.BuiltIn ? builtIn : null;
        string trimmed = rawKey?.Trim() ?? "";

        if (trimmed.Length == 0 || trimmed.StartsWith("halo1.", StringComparison.Ordinal)) return null;
        return trimmed;
    }

    private static bool SameEndpoint(string a, string b)
    {
        try
        {
            if (!Uri.TryCreate(a, UriKind.Absolute, out var ua)) return false;
            if (!Uri.TryCreate(b.Trim(), UriKind.Absolute, out var ub)) return false;
            return ua.Host.Equals(ub.Host, StringComparison.OrdinalIgnoreCase) && ua.Port == ub.Port
                && ua.AbsolutePath.TrimEnd('/').Equals(ub.AbsolutePath.TrimEnd('/'),
                                                       StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

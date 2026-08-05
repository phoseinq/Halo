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
        return string.Equals(trimmed, builtIn.Trim(), StringComparison.OrdinalIgnoreCase)
            ? Kind.BuiltIn
            : Kind.Custom;
    }

    internal static string? Key(Kind kind, string builtIn, string? rawKey) => kind switch
    {
        Kind.BuiltIn => builtIn,
        Kind.Custom => string.IsNullOrWhiteSpace(rawKey) ? null : rawKey.Trim(),
        _ => null,
    };
}

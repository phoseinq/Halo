using System;

namespace Halo.Settings;

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

        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (trimmed.StartsWith("halo1.", StringComparison.Ordinal)) return null;
        if (string.Equals(trimmed, builtIn.Trim(), StringComparison.Ordinal)) return null;
        return trimmed;
    }

    private static bool SameEndpoint(string a, string b)
    {
        try
        {
            if (!Uri.TryCreate(a, UriKind.Absolute, out var ua)) return false;
            if (!Uri.TryCreate(b.Trim(), UriKind.Absolute, out var ub)) return false;
            return ua.Scheme.Equals(ub.Scheme, StringComparison.OrdinalIgnoreCase)
                && ua.Host.Equals(ub.Host, StringComparison.OrdinalIgnoreCase) && ua.Port == ub.Port
                && ua.AbsolutePath.TrimEnd('/').Equals(ub.AbsolutePath.TrimEnd('/'),
                                                       StringComparison.OrdinalIgnoreCase)

                && string.Equals(ua.Query, ub.Query, StringComparison.Ordinal)
                && string.Equals(ua.UserInfo, ub.UserInfo, StringComparison.Ordinal);
        }
        catch { return false; }
    }

    internal readonly record struct Route(Kind Kind, string Target, string? Bearer, string? Error);

    internal static Route Resolve(string? rawEndpoint, string? rawKey, string builtInEndpoint,
        string builtInKey)
    {
        var kind = Decide(rawEndpoint, builtInEndpoint);
        if (kind == Kind.Off)
            return new Route(kind, "", null, Halo.Localization.Strings.Get("reports.off"));

        string target = kind == Kind.Custom ? rawEndpoint!.Trim() : builtInEndpoint;
        string? bearer = Key(kind, builtInKey, rawKey);
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new Route(kind, target, bearer, Halo.Localization.Strings.Get(
                kind == Kind.Custom ? "reports.customNotHttps" : "reports.builtInNotHttps"));

        return new Route(kind, target, bearer, null);
    }
}

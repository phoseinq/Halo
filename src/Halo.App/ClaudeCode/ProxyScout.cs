using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Halo.ClaudeCode;

internal static class ProxyScout
{
        public static volatile string? Found;

    internal static readonly int[] Ports =
        { 10809, 2080, 7890, 7897, 10808, 1080, 2081, 7891, 1081, 12334, 8086, 8080 };

    private static readonly HashSet<int> Socks = new() { 10808, 1080, 1081, 7891 };

        internal static string[] UrlsFor(int port) =>
        Socks.Contains(port)
            ? new[] { $"socks5://127.0.0.1:{port}", $"http://127.0.0.1:{port}" }
            : new[] { $"http://127.0.0.1:{port}", $"socks5://127.0.0.1:{port}" };

        internal static string? Pick(IEnumerable<int> ports, Func<int, bool> isOpen, Func<string, bool> works)
    {
        foreach (int port in ports)
        {

            if (!isOpen(port)) continue;
            foreach (var url in UrlsFor(port))
                if (works(url)) return url;
        }
        return null;
    }

        internal static bool IsOpen(int port)
    {
        try
        {
            using var c = new System.Net.Sockets.TcpClient();

            return c.ConnectAsync(System.Net.IPAddress.Loopback, port).Wait(120);
        }
        catch { return false; }
    }

    internal static string? FromSettings(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("env", out var env)
                || env.ValueKind != JsonValueKind.Object) return null;
            foreach (var key in new[] { "HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy",
                                        "ALL_PROXY", "all_proxy" })
                if (env.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var s = v.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s!.Trim();
                }
        }
        catch { }
        return null;
    }

    private static string? _settingsProxy;
    private static DateTime _settingsAt = DateTime.MinValue;

        internal static string? FromSettingsFile()
    {
        if (DateTime.UtcNow - _settingsAt < TimeSpan.FromMinutes(1)) return _settingsProxy;
        _settingsAt = DateTime.UtcNow;
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
            _settingsProxy = System.IO.File.Exists(path) ? FromSettings(System.IO.File.ReadAllText(path)) : null;
        }
        catch { _settingsProxy = null; }
        return _settingsProxy;
    }
}

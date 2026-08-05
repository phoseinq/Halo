using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Halo.Reports;

internal sealed record ReportFacts(
    string Kind,
    string At,
    string HaloVersion,
    string WindowsBuild,
    string Display,
    int Dpi,
    string Runtime,
    string Locale,
    int Cpus,
    int? RamMb,
    int? UptimeMin,
    string Primary,
    IReadOnlyList<string> Live,
    bool Expanded,
    bool Heavy,
    int Tier,
    string? ExceptionType,
    string? ExceptionMessage,
    IReadOnlyList<string> Stack,
    IReadOnlyList<string> Inner,
    string Description);

internal static class ReportPayload
{

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    internal static string Json(ReportFacts f)
    {
        var root = new JsonObject
        {
            ["kind"] = f.Kind,
            ["at"] = f.At,
            ["halo"] = f.HaloVersion,
            ["windows"] = f.WindowsBuild,
            ["display"] = f.Display,
            ["dpi"] = f.Dpi,
            ["runtime"] = f.Runtime,

            ["locale"] = f.Locale,

            ["machine"] = new JsonObject
            {
                ["cpus"] = f.Cpus,

                ["ram_mb"] = f.RamMb is int r ? JsonValue.Create(r) : null,
            },

            ["uptime_min"] = f.UptimeMin is int u ? JsonValue.Create(u) : null,

            ["surface"] = new JsonObject
            {
                ["primary"] = f.Primary,
                ["live"] = new JsonArray(ArrayOf(f.Live)),
                ["expanded"] = f.Expanded,
                ["heavy"] = f.Heavy,
                ["tier"] = f.Tier,
            },
        };
        if (f.ExceptionType is { Length: > 0 })
        {
            var block = new JsonObject
            {
                ["type"] = f.ExceptionType,
                ["message"] = f.ExceptionMessage ?? "",
                ["stack"] = new JsonArray(ArrayOf(f.Stack)),
            };

            if (f.Inner.Count > 0) block["inner"] = new JsonArray(ArrayOf(f.Inner));
            root["exception"] = block;
        }

        root["description"] = f.Description;
        return root.ToJsonString(Pretty);
    }

    private static JsonNode?[] ArrayOf(IReadOnlyList<string> items)
    {
        var nodes = new JsonNode?[items.Count];
        for (int i = 0; i < items.Count; i++) nodes[i] = JsonValue.Create(items[i]);
        return nodes;
    }

    internal static IReadOnlyList<string> StackLines(Exception? ex)
    {
        if (ex?.StackTrace is not { Length: > 0 } stack) return [];
        var lines = stack.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var scrubbed = new List<string>(lines.Length);
        foreach (var line in lines) scrubbed.Add(Scrub.All(line.Trim('\r', ' ')));
        return scrubbed;
    }

    internal static ReportFacts Collect(string kind, Exception? ex, string description)
    {
        string at = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",
            System.Globalization.CultureInfo.InvariantCulture);
        var shape = ShapeReport.Read();
        string[] live = shape.TryGetValue("live", out var l) && l.Length > 0
            ? l.Split(',', StringSplitOptions.RemoveEmptyEntries) : [];
        return new ReportFacts(
            Kind: kind,
            At: at,
            HaloVersion: Version,
            WindowsBuild: WindowsBuild,
            Display: Display,
            Dpi: Dpi,
            Runtime: Runtime,
            Locale: Locale,
            Cpus: Cpus,
            RamMb: RamMb,
            UptimeMin: kind == "manual" ? null : UptimeMin,
            Primary: shape.TryGetValue("primary", out var p) ? p : "unknown",
            Live: live,
            Expanded: shape.TryGetValue("expanded", out var e) && e == "1",
            Heavy: shape.TryGetValue("heavy", out var h) && h == "1",
            Tier: shape.TryGetValue("tier", out var t) && int.TryParse(t, out var tv) ? tv : 0,
            ExceptionType: ex?.GetType().FullName,

            ExceptionMessage: ex is null ? null : Scrub.All(ex.Message),
            Stack: StackLines(ex),
            Inner: InnerChain(ex),
            Description: description);
    }

    internal static IReadOnlyList<string> InnerChain(Exception? ex)
    {
        var chain = new List<string>();
        try
        {
            var inner = ex?.InnerException;
            while (inner is not null && chain.Count < 5)
            {
                chain.Add(inner.GetType().FullName + ": " + Scrub.All(inner.Message));
                inner = inner.InnerException;
            }
        }
        catch { }
        return chain;
    }

    private static string Locale
    {
        get
        {
            try { return System.Globalization.CultureInfo.CurrentUICulture.Name; }
            catch { return "unknown"; }
        }
    }

    private static int Cpus
    {
        get
        {
            try { return Environment.ProcessorCount; }
            catch { return 0; }
        }
    }

    private static int? RamMb
    {
        get
        {

            try
            {
                long bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                return bytes > 0 ? (int)(bytes / (1024 * 1024)) : null;
            }
            catch { return null; }
        }
    }

    private static int? UptimeMin
    {
        get
        {
            try
            {
                using var self = System.Diagnostics.Process.GetCurrentProcess();
                return (int)(DateTime.Now - self.StartTime).TotalMinutes;
            }
            catch { return null; }
        }
    }

    private static string Version
    {
        get
        {
            try { return typeof(ReportPayload).Assembly.GetName().Version?.ToString(4) ?? "unknown"; }
            catch { return "unknown"; }
        }
    }

    private static string Runtime
    {
        get
        {
            try { return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription; }
            catch { return "unknown"; }
        }
    }

    private static string WindowsBuild
    {
        get
        {
            try { return Environment.OSVersion.Version.ToString(); }
            catch { return "unknown"; }
        }
    }

    private static string Display
    {
        get
        {
            try
            {
                int w = Halo.Interop.Win32.GetSystemMetrics(Halo.Interop.Win32.SM_CXSCREEN);
                int h = Halo.Interop.Win32.GetSystemMetrics(Halo.Interop.Win32.SM_CYSCREEN);
                int hz = 0;
                try
                {
                    var parts = System.IO.File.ReadAllText(Halo.Shell.RateReport.Path)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1) int.TryParse(parts[1], out hz);
                }
                catch { }
                return hz > 0 ? $"{w}x{h} @ {hz} Hz" : $"{w}x{h}";
            }
            catch { return "unknown"; }
        }
    }

    private static int Dpi
    {
        get
        {
            try { return Halo.Interop.Win32.GetDpiForSystem(); }
            catch { return 0; }
        }
    }
}

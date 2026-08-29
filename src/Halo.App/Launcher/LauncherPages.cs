using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Launcher;

internal static class LauncherPages
{
    internal const string ActMute = "act:mute";
    internal const string ActLock = "act:lock";
    internal const string ActSleep = "act:sleep";
    internal const string ActCopyTranslation = "act:copytr";

    private const char Sep = '\\';

    private const string GlyphCopy = "\uE8C8";
    private const string GlyphWindows = "\uE770";
    private const string GlyphCpu = "\uE9D9";
    private const string GlyphClock = "\uE823";
    private const string GlyphNet = "\uE839";
    private const string GlyphPc = "\uE977";
    private const string GlyphGpu = "\uF211";
    private const string GlyphBolt = "\uE945";

    private const string GpuClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";

    internal static IReadOnlyList<LauncherRow> For(string page, string query, Func<bool>? muted = null)
        => page switch
        {
            LauncherState.PageSystem => System(),
            LauncherState.PageQuick => Quick(muted),
            LauncherState.PageClipboard => Clipboard(),
            LauncherState.PageReminders => Reminders(query),
            LauncherState.PageTranslate => Translate(),
            _ => [],
        };

    private static LauncherRow Info(string label, string detail)
        => new(label, null, false, LauncherRowKind.Info, null, detail);

    private static LauncherRow Action(string id, string label, string detail, string glyph)
        => new(label, null, true, LauncherRowKind.Action, id, detail, glyph);

    internal readonly record struct Ring(float Frac, string Detail);

    internal readonly record struct Gauge(string Label, float Frac, string Detail, bool Inverted = false,
                                          Ring[]? Parts = null, string? Badge = null,
                                          System.Drawing.Color? Tint = null);

    internal static IReadOnlyList<Gauge> SystemGauges()
    {
        var g = new List<Gauge>();

        float cpu = Halo.Interop.CpuLoad.Last;
        if (cpu >= 0f)
            g.Add(new Gauge("cpu", cpu, (int)Math.Round(cpu * 100) + "% busy - "
                + Environment.ProcessorCount + " threads", Tint: Halo.Widgets.Fx.VitalCpu));

        Halo.Interop.GpuLoad.Refresh();
        float gpu = Halo.Interop.GpuLoad.Last;
        if (gpu >= 0f)
            g.Add(new Gauge("gpu", gpu, (int)Math.Round(gpu * 100) + "% on the busiest engine",
                            Tint: Halo.Widgets.Fx.VitalGpu));

        try
        {
            var m = new Win32.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Win32.MEMORYSTATUSEX>() };
            if (Win32.GlobalMemoryStatusEx(ref m) && m.ullTotalPhys > 0)
            {
                double totalGb = m.ullTotalPhys / 1024d / 1024d / 1024d;
                double usedGb = (m.ullTotalPhys - m.ullAvailPhys) / 1024d / 1024d / 1024d;
                g.Add(new Gauge("memory", (float)(usedGb / totalGb),
                    $"{usedGb:0.0} of {totalGb:0.0} GB used, {totalGb - usedGb:0.0} free",
                    Tint: Halo.Widgets.Fx.VitalMemory));
            }
        }
        catch { }

        try
        {
            double free = 0, size = 0;
            var parts = new List<Ring>();
            var says = new List<string>();
            foreach (var d in global::System.IO.DriveInfo.GetDrives())
            {
                if (!d.IsReady || d.DriveType != global::System.IO.DriveType.Fixed) continue;
                if (d.TotalSize <= 0) continue;
                double f = d.AvailableFreeSpace / 1024d / 1024d / 1024d;
                double t = d.TotalSize / 1024d / 1024d / 1024d;
                free += f; size += t;
                string letter = d.Name.TrimEnd(Sep, ':').ToLowerInvariant();
                parts.Add(new Ring((float)((t - f) / t), $"{letter}: {f:0} GB free of {t:0}"));
                says.Add(letter + " " + f.ToString("0"));
            }
            if (size > 0)
                g.Add(new Gauge("storage", (float)((size - free) / size),
                    $"{free:0} GB free of {size:0} - " + string.Join(", ", says) + " GB free",
                    Parts: [.. parts], Tint: Halo.Widgets.Fx.VitalStorage));
        }
        catch { }

        try
        {
            if (Win32.GetSystemPowerStatus(out var p) && p.BatteryFlag != 128 && p.BatteryLifePercent <= 100)
            {

                bool charging = (p.BatteryFlag & 8) != 0 || p.ACLineStatus == 1;
                string say = charging
                    ? p.BatteryLifePercent + "% and charging - plugged in"
                    : p.BatteryLifePercent + "% - " + Remaining(p.BatteryLifeTime);

                g.Add(new Gauge("battery", p.BatteryLifePercent / 100f, say, Inverted: true,
                                Badge: charging ? GlyphBolt : null, Tint: Halo.Widgets.Fx.VitalBattery));
            }
        }
        catch { }

        return g;
    }

    private static string Remaining(int seconds)
    {
        if (seconds <= 0) return "on battery";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"about {(int)t.TotalHours}h {t.Minutes}m left"
            : $"about {t.Minutes}m left";
    }

    private static IReadOnlyList<LauncherRow> System()
    {
        var rows = new List<LauncherRow>();

        try
        {
            string? name = Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName");
            string? rel = Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
            int build = Environment.OSVersion.Version.Build;
            if (name is { Length: > 0 } && build >= 22000) name = name.Replace("Windows 10", "Windows 11");
            rows.Add(new LauncherRow(name ?? "Windows", null, false, LauncherRowKind.Info, null,
                (rel is { Length: > 0 } ? rel + " - " : "") + "build " + build, GlyphWindows,
                Tint: Halo.Widgets.Fx.VitalOs));
        }
        catch { }

        try
        {
            string? cpu = Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");

            string shown = (cpu ?? "").Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "").Trim();
            int at = shown.IndexOf(" @", StringComparison.Ordinal);
            if (at > 0) shown = shown[..at];
            rows.Add(new LauncherRow(shown.Length > 0 ? shown : "Processor", null, false, LauncherRowKind.Info,
                null, "", GlyphCpu, Tint: Halo.Widgets.Fx.VitalCpu));
        }
        catch { }

        try
        {

            string? card = null;
            for (int i = 0; i < 8; i++)
            {
                string? d = Reg(GpuClassKey + i.ToString("0000"), "DriverDesc");
                if (d is { Length: > 0 }) card = d;
            }
            if (card is { Length: > 0 })
                rows.Add(new LauncherRow(card, null, false, LauncherRowKind.Info, null, "", GlyphGpu,
                    Tint: Halo.Widgets.Fx.VitalGpu));
        }
        catch { }

        try
        {
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            rows.Add(new LauncherRow("Uptime", null, false, LauncherRowKind.Info, null,
                up.TotalDays >= 1 ? $"{(int)up.TotalDays}d {up.Hours}h {up.Minutes}m" : $"{up.Hours}h {up.Minutes}m",
                GlyphClock, Tint: Halo.Widgets.Fx.VitalStorage));
        }
        catch { }

        try
        {
            foreach (var nic in global::System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != global::System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == global::System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                var props = nic.GetIPProperties();

                bool routes = false;
                foreach (var gw in props.GatewayAddresses)
                    if (gw.Address is { } a && !a.Equals(global::System.Net.IPAddress.Any)) { routes = true; break; }
                if (!routes) continue;

                foreach (var ip in props.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != global::System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    rows.Add(new LauncherRow(ip.Address.ToString(), null, false, LauncherRowKind.Info, null,
                        Trim(nic.Name, 22), GlyphNet, Tint: Halo.Widgets.Fx.VitalMemory));
                    break;
                }
                break;
            }
        }
        catch { }

        try
        {
            rows.Add(new LauncherRow(Environment.MachineName, null, false, LauncherRowKind.Info, null,
                Environment.UserName, GlyphPc, Tint: Halo.Widgets.Fx.VitalBattery));
        }
        catch { }

        if (rows.Count == 0) rows.Add(new LauncherRow("nothing readable here", null, false, LauncherRowKind.Notice));
        return rows;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    private static string? Reg(string path, string name)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(name) as string;
        }
        catch { return null; }
    }

    private static IReadOnlyList<LauncherRow> Quick(Func<bool>? muted)
    {
        var rows = new List<LauncherRow>();
        bool? m = null;
        try { m = muted?.Invoke(); } catch { }

        foreach (var b in QuickActions.Enabled(id =>
                     Halo.Settings.SettingsStore.On(QuickActions.EnabledKey(id), QuickActions.DefaultOn(id))))
        {

            if (b.Id == QuickActions.IdMute)
            {
                rows.Add(Action(ActMute, m is null || !m.Value ? "Mute" : "Unmute",
                    m is null ? b.Detail : m.Value ? "currently muted" : "currently not muted", b.Glyph));
                continue;
            }
            rows.Add(Action(QuickActions.Prefix + b.Id, b.Label, b.Detail, b.Glyph));
        }

        for (int slot = 1; slot <= QuickActions.CustomSlots; slot++)
        {
            string raw = Halo.Settings.SettingsStore.Shared?.Current.Text(QuickActions.CustomKey(slot), "") ?? "";
            if (QuickActions.ParseCustom(raw) is not { } c) continue;
            rows.Add(Action(QuickActions.CustomPrefix + slot.ToString(
                         global::System.Globalization.CultureInfo.InvariantCulture),
                     c.Label, c.Target, "\uE8A7"));
        }

        if (rows.Count == 0)
            rows.Add(new LauncherRow("every action is switched off - see Settings", null, false,
                                     LauncherRowKind.Notice));
        return rows;
    }

    private static IReadOnlyList<LauncherRow> Clipboard()
    {
        try
        {
            var items = ClipboardHistory.Read();
            if (items is null)
                return [new LauncherRow("clipboard history is off in Windows settings", null, false,
                                        LauncherRowKind.Notice)];
            if (items.Count == 0)
                return [new LauncherRow("nothing copied yet", null, false, LauncherRowKind.Notice)];

            var rows = new List<LauncherRow>();
            for (int i = 0; i < items.Count; i++)
                rows.Add(new LauncherRow(items[i].Preview, null, true, LauncherRowKind.Action,
                                         ClipboardHistory.ActPrefix + items[i].Id, "copy back", GlyphCopy));
            return rows;
        }
        catch
        {
            return [new LauncherRow("clipboard history could not be read", null, false, LauncherRowKind.Notice)];
        }
    }

    private static string? _lastSource, _lastResult;
    private static bool _translating;

    internal static void SetTranslation(string? source, string? result, bool busy)
    {
        _lastSource = source; _lastResult = result; _translating = busy;

        if (!string.IsNullOrWhiteSpace(source))
            _detected = Halo.Widgets.Fx.IsRtl(source!) ? "fa" : "en";
    }

    private static string? _detected;

        internal static string DetectedSource() => _detected ?? "";

    internal static void SwapTexts()
    {
        if (_lastResult is not { Length: > 0 } answer) return;
        (_lastSource, _lastResult) = (answer, _lastSource);
        _detected = Halo.Widgets.Fx.IsRtl(answer) ? "fa" : "en";
    }

    internal static void CopyTranslation()
    {
        if (_lastResult is not { Length: > 0 } text) return;
        try
        {
            var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
        }
        catch { }
    }

    private static IReadOnlyList<LauncherRow> Translate()
    {
        var rows = new List<LauncherRow>();
        if (_translating)
            rows.Add(new LauncherRow("translating...", null, false, LauncherRowKind.Notice));
        else if (_lastResult is { Length: > 0 })
            rows.Add(new LauncherRow(_lastResult, null, true, LauncherRowKind.Action,
                                     ActCopyTranslation, "copy", GlyphCopy));
        else if (_lastSource is { Length: > 0 })
            rows.Add(new LauncherRow("no translation came back - offline, or the service said no",
                                     null, false, LauncherRowKind.Notice));

        rows.Add(new LauncherRow("sent to " + Translator.Service + " to be translated",
                                 null, false, LauncherRowKind.Notice));
        return rows;
    }

    internal const string ActSwapLangs = "act:langswap";
    internal const string LangPrefix = "lang:";

    internal static string SourceLang()
        => Halo.Settings.SettingsStore.Shared?.Current.Text(Translator.SourceKey, Translator.Auto) ?? Translator.Auto;

    internal static string TargetLang()
        => Halo.Settings.SettingsStore.Shared?.Current.Text(Translator.TargetKey, "") ?? "";

    internal static string EffectiveSource()
    {
        string stored = SourceLang();
        if (!Translator.IsAuto(stored)) return stored;
        string seen = DetectedSource();
        return seen.Length > 0 ? seen : "en";
    }

    internal static string EffectiveTarget()
    {
        string stored = TargetLang();
        if (stored.Length > 0) return stored;

        return EffectiveSource() == "fa" ? "en" : "fa";
    }

    internal static IReadOnlyList<LauncherRow> LanguageRows(bool forSource)
    {
        var rows = new List<LauncherRow>(Translator.Languages.Length + 1)
        {
            forSource
                ? new LauncherRow("Detect language", null, true, LauncherRowKind.Action,
                                  LangPrefix + Translator.Auto, "work it out from the text",
                                  "\uE721", Halo.Widgets.Fx.VitalOs)
                : new LauncherRow("Automatic", null, true, LauncherRowKind.Action,
                                  LangPrefix, "Persian in, English out - and back",
                                  "\uE721", Halo.Widgets.Fx.VitalOs),
        };
        foreach (var l in Translator.Languages)
            rows.Add(new LauncherRow(l.Name, null, true, LauncherRowKind.Action,
                                     LangPrefix + l.Code, l.Code, "\uE774"));
        return rows;
    }

    internal const string AddPrefix = "remadd:";

    private static IReadOnlyList<LauncherRow> Reminders(string query)
    {
        try
        {
            var now = DateTimeOffset.Now;
            string typed = (query ?? "").Trim();
            if (typed.Length > 0)
            {
                if (ReminderStore.ParseCommand(typed, now, out _) is { } cmd)
                    return
                    [
                        new LauncherRow(cmd.Text, null, true, LauncherRowKind.Action,
                                        AddPrefix + cmd.When.ToUnixTimeSeconds().ToString(
                                            global::System.Globalization.CultureInfo.InvariantCulture),
                                        ReminderStore.Describe(new Reminder("", cmd.When, cmd.Text), now),
                                        "\uE823", Halo.Widgets.Fx.VitalBattery),
                    ];

                var menu = new List<LauncherRow>();
                foreach (var (label, when) in ReminderStore.Choices(now))
                    menu.Add(new LauncherRow(label, null, true, LauncherRowKind.Action,
                                             AddPrefix + when.ToUnixTimeSeconds().ToString(
                                                 global::System.Globalization.CultureInfo.InvariantCulture),
                                             when.ToLocalTime().ToString("ddd HH:mm",
                                                 global::System.Globalization.CultureInfo.InvariantCulture),
                                             "\uE823", Halo.Widgets.Fx.VitalBattery));
                return menu;
            }

            var due = ReminderStore.Pending(ReminderStore.Load(), now);
            if (due.Count == 0)
                return [new LauncherRow("nothing pending - type what to be reminded of", null, false,
                                        LauncherRowKind.Notice)];
            var rows = new List<LauncherRow>();
            foreach (var r in due)

                rows.Add(new LauncherRow(r.Text, null, true, LauncherRowKind.Tick,
                                         ReminderStore.ActPrefix + r.Id,
                                         ReminderStore.Describe(r, now), null,
                                         Halo.Widgets.Fx.VitalBattery));
            return rows;
        }
        catch
        {
            return [new LauncherRow("reminders could not be read", null, false, LauncherRowKind.Notice)];
        }
    }
}

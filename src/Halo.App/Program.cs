using System;
using System.Linq;
using Halo.Interop;
using Halo.Shell;
using Halo.Widgets;

namespace Halo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {

        if (Environment.GetEnvironmentVariable("HALO_LANG") is { Length: > 0 } forcedLang)
            Halo.Localization.Strings.Use(Halo.Localization.Strings.Name(forcedLang));

        if (args.Length >= 1 && args[0] == "--banner-apply")
        {
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                string? line;
                while ((line = Console.In.ReadLine()) is not null) lines.Add(line);
                Console.WriteLine(Halo.Notifications.BannerApply.Apply(Halo.Notifications.BannerBatch.Parse(lines)));
            }
            catch (Exception e) { Console.Error.WriteLine("banner-apply failed: " + e.Message); }
            return;
        }
        if (args.Length >= 1 && args[0] == "--restore-notifications") { Halo.Notifications.BannerGate.Uninstall(); return; }

        if (args.Length >= 2 && args[0] == "--report-new") { NewReport(args[1]); return; }

        if (args.Length >= 1 && args[0] == "--report-clear") { Halo.Reports.ReportStore.Clear(); return; }

        if (args.Length >= 2 && args[0] == "--render-widget")
        {
            RenderWidget(args[1], args.Length > 2 ? args[2] : "media",
                args.Length > 3 && int.TryParse(args[3], out int sc) ? sc : 1, args);
            return;
        }

        if (args.Length >= 2 && args[0] == "--render-pill") { RenderPill(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-store")
        {
            string? pick = Array.Find(args, a => a.StartsWith("only=", StringComparison.Ordinal));
            RenderStore(args[1], Array.Exists(args, a => a == "live"),
                pick?[5..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return;
        }

        if (args.Length >= 3 && args[0] == "--render-storelogos")
        {
            RenderStoreLogos(args[1], args[2]);
            return;
        }

        if (args.Length >= 2 && args[0] == "--render-bar") { RenderBar(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-morph")
        { RenderMorph(args[1], args.Length >= 3 ? args[2] : "media"); return; }

        if (args.Length >= 1 && args[0] == "--probe-display") { ProbeDisplay(); return; }

        if (args.Length >= 1 && args[0] == "--probe-frame")
        {
            ProbeFrame(args.Length > 1 && int.TryParse(args[1], out var pfw) ? pfw : 560,
                       args.Length > 2 && int.TryParse(args[2], out var pfh) ? pfh : 420);
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-almanac")
        {
            Console.WriteLine($"zone     {TimeZoneInfo.Local.Id}");
            Console.WriteLine($"place    {Almanac.Place ?? "(none - offset-only zone)"}");
            Almanac.Poke();
            for (int i = 0; i < 60 && Almanac.Latest is null; i++) System.Threading.Thread.Sleep(500);
            Console.WriteLine($"weather  {(Almanac.Latest is { } wx ? $"{wx.TempC}C code {wx.Code} = {Almanac.Sky(wx.Code)}" : "(no reading)")}");
            Console.WriteLine($"country  {Almanac.PlaceCountry ?? "(not geocoded)"}   metric {Almanac.Metric}   calendar {Almanac.Calendar}");
            Console.WriteLine($"source   {(Almanac.FromDevice ? "windows location" : "time zone")}");
            Console.WriteLine($"label    {Almanac.Label}");
            Console.WriteLine($"title    {Almanac.Headline(DateTime.Now)}");
            Console.WriteLine($"body     {Almanac.Detail(DateTime.Now)}");
            return;
        }

        if (args.Length >= 2 && args[0] == "--render-cue") { RenderCue(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-pin") { RenderPin(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-marquee") { RenderMarquee(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-haloask") { RenderHaloAsk(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-vol") { RenderVol(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-bt") { RenderBt(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-notif") { RenderNotif(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-badges") { RenderBadges(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-ask") { RenderAsk(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-greeting") { RenderGreeting(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-local") { RenderLocal(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-copy") { RenderCopy(args[1]); return; }

        if (args.Length >= 2 && args[0] == "--render-glyphs") { RenderGlyphs(args[1]); return; }

        if (args.Length >= 3 && args[0] == "--probe-console")
        {
            int.TryParse(args[1], out int cpid);
            int cbelow = args.Length > 3 && int.TryParse(args[3], out var cb) ? cb : 0;
            System.IO.File.WriteAllText(args[2], Halo.Interop.ConsoleRead.Describe(cpid, 16, cbelow));
            return;
        }

        if (args.Length >= 4 && args[0] == "--probe-type")
        {
            int.TryParse(args[1], out int tpid);

            bool sent = args[2] switch
            {
                "enter" => Halo.Interop.ConsoleRead.Press(tpid, Halo.Interop.ConsoleRead.VkEnter),
                "tab" => Halo.Interop.ConsoleRead.Press(tpid, Halo.Interop.ConsoleRead.VkTab),
                var s when s.StartsWith("down:") && int.TryParse(s[5..], out var n)
                    => Halo.Interop.ConsoleRead.Press(tpid, Halo.Interop.ConsoleRead.VkDown, n),
                var s when s.StartsWith("up:") && int.TryParse(s[3..], out var n)
                    => Halo.Interop.ConsoleRead.Press(tpid, Halo.Interop.ConsoleRead.VkUp, n),
                _ => Halo.Interop.ConsoleRead.Type(tpid, args[2]),
            };
            System.Threading.Thread.Sleep(400);
            System.IO.File.WriteAllText(args[3], $"sent={sent}\n" + Halo.Interop.ConsoleRead.Dump(tpid, 10));
            return;
        }
        if (args.Length >= 2 && args[0] == "--render-fluent")
        { RenderFluent(args[1], args.Length > 2 ? args[2] : "E700", args.Length > 3 ? args[3] : "256"); return; }

        if (args.Length >= 2 && args[0] == "--render-bar")
        { RenderBar(args[1], args.Length > 2 ? args[2] : null, args.Length > 3 ? args[3] : null); return; }

        if (args.Length >= 1 && args[0] == "--probe-media") { ProbeMedia(); return; }

        if (args.Length >= 1 && args[0] == "--probe-tg")
        {
            for (int i = 0; i < 8; i++)
            {
                Halo.Widgets.TelegramPlayer.Poke();
                System.Threading.Thread.Sleep(1000);
                var (tpos, tdur) = Halo.Widgets.TelegramPlayer.Read();
                Console.WriteLine($"{i}s live={Halo.Widgets.TelegramPlayer.Live} video={Halo.Widgets.TelegramPlayer.VideoSource} pos={tpos} dur={(tdur?.ToString() ?? "-")} title={Halo.Widgets.TelegramPlayer.Title ?? "-"} debug={Halo.Widgets.TelegramPlayer.Debug ?? "-"} vdebug={Halo.Widgets.TelegramPlayer.VideoDebug ?? "-"}");
            }
            return;
        }

        if (args.Length >= 2 && args[0] == "--seek-tg"
            && double.TryParse(args[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double target))
        {
            Halo.Widgets.TelegramPlayer.Poke();
            System.Threading.Thread.Sleep(1200);
            var (p0, d0) = Halo.Widgets.TelegramPlayer.Read();
            Console.WriteLine($"before  pos={p0} dur={(d0?.ToString() ?? "-")} title={Halo.Widgets.TelegramPlayer.Title ?? "-"}");
            Console.WriteLine($"seek({target}) -> {Halo.Widgets.TelegramPlayer.SeekTo(target)} debug={Halo.Widgets.TelegramPlayer.Debug ?? "-"}");
            for (int i = 0; i < 4; i++)
            {
                Halo.Widgets.TelegramPlayer.Poke();
                System.Threading.Thread.Sleep(1000);
                var (p, d) = Halo.Widgets.TelegramPlayer.Read();
                Console.WriteLine($"after {i}s pos={p} dur={(d?.ToString() ?? "-")}");
            }
            return;
        }

        if (args.Length >= 1 && args[0] == "--speed-tg")
        {
            Halo.Widgets.TelegramPlayer.Poke();
            System.Threading.Thread.Sleep(2600);
            Console.WriteLine($"speed before: {Halo.Widgets.TelegramPlayer.Speed ?? "-"}");
            Console.WriteLine($"toggle -> {Halo.Widgets.TelegramPlayer.ToggleSpeed()}");
            Console.WriteLine($"speed after:  {Halo.Widgets.TelegramPlayer.Speed ?? "-"} debug={Halo.Widgets.TelegramPlayer.Debug ?? "-"}");
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-tg-tree") { Halo.Widgets.TelegramPlayer.DumpTree(Console.Out); return; }

        if (args.Length >= 2 && args[0] == "--probe-size")
        {
            var title = args[1];
            Console.WriteLine($"title       {title}");
            Console.WriteLine($"looks like a file  {Halo.Widgets.MediaFileInfo.LooksLikeFile(title)}");
            Halo.Widgets.MediaFileInfo.Size(title);
            for (int i = 0; i < 40; i++)
            {
                System.Threading.Thread.Sleep(100);
                if (Halo.Widgets.MediaFileInfo.Size(title) is { } b)
                { Console.WriteLine($"size        {b:N0} bytes = {Halo.Widgets.MediaFileInfo.Human(b)}"); return; }
            }
            Console.WriteLine("size        (not found)");
            return;
        }

        if (args.Length >= 2 && args[0] == "--probe-seek") { ProbeSeek(double.Parse(args[1],
            System.Globalization.CultureInfo.InvariantCulture), args.Length > 2 ? int.Parse(args[2]) : 1); return; }
        if (args.Length >= 1 && args[0] == "--probe-subpixel") { ProbeSubpixel(); return; }
        if (args.Length >= 1 && args[0] == "--probe-bar")
        {
            ProbeBar(args.Length > 1 ? int.Parse(args[1]) : 12); return;
        }

        if (args.Length >= 1 && args[0] == "--probe-behind")
        {
            ProbeBehind(args.Length > 1 ? int.Parse(args[1]) : 20); return;
        }

        if (args.Length >= 1 && args[0] == "--probe-fullscreen")
        {
            ProbeFullscreen(args.Length > 1 ? int.Parse(args[1]) : 20); return;
        }

        if (args.Length >= 1 && args[0] == "--probe-art")
        {
            ProbeArt(args.Length > 1 ? int.Parse(args[1]) : 45, args.Contains("nudge")); return;
        }

        if (args.Length >= 2 && args[0] == "--probe-downloads") { ProbeDownloads(args[1]); return; }

        if (args.Length >= 1 && args[0] == "--cancel-download") { CancelDownload(); return; }

        if (args.Length >= 2 && args[0] == "--probe-icon") { ProbeIcon(args[1]); return; }

        if (args.Length >= 1 && args[0] == "--probe-package")
        {
            Console.WriteLine($"packaged : {Halo.Interop.AppModel.IsPackaged}");
            Console.WriteLine($"identity : {Halo.Interop.AppModel.PackageFullName ?? "(none - ordinary install)"}");
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-volume")
        {
            var meter = new Halo.Widgets.AudioMeter();
            bool wasMuted = meter.Muted();
            float wasLevel = meter.Volume();
            Console.WriteLine($"before   : level {wasLevel:F3}, muted {wasMuted}");

            if (wasLevel <= 0f)
            {
                Console.WriteLine("no endpoint, or the level read failed - refusing to touch it");
                return;
            }
            try
            {
                if (!wasMuted) meter.ToggleMute();
                Console.WriteLine($"muted    : level {meter.Volume():F3}, muted {meter.Muted()}");
                float wanted = wasLevel > 0.5f ? 0.10f : 0.90f;
                meter.SetVolume(wanted);
                System.Threading.Thread.Sleep(150);
                float now = meter.Volume();
                Console.WriteLine($"wrote {wanted:F2} while muted -> reads back {now:F3}");
                Console.WriteLine(Math.Abs(now - wanted) < 0.01f
                    ? "verdict  : the write LANDS while muted - level and mute are independent"
                    : "verdict  : the write was IGNORED while muted");
            }
            finally
            {
                meter.SetVolume(wasLevel);
                if (!wasMuted) meter.Unmute();
                Console.WriteLine($"restored : level {meter.Volume():F3}, muted {meter.Muted()}");
            }
            return;
        }
        if (args.Length >= 1 && args[0] == "--probe-banner")
        {
            Environment.SetEnvironmentVariable("HALO_BANNER_ROOT", @"Software\Halo\ProbeBannerRoot");
            var edit = new Halo.Notifications.BannerEdit("probe.app", "ShowBanner", 0);
            int ok = Halo.Notifications.BannerWriter.Commit([edit]);
            Console.WriteLine($"packaged : {Halo.Interop.AppModel.IsPackaged}");
            Console.WriteLine($"route    : {(Halo.Interop.AppModel.IsPackaged ? "out-of-package child" : "direct")}");
            Console.WriteLine($"verified : {ok}/1");
            Console.WriteLine($"readback : {Halo.Notifications.BannerApply.Read("probe.app", "ShowBanner")?.ToString() ?? "(nothing)"}");
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-bt") { ProbeBt(); return; }

        if (args.Length >= 2 && args[0] == "--probe-tree") { ProbeTree(int.Parse(args[1])); return; }

        if (args.Length >= 1 && args[0] == "--probe-glasscache") { ProbeGlassCache(); return; }

        if (args.Length >= 2 && args[0] == "--probe-retry") { ProbeRetry(int.Parse(args[1])); return; }

        if (args.Length >= 2 && args[0] == "--render-shape")
        {

            if (int.TryParse(Environment.GetEnvironmentVariable("HALO_SS"), out var forcedSs))
                Halo.Shell.LayeredNotch.Supersample = forcedSs;

            if (args.Length >= 4)
            {
                var parts = args[3].Split(',');
                float P(int i, float dflt) => i < parts.Length && float.TryParse(parts[i],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : dflt;
                Halo.Shell.LayeredNotch.FrostMix = P(0, Halo.Shell.LayeredNotch.FrostMix);
                Halo.Shell.LayeredNotch.Sheen = P(1, Halo.Shell.LayeredNotch.Sheen);
                Halo.Shell.LayeredNotch.Grain = P(2, Halo.Shell.LayeredNotch.Grain);
                Halo.Shell.LayeredNotch.RimLight = P(3, Halo.Shell.LayeredNotch.RimLight);
            }

            System.Drawing.Bitmap back;
            if (args.Length >= 3 && System.IO.File.Exists(args[2]))
            {
                using var src0 = new System.Drawing.Bitmap(args[2]);
                using var fit0 = new System.Drawing.Bitmap(560, 220, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (var bgg0 = System.Drawing.Graphics.FromImage(fit0))
                    bgg0.DrawImage(src0, new System.Drawing.Rectangle(0, 0, 560, 220));

                back = Halo.Shell.LayeredNotch.BlurPyramid(fit0);
            }
            else
            {
                back = new System.Drawing.Bitmap(560, 220, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using var bgg = System.Drawing.Graphics.FromImage(back);
                bgg.Clear(System.Drawing.Color.Magenta);
            }
            using var _back = back;
            using var shot = new System.Drawing.Bitmap(560, 220, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var sg = System.Drawing.Graphics.FromImage(shot))
            {
                sg.Clear(System.Drawing.Color.Transparent);
                Halo.Shell.LayeredNotch.ShapeInto(sg, 560, 220, 30, NotchController.TintAppExpanded, back, 1f);
            }
            shot.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine("wrote " + args[1]);
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-ip")
        {
            Halo.ClaudeCode.IpCountry.Poke();
            System.Threading.Thread.Sleep(5000);
            Console.WriteLine($"ip={Halo.ClaudeCode.IpCountry.Ip} cc={Halo.ClaudeCode.IpCountry.Cc} "
                + $"isp={Halo.ClaudeCode.IpCountry.Isp} asn={Halo.ClaudeCode.IpCountry.Asn}");
            Console.WriteLine($"apiIp={Halo.ClaudeCode.IpCountry.ApiIp} apiCc={Halo.ClaudeCode.IpCountry.ApiCc} "
                + $"split={Halo.ClaudeCode.IpCountry.Split}");
            string? scored = Halo.ClaudeCode.IpCountry.Split
                ? Halo.ClaudeCode.IpCountry.ApiIp : Halo.ClaudeCode.IpCountry.Ip;
            Halo.ClaudeCode.IpRep.Want(scored);
            System.Threading.Thread.Sleep(5000);
            Console.WriteLine($"scored={scored} forIp={Halo.ClaudeCode.IpRep.ForIp} "
                + $"verdict={Halo.ClaudeCode.IpRep.Verdict} abuse={Halo.ClaudeCode.IpRep.Abuse} "
                + $"sev={Halo.ClaudeCode.IpRep.Sev}");
            Halo.ClaudeCode.DnsLeak.Want(scored,
                Halo.ClaudeCode.IpCountry.Split ? Halo.ClaudeCode.IpCountry.ApiCc : Halo.ClaudeCode.IpCountry.Cc);
            for (int i = 0; i < 40 && !Halo.ClaudeCode.DnsLeak.Done; i++) System.Threading.Thread.Sleep(500);
            Console.WriteLine($"dns done={Halo.ClaudeCode.DnsLeak.Done} resolvers={Halo.ClaudeCode.DnsLeak.Resolvers} "
                + $"where={Halo.ClaudeCode.DnsLeak.Where} leaking={Halo.ClaudeCode.DnsLeak.Leaking}");
            Console.WriteLine("mark=" + Halo.ClaudeCode.IpRep.Score(
                Halo.ClaudeCode.IpRep.Tor, Halo.ClaudeCode.IpRep.Abuser, Halo.ClaudeCode.IpRep.Bogon,
                Halo.ClaudeCode.IpRep.Vpn, Halo.ClaudeCode.IpRep.Proxy, Halo.ClaudeCode.IpRep.Datacenter,
                Halo.ClaudeCode.IpRep.Abuse, Halo.ClaudeCode.IpCountry.Split, Halo.ClaudeCode.DnsLeak.Leaking));
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-spectrum")
        {
            for (int i = 0; i < 20; i++)
            {

                var b = Halo.Widgets.AudioSpectrum.Bands();
                Console.WriteLine(b == null
                    ? "avail=False (no bars)"
                    : "avail=True " + string.Join(" ", Array.ConvertAll(b, v => v.ToString("0.00"))));
                System.Threading.Thread.Sleep(300);
            }
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-eqwake")
        {
            int gap = args.Length >= 2 && int.TryParse(args[1], out var g) ? g : 7;
            for (int round = 1; round <= 4; round++)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                long? firstLive = null;
                while (clock.ElapsedMilliseconds < 12_000)
                {
                    if (Halo.Widgets.AudioSpectrum.Bands() != null) { firstLive = clock.ElapsedMilliseconds; break; }
                    System.Threading.Thread.Sleep(8);
                }
                Console.WriteLine($"round {round}: live after {(firstLive?.ToString() ?? ">12000")} ms");
                if (firstLive == null) break;

                var warm = System.Diagnostics.Stopwatch.StartNew();
                while (warm.ElapsedMilliseconds < 1500) { Halo.Widgets.AudioSpectrum.Bands(); System.Threading.Thread.Sleep(8); }

                if (round == 3)
                {
                    Console.WriteLine($"  ...{gap}s with KeepWarm (the fix: not drawn, still warm)");
                    var held = System.Diagnostics.Stopwatch.StartNew();
                    while (held.ElapsedMilliseconds < gap * 1000)
                    { Halo.Widgets.AudioSpectrum.KeepWarm(); System.Threading.Thread.Sleep(8); }
                }
                else
                {
                    Console.WriteLine($"  ...parking for {gap}s (nobody is asking for bars)");
                    System.Threading.Thread.Sleep(gap * 1000);
                }
            }
            return;
        }

        if (args.Length >= 1 && args[0] == "--probe-timeline")
        {

            var probe = new System.Threading.Thread(() => ProbeTimeline());
            probe.SetApartmentState(System.Threading.ApartmentState.MTA);
            probe.Start();
            probe.Join();
            return;
        }

        if (args.Length >= 2 && args[0] == "--moods" && args[1] == "json") { MoodsJson(); return; }
        if (args.Length >= 1 && args[0] == "--moods") { Moods(); return; }

        _instance = new System.Threading.Mutex(true, "Halo.Notch.SingleInstance", out bool created);
        if (!created) { OpenSettingsPanel(); return; }
        if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase)) OpenSettingsPanel();

        try
        {

            if (args.Length >= 1 && args[0] == "--probe-crash")
            {
                _probeCrash = true;
                throw new InvalidOperationException(
                    "probe-crash: deliberate, raised by the dev hook",
                    new System.IO.IOException("probe-crash inner: pretend the notch surface was locked"));
            }

            Win32.OleInitialize(IntPtr.Zero);
            var notch = new LayeredNotch();
            notch.Show();
            Halo.ClaudeCode.Limits.Poke();
            Halo.ClaudeCode.NetMon.Poke();
            Halo.Codex.CodexNetMon.Poke();
            _ = new NotchController(notch);
            _tray = new Halo.Shell.TrayIcon();
            Win32.RunMessageLoop();
        }
        catch (Exception ex)
        {

            try
            {
                string json = Halo.Reports.ReportPayload.Json(
                    Halo.Reports.ReportPayload.Collect("crash", ex, ""));
                string path = Halo.Reports.ReportStore.Write(json, "crash");
                var settings = Halo.Reports.Intake.Settings();
                if (Halo.Reports.Intake.AutoCrash(settings) && (_probeCrash || Halo.Reports.Intake.CrashIsNew(ex))
                    && Halo.Reports.Intake.TrySend(json, settings))
                {

                    Halo.Reports.ReportStore.MarkSent(path);

                    if (!_probeCrash) Halo.Reports.Intake.RememberSent(ex);
                }
            }
            catch { }
            throw;
        }
    }

    private static void NewReport(string descPath)
    {
        string description = "";
        try { description = System.IO.File.ReadAllText(descPath); } catch { }
        try
        {
            string path = Halo.Reports.ReportStore.Write(
                Halo.Reports.ReportPayload.Json(
                    Halo.Reports.ReportPayload.Collect("manual", null, description)),
                "manual");
            Console.WriteLine(path);
        }
        catch (Exception ex) { Console.WriteLine("report failed: " + ex.Message); }
    }

    private static void MoodsJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"_moods\": \"Translate the VALUES. Keep every list non-empty - an empty one falls "
            + "back to English. Extra or missing lines are fine; Halo picks whichever fits the space.\",");
        var keys = new System.Collections.Generic.List<string>(Halo.Agents.Moods.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var set = Halo.Agents.Moods.Set(keys[i]);
            var quoted = new System.Collections.Generic.List<string>();
            foreach (var line in set) quoted.Add("\"" + line.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
            sb.AppendLine($"  \"mood.{keys[i]}\": [{string.Join(", ", quoted)}]{(i == keys.Count - 1 ? "" : ",")}");
        }
        sb.AppendLine("}");
        Console.Write(sb.ToString());
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo",
                "moods-template.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    private static void Moods()
    {
        int keys = 0, lines = 0;
        foreach (var key in Halo.Agents.Moods.Keys)
        {
            var set = Halo.Agents.Moods.Set(key);
            keys++; lines += set.Length;
            Console.WriteLine($"{key,-18} {set.Length,2}  {string.Join("  ·  ", set)}");
        }
        Console.WriteLine();
        Console.WriteLine($"{keys} keys, {lines} lines, none of them generated at runtime.");
    }

    private static void ProbeGlassCache()
    {
        var th = new System.Threading.Thread(() =>
        {
            var sb = new System.Text.StringBuilder();

            using var plate = new System.Drawing.Bitmap(560, 220, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var pg = System.Drawing.Graphics.FromImage(plate))
            using (var pb = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, 560, 220),
                System.Drawing.Color.FromArgb(255, 235, 120, 40),
                System.Drawing.Color.FromArgb(255, 20, 40, 150), 20f))
                pg.FillRectangle(pb, 0, 0, 560, 220);

            foreach (var (w, h, radius, tint, scale) in new[]
            {
                (560, 420, 22, 200, 1f),
                (560, 420, 22, 200, 1.5f),
                (220, 40, 20, 160, 1f),
            })
            {
                var notch = new Halo.Shell.LayeredNotch { Scale = scale };
                notch.SeedBackdrop(plate);
                float z = notch.Zoom;
                int dw = (int)MathF.Ceiling(w * z), dh = (int)MathF.Ceiling(h * z);

                using (var warm = new System.Drawing.Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                using (var wg = System.Drawing.Graphics.FromImage(warm))
                {
                    wg.ScaleTransform(z, z);
                    notch.DrawShape(wg, w, h, radius, tint, true);
                }
                using var viaCache = new System.Drawing.Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = System.Drawing.Graphics.FromImage(viaCache))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.ScaleTransform(z, z);
                    notch.DrawShape(g, w, h, radius, tint, true);
                }

                using var direct = new System.Drawing.Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = System.Drawing.Graphics.FromImage(direct))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.ScaleTransform(z, z);
                    Halo.Shell.LayeredNotch.ShapeInto(g, w, h, radius, tint, null, 1f, 0f);
                }

                long differing = 0; int worst = 0;
                for (int y = 0; y < dh; y++)
                    for (int x = 0; x < dw; x++)
                    {
                        var a = viaCache.GetPixel(x, y);
                        var b = direct.GetPixel(x, y);
                        int d = Math.Max(Math.Max(Math.Abs(a.A - b.A), Math.Abs(a.R - b.R)),
                                         Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
                        if (d != 0) { differing++; if (d > worst) worst = d; }
                    }
                sb.AppendLine($"zoom {z:0.00}  {dw}x{dh}  differing px {differing}/{(long)dw * dh}  worst channel delta {worst}");

                static double Time(int warm, int n, Action body)
                {
                    for (int i = 0; i < warm; i++) body();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < n; i++) body();
                    return sw.Elapsed.TotalMilliseconds / n;
                }
                using var scratch = new System.Drawing.Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var sg = System.Drawing.Graphics.FromImage(scratch);
                sg.ScaleTransform(z, z);
                double warmMs = Time(10, 120, () => notch.DrawShape(sg, w, h, radius, tint, true));

                int t = 0;
                double coldMs = Time(4, 40, () => notch.DrawShape(sg, w, h, radius, tint - (t++ % 7), true));
                sb.AppendLine($"           cached {warmMs,6:0.00} ms   uncached {coldMs,6:0.00} ms"
                    + $"   ({coldMs / Math.Max(0.001, warmMs):0.0}x)");
            }
            Console.Write(sb.ToString());
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo",
                    "glasscache-probe.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, sb.ToString());
            }
            catch { }
        });
        th.SetApartmentState(System.Threading.ApartmentState.STA);
        th.Start();
        th.Join();
    }

    private static void ProbeRetry(int pid)
    {
        var sb = new System.Text.StringBuilder();
        var rows = Halo.Interop.ConsoleRead.Tail(pid, 14, below: 2);
        if (rows is null) sb.AppendLine($"pid {pid}: console unreachable (no buffer to read)");
        else
        {
            sb.AppendLine($"pid {pid}: {rows.Length} rows");
            int? found = null;
            foreach (var row in rows)
            {
                var hit = Halo.ClaudeCode.ApiRetry.RetryIn(row);
                if (hit is { } s2) found = s2;
                sb.AppendLine($"  {(hit is null ? " " : ">")} | {(row.Length > 120 ? row[..120] : row)}");
            }
            sb.AppendLine(found is { } secs
                ? $"=> retrying, {Halo.ClaudeCode.ApiRetry.Caption(secs)} ({secs}s)"
                : "=> no retry line on screen");
        }

        Console.Write(sb.ToString());
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo",
                "retry-probe.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    private static void ProbeTree(int pid)
    {
        var map = new System.Collections.Generic.Dictionary<int, int>();
        var snap = Halo.Interop.Win32.CreateToolhelp32Snapshot(Halo.Interop.Win32.TH32CS_SNAPPROCESS, 0);
        var pe = new Halo.Interop.Win32.PROCESSENTRY32W
        { dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Halo.Interop.Win32.PROCESSENTRY32W>() };
        if (Halo.Interop.Win32.Process32FirstW(snap, ref pe))
            do { map[(int)pe.th32ProcessID] = (int)pe.th32ParentProcessID; }
            while (Halo.Interop.Win32.Process32NextW(snap, ref pe));
        Halo.Interop.Win32.CloseHandle(snap);
        Console.WriteLine($"snapshot has {map.Count} processes");
        int p = pid, guard = 0;
        while (p > 4 && guard++ < 20) { Console.WriteLine($"  {p}"); if (!map.TryGetValue(p, out p)) break; }
    }

    private static void CancelDownload()
    {
        Halo.Widgets.Downloads.Scan();
        if (Halo.Widgets.Downloads.Count == 0) { Console.WriteLine("nothing downloading"); return; }
        string? file = Halo.Widgets.Downloads.FilePath;
        Console.WriteLine($"cancelling '{Halo.Widgets.Downloads.Name}' file='{file}'");
        long before = -1;
        try { if (file != null) before = new System.IO.FileInfo(file).Length; } catch { }

        Halo.Widgets.Downloads.CancelDownload();
        System.Threading.Thread.Sleep(14000);

        long after = -1;
        try { if (file != null && System.IO.File.Exists(file)) after = new System.IO.FileInfo(file).Length; } catch { }
        Console.WriteLine(after < 0 ? "partial is gone -> stopped"
            : after == before ? $"partial held at {before:n0} -> stopped"
            : $"partial grew {before:n0} -> {after:n0} -> STILL RUNNING");
    }

    private static void ProbeDownloads(string outPath)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{DateTime.Now:HH:mm:ss}  probe-downloads");

        sb.AppendLine("\nChromiumProgress.Live():");
        var live = Halo.Widgets.ChromiumProgress.Live();
        if (live.Length == 0) sb.AppendLine("  (nothing in progress)");
        foreach (var e in live)
            sb.AppendLine($"  name='{e.Name}' received={e.Received:n0} total={e.Total:n0}"
                        + $" pct={(e.Total > 0 ? 100.0 * e.Received / e.Total : 0):0.0}");

        sb.AppendLine("\nChromiumProgress.DumpFields():");
        sb.AppendLine(Halo.Widgets.ChromiumProgress.DumpFields());

        sb.AppendLine("\nPartialFiles.All():");
        foreach (var p in Halo.Widgets.PartialFiles.All())
            sb.AppendLine($"  {p}");

        Halo.Widgets.Downloads.Scan();
        sb.AppendLine($"\nDownloads.Scan() -> {Halo.Widgets.Downloads.Count} item(s), selected="
                    + Halo.Widgets.Downloads.SelectedIndex);
        foreach (var d in Halo.Widgets.Downloads.Items)
            sb.AppendLine($"  key='{d.Key}' name='{d.Name}' pct={d.Percent} got={d.Downloaded:n0}"
                        + $" total={d.Total:n0} noPct={d.NoPct} noBytes={d.NoBytes} pid={d.OwnerPid}"
                        + $" exe='{d.ExePath}' file='{d.FilePath}'");

        System.IO.File.WriteAllText(outPath, sb.ToString());
    }

    private static void ProbeBt()
    {

        string[] want =
        [
            "System.ItemNameDisplay",
            "System.Devices.Aep.Bluetooth.Cod.Major",
            "System.Devices.Aep.Bluetooth.Cod.Minor",
            "System.Devices.Aep.Bluetooth.Cod.ServiceCapabilities",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.Manufacturer",
            "System.Devices.Aep.Bluetooth.LastSeenTime",
            "System.Devices.Aep.SignalStrength",
            "System.Devices.Aep.IsConnected",
        ];
        try
        {
            string sel = Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromConnectionStatus(
                Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected);

            var good = new System.Collections.Generic.List<string>();
            foreach (var k in want)
            {
                try
                {
                    Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(sel, new[] { k },
                        Windows.Devices.Enumeration.DeviceInformationKind.AssociationEndpoint)
                        .AsTask().GetAwaiter().GetResult();
                    good.Add(k);
                }
                catch (Exception ex) { Console.WriteLine($"  rejected {k}: {ex.Message.Split('\r', '\n')[0]}"); }
            }
            want = [.. good];
            var found = Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                sel, want, Windows.Devices.Enumeration.DeviceInformationKind.AssociationEndpoint)
                .AsTask().GetAwaiter().GetResult();
            Console.WriteLine($"{found.Count} connected endpoint(s)");
            foreach (var d in found)
            {
                Console.WriteLine($"\n  {d.Name}");
                Console.WriteLine($"    id  {d.Id}");
                foreach (var k in want)
                    Console.WriteLine($"    {k,-58} {(d.Properties.TryGetValue(k, out var v) && v != null ? v : "(absent)")}");

                static int Num(object? v) => v is null ? -1
                    : int.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
                        out var n) ? n : -1;
                d.Properties.TryGetValue("System.Devices.Aep.Bluetooth.Cod.Major", out var mj);
                d.Properties.TryGetValue("System.Devices.Aep.Bluetooth.Cod.Minor", out var mn);
                int major = Num(mj), minor = Num(mn);
                Console.WriteLine($"    -> major={major} minor={minor} ({mj?.GetType().Name ?? "null"})"
                    + $"  glyph U+{Halo.Widgets.BtWidget.GlyphForCod(major, minor):X4}");
            }
        }
        catch (Exception ex) { Console.WriteLine("probe failed: " + ex.Message); }
    }

    private static void ProbeIcon(string aumid)
    {
        var tmp = System.IO.Path.GetTempPath();
        var s = Halo.Notifications.ShellIcon.ForAumid(aumid);
        Console.WriteLine($"ShellIcon: {(s == null ? "NULL" : $"{s.Width}x{s.Height} -> probe_shell.png")}");
        s?.Save(System.IO.Path.Combine(tmp, "probe_shell.png"));
        var a = Halo.Widgets.AppIcon.ForAumid(aumid);
        Console.WriteLine($"AppIcon:   {(a == null ? "NULL" : $"{a.Width}x{a.Height} -> probe_app.png")}");
        a?.Save(System.IO.Path.Combine(tmp, "probe_app.png"));
    }

    private static void RenderPill(string outPath)
    {
        var t = new System.Threading.Thread(() =>
        {

            (string label, string state, string? tool, int agoMin, long ctxUsed, float usage, string? target)[] rows =
            {
                ("idle — white", "idle", null, 0, 120_000, 0.30f, null),
                ("thinking — amber", "working", null, 0, 120_000, 0.30f, null),
                ("shell — green", "working", "Bash", 0, 120_000, 0.30f, null),
                ("reading — cyan", "working", "Read", 0, 120_000, 0.30f, null),
                ("fetching — teal", "working", "WebFetch", 0, 120_000, 0.30f, null),
                ("writing — violet", "working", "Edit", 0, 120_000, 0.30f, null),
                ("digging — lime", "working", "Grep", 0, 120_000, 0.30f, null),
                ("planning — gold", "working", "TodoWrite", 0, 120_000, 0.30f, null),
                ("subagent — magenta", "working", "Task", 0, 120_000, 0.30f, null),
                ("watching — slate", "working", "Monitor", 0, 120_000, 0.30f, null),
                ("your turn — pink", "waiting_input", null, 0, 120_000, 0.30f, null),
                ("named: a program", "working", "Bash", 0, 120_000, 0.30f, "dotnet"),
                ("named: a file", "working", "Edit", 0, 120_000, 0.30f, "Fx.cs"),
                ("named: a host", "working", "WebFetch", 0, 120_000, 0.30f, "learn.microsoft.com"),
                ("an mcp server", "working", "mcp__serena__find_symbol", 0, 120_000, 0.30f, null),
                ("a tool with no slot", "working", "SomeOtherTool", 0, 120_000, 0.30f, null),
                ("thinking, 10 min in", "working", null, 10, 120_000, 0.30f, null),

                ("compacting, just in", "compacting", null, 0, 920_000, 0.30f, null),
                ("compacting, 2 min in", "compacting", null, 2, 986_000, 0.30f, null),
                ("named, but context 92%", "working", "Edit", 1, 920_000, 0.30f, "Fx.cs"),
                ("shell, usage 96%", "working", "Bash", 1, 120_000, 0.96f, null),
                ("both, and dragging", "working", "Grep", 15, 950_000, 0.97f, null),
            };
            const int pw = 220, ph = 40, gap = 12, labelW = 168, scale = 2;
            int width = labelW + pw + 20, height = rows.Length * (ph + gap) + gap;
            using var bmp = new System.Drawing.Bitmap(width * scale, height * scale,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.FromArgb(255, 30, 30, 34));
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var lf = new System.Drawing.Font("Segoe UI", 11f);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(180, 235, 235, 235));

            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halo-pill-demo");
            System.IO.Directory.CreateDirectory(root);
            float y = gap;
            int n = 0;
            foreach (var (label, state, tool, agoMin, ctxUsed, usage, target) in rows)
            {
                var now = DateTimeOffset.UtcNow;

                var path = System.IO.Path.Combine(root, $"status-{n++}.json");
                System.IO.File.WriteAllText(path, $$"""
                {
                  "pid": {{System.Environment.ProcessId}},
                  "sessionId": "pill",
                  "state": "{{state}}",
                  "consolePid": {{System.Environment.ProcessId}},
                  "updatedAt": "{{now:o}}",
                  "startedAt": "{{now.AddMinutes(-agoMin):o}}",
                  {{(tool is null ? "" : $"\"currentTool\": \"{tool}\",")}}
                  {{(target is null ? "" : $"\"toolTarget\": \"{target}\",")}}
                  "session": { "contextUsed": {{ctxUsed}}, "contextMax": 1000000, "promptTokens": 12000 }
                }
                """);
                IWidget w = new ClaudeCodeWidget(new Halo.ClaudeCode.StatusStore(path,
                    _ => DateTimeOffset.UtcNow.AddMinutes(-agoMin), watchFiles: false), 0, () => { });
                for (int i = 0; i < 60 && !w.IsActive; i++) System.Threading.Thread.Sleep(50);

                Halo.ClaudeCode.Limits.FiveHour = usage;
                Halo.ClaudeCode.Limits.FiveHourReset = DateTimeOffset.UtcNow.AddHours(2);
                Halo.ClaudeCode.Limits.CreditsUsed = 0;

                using (var warm = new System.Drawing.Bitmap(pw, ph,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                using (var wg = System.Drawing.Graphics.FromImage(warm))
                    for (int f = 0; f < 14; f++) w.DrawCollapsed(wg, pw, ph, 1f);

                using var pill = new System.Drawing.Bitmap(pw, ph,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var pg = System.Drawing.Graphics.FromImage(pill))
                {
                    pg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    using (var plate = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(236, 16, 16, 18)))
                    using (var pp = Fx.PillPath(pw, ph, ph / 2f))
                        pg.FillPath(plate, pp);
                    w.DrawCollapsed(pg, pw, ph, 1f);
                }
                g.DrawString(label, lf, lb, new System.Drawing.RectangleF(12, y + 10, labelW - 20, ph));
                g.DrawImage(pill, labelW, y);
                y += ph + gap;
            }
            bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine(outPath);
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();
    }

    private static void RenderBar(string outPath)
    {
        const int W = 220, H = 40, Pad = 16;
        float[] fracs = { 0.06f, 0.28f, 0.55f, 0.82f, 1f };
        var accent = System.Drawing.Color.FromArgb(255, 232, 96, 120);
        using var bmp = new System.Drawing.Bitmap(W * 2 + Pad * 3 + 90, (H + Pad) * fracs.Length + Pad + 26);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(System.Drawing.Color.FromArgb(20, 20, 24));
            using var lf = new System.Drawing.Font("Segoe UI", 11f);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(190, 232, 236, 244));
            g.DrawString("with track (agents)", lf, lb, 90 + Pad, 6);
            g.DrawString("no track (music)", lf, lb, 90 + Pad * 2 + W, 6);

            for (int i = 0; i < fracs.Length; i++)
            {
                float y = 26 + Pad + i * (H + Pad);
                g.DrawString($"{fracs[i] * 100:0}%", lf, lb, 22, y + H / 2f - 10);
                for (int col = 0; col < 2; col++)
                {
                    var st = g.Save();
                    g.TranslateTransform(90 + Pad + col * (W + Pad), y);

                    using (var path = Halo.Widgets.Fx.PillPath(W, H, H / 2f))
                    using (var back = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 26, 27, 32)))
                        g.FillPath(back, path);
                    Halo.Widgets.Fx.PillBar(g, W, H, 1f, fracs[i], accent, 0.5f, alive: false, track: col == 0, decorated: col == 0);
                    g.Restore(st);
                }
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderCue(string outPath)
    {

        const int W = 560, H = 220, Gap = 14;
        var states = new[] { (cap: false, on: true), (cap: false, on: false),
                             (cap: true,  on: true), (cap: true,  on: false) };
        using var bmp = new System.Drawing.Bitmap(W * 2 + Gap * 3, H * 2 + Gap * 3);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.FromArgb(18, 18, 22));
            for (int i = 0; i < states.Length; i++)
            {
                var st = g.Save();
                g.TranslateTransform(Gap + i % 2 * (W + Gap), Gap + i / 2 * (H + Gap));
                using (var path = Halo.Widgets.Fx.PillPath(W, H, 30f))
                using (var back = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 30, 32, 38)))
                    g.FillPath(back, path);
                Halo.Shell.NotchController.DrawToggleCue(g, W, H, 30f, 1f, states[i].cap, states[i].on, i % 2 == 0 ? 1f : 0f);
                Halo.Shell.NotchController.DrawCueEdge(g, W, H, 30f, 1f, states[i].cap, states[i].on,
                                                       i % 2 == 0 ? 1f : 0f);
                g.Restore(st);
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderMarquee(string outPath)
    {
        const int Rows = 30, W = 320, RowH = 21;
        const string Title = "The Lord of the Rings: The Fellowship of the Ring (Extended Edition) 2001";

        const string Rtl = "\u0641\u06CC\u0644\u0645 \u0628\u0633\u06CC\u0627\u0631 \u0637\u0648\u0644\u0627\u0646\u06CC \u0628\u0627 \u0646\u0627\u0645 \u062F\u0631\u0627\u0632 \u0628\u0631\u0627\u06CC \u0622\u0632\u0645\u0627\u06CC\u0634 \u0645\u0627\u0631\u06A9\u06CC - \u0641\u06CC\u0644\u0645 \u0628\u0633\u06CC\u0627\u0631 \u0637\u0648\u0644\u0627\u0646\u06CC \u0628\u0627 \u0646\u0627\u0645 \u062F\u0631\u0627\u0632 \u0628\u0631\u0627\u06CC \u0622\u0632\u0645\u0627\u06CC\u0634 \u0645\u0627\u0631\u06A9\u06CC";
        using var bmp = new System.Drawing.Bitmap(W * 2 + 30, Rows * RowH + 40);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(28, 28, 32));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var f = new System.Drawing.Font("Segoe UI", 10.5f);
            using var b = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(150, 235, 235, 235));
            using var lf = new System.Drawing.Font("Segoe UI", 9f);

            void Column(float x, string text, string label)
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(label, lf, lb, x, 6);
                var m = new Halo.Widgets.Marquee();

                for (int i = 0; i < 40; i++) m.Draw(g, text, f, b, -9999f, -9999f, W, true, 1f / 60f);
                for (int r = 0; r < Rows; r++)
                {

                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    m.Draw(g, text, f, b, x, 26 + r * RowH, W, true, 1f / 60f);
                }
            }
            Column(10, Title, "LTR, one frame per row");
            Column(W + 20, Rtl, "RTL, one frame per row");
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine(outPath);
    }

    private static void RenderPin(string outPath)
    {

        using var bmp = new System.Drawing.Bitmap(620, 150);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.FromArgb(28, 28, 32));
            using var lf = new System.Drawing.Font("Segoe UI", 11f);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(170, 235, 235, 235));
            void Cell(float ox, bool pinned, float hover, string label, bool rec = false, float hold = 0f)
            {
                var st = g.Save();
                g.TranslateTransform(ox, 14);
                g.ScaleTransform(3.2f, 3.2f);
                Halo.Shell.NotchController.DrawPushpin(
                    g, new System.Drawing.RectangleF(0, 0, 24, 24), pinned, hover, 1f, rec, hold);
                g.Restore(st);
                g.DrawString(label, lf, lb, ox - 4, 108);
            }
            Cell(20, false, 0f, "off");
            Cell(140, true, 0f, "pinned");
            Cell(260, false, 0f, "in capture", rec: true);
            Cell(380, true, 0f, "pinned+cap", rec: true);
            Cell(500, false, 1f, "mid-hold", hold: 1f);
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderHaloAsk(string outPath)
    {
        (string title, string body, string yes, string no, int hover)[] cases =
        [
            ("Connect Claude Code to Halo?",
             "Halo will add its hooks to your Claude Code settings so live sessions show up in the pill. It edits ~/.claude/settings.json and keeps a backup.",
             "Connect", "Not now", -1),
            ("Connect Claude Code to Halo?",
             "Halo will add its hooks to your Claude Code settings so live sessions show up in the pill. It edits ~/.claude/settings.json and keeps a backup.",
             "Connect", "Not now", 0),
            ("Connect Codex to Halo?", "", "Connect", "Not now", 1),
        ];

        int gap = 26, y = gap;
        var heights = new int[cases.Length];
        for (int i = 0; i < cases.Length; i++)
        {
            heights[i] = Halo.Widgets.HaloAsk.Height(cases[i].title, cases[i].body);
            y += heights[i] + gap;
        }

        using var bmp = new System.Drawing.Bitmap(Halo.Widgets.HaloAsk.W + gap * 2, y);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {

            using (var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Color.FromArgb(58, 62, 88), System.Drawing.Color.FromArgb(96, 64, 72), 25f))
                g.FillRectangle(bg, 0, 0, bmp.Width, bmp.Height);

            int top = gap;
            for (int i = 0; i < cases.Length; i++)
            {
                var (title, body, yes, no, hover) = cases[i];
                int h = heights[i];
                using var panel = new System.Drawing.Bitmap(Halo.Widgets.HaloAsk.W, h,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var pg = System.Drawing.Graphics.FromImage(panel))
                {
                    pg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var shape = Halo.Widgets.Fx.PillPath(Halo.Widgets.HaloAsk.W, h, 26f, 0.5f);
                    using (var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(238, 16, 16, 19)))
                        pg.FillPath(fill, shape);
                    Halo.Widgets.HaloAsk.Draw(pg, Halo.Widgets.HaloAsk.W, h, title, body, yes, no, hover, 1f);
                }
                g.DrawImage(panel, gap, top);
                top += h + gap;
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("wrote " + outPath);
    }

    private static void RenderVol(string outPath)
    {
        (float vol, bool muted, string label)[] cases =
        [
            (0f, false, "0 - silent"), (0.5f, true, "muted at 50"),
            (0.15f, false, "15 - low"), (0.5f, false, "50 - mid"), (0.9f, false, "90 - high"),
        ];
        using var bmp = new System.Drawing.Bitmap(cases.Length * 116 + 20, 190);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(24, 24, 28));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fam = new System.Drawing.FontFamily("Segoe Fluent Icons");
            using var lf = new System.Drawing.Font("Segoe UI", 10f);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 235, 235, 235));
            using var ink = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 255, 255, 255));

            for (int i = 0; i < cases.Length; i++)
            {
                var (vol, muted, label) = cases[i];
                string glyph = Halo.Widgets.MediaWidget.VolumeGlyph(vol, muted);
                float ox = 20 + i * 116;

                foreach (var (size, dy) in new[] { (16f, 26f), (80f, 70f) })
                {
                    using var path = new System.Drawing.Drawing2D.GraphicsPath();
                    using var sf = new System.Drawing.StringFormat(System.Drawing.StringFormat.GenericTypographic);
                    path.AddString(glyph, fam, 0, size, System.Drawing.PointF.Empty, sf);
                    path.Flatten();
                    var b = path.GetBounds();
                    if (b.Width <= 0) continue;
                    using var m = new System.Drawing.Drawing2D.Matrix();
                    m.Translate(ox + (96 - b.Width) / 2f - b.X, dy - b.Y);
                    path.Transform(m);
                    g.FillPath(ink, path);
                }
                g.DrawString(label, lf, lb, ox, 160);
                g.DrawString("U+" + ((int)glyph[0]).ToString("X4"), lf, lb, ox, 4);
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("wrote " + outPath);
    }

    private static void RenderBt(string outPath)
    {
        const int W = 220, H = 40;

        (string name, int pct, int major, int minor)[] cases =
        [
            ("Boy", 43, 2, 3),
            ("Boy", 8, 2, 3),
            ("WH-1000XM4", 62, 4, 6),
            ("SRS-XB33", 71, 4, 5),
            ("DualSense", 25, 5, 0x02),
            ("MX Master", 80, 5, 0x20),
            ("MX Keys", 55, 5, 0x10),
            ("Watch", 90, 7, 1),
            ("ThinkPad", 34, 1, 3),
            ("Nameless", 47, -1, -1),
        ];

        using var bmp = new System.Drawing.Bitmap(700, cases.Length * 76 + 30);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(24, 24, 28));
            using var lf = new System.Drawing.Font("Segoe UI", 11f);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(150, 235, 235, 235));

            var built = new Halo.Widgets.BtWidget[cases.Length];
            for (int i = 0; i < cases.Length; i++)
            {
                built[i] = new Halo.Widgets.BtWidget();
                built[i].Show(cases[i].name, cases[i].pct, cases[i].major, cases[i].minor);
            }
            System.Threading.Thread.Sleep(450);

            for (int i = 0; i < cases.Length; i++)
            {
                var (name, pct, major, minor) = cases[i];
                var wdg = built[i];

                using var cell = new System.Drawing.Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var cg = System.Drawing.Graphics.FromImage(cell))
                    for (int f = 0; f < 90; f++)
                    {
                        cg.Clear(System.Drawing.Color.FromArgb(255, 12, 12, 14));
                        wdg.DrawCollapsed(cg, W, H, 1f);
                    }

                float y = 20 + i * 76;
                g.DrawImage(cell, 24f, y);
                g.DrawString($"{name} - {pct}% - cod {major}/{minor}", lf, lb, 24f, y + H + 6);

                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(cell, new System.Drawing.RectangleF(280f, y - 14, W * 4f / 2.6f, H * 4f / 2.6f));
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                const int BH = 200, BW = 1100;
                using var big = new System.Drawing.Bitmap(BW, BH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var bg = System.Drawing.Graphics.FromImage(big))
                    for (int f = 0; f < 90; f++)
                    {
                        bg.Clear(System.Drawing.Color.FromArgb(255, 0, 0, 0));
                        wdg.DrawCollapsed(bg, BW, BH, 1f);
                    }

                var (bcx, bcy, _, bir, _, _) = Halo.Widgets.BtWidget.Metrics(BH);
                var (dx, dy, ok) = InkOffset(big, bcx, bcy, bir * 0.94f);

                using var real = new System.Drawing.Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var rg = System.Drawing.Graphics.FromImage(real))
                    for (int f = 0; f < 90; f++)
                    {
                        rg.Clear(System.Drawing.Color.FromArgb(255, 0, 0, 0));
                        wdg.DrawCollapsed(rg, W, H, 1f);
                    }
                var (rcx, rcy, _, rir, _, _) = Halo.Widgets.BtWidget.Metrics(H);
                var (rdx, rdy, rok) = InkOffset(real, rcx, rcy, rir * 0.94f);

                string note = ok && rok
                    ? $"at 40px dx={rdx:+0.00;-0.00} dy={rdy:+0.00;-0.00} ({rdx / rir * 100f,5:+0.0;-0.0}% of r)"
                      + $"   |  at 200px dx={dx / bir * 100f,5:+0.0;-0.0}% of r"
                    : "glyph ink not found";
                Console.WriteLine($"{name,-16} {pct,3}%  {note}");

                const float Crop = 210f;
                float ox = 700f, oy = y - 14;
                g.DrawImage(big, new System.Drawing.RectangleF(ox, oy, Crop, BH),
                    new System.Drawing.RectangleF(0, 0, Crop, BH), System.Drawing.GraphicsUnit.Pixel);
                using (var cp = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 255, 70, 70), 1f))
                {
                    g.DrawLine(cp, ox + bcx, oy, ox + bcx, oy + BH);
                    g.DrawLine(cp, ox, oy + bcy, ox + Crop, oy + bcy);
                }
                g.DrawString($"dx={dx:+0.0;-0.0} dy={dy:+0.0;-0.0}", lf, lb, ox, oy + BH + 4);
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("wrote " + outPath);

        string morphPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(outPath) ?? ".",
            System.IO.Path.GetFileNameWithoutExtension(outPath) + "-morph.png");
        float[] steps = [0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.8f, 1f];
        using (var strip = new System.Drawing.Bitmap(620, steps.Length * 92 + 20))
        using (var sg = System.Drawing.Graphics.FromImage(strip))
        {
            sg.Clear(System.Drawing.Color.FromArgb(24, 24, 28));
            using var lf = new System.Drawing.Font("Segoe UI", 10f);
            using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(150, 235, 235, 235));
            var wdg = new Halo.Widgets.BtWidget();
            wdg.Show("Boy", 43);
            System.Threading.Thread.Sleep(450);

            for (int i = 0; i < steps.Length; i++)
            {
                float t = steps[i];
                int mw = (int)(220 + (560 - 220) * t), mh = (int)(40 + (220 - 40) * t);
                float cf = Halo.Shell.NotchController.ContentFade(t);
                float mf = Halo.Shell.NotchController.MiniFade(t);
                using var frame = new System.Drawing.Bitmap(mw, mh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var fg = System.Drawing.Graphics.FromImage(frame))
                    for (int f = 0; f < 60; f++)
                    {
                        fg.Clear(System.Drawing.Color.FromArgb(255, 12, 12, 14));
                        if (mf > 0.01f) wdg.DrawCollapsed(fg, mw, mh, mf);
                        wdg.DrawContent(fg, mw, mh, cf);
                    }
                float s = 420f / 560f, dy = 12 + i * 92;
                sg.DrawImage(frame, new System.Drawing.RectangleF(24, dy, mw * s, mh * s));
                sg.DrawString($"t={t:0.00}  {mw}x{mh}  content={cf:0.00} mini={mf:0.00}", lf, lb, 470, dy + 8);
            }
            strip.Save(morphPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        Console.WriteLine("wrote " + morphPath);
    }

    private static (float dx, float dy, bool ok) InkOffset(System.Drawing.Bitmap b, float cx, float cy, float r)
    {
        var data = b.LockBits(new System.Drawing.Rectangle(0, 0, b.Width, b.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var buf = new byte[data.Stride * b.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            double sw = 0, sx = 0, sy = 0;
            int y0 = (int)Math.Max(0, cy - r), y1 = (int)Math.Min(b.Height - 1, cy + r);
            int x0 = (int)Math.Max(0, cx - r), x1 = (int)Math.Min(b.Width - 1, cx + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    int o = y * data.Stride + x * 4;

                    double lum = (buf[o] + buf[o + 1] + buf[o + 2]) / 3.0;
                    if (lum < 60) continue;
                    sw += lum; sx += lum * x; sy += lum * y;
                }
            return sw <= 0 ? (0, 0, false) : ((float)(sx / sw - cx), (float)(sy / sw - cy), true);
        }
        finally { b.UnlockBits(data); }
    }

    private static void ProbeFrame(int w, int h)
    {
        var th = new System.Threading.Thread(() =>
        {
            var notch = new Halo.Shell.LayeredNotch();
            notch.Show();
            var menu = new Halo.Shell.MenuFrame
            {
                Show = false, RowIcons = [], RowImages = [], RowImageOffsets = [], RowCounts = [],
                SessImages = [], SessIcons = [], RowRings = [], RowProgress = [], SessRings = [],
                OpenRow = -1,
            };
            var widget = new Halo.Widgets.ClaudeCodeWidget(new Halo.ClaudeCode.StatusStore(), 0, () => { });
            const int Warm = 20, N = 120;
            int radius = 22;

            static double Time(int warm, int n, Action body)
            {
                for (int i = 0; i < warm; i++) body();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < n; i++) body();
                return sw.Elapsed.TotalMilliseconds / n;
            }

            double dib = Time(Warm, N, () =>
            {
                var bmi = new Halo.Interop.Win32.BITMAPINFOHEADER
                {
                    biSize = System.Runtime.InteropServices.Marshal.SizeOf<Halo.Interop.Win32.BITMAPINFOHEADER>(),
                    biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = 0,
                };
                IntPtr screenDc = Halo.Interop.Win32.GetDC(IntPtr.Zero);
                IntPtr d = Halo.Interop.Win32.CreateDIBSection(screenDc, ref bmi, 0, out _, IntPtr.Zero, 0);
                IntPtr mem = Halo.Interop.Win32.CreateCompatibleDC(screenDc);
                IntPtr old = Halo.Interop.Win32.SelectObject(mem, d);
                Halo.Interop.Win32.SelectObject(mem, old);
                Halo.Interop.Win32.DeleteObject(d);
                Halo.Interop.Win32.DeleteDC(mem);
                Halo.Interop.Win32.ReleaseDC(IntPtr.Zero, screenDc);
            });

            using var scratch = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var sg = System.Drawing.Graphics.FromImage(scratch);

            using var plate = new System.Drawing.Bitmap(560, 420, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var pg = System.Drawing.Graphics.FromImage(plate))
            using (var pb = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, 560, 420),
                System.Drawing.Color.FromArgb(255, 210, 90, 40),
                System.Drawing.Color.FromArgb(255, 30, 60, 170), 30f))
                pg.FillRectangle(pb, 0, 0, 560, 420);

            double shape = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                Halo.Shell.LayeredNotch.ShapeInto(sg, w, h, radius, 200, plate, 1f);
            });

            double cached = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                notch.DrawShape(sg, w, h, radius, 200, true);
            });

            double bare = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                Halo.Shell.LayeredNotch.ShapeInto(sg, w, h, radius, 200, null, 0f);
            });

            Halo.Shell.LayeredNotch.Supersample = 1;
            double ss1 = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                Halo.Shell.LayeredNotch.ShapeInto(sg, w, h, radius, 200, plate, 1f);
            });

            double morph = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                Halo.Shell.LayeredNotch.ShapeInto(sg, w, h, radius, 200, plate, 1f);
                sg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                widget.DrawCollapsed(sg, w, h, 0.5f);
                widget.DrawContent(sg, w, h, 0.5f);
            });
            Halo.Shell.LayeredNotch.Supersample = 2;

            double collapsedOnly = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                sg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                widget.DrawCollapsed(sg, w, h, 0.5f);
            });
            double contentHalf = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                sg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                widget.DrawContent(sg, w, h, 0.5f);
            });
            Halo.Shell.LayeredNotch.Supersample = 2;
            double content = Time(Warm, N, () =>
            {
                sg.Clear(System.Drawing.Color.Transparent);
                notch.DrawShape(sg, w, h, radius, 200, true);
                sg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                widget.DrawContent(sg, w, h, 1f);
            });

            double full = Time(Warm, N, () => notch.Render(w, h, radius, 200, 1f, 0f, true, menu,
                (g, cw, ch, f) => widget.DrawContent(g, cw, ch, f), (g, cw, ch, f) => { }));

            Console.WriteLine($"panel        {w}x{h}");
            Console.WriteLine($"dib only     {dib,7:0.00} ms   ({1000.0 / Math.Max(0.001, dib),6:0} fps)");
            Console.WriteLine($"shape glass  {shape,7:0.00} ms");
            Console.WriteLine($"shape bare   {bare,7:0.00} ms   (the glass layer costs {shape - bare:0.00})");
            Console.WriteLine($"shape ss=1   {ss1,7:0.00} ms   ({shape / Math.Max(0.001, ss1):0.0}x cheaper than ss=2)");
            Console.WriteLine($"shape cached {cached,7:0.00} ms   ({shape / Math.Max(0.001, cached):0.0}x cheaper than compositing it)");
            Console.WriteLine($"morph frame  {morph,7:0.00} ms   ({1000.0 / Math.Max(0.001, morph),6:0} fps  - ss=1, both content layers)");
            Console.WriteLine($"  collapsed  {collapsedOnly,7:0.00} ms   (fade 0.5, on its own)");
            Console.WriteLine($"  content    {contentHalf,7:0.00} ms   (fade 0.5, on its own)");

            Halo.Shell.LayeredNotch.Supersample = 1;
            Console.WriteLine("morph sweep  (220x40 -> 560x420, collapsed fading out under the panel)");
            double worst = 0; string worstAt = "";
            foreach (float t in new[] { 0.15f, 0.30f, 0.50f, 0.70f, 0.85f })
            {
                int mw = (int)(220 + (w - 220) * t), mh = (int)(40 + (h - 40) * t);
                float mini = 1f - t, cf = t;
                using var ms = new System.Drawing.Bitmap(mw, mh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var mg = System.Drawing.Graphics.FromImage(ms);
                double one = Time(8, 60, () =>
                {
                    mg.Clear(System.Drawing.Color.Transparent);
                    Halo.Shell.LayeredNotch.ShapeInto(mg, mw, mh, radius, 200, plate, 1f);
                    mg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if (mini > 0.01f) widget.DrawCollapsed(mg, mw, mh, mini);
                    widget.DrawContent(mg, mw, mh, cf);
                });
                if (one > worst) { worst = one; worstAt = $"t={t:0.00} {mw}x{mh}"; }
                Console.WriteLine($"  t={t:0.00}  {mw,4}x{mh,-4} mini={mini:0.00}  {one,6:0.00} ms  ({1000.0 / Math.Max(0.001, one),5:0} fps)");
            }
            Console.WriteLine($"  worst      {worst,7:0.00} ms   ({1000.0 / Math.Max(0.001, worst),6:0} fps at {worstAt})");

            foreach (var (cw, ch, cf2, what) in new[]
            {
                (220, 40, 0.50f, "collapsed's own size"),
                (w, h, 0.50f, "panel size, same opacity"),
                (w, h, 0.15f, "panel size, nearly invisible"),
            })
            {
                using var cs = new System.Drawing.Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var cgx = System.Drawing.Graphics.FromImage(cs);
                double one = Time(8, 60, () =>
                {
                    cgx.Clear(System.Drawing.Color.Transparent);
                    cgx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    widget.DrawCollapsed(cgx, cw, ch, cf2);
                });
                Console.WriteLine($"  collapsed {one,7:0.00} ms   {cw}x{ch} @ {cf2:0.00}  ({what})");
            }

            {
                var accent = System.Drawing.Color.FromArgb(255, 120, 200, 160);
                using var ps = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var pgx = System.Drawing.Graphics.FromImage(ps);
                pgx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                double bar = Time(8, 60, () =>
                {
                    pgx.Clear(System.Drawing.Color.Transparent);
                    Halo.Widgets.Fx.PillBar(pgx, w, h, 0.5f, 0.4f, accent, 0.3f);
                });
                double glow = Time(8, 60, () =>
                {
                    pgx.Clear(System.Drawing.Color.Transparent);
                    Halo.Widgets.Fx.Glow(pgx, w, h, 0.5f, 60f, h / 2f, w * 0.7f, h * 2.2f, 26, accent);
                });

                double fonts = Time(50, 400, () =>
                {
                    using var a = new System.Drawing.Font("Segoe UI", 13f, System.Drawing.GraphicsUnit.Pixel);
                    using var b = new System.Drawing.Font("Segoe UI", 13f, System.Drawing.GraphicsUnit.Pixel);
                    using var c = new System.Drawing.Font("Segoe UI Semibold", 13f, System.Drawing.GraphicsUnit.Pixel);
                    using var d = new System.Drawing.Font("Segoe UI Semibold", 13f, System.Drawing.GraphicsUnit.Pixel);
                });
                using var mf = new System.Drawing.Font("Segoe UI", 13f, System.Drawing.GraphicsUnit.Pixel);
                double measure = Time(50, 400, () =>
                {
                    pgx.MeasureString("running a command", mf, int.MaxValue, System.Drawing.StringFormat.GenericTypographic);
                    pgx.MeasureString("4m 12s", mf, int.MaxValue, System.Drawing.StringFormat.GenericTypographic);
                });
                Console.WriteLine($"  4x new Font{fonts,7:0.00} ms   (per DrawCollapsed call)");
                Console.WriteLine($"  2x Measure {measure,7:0.00} ms");
                Console.WriteLine($"  Fx.PillBar {bar,7:0.00} ms   {w}x{h}");
                Console.WriteLine($"  Fx.Glow    {glow,7:0.00} ms   {w}x{h}  (dest {(int)(w * 1.4f)}x{(int)(h * 4.4f)})");
            }
            Halo.Shell.LayeredNotch.Supersample = 2;

            Console.WriteLine($"+ content    {content,7:0.00} ms   (widget adds {content - cached:0.00})");
            Console.WriteLine($"full Render  {full,7:0.00} ms   ({1000.0 / Math.Max(0.001, full),6:0} fps)");
            Console.WriteLine($"blit+dib     {full - content:0.00} ms   (what Render does beyond the drawing)");
        });
        th.SetApartmentState(System.Threading.ApartmentState.STA);
        th.Start();
        th.Join();
    }

    private static void ProbeDisplay()
    {
        var th = new System.Threading.Thread(() =>
        {
            var notch = new Halo.Shell.LayeredNotch();
            var info = Halo.Interop.Display.Probe(notch.Hwnd);
            Console.WriteLine($"hwnd     {notch.Hwnd}");
            Console.WriteLine($"refresh  {(info.Hz > 0 ? info.Hz + " Hz" : "(unreadable)")}");
            Console.WriteLine($"dpi      {(info.Dpi > 0f ? $"{info.Dpi:0.00}x ({info.Dpi * 96f:0} dpi)" : "(unreadable)")}");
            Console.WriteLine($"auto fps {Halo.Shell.NotchController.AutoCeiling(info.Hz)}"
                + (Halo.Shell.NotchController.AutoCeiling(info.Hz) == Halo.Shell.NotchController.MaxFps
                    && info.Hz != Halo.Shell.NotchController.MaxFps ? "  (fallback - the reading was not usable)" : ""));
            Console.WriteLine($"period   {Halo.Shell.NotchController.IntervalMs(Halo.Shell.NotchController.AutoCeiling(info.Hz)):0.000} ms");
        });
        th.SetApartmentState(System.Threading.ApartmentState.STA);
        th.Start();
        th.Join();
    }

    private static byte[] SampleCover()
    {
        using var art = new System.Drawing.Bitmap(320, 320);
        using (var ag = System.Drawing.Graphics.FromImage(art))
        {
            using var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, 320, 320),
                System.Drawing.Color.FromArgb(255, 236, 84, 60),
                System.Drawing.Color.FromArgb(255, 42, 30, 120), 35f);
            ag.FillRectangle(lg, 0, 0, 320, 320);
            ag.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var ring = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 255, 236, 180), 16f);
            ag.DrawEllipse(ring, 70, 70, 180, 180);
            using var dot = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 20, 18, 26));
            ag.FillEllipse(dot, 145, 145, 30, 30);
        }
        using var ms = new System.IO.MemoryStream();
        art.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static void RenderMorph(string outPath, string which)
    {
        var th = new System.Threading.Thread(() =>
        {

            float[] steps = [0f, 0.10f, 0.20f, 0.32f, 0.45f, 0.58f, 0.72f, 0.86f, 1f];
            const int pad = 16, cellW = 560, capH = 18;
            Halo.Widgets.IWidget widget;
            if (which == "vlc")
            {

                Halo.Widgets.VlcMonitor.ExePath = VlcExe();
                widget = new Halo.Widgets.VlcWidget(new Halo.Widgets.MediaSessions());
            }
            else
            {
                var media = new Halo.Widgets.MediaWidget(new Halo.Widgets.MediaSessions(), 0);
                media.Seed("Bohemian Rhapsody", "Queen", SampleCover(), 0.42);
                widget = media;
            }

            float tall = pad;
            foreach (float t in steps) tall += 40 + (220 - 40) * t + capH + pad;
            using var bmp = new System.Drawing.Bitmap(cellW + pad * 2, (int)tall + pad);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Color.FromArgb(255, 24, 26, 33),
                    System.Drawing.Color.FromArgb(255, 48, 32, 44), 60f))
                    g.FillRectangle(lg, 0, 0, bmp.Width, bmp.Height);

                using var cap = new System.Drawing.Font("Consolas", 12f, System.Drawing.GraphicsUnit.Pixel);
                using var capBrush = new System.Drawing.SolidBrush(
                    System.Drawing.Color.FromArgb(160, 255, 255, 255));
                var notch = new Halo.Shell.LayeredNotch();
                float y = pad;
                foreach (float t in steps)
                {

                    int w = (int)(220 + (560 - 220) * t);
                    int h = (int)(40 + (220 - 40) * t);
                    int r = (int)(20 + (30 - 20) * t);
                    float mini = Halo.Shell.NotchController.MiniFade(t);
                    float content = Halo.Shell.NotchController.ContentFade(t);
                    var st = g.Save();
                    g.TranslateTransform(pad + (cellW - w) / 2f, y);

                    g.SetClip(new System.Drawing.RectangleF(0, 0, w, h));
                    notch.DrawShape(g, w, h, r, 190, glass: false);

                    if (which == "vlc") Halo.Widgets.VlcMonitor.Name = "Interstellar.2014.2160p.mkv";
                    if (mini > 0.01f) widget.DrawCollapsed(g, w, h, mini);
                    widget.DrawContent(g, w, h, content);
                    g.Restore(st);

                    var art = Halo.Widgets.MediaWidget.ArtRect(h);
                    g.DrawString(
                        $"t={t:0.00}  h={h,3}  preview={mini:0.00}  panel={content:0.00}  ink={mini + content:0.00}"
                        + $"  shared art={art.Width:0.0}px @ {art.X:0.0},{art.Y:0.0}",
                        cap, capBrush, pad, y + h + 3);
                    y += h + capH + pad;
                }
            }
            bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"wrote {outPath}");
        });
        th.SetApartmentState(System.Threading.ApartmentState.STA);
        th.Start();
        th.Join();
    }

    private static string? VlcExe()
    {
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("vlc"))
                using (p)
                {
                    try { if (p.MainModule?.FileName is { } f) return f; } catch { }
                }
        }
        catch { }
        foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            try
            {
                string p = System.IO.Path.Combine(Environment.GetFolderPath(root), "VideoLAN", "VLC", "vlc.exe");
                if (System.IO.File.Exists(p)) return p;
            }
            catch { }
        }
        return null;
    }

    private static void RenderGreeting(string outPath)
    {

        float[] install = [0.05f, 0.20f, 0.36f, 0.51f, 0.59f, 0.70f, 0.85f, 0.97f];
        float[] login = [0.10f, 0.35f, 0.62f, 0.88f];

        const int cellW = 620, pad = 18;

        float tall = 40f;
        foreach (float t in install) tall += Halo.Shell.GreetingPlan.Install(t).PillH + pad;
        foreach (float t in login) tall += Halo.Shell.GreetingPlan.Login(t).PillH + pad;
        using var bmp = new System.Drawing.Bitmap(cellW + pad * 2, (int)tall + pad * 4);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Color.FromArgb(255, 22, 26, 34),
                System.Drawing.Color.FromArgb(255, 46, 30, 40), 60f))
                g.FillRectangle(lg, 0, 0, bmp.Width, bmp.Height);

            using var cap = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.GraphicsUnit.Pixel);
            using var capBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(150, 255, 255, 255));
            var notch = new Halo.Shell.LayeredNotch();
            float y = pad;

            g.DrawString("install - the pill opens, writes, clears, then says who it is", cap, capBrush, pad, y);
            y += 20f;
            foreach (float t in install)
            {
                var s = Halo.Shell.GreetingPlan.Install(t);
                int w = (int)s.PillW, h = (int)s.PillH;
                float x = pad + (cellW - w) / 2f;
                var st = g.Save();
                g.TranslateTransform(x, y);
                notch.DrawShape(g, w, h, (int)s.Radius, 190, glass: false);
                var box = Halo.Widgets.Greeting.InkBox(w, h);
                Halo.Widgets.Greeting.DrawHello(g, box, s.Written, s.HelloAlpha,
                    System.Drawing.Color.White, 9f);
                if (s.LineAlpha > 0f)
                    Halo.Widgets.Greeting.DrawLine(g, Halo.Widgets.Greeting.Lines[s.LineIndex], box,
                        s.LineWritten, s.LineAlpha, System.Drawing.Color.White, 9f);
                g.Restore(st);
                g.DrawString($"t={t:0.00}", cap, capBrush, pad, y + h - 14f);
                y += h + pad;
            }

            y += pad;
            g.DrawString("login - the same hand, inside a pill that never opens", cap, capBrush, pad, y);
            y += 20f;
            foreach (float t in login)
            {
                var s = Halo.Shell.GreetingPlan.Login(t);
                int w = (int)s.PillW, h = (int)s.PillH;
                float x = pad + (cellW - w) / 2f;
                var st = g.Save();
                g.TranslateTransform(x, y);
                notch.DrawShape(g, w, h, (int)s.Radius, 190, glass: false);
                Halo.Widgets.Greeting.DrawHello(g, Halo.Widgets.Greeting.InkBox(w, h),
                    s.Written, s.HelloAlpha, System.Drawing.Color.White, 11f);
                g.Restore(st);
                g.DrawString($"t={t:0.00}", cap, capBrush, pad, y + h - 4f);
                y += h + pad;
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"wrote {outPath}");
    }

    private static void RenderAsk(string outPath)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(20);
        var question = new Halo.ClaudeCode.PendingAsk(
            "n1", 100, "sess", "AskUserQuestion", null,

            "\u062F\u0627\u0631\u06CC \u0647\u0645\u06CC\u0646 "
                + "\u067E\u0646\u0644 \u0631\u0627 \u0631\u0648\u06CC "
                + "\u062F\u0633\u06A9\u062A\u0627\u067E "
                + "\u0645\u06CC\u200C\u0628\u06CC\u0646\u06CC \u2014 "
                + "\u062A\u06CC\u0631\u06AF\u06CC \u067E\u0634\u062A\u0634 "
                + "(tint 150) \u0686\u0637\u0648\u0631 "
                + "\u0627\u0633\u062A\u061F",
            [new Halo.ClaudeCode.AskOption("Cadence", "the CPU one"),
             new Halo.ClaudeCode.AskOption("Icon", "the visible one"),
             new Halo.ClaudeCode.AskOption("Measure more first",
                 "no code yet - sit on the profiler until the regression names itself, "
                 + "which is the option that costs a day and saves three")], expires);

        var permission = new Halo.ClaudeCode.PendingAsk(
            "n2", 100, "sess", "Bash", "git push --force-with-lease origin master", null,
            [new Halo.ClaudeCode.AskOption("allow", "run it"),
             new Halo.ClaudeCode.AskOption("deny", "skip it")], expires);

        int W = Halo.Widgets.AskBanner.W, pad = 24;
        int h1 = Halo.Widgets.AskBanner.Height(question, W);
        int h2 = Halo.Widgets.AskBanner.Height(permission, W);

        int[] tints =
        [
            Halo.Shell.NotchController.TintAskDesk,
            Halo.Shell.NotchController.TintAskApp,
        ];
        int total = h1 * tints.Length + h2 + pad * (tints.Length + 2);
        using var bmp = new System.Drawing.Bitmap(W + pad * 2, total);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {

            using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, W + pad * 2, total),
                System.Drawing.Color.FromArgb(70, 150, 210), System.Drawing.Color.FromArgb(210, 110, 70), 35f))
                g.FillRectangle(lg, 0, 0, W + pad * 2, total);

            using (var wb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(238, 240, 244)))
            using (var kb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(18, 18, 20)))
                for (int i = 0; i < tints.Length + 1; i++)
                {
                    g.FillRectangle(wb, 0, pad + i * (h1 + pad) + 92, W + pad * 2, 74);
                    g.FillRectangle(kb, 0, pad + i * (h1 + pad) + 176, W + pad * 2, 74);
                }

            g.TranslateTransform(pad, pad);
            for (int i = 0; i < tints.Length; i++)
            {

                string? typed = i == 1 ? "\u0633\u0644\u0627\u0645 - profile first" : null;
                new Halo.Shell.LayeredNotch().DrawShape(g, W, h1, 26, tints[i], glass: false);
                Halo.Widgets.AskBanner.Draw(g, W, h1, 1f, question, hover: 1, tints[i], typed);
                g.TranslateTransform(0, h1 + pad);
            }

            new Halo.Shell.LayeredNotch().DrawShape(g, W, h2, 26, tints[^1], glass: false);
            Halo.Widgets.AskBanner.Draw(g, W, h2, 1f, permission, hover: -1, tints[^1], closeHover: true);
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderNotif(string outPath)
    {

        int W = Halo.Widgets.NotifBanner.W, H = Halo.Widgets.NotifBanner.SummaryH, pad = 24, detailRoom = 340;
        using var bmp = new System.Drawing.Bitmap(W + pad * 2, H * 2 + detailRoom + pad * 4);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, W + pad * 2, H * 2 + pad * 3),
                System.Drawing.Color.FromArgb(70, 150, 210), System.Drawing.Color.FromArgb(210, 110, 70), 35f))
                g.FillRectangle(lg, 0, 0, W + pad * 2, H * 2 + pad * 3);
            g.TranslateTransform(pad, pad);

            using var icon = new System.Drawing.Bitmap(64, 64);
            using (var ig = System.Drawing.Graphics.FromImage(icon))
            {
                ig.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                ig.Clear(System.Drawing.Color.Transparent);
                using var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 40, 150, 235));
                ig.FillEllipse(b, 2, 2, 60, 60);
            }

            using var shot = new System.Drawing.Bitmap(1920, 1080);
            using (var sgg = System.Drawing.Graphics.FromImage(shot))
            {
                using var lg2 = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new System.Drawing.Rectangle(0, 0, 1920, 1080),
                    System.Drawing.Color.FromArgb(30, 30, 40), System.Drawing.Color.FromArgb(90, 60, 120), 45f);
                sgg.FillRectangle(lg2, 0, 0, 1920, 1080);
                using var wf = new System.Drawing.Font("Segoe UI", 120f);
                sgg.DrawString("desktop", wf, System.Drawing.Brushes.White, 500, 450);
            }
            new Halo.Shell.LayeredNotch().DrawShape(g, W, H, 26, 245, glass: false);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var n = new Halo.Notifications.NotifItem
            {
                Icon = icon,

                App = Halo.Notifications.NotifItem.ScreenshotApp,
                Title = Halo.Notifications.NotifItem.ScreenshotTitle,

                Body = "Saved to the clipboard. Click the banner to edit it, or press Ctrl+V in any "
                     + "app to paste it straight in.",
                Code = "482913",
                Preview = shot,
            };
            Halo.Widgets.NotifBanner.Draw(g, W, H, 1f, n, 0f, false);

            g.TranslateTransform(0, H + pad);
            new Halo.Shell.LayeredNotch().DrawShape(g, W, H, 26, 245, glass: false);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Halo.Widgets.NotifBanner.Draw(g, W, H, 1f, new Halo.Notifications.NotifItem
            {
                Icon = icon,
                App = "Telegram",
                Title = "\u0633\u0644\u0627\u0645",
                Body = "\u0628\u0632\u0646 \u0628\u0631\u06cc\u0645",

                Stacked = 5,
            }, 0f, false);

            var mixed = new Halo.Notifications.NotifItem
            {
                Icon = icon,
                App = "ChatGPT",
                Title = "\u0633\u0627\u062e\u062a \u067e\u0646\u0644 \u0645\u062f\u06cc\u0631\u06cc\u062a \u062f\u0633\u062a\u0631\u0633\u06cc Halo",
                Body = "\u0627\u0648\u06a9\u06cc\u060c \u0622\u062e\u0631\u06cc\u0646 \u0627\u0646\u062a\u062e\u0627\u0628: \u0645\u0646\u0648 \u0633\u0645\u062a \u0686\u067e\u060c \u0645\u062d\u062a\u0648\u0627\u06cc \u062a\u0646\u0638\u06cc\u0645\u0627\u062a \u0633\u0645\u062a \u0631\u0627\u0633\u062a. "
                     + "Halo | Media | [ Enabled ] | General | Appearance | Playback | "
                     + "Auto-show on track change | FEATURES | Include VLC | Show collapsed progress | "
                     + "Downloads | File Tray | Bluetooth | Follow active player | Notifications | "
                     + "Idle timeout 15 sec | Claude",
            };
            int dh = Math.Min(detailRoom, Halo.Widgets.NotifBanner.DetailHeight(mixed));
            g.TranslateTransform(0, H + pad);
            new Halo.Shell.LayeredNotch().DrawShape(g, W, dh, 26, 245, glass: false);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Halo.Widgets.NotifBanner.Draw(g, W, dh, 1f, mixed, 1f, true);
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderBar(string outPath, string? accentHex, string? fracStr)
    {
        const int W = 220, H = 40, Zoom = 2, Rows = 7, Pad = 8;
        var accent = System.Drawing.Color.FromArgb(228, 168, 64);
        float frac = fracStr != null
            ? float.Parse(fracStr, System.Globalization.CultureInfo.InvariantCulture) : 0.42f;
        if (accentHex != null)
            accent = System.Drawing.Color.FromArgb(
                (int)(uint.Parse(accentHex, System.Globalization.NumberStyles.HexNumber) | 0xFF000000));
        using var bmp = new System.Drawing.Bitmap(W * Zoom + Pad * 2 + 150,
            (H * Zoom + Pad) * Rows + Pad, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(255, 18, 18, 21));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var label = new System.Drawing.Font("Segoe UI", 13f, System.Drawing.GraphicsUnit.Pixel);
        using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(225, 225, 230));

        for (int r = 0; r < Rows; r++)
        {
            bool paused = r == Rows - 1;
            g.DrawString(paused ? "paused" : $"playing +{r * 430}ms", label, lb, 8, Pad + r * (H * Zoom + Pad) + 26);
            using var pill = new System.Drawing.Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var pg = System.Drawing.Graphics.FromImage(pill))
            {
                pg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var back = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 12, 12, 14)))
                using (var pp = Halo.Widgets.Fx.PillPath(W, H, H / 2f))
                    pg.FillPath(back, pp);

                Halo.Widgets.Fx.PillBar(pg, W, H, 1f, frac, accent, 0.5f, alive: !paused);

                Halo.Widgets.MediaWidget.ArtGlow(pg, W, H, 1f, accent);
            }
            g.DrawImage(pill, new System.Drawing.Rectangle(150, Pad + r * (H * Zoom + Pad), W * Zoom, H * Zoom));
            if (!paused) System.Threading.Thread.Sleep(430);
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine(outPath);
    }

    private static void ProbeSeek(double secs, int count)
    {
        var sessions = new Halo.Widgets.MediaSessions();
        for (int i = 0; i < 40 && sessions.Session(0) is null; i++) System.Threading.Thread.Sleep(100);
        var s = sessions.Session(0);
        if (s is null) { Console.WriteLine("no session"); return; }

        var widget = new Halo.Widgets.MediaWidget(sessions, 0);

        using var scratch = new System.Drawing.Bitmap(220, 40,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var sg = System.Drawing.Graphics.FromImage(scratch);
        void Pump() { try { widget.DrawCollapsed(sg, 220, 40, 1f); } catch { } }
        for (int i = 0; i < 20 && widget.RingProgress < 0f; i++) { Pump(); System.Threading.Thread.Sleep(100); }

        Console.WriteLine($"before   pos={s.GetTimelineProperties().Position}");

        for (int n = 1; n <= count; n++)
        {
            widget.SeekByForProbe((int)secs);

            int gap = int.TryParse(Environment.GetEnvironmentVariable("HALO_SEEK_GAP"), out var gv) ? gv : 120;
            for (int k = 0; k < Math.Max(1, gap / 100); k++) { Pump(); System.Threading.Thread.Sleep(100); }
            Console.WriteLine($"tap {n}    player={s.GetTimelineProperties().Position}"
                + $"  widget={widget.PositionForProbe}  ring={widget.RingProgress:0.0000}");
        }
        for (int i = 1; i <= 16; i++)
        {
            System.Threading.Thread.Sleep(400);
            Pump();
            var now = s.GetTimelineProperties();
            Console.WriteLine($"  +{i * 400,4}ms pos={now.Position}  updated={now.LastUpdatedTime:HH:mm:ss.fff}"
                + $"  widget.RingProgress={widget.RingProgress:0.0000}");
        }
    }

    private static void ProbeBehind(int secs)
    {
        Win32.OleInitialize(IntPtr.Zero);
        var notch = new Halo.Shell.LayeredNotch();
        notch.Show();
        System.Threading.Thread.Sleep(300);

        int agree = 0, total = 0;
        for (int i = 0; i < Math.Max(1, secs) * 2; i++)
        {
            bool nowDesk = notch.ProbeBehind(out var now);
            bool wasDesk = notch.ProbeBehindByHiding(out var was);
            total++;
            bool same = nowDesk == wasDesk && now == was;
            if (same) agree++;
            Console.WriteLine($"{(same ? "  ok  " : "DIFFER")} now={Describe(now, nowDesk),-44}"
                + $" original={Describe(was, wasDesk)}");
            System.Threading.Thread.Sleep(400);
        }
        Console.WriteLine($"\nagreed with the original on {agree}/{total}");
    }

    private static void ProbeFullscreen(int secs)
    {
        int cx = Win32.GetSystemMetrics(Win32.SM_CXSCREEN), cy = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);
        var lines = new System.Collections.Generic.List<string>();
        void Say(string s) { Console.WriteLine(s); lines.Add(s); }

        Say($"screen {cx}x{cy} - sampling every 8ms for {secs}s. switch between your windows now.");
        Say("a YES on anything that is not a game is the bug: that frame hides the pill.");

        IntPtr lastFg = new(-1);
        bool lastVerdict = false, first = true;
        int flips = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < secs)
        {
            var fg = Win32.GetForegroundWindow();
            bool desktop = Halo.Shell.LayeredNotch.IsDesktopWindow(fg);
            Win32.GetWindowRect(fg, out var r);

            bool verdict = fg != IntPtr.Zero && !desktop
                && Halo.Shell.LayeredNotch.CoversScreen(r, cx, cy);
            if (first || fg != lastFg || verdict != lastVerdict)
            {
                if (!first && verdict != lastVerdict) flips++;
                Say($"{clock.Elapsed.TotalSeconds,7:0.000}s  fullscreen={(verdict ? "YES" : "no "),-3}"
                    + $"  rect={r.left},{r.top} {r.right}x{r.bottom}  {(desktop ? "[shell] " : "")}"
                    + $"{Halo.Shell.LayeredNotch.ClassNameOf(fg)}  {Describe(fg, false)}");
                lastFg = fg; lastVerdict = verdict; first = false;
            }
            System.Threading.Thread.Sleep(8);
        }
        Say($"the verdict flipped {flips} time(s) in {secs}s");

        try
        {
            string log = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Halo", "fullscreen-probe.txt");
            System.IO.File.WriteAllLines(log, lines);
            Console.WriteLine("wrote " + log);
        }
        catch { }
    }

    private static string Describe(IntPtr hwnd, bool isDesktop)
    {
        if (isDesktop || hwnd == IntPtr.Zero) return "desktop";
        try
        {
            var buf = new System.Text.StringBuilder(200);
            Win32.GetWindowTextW(hwnd, buf, buf.Capacity);
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            string proc = "?";
            try { using var p = System.Diagnostics.Process.GetProcessById((int)pid); proc = p.ProcessName; }
            catch { }
            string title = buf.ToString();
            if (title.Length > 28) title = title[..28];
            return $"{proc}:'{title}'";
        }
        catch { return hwnd.ToString(); }
    }

    private static void ProbeSubpixel()
    {
        const int W = 220, H = 40;
        var accent = System.Drawing.Color.FromArgb(255, 232, 96, 120);
        double prev = 0;
        Console.WriteLine("frac        fill px    ink        change");
        for (int i = 0; i <= 14; i++)
        {
            float frac = 0.30f + i * 0.00072f;
            using var bmp = new System.Drawing.Bitmap(W, H,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Halo.Widgets.Fx.PillBar(g, W, H, 1f, frac, accent, 0.5f, alive: false, track: false, decorated: false);
            }
            double ink = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    ink += c.A / 255.0 * (c.R + c.G + c.B) / 3.0;
                }
            Console.WriteLine($"{frac:0.00000}   {W * frac,7:0.000}   {ink,10:0.0}   {(i == 0 ? "" : (ink - prev).ToString("+0.0;-0.0; 0.0"))}");
            prev = ink;
        }
    }

    private static void ProbeBar(int secs)
    {
        var sessions = new Halo.Widgets.MediaSessions();
        for (int i = 0; i < 40 && sessions.Session(0) is null; i++) System.Threading.Thread.Sleep(100);
        if (sessions.Session(0) is null) { Console.WriteLine("no session"); return; }
        var widget = new Halo.Widgets.MediaWidget(sessions, 0);
        System.Threading.Thread.Sleep(600);

        long t0 = Environment.TickCount64;
        float lastFrac = -1f;
        int lastPx = -1;
        for (int i = 0; i < Math.Max(1, secs) * 10; i++)
        {
            float frac = widget.RingProgress;
            int px = frac < 0 ? -1 : (int)(220 * frac);
            string dF = lastFrac < 0 || frac < 0 ? "" : $"  d={frac - lastFrac:+0.000000;-0.000000; 0.000000}";
            string dP = lastPx < 0 || px < 0 ? "" : (px != lastPx ? $"  PIXEL MOVED {lastPx}->{px}" : "");
            Console.WriteLine($"{(Environment.TickCount64 - t0) / 1000.0,6:0.0}s  frac={frac,8:0.00000}  px={px,4}{dF}{dP}");
            lastFrac = frac;
            lastPx = px;
            System.Threading.Thread.Sleep(100);
        }
    }

    private static void ProbeArt(int secs, bool nudge)
    {
        var sessions = new Halo.Widgets.MediaSessions();
        for (int i = 0; i < 40 && sessions.Session(0) is null; i++) System.Threading.Thread.Sleep(100);
        var s = sessions.Session(0);
        if (s is null) { Console.WriteLine("no session"); return; }
        try { Console.WriteLine($"app='{sessions.SlotApp(0)}'  aumid='{s.SourceAppUserModelId}'"); } catch { }
        Console.WriteLine("skip to a fresh track now; seek inside the player once the cover is late");

        long t0 = Environment.TickCount64;
        string Stamp() => $"{(Environment.TickCount64 - t0) / 1000.0,6:0.0}s";

        s.MediaPropertiesChanged += (_, __) => Console.WriteLine($"{Stamp()}  << MediaPropertiesChanged");
        s.TimelinePropertiesChanged += (_, __) => Console.WriteLine($"{Stamp()}  << TimelinePropertiesChanged");

        string? last = null;
        bool nudged = false;
        for (int i = 0; i < Math.Max(1, secs) * 2; i++)
        {
            try
            {
                var props = s.TryGetMediaPropertiesAsync().AsTask().GetAwaiter().GetResult();
                var tl = s.GetTimelineProperties();

                string thumbState;
                byte[]? thumb = null;
                if (props?.Thumbnail is null) thumbState = "NO-REF (player published nothing)";
                else
                {
                    try
                    {
                        thumb = Halo.Widgets.MediaWidget.ReadThumbForProbe(props.Thumbnail).GetAwaiter().GetResult();
                        thumbState = thumb is { Length: > 0 }
                            ? thumb.Length + "B"
                            : "REF-BUT-EMPTY (we opened it and got 0 bytes)";
                    }
                    catch (Exception ex) { thumbState = "READ-THREW " + ex.GetType().Name + ": " + ex.Message; }
                }
                string title = props?.Title ?? "";
                if (title != last)
                {
                    t0 = Environment.TickCount64;
                    last = title;
                    nudged = false;
                    Console.WriteLine($"{Stamp()}  TRACK '{title}'");
                }
                Console.WriteLine($"{Stamp()}  thumb={thumbState}"
                    + $"  pos={tl.Position:mm\\:ss}");

                if (nudge && !nudged && thumb is not { Length: > 0 } && Environment.TickCount64 - t0 > 20_000)
                {
                    nudged = true;
                    Console.WriteLine($"{Stamp()}  >> nudge: seek to the position it is already at");
                    try { s.TryChangePlaybackPositionAsync(tl.Position.Ticks).AsTask().GetAwaiter().GetResult(); }
                    catch (Exception ex) { Console.WriteLine($"{Stamp()}  nudge failed: {ex.Message}"); }
                }
            }
            catch (Exception ex) { Console.WriteLine($"{Stamp()}  poll failed: {ex.Message}"); }
            System.Threading.Thread.Sleep(500);
        }
    }

    private static void ProbeTimeline()
    {
        var sessions = new Halo.Widgets.MediaSessions();

        for (int i = 0; i < 40 && sessions.Session(0) is null; i++) System.Threading.Thread.Sleep(100);
        var slots = new Halo.Widgets.MediaWidget[Halo.Widgets.MediaSessions.MaxSlots];
        for (int s = 0; s < slots.Length; s++) slots[s] = new Halo.Widgets.MediaWidget(sessions, s);
        for (int i = 0; i < 30; i++)
        {
            for (int s = 0; s < slots.Length; s++)
            {
                if (sessions.Session(s) is null) continue;
                Console.WriteLine($"{i * 0.5,5:0.0}s [{s}] {slots[s].ProbeLine() ?? "session hooked, no title yet"}");
            }
            System.Threading.Thread.Sleep(500);
        }
    }

    private static void ProbeMedia()
    {
        var sessions = new Halo.Widgets.MediaSessions();
        for (int i = 0; i < 40 && sessions.Session(0) is null; i++) System.Threading.Thread.Sleep(100);

        for (int slot = 0; slot < Halo.Widgets.MediaSessions.MaxSlots; slot++)
        {
            var s = sessions.Session(slot);
            if (s is null) { Console.WriteLine($"slot {slot}   (empty)"); continue; }
            string? aumid = null;
            try { aumid = s.SourceAppUserModelId; } catch { }
            Console.WriteLine($"slot {slot}   app='{sessions.SlotApp(slot)}'  aumid='{aumid}'");

            bool thumb = false;
            try
            {
                var props = s.TryGetMediaPropertiesAsync().AsTask().GetAwaiter().GetResult();
                thumb = props?.Thumbnail != null;
                Console.WriteLine($"          title='{props?.Title}'  thumbnail={(thumb ? "yes" : "NONE")}");
            }
            catch (Exception ex) { Console.WriteLine("          properties failed: " + ex.Message); }

            try
            {
                var tl = s.GetTimelineProperties();
                var pb = s.GetPlaybackInfo();
                Console.WriteLine($"          pos={tl.Position} start={tl.StartTime} end={tl.EndTime}");
                Console.WriteLine($"          minSeek={tl.MinSeekTime} maxSeek={tl.MaxSeekTime}"
                    + $"  lastUpdated={tl.LastUpdatedTime:HH:mm:ss}");
                Console.WriteLine($"          canSeek={pb.Controls.IsPlaybackPositionEnabled}"
                    + $" canRate={pb.Controls.IsPlaybackRateEnabled} rate={pb.PlaybackRate}"
                    + $" state={pb.PlaybackStatus} type={pb.PlaybackType}");
            }
            catch (Exception ex) { Console.WriteLine("          timeline failed: " + ex.Message); }

            var shell = aumid is null ? null : Halo.Notifications.ShellIcon.ForAumid(aumid);
            var exe = Halo.Widgets.AppIcon.ForAumid(aumid);
            var chain = Halo.Widgets.AppIcon.ForSessionApp(aumid);
            Console.WriteLine($"          ShellIcon={(shell is null ? "NULL" : $"{shell.Width}x{shell.Height}")}"
                + $"   AppIcon={(exe is null ? "NULL" : $"{exe.Width}x{exe.Height}")}"
                + $"   chain={(chain is null ? "NULL → the glyph fallback draws" : $"{chain.Width}x{chain.Height}")}");
        }
    }

    private static void RenderGlyphs(string outPath)
    {
        (string glyph, string name)[] rows =
        {
            ("", "media art fallback"),
            ("", "media (menu)"),
            ("", "agent fallback"),
            ("", "download"),
            ("", "robot / generic agent"),
            ("", "bluetooth"),
            ("", "file tray"),
        };
        const int Tile = 22, Zoom = 6, Pad = 10, LabelW = 190;
        int cell = Tile * Zoom;
        int width = LabelW + Pad * 3 + cell * 2, height = Pad + rows.Length * (cell + Pad);

        using var bmp = new System.Drawing.Bitmap(width, height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(255, 24, 24, 28));
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var label = new System.Drawing.Font("Segoe UI", 15f, System.Drawing.GraphicsUnit.Pixel);
        using var head = new System.Drawing.Font("Segoe UI Semibold", 14f, System.Drawing.GraphicsUnit.Pixel);
        using var lb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 235, 240));
        using var white = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        using var tileBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(40, 255, 255, 255));
        using var cross = new System.Drawing.Pen(System.Drawing.Color.FromArgb(120, 255, 90, 140), 1f);

        int y = Pad;
        for (int i = 0; i < rows.Length; i++)
        {
            var (glyph, name) = rows[i];
            g.DrawString(name, label, lb, new System.Drawing.PointF(Pad, y + cell / 2f - 10f));
            if (i == 0)
            {
                g.DrawString("StringFormat", head, lb, new System.Drawing.PointF(LabelW + Pad * 2, 2f));
                g.DrawString("ink", head, lb, new System.Drawing.PointF(LabelW + Pad * 3 + cell, 2f));
            }

            for (int col = 0; col < 2; col++)
            {
                float tx = LabelW + Pad * 2 + col * (cell + Pad);
                var tile = new System.Drawing.RectangleF(tx, y, cell, cell);

                using (var p = Halo.Widgets.Fx.Rounded(tile, 14f * Zoom)) g.FillPath(tileBrush, p);
                g.DrawLine(cross, tx, y + cell / 2f, tx + cell, y + cell / 2f);
                g.DrawLine(cross, tx + cell / 2f, y, tx + cell / 2f, y + cell);

                using var gf = new System.Drawing.Font("Segoe Fluent Icons", Tile * 0.5f * Zoom,
                    System.Drawing.GraphicsUnit.Pixel);
                if (col == 0)
                {
                    using var sf = new System.Drawing.StringFormat(System.Drawing.StringFormat.GenericTypographic)
                    {
                        Alignment = System.Drawing.StringAlignment.Center,
                        LineAlignment = System.Drawing.StringAlignment.Center,
                    };
                    g.DrawString(glyph, gf, white, tile, sf);
                }
                else Halo.Widgets.Fx.GlyphCentred(g, tile, glyph, gf, white);
            }
            y += cell + Pad;
        }

        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine(outPath);
    }

    private static void RenderCopy(string outPath)
    {
        int W = Halo.Widgets.NotifBanner.W, H = Halo.Widgets.NotifBanner.SummaryH;
        const int Zoom = 4, Pad = 6;

        var states = new[] { false, true };
        var shots = new System.Drawing.Bitmap[states.Length];
        var rects = new System.Drawing.RectangleF[states.Length];

        for (int s = 0; s < states.Length; s++)
        {
            var n = new Halo.Notifications.NotifItem
            {
                App = "Aurora", Title = "Verify your sign-in",
                Body = "Your verification code is 482913. It expires in 10 minutes.",
                Code = "482913", Copied = states[s],
            };
            rects[s] = Halo.Widgets.NotifBanner.CopyRect(n, W);
            var full = new System.Drawing.Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = System.Drawing.Graphics.FromImage(full))
            {
                g.Clear(System.Drawing.Color.FromArgb(255, 18, 18, 22));
                Halo.Widgets.NotifBanner.Draw(g, W, H, 1f, n, 0f, false);
            }
            shots[s] = full;
        }

        int cw = (int)Math.Ceiling(rects[0].Width) + Pad * 2;
        int ch = (int)Math.Ceiling(rects[0].Height) + Pad * 2;
        using var bmp = new System.Drawing.Bitmap(cw * Zoom, ch * Zoom * states.Length);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.Clear(System.Drawing.Color.FromArgb(255, 12, 12, 14));
            for (int s = 0; s < states.Length; s++)
            {
                var r = rects[s];
                var src = new System.Drawing.Rectangle((int)r.X - Pad, (int)r.Y - Pad, cw, ch);
                var dst = new System.Drawing.Rectangle(0, s * ch * Zoom, cw * Zoom, ch * Zoom);
                g.DrawImage(shots[s], dst, src, System.Drawing.GraphicsUnit.Pixel);

                float mid = dst.Y + (Pad + r.Height / 2f) * Zoom;
                using var guide = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 255, 70, 70), 1f);
                g.DrawLine(guide, dst.X, mid, dst.Right, mid);
            }
        }
        foreach (var s in shots) s.Dispose();
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderLocal(string outPath)
    {
        int W = Halo.Widgets.NotifBanner.W, H = Halo.Widgets.NotifBanner.SummaryH, pad = 20;
        using var shot = new System.Drawing.Bitmap(1920, 1080);
        using (var sg = System.Drawing.Graphics.FromImage(shot))
        {
            using var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                System.Drawing.Color.FromArgb(240, 245, 250), System.Drawing.Color.FromArgb(150, 190, 235), 45f);
            sg.FillRectangle(lg, 0, 0, 1920, 1080);
            using var wf = new System.Drawing.Font("Segoe UI", 130f);
            sg.DrawString("desktop", wf, System.Drawing.Brushes.DimGray, 430, 440);
        }

        var notices = Halo.Shell.NotchController.SampleLocalNotices(shot);
        using var bmp = new System.Drawing.Bitmap(W + pad * 2, notices.Length * (H + pad) + pad);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Color.FromArgb(60, 140, 200), System.Drawing.Color.FromArgb(200, 100, 60), 35f))
                g.FillRectangle(lg, 0, 0, bmp.Width, bmp.Height);

            var notch = new Halo.Shell.LayeredNotch();
            for (int i = 0; i < notices.Length; i++)
            {
                var state = g.Save();
                g.TranslateTransform(pad, pad + i * (H + pad));
                notch.DrawShape(g, W, H, 26, 245, glass: false);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Halo.Widgets.NotifBanner.Draw(g, W, H, 1f, notices[i], 0f, false);
                using (var guide = new System.Drawing.Pen(System.Drawing.Color.FromArgb(90, 255, 80, 80), 1f))
                    g.DrawLine(guide, 0, H / 2f, W, H / 2f);
                g.Restore(state);
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderBadges(string outPath)
    {
        var badges = Halo.Shell.Badges.All();
        using var bmp = new System.Drawing.Bitmap(badges.Length * 84 + 20, 104);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(System.Drawing.Color.FromArgb(28, 28, 32));
            for (int i = 0; i < badges.Length; i++)
                g.DrawImage(badges[i], 10 + i * 84, 20, 64, 64);
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static IWidget Tray()
    {
        var tray = new FileTray();
        FileTray.SetDragActive(true);
        return tray;
    }

    private static System.Threading.Mutex? _instance;
    private static Halo.Shell.TrayIcon? _tray;

    private static bool _probeCrash;

    private static void OpenSettingsPanel()
    {
        try
        {
            string exe = System.IO.Path.Combine(AppContext.BaseDirectory, "Halo.Settings.exe");
            if (!System.IO.File.Exists(exe)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch { }
    }

    internal static void OpenSettings() => OpenSettingsPanel();

    private static void Teardown()
    {
        try { _tray?.Dispose(); _tray = null; } catch { }
        try { _instance?.ReleaseMutex(); } catch { }
        try { _instance?.Dispose(); _instance = null; } catch { }
    }

    internal static void Quit()
    {
        Teardown();
        Environment.Exit(0);
    }

    internal static void Restart()
    {
        string exe = Environment.ProcessPath ?? "";
        Teardown();
        try
        {
            if (exe.Length > 0)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch { }
        Environment.Exit(0);
    }

    private static void RenderFluent(string outPath, string startOrList, string countArg)
    {
        var codes = new System.Collections.Generic.List<int>();
        if (startOrList.Contains(','))
        {
            foreach (var part in startOrList.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), System.Globalization.NumberStyles.HexNumber, null, out var c)) codes.Add(c);
        }
        else if (int.TryParse(startOrList, System.Globalization.NumberStyles.HexNumber, null, out var start))
        {
            int n = int.TryParse(countArg, out var parsed) ? parsed : 256;
            for (int i = 0; i < n; i++) codes.Add(start + i);
        }
        if (codes.Count == 0) return;

        const int Cell = 92, Cols = 12, Label = 18;
        int rows = (codes.Count + Cols - 1) / Cols;
        using var bmp = new System.Drawing.Bitmap(Cols * Cell, rows * (Cell + Label) + 10);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(24, 24, 28));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var glyphFont = new System.Drawing.Font("Segoe Fluent Icons", 46f, System.Drawing.GraphicsUnit.Pixel);
            using var labelFont = new System.Drawing.Font("Consolas", 14f, System.Drawing.GraphicsUnit.Pixel);
            using var ink = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var dim = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(150, 160, 170));
            using var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center,
            };
            for (int i = 0; i < codes.Count; i++)
            {
                float x = i % Cols * Cell, y = i / Cols * (Cell + Label);
                g.DrawString(((char)codes[i]).ToString(), glyphFont, ink,
                    new System.Drawing.RectangleF(x, y, Cell, Cell), sf);
                g.DrawString(codes[i].ToString("X4"), labelFont, dim,
                    new System.Drawing.RectangleF(x, y + Cell - 4, Cell, Label), sf);
            }
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RenderStoreLogos(string outDir, string iconPath)
    {
        System.IO.Directory.CreateDirectory(outDir);
        using var icon = new System.Drawing.Bitmap(iconPath);

        void One(string name, int w, int h, float iconFrac, float titleY, float tagY)
        {
            using var bmp = new System.Drawing.Bitmap(w, h,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            using (var wall = StoreWallpaper(w, h, 0)) g.DrawImage(wall, 0, 0, w, h);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            float s = w * iconFrac;
            float iy = h * 0.5f - s * 0.5f - h * 0.09f;
            g.DrawImage(icon, w / 2f - s / 2f, iy, s, s);

            var centre = new System.Drawing.StringFormat
            { Alignment = System.Drawing.StringAlignment.Center };
            using var tf = new System.Drawing.Font("Segoe UI Semibold", w * 0.075f,
                System.Drawing.GraphicsUnit.Pixel);
            using var gf = new System.Drawing.Font("Segoe UI", w * 0.038f,
                System.Drawing.GraphicsUnit.Pixel);
            using var tb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(248, 255, 255, 255));
            using var gb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(165, 255, 255, 255));
            g.DrawString("Halo DynamicWin", tf, tb, w / 2f, h * titleY, centre);
            g.DrawString("A Dynamic Island for Windows", gf, gb, w / 2f, h * tagY, centre);

            string path = System.IO.Path.Combine(outDir, name);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"wrote {path}");
        }

        One("poster-720x1080.png", 720, 1080, 0.42f, 0.615f, 0.688f);
        One("box-1080x1080.png", 1080, 1080, 0.34f, 0.640f, 0.762f);
    }

    private static System.Drawing.Bitmap StoreWallpaper(int w, int h, int variant)
    {
        (int r, int g, int b) baseA, baseB;
        (int r, int g, int b) c1, c2, c3;
        switch (variant)
        {
            case 1:
                baseA = (10, 20, 24); baseB = (14, 34, 40);
                c1 = (16, 170, 160); c2 = (110, 190, 80); c3 = (40, 130, 200); break;
            case 2:
                baseA = (10, 14, 30); baseB = (22, 20, 52);
                c1 = (70, 90, 220); c2 = (50, 150, 230); c3 = (130, 80, 210); break;
            case 3:
                baseA = (18, 12, 26); baseB = (38, 16, 38);
                c1 = (150, 80, 220); c2 = (215, 70, 150); c3 = (60, 110, 230); break;
            case 4:
                baseA = (24, 14, 12); baseB = (40, 20, 16);
                c1 = (230, 120, 30); c2 = (205, 60, 60); c3 = (200, 160, 40); break;
            default:
                baseA = (14, 16, 28); baseB = (34, 18, 44);
                c1 = (196, 155, 4); c2 = (44, 165, 224); c3 = (214, 72, 96); break;
        }

        var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
            new System.Drawing.Rectangle(0, 0, w, h),
            System.Drawing.Color.FromArgb(255, baseA.r, baseA.g, baseA.b),
            System.Drawing.Color.FromArgb(255, baseB.r, baseB.g, baseB.b), 70f))
            g.FillRectangle(lg, 0, 0, w, h);

        (float cx, float cy, float r, System.Drawing.Color c)[] blobs =
        [
            (w * 0.30f, h * 0.10f, w * 0.42f, System.Drawing.Color.FromArgb(255, c1.r, c1.g, c1.b)),
            (w * 0.78f, h * 0.30f, w * 0.38f, System.Drawing.Color.FromArgb(255, c2.r, c2.g, c2.b)),
            (w * 0.55f, h * 0.92f, w * 0.45f, System.Drawing.Color.FromArgb(255, c3.r, c3.g, c3.b)),
        ];
        foreach (var (cx, cy, r, c) in blobs)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
            using var pg = new System.Drawing.Drawing2D.PathGradientBrush(path)
            {
                CenterColor = System.Drawing.Color.FromArgb(120, c),
                SurroundColors = [System.Drawing.Color.FromArgb(0, c)],
            };
            g.FillPath(pg, path);
        }
        return bmp;
    }

    private static void StoreShot(string outPath, int pillW, int pillH, int radius,
        Action<System.Drawing.Graphics> drawContent, string headline, string sub, float scale = 1.7f,
        int variant = 0)
    {
        const int W = 1920, H = 1080;

        int dw = (int)(pillW * scale), dh = (int)(pillH * scale);

        using var bmp = new System.Drawing.Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        using (var wall = StoreWallpaper(W, H, variant)) g.DrawImage(wall, 0, 0, W, H);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        int px = (W - dw) / 2, py = 0;
        var notch = new Halo.Shell.LayeredNotch();

        Halo.Shell.LayeredNotch.WantCaptureHeight(pillH);
        int capW = 560, capH = Halo.Shell.LayeredNotch.CaptureH;
        using (var strip = new System.Drawing.Bitmap(capW, capH,
                   System.Drawing.Imaging.PixelFormat.Format24bppRgb))
        {
            using (var sg = System.Drawing.Graphics.FromImage(strip))
            {
                sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                sg.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, capW, capH),
                    new System.Drawing.RectangleF(px - (capW - pillW) * scale / 2f, py,
                        capW * scale, capH * scale),
                    System.Drawing.GraphicsUnit.Pixel);
            }
            notch.SeedBackdrop(Halo.Shell.LayeredNotch.BlurPyramid(strip));
        }

        var st = g.Save();
        g.TranslateTransform(px, py);
        g.ScaleTransform(scale, scale);
        g.SetClip(new System.Drawing.RectangleF(0, 0, pillW, pillH));
        notch.DrawShape(g, pillW, pillH, radius, 190, glass: true);
        drawContent(g);
        g.Restore(st);

        using var hf = new System.Drawing.Font("Segoe UI Semibold", 56f, System.Drawing.GraphicsUnit.Pixel);
        using var sf = new System.Drawing.Font("Segoe UI", 30f, System.Drawing.GraphicsUnit.Pixel);
        using var hb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(248, 255, 255, 255));
        using var sb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(175, 255, 255, 255));
        var centre = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center };
        g.DrawString(headline, hf, hb, W / 2f, 700f, centre);
        g.DrawString(sub, sf, sb, W / 2f, 782f, centre);

        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"wrote {outPath}");
    }

    private static void StoreWarm(Action<System.Drawing.Graphics> draw)
    {
        using var warm = new System.Drawing.Bitmap(560, 220,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var wg = System.Drawing.Graphics.FromImage(warm);
        for (int f = 0; f < 45; f++)
        {
            wg.Clear(System.Drawing.Color.FromArgb(20, 20, 22));
            try { draw(wg); } catch { }
            System.Threading.Thread.Sleep(12);
        }
    }

    private static void RenderStore(string outDir, bool liveMedia, string[]? only)
    {
        var t = new System.Threading.Thread(() =>
        {
            System.IO.Directory.CreateDirectory(outDir);
            string P(string n) => System.IO.Path.Combine(outDir, n);
            bool Want(string name) => only is null || Array.Exists(only,
                s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));

            Halo.Widgets.AudioSpectrum.KeepWarm();

            var media = new Halo.Widgets.MediaWidget(new Halo.Widgets.MediaSessions(), 0);
            if (liveMedia && (Want("media") || Want("pill")))
            {

                for (int i = 0; i < 100 && !media.IsActive; i++) System.Threading.Thread.Sleep(100);
                if (!media.IsActive)
                    Console.WriteLine("no media session found - play something, or drop the 'live' argument");
            }
            else media.Seed("Bohemian Rhapsody", "Queen", SampleCover(), 0.42);
            if (Want("media"))
            {
                StoreWarm(g => media.DrawContent(g, 560, 220, 1f));
                StoreShot(P("01-media.png"), 560, 220, 30, g => media.DrawContent(g, 560, 220, 1f),
                    "Everything that is playing, in one place",
                    "Art, a seek bar that really seeks, volume and transport - read from Windows' own media session",
                    variant: 0);
            }

            string trayDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halo-store", "Documents");
            System.IO.Directory.CreateDirectory(trayDir);
            foreach (var name in new[] { "quarterly-report.pdf", "logo-final.png", "notes.txt",
                                         "invoice-0148.pdf", "screenshot.png", "budget.xlsx" })
            {
                string p = System.IO.Path.Combine(trayDir, name);
                if (!System.IO.File.Exists(p)) System.IO.File.WriteAllText(p, "halo store screenshot sample");
                Halo.Widgets.FileTray.Add(p);
            }
            var tray = new Halo.Widgets.FileTray();
            if (Want("tray"))
            {
                StoreWarm(g => tray.DrawContent(g, 560, 220, 1f));
                StoreShot(P("02-tray.png"), 560, 220, 30, g => tray.DrawContent(g, 560, 220, 1f),
                    "Drop files on the pill and it holds them",
                    "Drag them back out into any window later - a different app, a different desktop, an hour on",
                    variant: 1);
            }

            int nw = Halo.Widgets.NotifBanner.W, nh = Halo.Widgets.NotifBanner.SummaryH;

            var icon = Halo.Notifications.ShellIcon.ForAppName("telegram")
                ?? Halo.Notifications.ShellIcon.ForPath(System.IO.Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                       "Telegram Desktop", "Telegram.exe"));
            if (icon == null)
            {
                Console.WriteLine("telegram icon did not resolve - falling back to a drawn disc");
                var disc = new System.Drawing.Bitmap(64, 64);
                using (var ig = System.Drawing.Graphics.FromImage(disc))
                {
                    ig.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    ig.Clear(System.Drawing.Color.Transparent);
                    using var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 40, 150, 235));
                    ig.FillEllipse(b, 2, 2, 60, 60);
                }
                icon = disc;
            }
            var notif = new Halo.Notifications.NotifItem
            {
                Icon = icon,
                App = "TELEGRAM",
                Title = "Your verification code",
                Body = "Use 482913 to sign in. It expires in ten minutes and works only once.",
                Code = "482913",
            };
            if (Want("notifications"))
                StoreShot(P("03-notifications.png"), nw, nh, 26,
                    g => Halo.Widgets.NotifBanner.Draw(g, nw, nh, 1f, notif, 0f, false),
                    "Every notification, mirrored with its real icon",
                    "Windows' own banner goes quiet, so nothing is said to you twice. A code becomes one click to copy",
                    variant: 2);

            if (Want("agents"))
            {
            string demoRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halo-store-agent");
            System.IO.Directory.CreateDirectory(demoRoot);
            var now = DateTimeOffset.UtcNow;
            System.IO.File.WriteAllText(System.IO.Path.Combine(demoRoot, "status.json"), $$"""
            {
              "pid": {{System.Environment.ProcessId}},
              "sessionId": "demo",
              "cwd": "C:\\Projects\\halo",
              "state": "working",
              "consolePid": {{System.Environment.ProcessId}},
              "updatedAt": "{{now:o}}",
              "startedAt": "{{now.AddMinutes(-12):o}}",
              "currentTool": "Edit",
              "session": { "contextUsed": 341000, "contextMax": 1000000, "promptTokens": 48200 }
            }
            """);
            var agent = new Halo.Widgets.ClaudeCodeWidget(
                new Halo.ClaudeCode.StatusStore(System.IO.Path.Combine(demoRoot, "status.json"),
                    _ => DateTimeOffset.UtcNow.AddMinutes(-12), watchFiles: false), 0, () => { });

            using (var warm = new System.Drawing.Bitmap(560, 220))
            using (var wg = System.Drawing.Graphics.FromImage(warm)) agent.DrawContent(wg, 560, 220, 1f);
            System.Threading.Thread.Sleep(8000);
            SeedAgentDemo(hot: false);
            StoreWarm(g => agent.DrawContent(g, 560, 220, 1f));
            StoreShot(P("04-agents.png"), 560, 220, 30, g => agent.DrawContent(g, 560, 220, 1f),
                "A live panel for every coding session",
                "Context left, your 5-hour and weekly limits, and a stop button that really stops the prompt",
                variant: 3);
            }

            if (Want("pill"))
            {
                Halo.Widgets.AudioSpectrum.KeepWarm();
                bool bars = false;
                for (int i = 0; i < 80 && !bars; i++)
                {
                    bars = Halo.Widgets.AudioSpectrum.Bands() is not null;
                    if (!bars) System.Threading.Thread.Sleep(100);
                }
                if (!bars) Console.WriteLine("no loopback audio - the collapsed pill's bars are the fallback");

                if (liveMedia) media.SeedPosition(0.45);
                StoreWarm(g => media.DrawCollapsed(g, 220, 40, 1f));
                StoreShot(P("05-pill.png"), 220, 40, 20, g => media.DrawCollapsed(g, 220, 40, 1f),
                    "The rest of the time, it stays out of the way",
                    "A small glass pill at the top of the screen. Hover to open it, drag it anywhere, pin it above fullscreen apps",
                    scale: 3.4f, variant: 4);
            }
        });
        t.SetApartmentState(System.Threading.ApartmentState.MTA);
        t.Start();
        t.Join();
    }

    private static void SeedAgentDemo(bool hot)
    {
        Halo.ClaudeCode.Limits.FiveHour = hot ? 0.93f : 0.42f;
        Halo.ClaudeCode.Limits.FiveHourReset = DateTimeOffset.UtcNow.AddHours(2).AddMinutes(48);
        Halo.ClaudeCode.Limits.Week = hot ? 0.78f : 0.61f;
        Halo.ClaudeCode.Limits.WeekReset = DateTimeOffset.UtcNow.AddDays(3).AddHours(5);
        Halo.ClaudeCode.Limits.CreditsUsed = 0;
        Halo.ClaudeCode.Limits.LastSuccess = DateTime.UtcNow.AddMinutes(-2);

        const string demoIp = "203.0.113.24";
        Halo.ClaudeCode.IpCountry.Ip = demoIp;
        Halo.ClaudeCode.IpCountry.ApiIp = null;
        Halo.ClaudeCode.IpCountry.Cc = "NL";
        Halo.ClaudeCode.IpCountry.Isp = "Example ISP";
        Halo.ClaudeCode.IpCountry.Asn = "AS64496";
        try
        {
            using var flagHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            Halo.ClaudeCode.IpCountry.Flag = new System.Drawing.Bitmap(new System.IO.MemoryStream(
                flagHttp.GetByteArrayAsync("https://flagcdn.com/w320/nl.png").Result));
        }
        catch { }
        Halo.ClaudeCode.IpRep.ForIp = demoIp;
        Halo.ClaudeCode.IpRep.Verdict = "residential";
        Halo.ClaudeCode.IpRep.Abuse = null;
        Halo.ClaudeCode.IpRep.Sev = 0;
        Halo.ClaudeCode.IpRep.Tor = false;
        Halo.ClaudeCode.IpRep.Abuser = false;
        Halo.ClaudeCode.IpRep.Bogon = false;
        Halo.ClaudeCode.IpRep.Vpn = false;
        Halo.ClaudeCode.IpRep.Proxy = false;
        Halo.ClaudeCode.IpRep.Datacenter = false;
        Halo.ClaudeCode.DnsLeak.ForIp = demoIp;
        Halo.ClaudeCode.DnsLeak.Running = false;
        Halo.ClaudeCode.DnsLeak.Done = true;
        Halo.ClaudeCode.DnsLeak.Resolvers = 3;
        Halo.ClaudeCode.DnsLeak.Where = "NL";
        Halo.ClaudeCode.DnsLeak.Leaking = false;
    }

    private static void RenderWidget(string outPath, string which, int scale = 1, string[]? args = null)
    {
        var t = new System.Threading.Thread(() =>
        {
            if (which == "download")
            {
                Halo.Widgets.Downloads.Name = "Source.Code.2011.1080p.BluRay.10bit.x265.Farsi.Dubbed.mkv";
                Halo.Widgets.Downloads.Percent = 36;

                Halo.Widgets.Downloads.ExePath = new[]
                {
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                }.FirstOrDefault(System.IO.File.Exists) ?? @"C:\Windows\explorer.exe";
                Halo.Widgets.Downloads.Hwnd = new IntPtr(1);
            }
            if (which == "download-install")
            {
                Halo.Widgets.Downloads.Name = "Microsoft Store";
                Halo.Widgets.Downloads.ExePath = "Microsoft.WindowsStore_8wekyb3d8bbwe!App";
                Halo.Widgets.Downloads.IsStore = true;
                Halo.Widgets.Downloads.Installing = true;
                which = "download";
            }

            string demoRoot = "";

            string codexRoot = "";
            if (which == "codex-demo")
            {
                codexRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halo-codex-demo");
                System.IO.Directory.CreateDirectory(codexRoot);
                var cnow = DateTimeOffset.UtcNow;
                System.IO.File.WriteAllText(System.IO.Path.Combine(codexRoot, "cli.json"), $$"""
                {
                  "pid": {{System.Environment.ProcessId}},
                  "source": "cli",
                  "state": "working",
                  "consolePid": {{System.Environment.ProcessId}},
                  "updatedAt": "{{cnow:o}}",
                  "startedAt": "{{cnow.AddMinutes(-4):o}}",
                  "currentTool": "apply_patch",
                  "contextUsed": 712000,
                  "contextMax": 1000000,
                  "primaryLimit": { "usedPercent": 61, "windowMinutes": 300, "resetsAt": "{{cnow.AddHours(1).AddMinutes(52):o}}" },
                  "secondaryLimit": { "usedPercent": 34, "windowMinutes": 10080, "resetsAt": "{{cnow.AddDays(4):o}}" }
                }
                """);
            }
            bool demo = which is "claude-demo" or "claude-idle" or "claude-hot";

            bool hot = which == "claude-hot";
            if (demo)
            {
                demoRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halo-claude-demo");
                System.IO.Directory.CreateDirectory(demoRoot);
                var now = DateTimeOffset.UtcNow;

                var demoState = which == "claude-idle" ? "idle" : "working";
                long ctxUsed = hot ? 862_000 : 341_000;
                System.IO.File.WriteAllText(System.IO.Path.Combine(demoRoot, "status.json"), $$"""
                {
                  "pid": {{System.Environment.ProcessId}},
                  "sessionId": "demo",
                  "cwd": "C:\\Projects\\halo",
                  "state": "{{demoState}}",
                  "consolePid": {{System.Environment.ProcessId}},
                  "updatedAt": "{{now:o}}",
                  "startedAt": "{{now.AddMinutes(-12):o}}",
                  "currentTool": "Edit",
                  "session": { "contextUsed": {{ctxUsed}}, "contextMax": 1000000, "promptTokens": 48200 }
                }
                """);
                Halo.ClaudeCode.Limits.FiveHour = hot ? 0.93f : 0.42f;
                Halo.ClaudeCode.Limits.FiveHourReset = now.AddHours(2).AddMinutes(48);
            }

            IWidget w = which switch
            {
                "claude-demo" or "claude-idle" or "claude-hot" => new ClaudeCodeWidget(
                    new Halo.ClaudeCode.StatusStore(System.IO.Path.Combine(demoRoot, "status.json"),
                        _ => DateTimeOffset.UtcNow.AddMinutes(-12), watchFiles: false), 0, () => { }),
                "claude" => new ClaudeCodeWidget(new Halo.ClaudeCode.StatusStore(), 0, () => { }),
                "codex-demo" => new CodexWidget(
                    new Halo.Codex.CodexStatusStore(codexRoot, codexRoot, _ => true, watchFiles: false),
                    Halo.Codex.CodexSurface.Cli, () => { }, observeLimits: _ => { }),
                "codex" => new CodexWidget(new Halo.Codex.CodexStatusStore(), Halo.Codex.CodexSurface.Cli, () => { }),
                "download" => new DownloadWidget(),

                "tray" => Tray(),
                _ => new MediaWidget(new MediaSessions(), 0),
            };
            for (int i = 0; i < 100 && !w.IsActive; i++)
                System.Threading.Thread.Sleep(100);
            scale = Math.Clamp(scale, 1, 6);
            if (demo || which == "codex-demo")
            {

                using (var warm = new System.Drawing.Bitmap(560, 220))
                using (var wg = System.Drawing.Graphics.FromImage(warm))
                    w.DrawContent(wg, 560, 220, 1f);
            }

            if (which is "claude" or "codex" or "codex-demo" || demo)
                System.Threading.Thread.Sleep(8000);

            if (demo || which == "codex-demo") SeedAgentDemo(hot);

            if (args is { Length: > 4 } && args[4].Contains(','))
            {
                var xy = args[4].Split(',');
                if (float.TryParse(xy[0], out float mx) && float.TryParse(xy[1], out float my))
                {
                    Halo.Widgets.WidgetInput.Mouse = new System.Drawing.PointF(mx, my);
                    Halo.Widgets.WidgetInput.Over = true;
                }
            }

            if (Environment.GetEnvironmentVariable("HALO_RENDER_NET") == "1")
            {
                Halo.ClaudeCode.IpCountry.Poke();
                System.Threading.Thread.Sleep(5000);
                string? exit = Halo.ClaudeCode.IpCountry.Split
                    ? Halo.ClaudeCode.IpCountry.ApiIp : Halo.ClaudeCode.IpCountry.Ip;
                Halo.ClaudeCode.IpRep.Want(exit);
                Halo.ClaudeCode.DnsLeak.Want(exit,
                    Halo.ClaudeCode.IpCountry.Split ? Halo.ClaudeCode.IpCountry.ApiCc : Halo.ClaudeCode.IpCountry.Cc);
                for (int i = 0; i < 40 && !Halo.ClaudeCode.DnsLeak.Done; i++) System.Threading.Thread.Sleep(500);
            }

            using (var warm = new System.Drawing.Bitmap(560, 220,
                       System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var wg = System.Drawing.Graphics.FromImage(warm))
                for (int f = 0; f < 45; f++)
                {
                    wg.Clear(System.Drawing.Color.FromArgb(20, 20, 22));
                    try { w.DrawContent(wg, 560, 220, 1f); } catch { }
                    System.Threading.Thread.Sleep(12);
                }

            using var bmp = new System.Drawing.Bitmap(560 * scale, 220 * scale);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.FromArgb(20, 20, 22));
                g.ScaleTransform(scale, scale);
                w.DrawContent(g, 560, 220, 1f);
            }
            bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        });
        t.SetApartmentState(System.Threading.ApartmentState.MTA);
        t.Start();
        t.Join();
    }
}

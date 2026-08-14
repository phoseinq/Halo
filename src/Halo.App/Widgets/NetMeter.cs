using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;

namespace Halo.Widgets;

internal sealed class NetMeter
{
    private readonly Dictionary<string, (long Down, long Up)> _last = new();
    private long _stamp;
    private double _downRate, _upRate;
    private bool _on, _upOn;
    private double _quietFor, _upQuietFor;
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Now);

    private DateTime _minute = NetMinutes.MinuteOf(DateTime.Now);
    private long _savedAt;
    private bool _dirty;

    internal NetLedger Ledger { get; private set; } = new();

        internal double DownRate => _downRate;
    internal double UpRate => _upRate;

    internal bool Busy => _on || _upOn;

        internal NetLink? Link { get; private set; }

    internal long? LinkSpeed { get; private set; }
    internal string? LocalIp { get; private set; }
    internal string? Adapter { get; private set; }

    internal const int TraceLen = 60;
    private readonly double[] _trace = new double[TraceLen];
    private int _traceAt;
    private bool _traceWrapped;

        internal double[] TraceSnapshot()
    {
        lock (_trace)
        {
            int count = _traceWrapped ? TraceLen : _traceAt;
            var copy = new double[count];
            for (int i = 0; i < count; i++)
                copy[i] = _trace[(_traceAt - count + i + TraceLen) % TraceLen];
            return copy;
        }
    }

    private void Record(double rate)
    {
        lock (_trace)
        {
            _trace[_traceAt] = rate;
            _traceAt = (_traceAt + 1) % TraceLen;
            if (_traceAt == 0) _traceWrapped = true;
        }
    }

    private readonly Dictionary<string, (long Speed, string? Ip, string Name)> _meta = new();
    private long _metaAt;
    private const long MetaEveryMs = 15_000;

    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "net-usage.tsv");

    private static readonly string HoursPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "net-hours.tsv");

        internal NetHours Hours { get; private set; } = new();

    internal NetMinutes Minutes { get; private set; } = new();

    internal readonly record struct Sample(string Key, NetLink Link, long Rx, long Tx);

        internal static string AdapterKey(string mac, string id)
        => string.IsNullOrEmpty(mac) || mac.All(c => c == '0') ? id : mac;

        internal static List<Sample> Dedupe(IEnumerable<Sample> samples)
    {

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<Sample>();
        foreach (var s in samples) if (seen.Add(s.Key)) kept.Add(s);
        return kept;
    }

    internal NetMeter()
    {

        try { if (File.Exists(Path)) Ledger = NetLedger.Load(File.ReadAllLines(Path)); }
        catch { }
        try { if (File.Exists(HoursPath)) Hours = NetHours.Load(File.ReadAllLines(HoursPath)); }
        catch { }

        try { Hours.Trim(DateTime.Now); } catch { }
    }

        internal void Poll()
    {
        try
        {
            long now = Environment.TickCount64;
            double dt = _stamp == 0 ? 0 : (now - _stamp) / 1000.0;
            _stamp = now;

            var today = DateOnly.FromDateTime(DateTime.Now);
            if (today != _day) { _day = today; Ledger.Trim(today); _dirty = true; }

            var minute = NetMinutes.MinuteOf(DateTime.Now);
            if (minute != _minute) { _minute = minute; Minutes.Trim(DateTime.Now); }

            long down = 0, up = 0;
            NetLink? busiest = null;
            string? busiestKey = null;
            long busiestBytes = 0;
            bool refreshMeta = now - _metaAt > MetaEveryMs;
            if (refreshMeta) { _metaAt = now; _meta.Clear(); }
            var samples = new List<Sample>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var link = LinkOf(nic);
                if (link is not { } kind) continue;
                var stats = nic.GetIPStatistics();
                string key = AdapterKey(nic.GetPhysicalAddress().ToString(), nic.Id);
                samples.Add(new Sample(key, kind, stats.BytesReceived, stats.BytesSent));
                if (refreshMeta) _meta[key] = (nic.Speed, Ipv4Of(nic), nic.Name);
            }
            foreach (var (id, kind, rx, tx) in Dedupe(samples))
            {
                if (_last.TryGetValue(id, out var prev))
                {
                    long dRx = NetRate.Delta(prev.Down, rx), dTx = NetRate.Delta(prev.Up, tx);
                    if (dRx > 0 || dTx > 0)
                    {
                        Ledger.Add(today, kind, dRx, dTx);

                        Hours.Add(DateTime.Now, kind, dRx, dTx);
                        Minutes.Add(DateTime.Now, kind, dRx, dTx);
                        _dirty = true;
                        down += dRx; up += dTx;

                        if (dRx + dTx > busiestBytes) { busiestBytes = dRx + dTx; busiest = kind; busiestKey = id; }
                    }
                }
                _last[id] = (rx, tx);
            }

            _downRate = NetRate.Smooth(_downRate, NetRate.PerSecond(down, dt));
            _upRate = NetRate.Smooth(_upRate, NetRate.PerSecond(up, dt));

            Record(_downRate + _upRate);

            var latch = NetRate.Latch(_on, _downRate, _quietFor, dt);
            _on = latch.On;
            _quietFor = latch.QuietFor;
            var upLatch = NetRate.Latch(_upOn, _upRate, _upQuietFor, dt,
                                        NetRate.UpOnBytesPerSec);
            _upOn = upLatch.On;
            _upQuietFor = upLatch.QuietFor;
            if (busiest is not null) Link = busiest;

            if (busiestKey is not null && _meta.TryGetValue(busiestKey, out var m))
            {
                LinkSpeed = m.Speed > 0 ? m.Speed : null;
                LocalIp = m.Ip;
                Adapter = m.Name;
            }

            if (_dirty && now - _savedAt > 30_000) Save(now);
        }
        catch { }
    }

        internal void Flush() => Save(Environment.TickCount64);

        internal void Seed(double down, double up, NetLink link, NetLedger ledger, NetHours? hours = null,
                       NetMinutes? minutes = null)
    {
        if (hours is not null) Hours = hours;

        if (minutes is not null) Minutes = minutes;

        _downRate = down;
        _upRate = up;
        _on = true;
        Link = link;
        Ledger = ledger;

        LinkSpeed = 866_000_000;
        LocalIp = "192.168.1.34";
        Adapter = "Wi-Fi";
    }

        internal void SeedTrace(IEnumerable<double> bytesPerSecond)
    {
        foreach (double v in bytesPerSecond) Record(v);
    }

    private void Save(long now)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

            string tmp = Path + ".tmp";
            File.WriteAllLines(tmp, Ledger.Save());
            File.Move(tmp, Path, overwrite: true);
            _savedAt = now;
            _dirty = false;
        }
        catch { }

        try
        {
            Hours.Trim(DateTime.Now);
            string tmp = HoursPath + ".tmp";
            File.WriteAllLines(tmp, Hours.Save());
            File.Move(tmp, HoursPath, overwrite: true);
        }
        catch { }
    }

    private static string? Ipv4Of(NetworkInterface nic)
    {
        try
        {
            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                string text = addr.Address.ToString();
                if (text.StartsWith("169.254", StringComparison.Ordinal)) continue;
                return text;
            }
        }
        catch { }
        return null;
    }

    internal static NetLink? LinkOf(NetworkInterface nic)
    {
        if (nic.OperationalStatus != OperationalStatus.Up) return null;
        return nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => NetLink.Wifi,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => NetLink.Lan,
            _ => null,
        };
    }
}

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
    private long _savedAt;
    private bool _dirty;

    internal NetLedger Ledger { get; private set; } = new();

        internal double DownRate => _downRate;
    internal double UpRate => _upRate;

    internal bool Busy => _on || _upOn;

        internal NetLink? Link { get; private set; }

    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "net-usage.tsv");

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

            long down = 0, up = 0;
            NetLink? busiest = null;
            long busiestBytes = 0;
            var samples = new List<Sample>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var link = LinkOf(nic);
                if (link is not { } kind) continue;
                var stats = nic.GetIPStatistics();
                samples.Add(new Sample(AdapterKey(nic.GetPhysicalAddress().ToString(), nic.Id), kind,
                                       stats.BytesReceived, stats.BytesSent));
            }
            foreach (var (id, kind, rx, tx) in Dedupe(samples))
            {
                if (_last.TryGetValue(id, out var prev))
                {
                    long dRx = NetRate.Delta(prev.Down, rx), dTx = NetRate.Delta(prev.Up, tx);
                    if (dRx > 0 || dTx > 0)
                    {
                        Ledger.Add(today, kind, dRx, dTx);
                        _dirty = true;
                        down += dRx; up += dTx;

                        if (dRx + dTx > busiestBytes) { busiestBytes = dRx + dTx; busiest = kind; }
                    }
                }
                _last[id] = (rx, tx);
            }

            _downRate = NetRate.Smooth(_downRate, NetRate.PerSecond(down, dt));
            _upRate = NetRate.Smooth(_upRate, NetRate.PerSecond(up, dt));
            var latch = NetRate.Latch(_on, _downRate + _upRate, _quietFor, dt);
            _on = latch.On;
            _quietFor = latch.QuietFor;
            var upLatch = NetRate.Latch(_upOn, _upRate, _upQuietFor, dt,
                                        NetRate.UpOnBytesPerSec);
            _upOn = upLatch.On;
            _upQuietFor = upLatch.QuietFor;
            if (busiest is not null) Link = busiest;

            if (_dirty && now - _savedAt > 30_000) Save(now);
        }
        catch { }
    }

        internal void Flush() => Save(Environment.TickCount64);

        internal void Seed(double down, double up, NetLink link, NetLedger ledger)
    {

        _downRate = down;
        _upRate = up;
        _on = true;
        Link = link;
        Ledger = ledger;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Halo.ClaudeCode;

internal sealed record AskOption(string Label, string Description);

internal enum AskDelivery { Option, FreeText, Chat, Submit }

internal sealed record PendingAsk(
    string Nonce,
    int Pid,
    string? Session,
    string Tool,
    string? Target,
    string? Question,
    IReadOnlyList<AskOption> Options,
    DateTimeOffset ExpiresAt,

    bool MultiSelect = false,
    bool HasPreview = false)
{
    internal bool IsQuestion => Tool == "AskUserQuestion";
}

internal sealed class AskQueue
{
    private readonly List<PendingAsk> _items = [];

    internal int Count => _items.Count;

    internal void Observe(PendingAsk ask)
    {
        foreach (var existing in _items)
            if (existing.Nonce == ask.Nonce) return;
        _items.Add(ask);
    }

    internal PendingAsk? Head(DateTimeOffset now)
    {
        foreach (var item in _items)
            if (now < item.ExpiresAt) return item;
        return null;
    }

    internal void Remove(string nonce) => _items.RemoveAll(i => i.Nonce == nonce);

    internal IReadOnlyList<string> Nonces() => _items.ConvertAll(i => i.Nonce);

    internal IReadOnlyList<string> Sweep(DateTimeOffset now)
    {
        var dropped = new List<string>();
        foreach (var item in _items)
            if (now >= item.ExpiresAt) dropped.Add(item.Nonce);
        foreach (var nonce in dropped) Remove(nonce);
        return dropped;
    }
}

internal sealed class AskStore
{
    private readonly string _dir;
    private readonly Func<DateTimeOffset> _clock;
    private readonly AskQueue _queue = new();
    private readonly HashSet<string> _acked = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private int _version;

    internal AskStore(string dir, Func<DateTimeOffset>? clock = null)
    {
        _dir = dir;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    internal int Version => System.Threading.Volatile.Read(ref _version);

    internal PendingAsk? Pending
    {
        get { lock (_lock) return _queue.Head(_clock()); }
    }

    internal void Rescan()
    {
        try
        {
            if (!Directory.Exists(_dir)) return;
            var now = _clock();
            string? before;
            lock (_lock) before = _queue.Head(now)?.Nonce;

            var onDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.GetFiles(_dir, "ask-*.json"))
            {
                var ask = Parse(path);
                if (ask is null || now >= ask.ExpiresAt) continue;
                onDisk.Add(ask.Nonce);
                lock (_lock) _queue.Observe(ask);

                if (_acked.Add(ask.Nonce)) Touch(Path.Combine(_dir, $"ack-{ask.Nonce}"));
            }

            List<string> gone;
            lock (_lock)
            {
                gone = [.. _queue.Nonces().Where(n => !onDisk.Contains(n))];
                foreach (var nonce in gone) _queue.Remove(nonce);
            }
            foreach (var nonce in gone) Forget(nonce);

            List<string> expired;
            lock (_lock) expired = [.. _queue.Sweep(now)];
            foreach (var nonce in expired) Forget(nonce);

            string? after;
            lock (_lock) after = _queue.Head(now)?.Nonce;
            if (before != after) System.Threading.Interlocked.Increment(ref _version);
        }
        catch { }
    }

    internal bool Answer(PendingAsk ask, string label, AskDelivery delivery = AskDelivery.Option)
    {
        if (ask.IsQuestion) return Press(ask, label, delivery);
        try
        {
            string decision = label;
            string reason = $"{label} from the pill";
            var json = new JsonObject
            {
                ["nonce"] = ask.Nonce,
                ["decision"] = decision,
                ["reason"] = reason,
            }.ToJsonString();

            string path = Path.Combine(_dir, $"answer-{ask.Nonce}.json");
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch { }
        finally
        {
            lock (_lock) _queue.Remove(ask.Nonce);
            System.Threading.Interlocked.Increment(ref _version);
        }
        return true;
    }

    private bool Press(PendingAsk ask, string label, AskDelivery delivery)
    {
        if (ask.Pid <= 0) { Trace($"no pid for {ask.Nonce}"); return false; }

        if (delivery == AskDelivery.Submit)
        {

            Trace($"submit -> pid {ask.Pid} (queued)");
            System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { WalkToSubmit(ask); } catch { } });
            lock (_lock) { _queue.Remove(ask.Nonce); _ticked.Remove(ask.Nonce); }
            Forget(ask.Nonce);
            try { File.Delete(Path.Combine(_dir, $"ask-{ask.Nonce}.json")); } catch { }
            System.Threading.Interlocked.Increment(ref _version);
            return true;
        }

        int index = -1;

        if (delivery == AskDelivery.Option)
            for (int i = 0; i < ask.Options.Count && index < 0; i++)
                if (string.Equals(ask.Options[i].Label, label, StringComparison.Ordinal)) index = i;

        bool sent = index >= 0 && index < 9

            ? Interop.ConsoleRead.Type(ask.Pid, (index + 1).ToString())
            : Write(ask, label, delivery);
        Trace($"{(index >= 0 ? "row " + (index + 1) : delivery.ToString())} -> pid {ask.Pid} = {sent}"
            + (ask.MultiSelect ? " (multiSelect: toggled, still open)" : ""));
        if (!sent) return false;

        if (ask.MultiSelect)
        {

            if (index >= 0)
                lock (_lock)
                {
                    if (!_ticked.TryGetValue(ask.Nonce, out var set)) _ticked[ask.Nonce] = set = [];
                    if (!set.Add(index)) set.Remove(index);
                }

            System.Threading.Interlocked.Increment(ref _version);
            return true;
        }

        lock (_lock) { _queue.Remove(ask.Nonce); _ticked.Remove(ask.Nonce); }
        Forget(ask.Nonce);
        try { File.Delete(Path.Combine(_dir, $"ask-{ask.Nonce}.json")); } catch { }
        System.Threading.Interlocked.Increment(ref _version);
        return true;
    }

    private static void WalkToSubmit(PendingAsk ask)
    {
        for (int step = 0; step < 5; step++)
        {
            if (!Interop.ConsoleRead.Press(ask.Pid, Interop.ConsoleRead.VkRight)) return;
            System.Threading.Thread.Sleep(140);
            if (ShowingList(ask.Pid)) continue;
            bool sent = Interop.ConsoleRead.Press(ask.Pid, Interop.ConsoleRead.VkEnter);
            Trace($"submit walk -> pid {ask.Pid} after {step + 1} right = {sent}");
            return;
        }
        Trace($"submit walk -> pid {ask.Pid} never left the list");
    }

    private static bool ShowingList(int pid)
    {
        try
        {
            string text = Interop.ConsoleRead.Dump(pid, 20) ?? "";
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\.\s*\[");
        }
        catch { return true; }
    }

    internal static int RowNumber(int optionCount, AskDelivery delivery) => delivery switch
    {
        AskDelivery.FreeText => optionCount + 1,
        AskDelivery.Chat => optionCount + 2,
        _ => 0,
    };

    private static bool Write(PendingAsk ask, string text, AskDelivery delivery)
    {
        int pid = ask.Pid;
        int row = RowNumber(ask.Options.Count, delivery);

        if (row is <= 0 or > 9) return false;

        if (delivery == AskDelivery.FreeText && string.IsNullOrWhiteSpace(text)) return false;
        if (!Interop.ConsoleRead.Type(pid, row.ToString())) return false;
        if (delivery != AskDelivery.FreeText) return true;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {

                System.Threading.Thread.Sleep(140);
                if (!Interop.ConsoleRead.Type(pid, text)) return;

                if (ask.MultiSelect) return;
                System.Threading.Thread.Sleep(140);
                Interop.ConsoleRead.Press(pid, Interop.ConsoleRead.VkEnter);
            }
            catch { }
        });
        return true;
    }

    private static void Trace(string line)
    {
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "ask-debug.txt");
            Halo.Reports.DebugFile.Append(path, $"{DateTime.Now:HH:mm:ss.fff} {line}\n");
        }
        catch { }
    }

    private readonly Dictionary<string, HashSet<int>> _ticked = [];

    internal IReadOnlySet<int> Ticked(string nonce)
    {

        lock (_lock)
            return _ticked.TryGetValue(nonce, out var set) ? new HashSet<int>(set) : new HashSet<int>();
    }

    private void Forget(string nonce)
    {
        _acked.Remove(nonce);

        Delete(Path.Combine(_dir, $"ack-{nonce}"));
    }

    private PendingAsk? Parse(string path)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o) return null;
            string? nonce = o["nonce"]?.GetValue<string>();
            string? tool = o["tool"]?.GetValue<string>();
            if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(tool)) return null;
            if (!DateTimeOffset.TryParse(o["expiresAt"]?.GetValue<string>(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var expires))
                return null;

            var options = new List<AskOption>();
            if (o["options"] is JsonArray arr)
                foreach (var n in arr)
                    if (n is JsonObject oo && oo["label"]?.GetValue<string>() is { Length: > 0 } label)
                        options.Add(new AskOption(label, oo["description"]?.GetValue<string>() ?? ""));
            if (options.Count == 0) return null;

            return new PendingAsk(
                nonce,
                o["pid"] is JsonValue pv && pv.TryGetValue<int>(out var pid) ? pid : 0,
                o["session"]?.GetValue<string>(),
                tool,
                o["target"]?.GetValue<string>(),
                o["question"]?.GetValue<string>(),
                options,
                expires,

                o["multiSelect"] is JsonValue mv && mv.TryGetValue<bool>(out var multi) && multi,
                o["hasPreview"] is JsonValue hv && hv.TryGetValue<bool>(out var prev) && prev);
        }
        catch { return null; }
    }

    private static void Touch(string path)
    {
        try { if (!File.Exists(path)) File.WriteAllText(path, ""); } catch { }
    }

    private static void Delete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

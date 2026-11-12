using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Halo.Launcher;

internal static class ClipboardHistory
{
    internal const string ActPrefix = "clip:";
    internal const string RunPrefix = "cliprun:";
    internal const string PinPrefix = "clippin:";
    internal const string FlipPrefix = "clipflip:";
    internal const string LockPrefix = "cliplock:";
    internal const string DropPrefix = "clipdrop:";
    internal const string ClearAll = "act:clipclear";
    internal const string VaultPrefix = "vault:";
    internal const string VaultDropPrefix = "vaultdrop:";
    internal const string OpenVault = "act:vault";
    internal const string CommandGlyph = "\uE756";

    internal sealed record Item(string Id, string Preview, bool Pinned = false);

    private static HashSet<string> PinnedIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string root = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Clipboard", "Pinned");
            if (!System.IO.Directory.Exists(root)) return ids;
            foreach (var board in System.IO.Directory.GetDirectories(root))
                foreach (var item in System.IO.Directory.GetDirectories(board))
                    ids.Add(System.IO.Path.GetFileName(item).Trim('{', '}'));
        }
        catch { }
        return ids;
    }

        internal static List<string> ToClear(IEnumerable<Item> items)
    {
        var ids = new List<string>();
        foreach (var item in items) if (!item.Pinned) ids.Add(item.Id);
        return ids;
    }

    internal static void ClearUnpinned()
    {
        try
        {
            var keep = PinnedIds();
            Task.Run(async () =>
            {
                var res = await Clipboard.GetHistoryItemsAsync();
                if (res.Status != ClipboardHistoryItemsResultStatus.Success) return;
                foreach (var it in res.Items)
                    if (!keep.Contains(it.Id.Trim('{', '}'))) Clipboard.DeleteItemFromHistory(it);
            }).Wait(FirstWaitMs);
        }
        catch { }
        RefreshNow();
    }

        internal static void RefreshNow()
    {
        try { Task.Run(Fetch).Wait(FirstWaitMs); } catch { }
    }

    internal static bool UnpinInWindows(string id)
    {
        if (TextOf(id) is not { Length: > 0 } text || !Delete(id)) return false;
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            return true;
        }
        catch { return false; }
    }

    private static readonly object Gate = new();
    private static IReadOnlyList<Item>? _cached;
    private static bool _denied, _loaded;
    private static int _version, _refreshing;
    private const int FirstWaitMs = 800, WaitMs = 400;

        internal static int Version => System.Threading.Volatile.Read(ref _version);

        internal static void RefreshSoon()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0) return;
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try { Fetch(); }
            catch { }
            finally { System.Threading.Volatile.Write(ref _refreshing, 0); }
        });
    }

    internal static IReadOnlyList<Item>? Read()
    {
        lock (Gate)
        {
            if (_loaded)
            {
                RefreshSoon();
                return _denied ? null : _cached;
            }
        }
        try { Task.Run(Fetch).Wait(FirstWaitMs); } catch { }
        lock (Gate) return _denied ? null : _cached ?? [];
    }

    private static async Task FetchAsync()
    {
        var res = await Clipboard.GetHistoryItemsAsync();
        if (res.Status != ClipboardHistoryItemsResultStatus.Success)
        {
            Publish(null);
            return;
        }
        var list = new List<Item>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pinned = PinnedIds();
        foreach (var it in res.Items)
        {
            string preview;
            try
            {
                if (!it.Content.Contains(StandardDataFormats.Text)) continue;
                preview = Preview(await it.Content.GetTextAsync() ?? "");
                if (preview.Length == 0) continue;
            }
            catch { continue; }

            if (!seen.Add(preview)) continue;
            list.Add(new Item(it.Id, preview, pinned.Contains(it.Id.Trim('{', '}'))));
        }
        Publish(list);
    }

    private static void Fetch()
    {
        try { FetchAsync().Wait(); }
        catch { Publish(null); }
    }

    private static void Publish(List<Item>? list)
    {
        bool changed;
        lock (Gate)
        {
            changed = !_loaded || _denied != (list is null) || !SameItems(_cached, list);
            _loaded = true;
            _denied = list is null;
            _cached = list;
        }
        if (changed) System.Threading.Interlocked.Increment(ref _version);
    }

    private static bool SameItems(IReadOnlyList<Item>? a, IReadOnlyList<Item>? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    internal static string Preview(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var clean = Halo.Widgets.Fx.CleanText(raw);
        var sb = new System.Text.StringBuilder(clean.Length);
        bool gap = false;
        foreach (char c in clean)
        {
            if (char.IsWhiteSpace(c)) { gap = sb.Length > 0; continue; }
            if (gap) { sb.Append(' '); gap = false; }
            sb.Append(c);
        }
        var one = sb.ToString();
        return one.Length > 70 ? one[..70] + "..." : one;
    }

    internal static (string Glyph, System.Drawing.Color Tint) Kind(string preview)
    {
        string s = preview.TrimStart();
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return ("\uE71B", Halo.Widgets.Fx.VitalMemory);
        if (s.Length > 2 && s.Contains('@') && !s.Contains(' ') && s.IndexOf('.', s.IndexOf('@')) > 0)
            return ("\uE715", Halo.Widgets.Fx.VitalBattery);
        if ((s.Length > 2 && char.IsLetter(s[0]) && s[1] == ':' && s[2] is '\\' or '/') || s.StartsWith(@"\\", StringComparison.Ordinal))
            return ("\uE8B7", Halo.Widgets.Fx.VitalStorage);
        string first = s.Split(' ', 2)[0];
        if (s.Contains(" && ", StringComparison.Ordinal) || s.StartsWith("./", StringComparison.Ordinal) || Commands.Contains(first))
            return ("\uE756", Halo.Widgets.Fx.VitalOs);
        return ("\uE8A5", Halo.Widgets.Fx.VitalCpu);
    }

    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "sudo", "cd", "git", "docker", "npm", "pnpm", "yarn", "bash", "sh", "curl", "wget", "ssh", "scp", "python", "pip",
        "wsl", "dotnet", "pwsh", "powershell", "cat", "ls", "sed", "grep", "nano", "vim", "systemctl", "journalctl", "apt",
    };

        internal static string? TextOf(string id)
    {
        try
        {
            var res = Task.Run(async () => await Clipboard.GetHistoryItemsAsync());
            if (!res.Wait(WaitMs) || res.Result.Status != ClipboardHistoryItemsResultStatus.Success) return null;
            foreach (var it in res.Result.Items)
                if (it.Id == id && it.Content.Contains(StandardDataFormats.Text))
                {
                    var text = Task.Run(async () => await it.Content.GetTextAsync());
                    return text.Wait(WaitMs) ? text.Result : null;
                }
        }
        catch { }
        return null;
    }

        internal static bool Delete(string id)
    {
        try
        {
            var res = Task.Run(async () => await Clipboard.GetHistoryItemsAsync());
            if (!res.Wait(WaitMs) || res.Result.Status != ClipboardHistoryItemsResultStatus.Success) return false;
            foreach (var it in res.Result.Items)
                if (it.Id == id) return Clipboard.DeleteItemFromHistory(it);
        }
        catch { }
        return false;
    }

    internal static bool Restore(string id)
    {
        try
        {
            var res = Task.Run(async () => await Clipboard.GetHistoryItemsAsync());
            if (!res.Wait(WaitMs) || res.Result.Status != ClipboardHistoryItemsResultStatus.Success) return false;
            foreach (var it in res.Result.Items)
                if (it.Id == id)
                    return Clipboard.SetHistoryItemAsContent(it) == SetHistoryItemAsContentStatus.Success;
        }
        catch { }
        return false;
    }
}

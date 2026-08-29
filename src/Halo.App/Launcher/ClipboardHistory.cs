using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Halo.Launcher;

internal static class ClipboardHistory
{
    internal const string ActPrefix = "clip:";

    internal sealed record Item(string Id, string Preview);

    private const int CacheMs = 2000;
    private const int WaitMs = 400;

    private static IReadOnlyList<Item>? _cached;
    private static bool _denied;
    private static long _readAt;

    internal static IReadOnlyList<Item>? Read()
    {
        if (Environment.TickCount64 - _readAt < CacheMs) return _denied ? null : _cached;

        try
        {

            var task = Task.Run(async () => await Clipboard.GetHistoryItemsAsync());
            if (!task.Wait(WaitMs)) return _denied ? null : _cached;

            var res = task.Result;
            _readAt = Environment.TickCount64;
            _denied = res.Status != ClipboardHistoryItemsResultStatus.Success;
            if (_denied) { _cached = null; return null; }

            var list = new List<Item>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in res.Items)
            {
                string preview;
                try
                {
                    if (!it.Content.Contains(StandardDataFormats.Text)) continue;
                    var text = Task.Run(async () => await it.Content.GetTextAsync()).Result ?? "";
                    preview = Preview(text);
                    if (preview.Length == 0) continue;
                }
                catch { continue; }

                if (!seen.Add(preview)) continue;
                list.Add(new Item(it.Id, preview));
            }
            _cached = list;
            return list;
        }
        catch
        {
            _readAt = Environment.TickCount64;
            _denied = true;
            _cached = null;
            return null;
        }
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

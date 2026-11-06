using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Halo.Notifications;

internal sealed class NotifMute
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "notif-mute.json");

    private readonly object _lock = new();
    private bool _all;
    private readonly HashSet<string> _apps = new(StringComparer.OrdinalIgnoreCase);

    internal NotifMute() => Load();

    internal static string Key(NotifItem n) => n.Aumid.Length > 0 ? n.Aumid : n.App;

        internal static bool Silences(bool all, IReadOnlySet<string> apps, string key)
        => all || (key.Length > 0 && apps.Contains(key));

    internal bool Silences(NotifItem n) { lock (_lock) return Silences(_all, _apps, Key(n)); }

    internal bool All { get { lock (_lock) return _all; } }

    internal bool AppMuted(NotifItem n) { lock (_lock) return _apps.Contains(Key(n)); }

    internal void ToggleAll() { lock (_lock) _all = !_all; Save(); }

    internal void ToggleApp(NotifItem n)
    {
        string key = Key(n);
        if (key.Length == 0) return;
        lock (_lock) { if (!_apps.Remove(key)) _apps.Add(key); }
        Save();
    }

    internal static string Serialize(bool all, IEnumerable<string> apps)
    {
        var arr = new JsonArray();
        foreach (var a in apps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) arr.Add(a);
        return new JsonObject { ["all"] = all, ["apps"] = arr }.ToJsonString();
    }

    internal static (bool All, List<string> Apps) Deserialize(string json)
    {
        var apps = new List<string>();
        if (JsonNode.Parse(json) is not JsonObject o) return (false, apps);
        if (o["apps"] is JsonArray arr)
            foreach (var x in arr) if (x?.GetValue<string>() is { Length: > 0 } s) apps.Add(s);
        return (o["all"]?.GetValue<bool>() ?? false, apps);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var (all, apps) = Deserialize(File.ReadAllText(FilePath));
            lock (_lock) { _all = all; foreach (var a in apps) _apps.Add(a); }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            string json;
            lock (_lock) json = Serialize(_all, _apps);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { }
    }
}

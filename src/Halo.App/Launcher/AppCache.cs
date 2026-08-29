using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Halo.Launcher;

internal static class AppCache
{
    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "apps.json");

    internal static string ToJson(IReadOnlyList<AppEntry> apps)
    {
        var arr = new JsonArray();
        foreach (var a in apps) arr.Add(new JsonObject { ["n"] = a.Name, ["a"] = a.Aumid });
        return new JsonObject { ["apps"] = arr }.ToJsonString();
    }

    internal static IReadOnlyList<AppEntry> FromJson(string? json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            if (JsonNode.Parse(json) is not JsonObject root) return [];
            if (root["apps"] is not JsonArray arr) return [];
            var list = new List<AppEntry>(arr.Count);
            foreach (var node in arr)
            {
                if (node is not JsonObject o) continue;
                string name = o["n"] is JsonValue n && n.TryGetValue<string>(out var s) ? s : "";
                string aumid = o["a"] is JsonValue a && a.TryGetValue<string>(out var t) ? t : "";
                list.Add(new AppEntry(name, aumid));
            }
            return Dedupe(list);
        }
        catch { return []; }
    }

    internal static IReadOnlyList<AppEntry> Dedupe(IEnumerable<AppEntry> apps)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<AppEntry>();
        foreach (var a in apps)
        {
            if (string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Aumid)) continue;
            if (!ids.Add(a.Aumid)) continue;
            if (!names.Add(a.Name.Trim())) continue;
            list.Add(a);
        }
        return list;
    }

    internal static IReadOnlyList<AppEntry> Read(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : []; }
        catch { return []; }
    }

    internal static bool Save(string path, IReadOnlyList<AppEntry> apps)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson(apps));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch { return false; }
    }
}

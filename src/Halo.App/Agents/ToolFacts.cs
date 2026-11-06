using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Halo.Agents;

internal static class ToolFacts
{
    private static readonly HashSet<string> Setup = new(StringComparer.OrdinalIgnoreCase)
    {
        "cd", "pushd", "popd", "Set-Location", "sl", "echo", "export", "set", "sleep", "Start-Sleep", "source",
        "true", "clear", "cls",
    };

    private static readonly HashSet<string> Shells = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash", "sh", "zsh", "pwsh", "powershell", "cmd",
    };

    internal static string? CodexTarget(string? arguments, string? input)
    {
        try
        {
            if (!string.IsNullOrEmpty(input) && input.Contains("*** ", StringComparison.Ordinal))
                return PatchFile(input);
            if (string.IsNullOrWhiteSpace(arguments)) return null;
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            foreach (var key in new[] { "cmd", "command" })
            {
                if (!root.TryGetProperty(key, out var v)) continue;
                if (v.ValueKind == JsonValueKind.String) return Program(v.GetString());
                if (v.ValueKind == JsonValueKind.Array)
                {
                    var parts = v.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                                 .Select(e => e.GetString() ?? "").ToList();

                    if (parts.Count >= 3 && Shells.Contains(Clean(parts[0]) ?? "")) return Program(parts[^1]);
                    return Program(string.Join(' ', parts));
                }
            }
            foreach (var key in new[] { "path", "file_path", "file" })
                if (root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                    return Leaf(p.GetString());
            foreach (var key in new[] { "query", "pattern" })
                if (root.TryGetProperty(key, out var q) && q.ValueKind == JsonValueKind.String)
                    return Cut(q.GetString());
        }
        catch { }
        return null;
    }

    internal static string? PatchFile(string patch)
    {
        foreach (var line in patch.Split('\n'))
        {
            var l = line.Trim();
            foreach (var head in new[] { "*** Update File:", "*** Add File:", "*** Delete File:" })
                if (l.StartsWith(head, StringComparison.Ordinal)) return Leaf(l[head.Length..].Trim());
        }
        return null;
    }

        internal static string? Program(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        foreach (var part in command.Split(new[] { '|', ';', '&', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = part.Trim();
            if (line.Length == 0) continue;
            string? name = null;
            if (line[0] is '"' or '\'')
            {
                var end = line.IndexOf(line[0], 1);
                if (end > 1) name = Clean(line.Substring(1, end - 1));
            }
            else
                foreach (var word in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.Contains('=')) continue;
                    name = Clean(word.Trim('"', '\'', '('));
                    break;
                }
            if (name is not null && !Setup.Contains(name)) return PlainWords.WithSubcommand(name, line);
        }
        return null;
    }

    private static string? Clean(string word)
    {
        var leaf = Leaf(word);
        if (string.IsNullOrEmpty(leaf)) return null;
        if (leaf.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) leaf = leaf[..^4];
        return leaf.Length is > 0 and <= 14 ? leaf : null;
    }

    internal static string? Leaf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var s = path.Replace('\\', '/').TrimEnd('/');
        var i = s.LastIndexOf('/');
        var leaf = i >= 0 ? s[(i + 1)..] : s;
        return leaf.Length > 0 ? leaf : null;
    }

    private static string? Cut(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= 24 ? s.Trim() : s.Trim()[..24];
}

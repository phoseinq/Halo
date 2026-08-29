using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Halo.Panels;

internal enum PanelRowKind
{
    Text,
    Slider,
    Toggle,
    Buttons,
    Meter,
}

internal sealed record PanelRow(
    PanelRowKind Kind,
    string Id,
    string Label,
    string Text,
    double Value,
    double Min,
    double Max,
    string Unit,
    IReadOnlyList<string> Options);

internal sealed record PanelSpec(string Title, IReadOnlyList<PanelRow> Rows)
{

    internal const int MaxRows = 4;
    internal const int MaxOptions = 5;
    private const int MaxLabel = 48, MaxText = 64;

    internal static PanelSpec? Parse(JsonObject? json)
    {
        if (json is null) return null;
        var rows = new List<PanelRow>();
        if (json["rows"] is JsonArray array)
            foreach (var node in array)
            {
                if (rows.Count >= MaxRows) break;
                if (Row(node as JsonObject) is { } row) rows.Add(row);
            }

        if (rows.Count == 0) return null;
        return new PanelSpec(Clip(Str(json, "title"), MaxLabel), rows);
    }

    private static PanelRow? Row(JsonObject? o)
    {
        if (o is null) return null;
        if (!Kind(Str(o, "type"), out var kind)) return null;

        string id = Clip(Str(o, "id"), 32);
        string label = Clip(Str(o, "label"), MaxLabel);

        var options = new List<string>();
        if (o["options"] is JsonArray opts)
            foreach (var node in opts)
            {
                if (options.Count >= MaxOptions) break;
                string text = Clip(node?.GetValue<string>() ?? "", 20);
                if (text.Length > 0) options.Add(text);
            }

        if (kind == PanelRowKind.Buttons && options.Count < 2) return null;

        if (id.Length == 0 && kind is PanelRowKind.Slider or PanelRowKind.Toggle or PanelRowKind.Buttons)
            return null;

        double min = Num(o, "min", 0);
        double max = Num(o, "max", kind == PanelRowKind.Meter ? 1 : 100);

        if (!(max > min)) { min = 0; max = kind == PanelRowKind.Meter ? 1 : 100; }

        double value = kind switch
        {
            PanelRowKind.Toggle => Bool(o, "value") ? 1 : 0,
            PanelRowKind.Buttons => Math.Clamp(Num(o, "value", 0), 0, options.Count - 1),
            PanelRowKind.Meter => Math.Clamp(Num(o, "value", 0), 0, 1),
            PanelRowKind.Slider => Math.Clamp(Num(o, "value", min), min, max),
            _ => Num(o, "value", 0),
        };

        return new PanelRow(kind, id, label, Clip(Str(o, "text"), MaxText),
            value, min, max, Clip(Str(o, "unit"), 8), options);
    }

    private static bool Kind(string name, out PanelRowKind kind)
    {
        switch (name.ToLowerInvariant())
        {
            case "text": kind = PanelRowKind.Text; return true;
            case "slider": kind = PanelRowKind.Slider; return true;
            case "toggle": kind = PanelRowKind.Toggle; return true;
            case "buttons": kind = PanelRowKind.Buttons; return true;
            case "meter": kind = PanelRowKind.Meter; return true;
            default: kind = PanelRowKind.Text; return false;
        }
    }

    private static string Str(JsonObject o, string key)
        => o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    private static double Num(JsonObject o, string key, double fallback)
    {
        if (o[key] is not JsonValue v) return fallback;
        if (v.TryGetValue<double>(out var d)) return double.IsFinite(d) ? d : fallback;
        return v.TryGetValue<string>(out var s)
            && double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed) ? parsed : fallback;
    }

    private static bool Bool(JsonObject o, string key)
    {
        if (o[key] is not JsonValue v) return false;
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.TryGetValue<string>(out var s))
            return s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("on", StringComparison.OrdinalIgnoreCase);
        return v.TryGetValue<double>(out var d) && d != 0;
    }

    private static string Clip(string s, int max)
    {
        s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length <= max ? s : s[..max];
    }
}

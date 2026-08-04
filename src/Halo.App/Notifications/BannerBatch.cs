using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Halo.Notifications;

internal readonly record struct BannerEdit(string Subkey, string Name, int? Value);

internal static class BannerBatch
{
    internal static string Serialize(IEnumerable<BannerEdit> edits)
    {
        var sb = new StringBuilder();
        foreach (var e in edits)
        {

            if (e.Subkey is null || string.IsNullOrWhiteSpace(e.Name)) continue;
            sb.Append(e.Subkey).Append('\t').Append(e.Name).Append('\t')
              .Append(e.Value?.ToString(CultureInfo.InvariantCulture) ?? "").Append('\n');
        }
        return sb.ToString();
    }

    internal static List<BannerEdit> Parse(IEnumerable<string> lines)
    {
        var edits = new List<BannerEdit>();
        foreach (var raw in lines)
        {
            var line = raw?.TrimEnd('\r') ?? "";
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            if (parts.Length != 3) continue;
            if (string.IsNullOrWhiteSpace(parts[1])) continue;
            int? value = null;
            if (parts[2].Length > 0)
            {
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) continue;
                value = n;
            }
            edits.Add(new BannerEdit(parts[0], parts[1], value));
        }
        return edits;
    }
}

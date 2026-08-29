using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Halo.Launcher;

internal static class Translator
{
    internal const string Service = "translated.net";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    internal static string LangPair(string text, bool rtl) => rtl ? "fa|en" : "en|fa";

    internal const string SourceKey = "launcher.translate.from";
    internal const string TargetKey = "launcher.translate.to";
    internal const string Auto = "auto";

        internal readonly record struct Lang(string Code, string Name);

    internal static readonly Lang[] Languages =
    [
        new("en", "English"),
        new("fa", "Persian"),
        new("ar", "Arabic"),
        new("tr", "Turkish"),
        new("de", "German"),
        new("fr", "French"),
        new("es", "Spanish"),
        new("ru", "Russian"),
        new("it", "Italian"),
        new("pt", "Portuguese"),
        new("nl", "Dutch"),
        new("sv", "Swedish"),
        new("pl", "Polish"),
        new("uk", "Ukrainian"),
        new("hi", "Hindi"),
        new("ur", "Urdu"),
        new("zh", "Chinese"),
        new("ja", "Japanese"),
        new("ko", "Korean"),
        new("he", "Hebrew"),
        new("az", "Azerbaijani"),
        new("ku", "Kurdish"),
        new("el", "Greek"),
        new("id", "Indonesian"),
    ];

        internal static string Name(string? code)
    {
        string c = (code ?? "").Trim();
        if (c.Length == 0 || string.Equals(c, Auto, StringComparison.OrdinalIgnoreCase)) return "Detect";
        foreach (var l in Languages) if (string.Equals(l.Code, c, StringComparison.OrdinalIgnoreCase)) return l.Name;
        return c;
    }

    internal static string TargetName(string? code)
    {
        string c = (code ?? "").Trim();
        return c.Length == 0 || string.Equals(c, Auto, StringComparison.OrdinalIgnoreCase) ? "Auto" : Name(c);
    }

        internal static bool IsAuto(string? code)
        => string.IsNullOrWhiteSpace(code) || string.Equals(code.Trim(), Auto, StringComparison.OrdinalIgnoreCase);

    internal static (string From, string To)? Swap(string? from, string? to, string? detected)
    {
        string target = (to ?? "").Trim();
        if (target.Length == 0) return null;
        if (!IsAuto(from)) return (target, from!.Trim());
        string left = (detected ?? "").Trim();
        return left.Length == 0 || IsAuto(left) ? null : (target, left);
    }

    internal static string Resolve(string? from, string? to, string text, bool rtl)
    {
        string target = (to ?? "").Trim();
        if (target.Length == 0) return LangPair(text, rtl);
        string source = IsAuto(from) ? (rtl ? "fa" : "en") : from!.Trim();

        return string.Equals(source, target, StringComparison.OrdinalIgnoreCase)
            ? LangPair(text, rtl) : source + "|" + target;
    }

    internal static string? ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("responseStatus", out var st))
            {
                int code = st.ValueKind == JsonValueKind.Number ? st.GetInt32()
                         : int.TryParse(st.GetString(), out int parsed) ? parsed : 0;
                if (code != 200) return null;
            }
            if (!root.TryGetProperty("responseData", out var data)) return null;
            if (!data.TryGetProperty("translatedText", out var text)) return null;
            var s = text.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
        catch { return null; }
    }

    internal static async Task<string?> TranslateAsync(string text, bool rtl, string? from, string? to)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(text)
                       + "&langpair=" + Resolve(from, to, text, rtl);
            return ParseResponse(await Http.GetStringAsync(url));
        }
        catch { return null; }
    }
}

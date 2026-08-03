using System;
using System.Text.RegularExpressions;

namespace Halo.Reports;

internal static partial class Scrub
{

    [GeneratedRegex(@"(?:[A-Za-z]:\\|\\\\)[^\s""'<>|]*", RegexOptions.None, 200)]
    private static partial Regex PathLike();

    internal static string Paths(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        try
        {
            return PathLike().Replace(text, m =>
            {
                var span = m.Value;
                int cut = span.LastIndexOf('\\');

                return cut >= 0 && cut < span.Length - 1 ? span[(cut + 1)..] : "<path>";
            });
        }
        catch { return "<path>"; }
    }

    internal const int MinUserName = 3;

    internal static string User(string? text, string? user)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (string.IsNullOrEmpty(user) || user.Length < MinUserName) return text;
        try { return Regex.Replace(text, Regex.Escape(user), "<user>", RegexOptions.IgnoreCase); }
        catch { return text; }
    }

    internal static string All(string? text, string? user) => User(Paths(text), user);

    internal static string All(string? text)
    {
        string? user = null;
        try { user = Environment.UserName; } catch { }
        return All(text, user);
    }
}

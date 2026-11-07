using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Halo.Notifications;

internal static class OtpCode
{

    private static readonly Regex CodeWord = new(
        @"\b(code|otp|passcode|pin|verification|verify|one[- ]?time|2fa|two[- ]factor|login|log[- ]in|sign[- ]?in|security|confirm)\b|\u06A9\u062F|\u0631\u0645\u0632|\u062A\u0627\u06CC\u06CC\u062F|\u062A\u0623\u06CC\u06CC\u062F|\u06CC\u06A9\u0628\u0627\u0631|\u06CC\u06A9\u200C\u0628\u0627\u0631|\u0648\u0631\u0648\u062F|\u067E\u06CC\u0646|\u06AF\u0630\u0631\u0648\u0627\u0698\u0647",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Six = new(@"(?<![\d])(\d{6}|\d{8})(?![\d])", RegexOptions.Compiled);
    private static readonly Regex Split = new(@"(?<![\d])(\d{3,4})[ \-](\d{3,4})(?![\d])", RegexOptions.Compiled);
    private static readonly Regex Short = new(@"(?<![\d\w])(\d{4,8})(?![\d\w])", RegexOptions.Compiled);

    private static readonly Regex Mixed = new(
        @"(?<![A-Za-z0-9])(?=[A-Z0-9]*\d)(?=[A-Z0-9]*[A-Z])([A-Z0-9]{5,8})(?![A-Za-z0-9])", RegexOptions.Compiled);

        internal static string Find(string? title, string? body)
    {
        string t = Digits(title ?? ""), b = Digits(body ?? "");
        bool worded = CodeWord.IsMatch(t) || CodeWord.IsMatch(b);
        foreach (var text in new[] { t, b })
        {
            if (text.Length == 0) continue;
            if (Six.Match(text) is { Success: true } m6) return m6.Groups[1].Value;
            if (Split.Match(text) is { Success: true } ms && (worded || ms.Groups[1].Length == 3 && ms.Groups[2].Length == 3))
                return ms.Groups[1].Value + ms.Groups[2].Value;
        }
        if (!worded) return "";
        foreach (var text in new[] { t, b })
        {
            if (Short.Match(text) is { Success: true } mm && !LooksLikeYear(mm.Groups[1].Value)) return mm.Groups[1].Value;
            if (Mixed.Match(text) is { Success: true } mx) return mx.Groups[1].Value;
        }
        return "";
    }

    private static bool LooksLikeYear(string s) =>
        s.Length == 4 && (s.StartsWith("19", StringComparison.Ordinal) || s.StartsWith("20", StringComparison.Ordinal));

        internal static string Digits(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + (c - '\u06F0'))
                    : c is >= '\u0660' and <= '\u0669' ? (char)('0' + (c - '\u0660'))
                    : c);
        return sb.ToString();
    }
}

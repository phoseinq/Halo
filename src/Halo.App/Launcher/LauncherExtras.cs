using System;
using System.Collections.Generic;

namespace Halo.Launcher;

internal static class LauncherExtras
{
    internal const string CopyPrefix = "copy:";
    internal const string WebPrefix = "web:";
    internal const string RunPrefix = "run:";
    internal const string FilePrefix = "file:";

    internal const int MinWebQuery = 2;

    internal readonly record struct Set(IReadOnlyList<LauncherRow> Above, IReadOnlyList<LauncherRow> Below, bool Exclusive);

    private static readonly Set Nothing = new([], [], false);

        internal static bool FromQuery(string? id)
        => id is not null && (id.StartsWith(CopyPrefix, StringComparison.Ordinal)
                              || id.StartsWith(WebPrefix, StringComparison.Ordinal)
                              || id.StartsWith(RunPrefix, StringComparison.Ordinal));

    internal static Set For(string query)
    {
        string q = (query ?? "").Trim();
        if (q.Length == 0) return Nothing;

        if (q[0] == '>')
        {
            string cmd = q[1..].Trim();
            return cmd.Length == 0
                ? new Set([new LauncherRow("type a command to run", null, false, LauncherRowKind.Notice)], [], true)
                : new Set([new LauncherRow(cmd, null, true, LauncherRowKind.Action, RunPrefix + cmd, "run", "\uE756",
                                           Halo.Widgets.Fx.VitalOs)], [], true);
        }

        var above = new List<LauncherRow>(1);
        if (LauncherCalc.Try(q) is { } answer)
            above.Add(new LauncherRow(answer.Shown, null, true, LauncherRowKind.Action, CopyPrefix + answer.Copy,
                                      "copy", "\uE8EF", Halo.Widgets.Fx.VitalGpu));

        var below = new List<LauncherRow>(1);
        if (q.Length >= MinWebQuery)
            below.Add(new LauncherRow(q, null, true, LauncherRowKind.Action, WebPrefix + q, "web", "\uE774",
                                      Halo.Widgets.Fx.VitalMemory));
        return new Set(above, below, false);
    }

    internal static string WebUrl(string query)
        => "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
}

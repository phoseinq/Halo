using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Halo.ClaudeCode;

internal static class HookConnect
{
    internal enum Step
    {
        Wait,
        Install,
    }

    internal static Step Next(bool busy, bool alreadyTried, bool undone, Func<bool> agentSeen, Func<bool> hooksInstalled)
    {
        if (busy || alreadyTried || undone) return Step.Wait;
        if (!agentSeen()) return Step.Wait;
        return hooksInstalled() ? Step.Wait : Step.Install;
    }

    internal static string? MarkFor(bool installed) => installed ? HookMarks.Done : null;

    internal static string Short(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;
    }

    internal static (string App, string Title, string Body) Notice(string agent, string settingsPath) =>
        (agent,
         $"{agent} connected",
         $"Hooks added to {Short(settingsPath)}. Your previous file is saved as .halo-bak.");

    internal static (string App, string Title, string Body) Failed(string agent, string why) =>
        (agent,
         $"Could not connect {agent}",
         string.IsNullOrWhiteSpace(why)
             ? "Halo could not write the hook settings. Nothing was changed."
             : $"Halo could not write the hook settings: {why}. Nothing was changed.");

    private static readonly (string Agent, string[] Processes)[] Agents =
    [
        ("Claude Code", ["claude"]),
        ("Codex", ["codex", "ChatGPT"]),
    ];

    private static readonly object Gate = new();
    private static long _nextScan;
    private static readonly HashSet<string> Busy = [];

    private static readonly HashSet<string> Settled = [];

    private static bool IsSettled(string agent) { lock (Gate) return Settled.Contains(agent); }

    internal static string HookExe()
    {
        if (Halo.Interop.AppModel.IsPackaged)
        {
            string stub = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "Halo.Hooks.exe");
            if (File.Exists(stub)) return stub;
        }
        return Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
    }

    private static bool Running(string[] names)
    {
        foreach (var n in names)
        {
            try { if (Process.GetProcessesByName(n).Length > 0) return true; }
            catch { }
        }
        return false;
    }

    internal static void Tick(Action<string, string, string> notify)
    {
        long now = Environment.TickCount64;
        lock (Gate)
        {
            if (now < _nextScan) return;
            _nextScan = now + 5000;
        }

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            foreach (var (agent, processes) in Agents)
            {
                try
                {

                    lock (Gate) if (Busy.Contains(agent)) continue;
                    string mark = HookMarks.Of(agent);

                    bool installed = false;
                    var step = Next(
                        busy: false,

                        alreadyTried: string.Equals(mark, HookMarks.Done, StringComparison.OrdinalIgnoreCase)
                                      || IsSettled(agent),
                        undone: string.Equals(mark, HookMarks.Undone, StringComparison.OrdinalIgnoreCase),
                        agentSeen: () => Running(processes),
                        hooksInstalled: () => installed = Query(agent) == 0);

                    if (installed) lock (Gate) Settled.Add(agent);
                    if (step != Step.Install) continue;

                    bool mine;
                    lock (Gate) mine = Busy.Add(agent);
                    if (!mine) continue;
                    try
                    {
                        var (ok, why, path) = Install(agent);
                        if (MarkFor(ok) is string mark2) HookMarks.Write(agent, mark2);
                        lock (Gate) Settled.Add(agent);
                        var (a, t, b) = ok ? Notice(agent, path) : Failed(agent, why);
                        notify(a, t, b);
                    }
                    finally { lock (Gate) Busy.Remove(agent); }
                }
                catch { }
            }
        });
    }

    private static int Query(string agent)
    {
        try
        {
            var psi = new ProcessStartInfo(HookExe()) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(agent == "Codex" ? "query-codex-hooks" : "query-claude-hooks");
            using var p = Process.Start(psi);
            if (p == null) return 2;

            if (!p.WaitForExit(15_000)) { try { p.Kill(entireProcessTree: true); } catch { } return 2; }
            return p.ExitCode;
        }
        catch { return 2; }
    }

    private static (bool Ok, string Why, string Path) Install(string agent)
    {

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string settings = agent == "Codex"
            ? Path.Combine(home, ".codex", "hooks.json")
            : Path.Combine(home, ".claude", "settings.json");
        try
        {
            string exe = HookExe();
            var psi = new ProcessStartInfo(exe)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            psi.ArgumentList.Add(agent == "Codex" ? "install-codex-hooks" : "install-claude-hooks");
            psi.ArgumentList.Add(exe);
            using var p = Process.Start(psi);
            if (p == null) return (false, "the helper did not start", settings);

            var err = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return (false, "timed out", settings);
            }

            string why = "";
            try { if (err.Wait(2_000)) why = err.Result.Trim(); } catch { }
            return (p.ExitCode == 0, why, settings);
        }
        catch (Exception e) { return (false, e.Message, settings); }
    }
}

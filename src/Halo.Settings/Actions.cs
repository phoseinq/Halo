using System;
using System.Diagnostics;
using System.IO;

namespace Halo.Settings;

internal static class Actions
{
    private static string HaloDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo");

    internal static bool NeedsUiThread(string key) => key is "api.token" or "report.problem";

    internal static void Run(string key)
    {
        try
        {
            switch (key)
            {

                case "general.reset":
                    Directory.CreateDirectory(HaloDir);
                    File.WriteAllText(Path.Combine(HaloDir, "offset"), "0");
                    break;

                case "hooks.claude":
                case "hooks.codex":
                    {
                        bool codex = key == "hooks.codex";
                        string agent = codex ? "Codex" : "Claude Code";

                        bool connected = Live.Connected(codex ? "codex" : "claude");

                        if (connected)
                        {
                            if (Hooks(codex ? "uninstall-codex-hooks" : "uninstall-claude-hooks") == 0)
                                Halo.ClaudeCode.HookMarks.Write(agent, Halo.ClaudeCode.HookMarks.Undone);
                        }
                        else
                        {

                            if (Hooks(codex ? "install-codex-hooks" : "install-claude-hooks") == 0)
                                Halo.ClaudeCode.HookMarks.Write(agent, Halo.ClaudeCode.HookMarks.Done);
                        }
                    }
                    break;

                case "reset.everything":
                    {
                        string app = System.IO.Path.Combine(AppContext.BaseDirectory, "Halo.App.exe");
                        if (System.IO.File.Exists(app))
                        {

                            foreach (var verb in new[] { "--restore-notifications", "--report-clear" })
                            {
                                try
                                {
                                    var psi = new ProcessStartInfo(app) { UseShellExecute = false, CreateNoWindow = true };
                                    psi.ArgumentList.Add(verb);
                                    using var p = Process.Start(psi);
                                    p?.WaitForExit(60_000);
                                }
                                catch { }
                            }
                        }

                        if (Hooks("uninstall-claude-hooks") == 0)
                            Halo.ClaudeCode.HookMarks.Write("Claude Code", Halo.ClaudeCode.HookMarks.Undone);
                        if (Hooks("uninstall-codex-hooks") == 0)
                            Halo.ClaudeCode.HookMarks.Write("Codex", Halo.ClaudeCode.HookMarks.Undone);

                        try
                        {

                            foreach (var name in new[]
                                     {
                                         "offset", "pin", "tray.txt", "notif-seen.txt", "limit-fired",
                                         "notif-debug.txt", "banner-orig.tsv",
                                     })
                            {
                                try { System.IO.File.Delete(System.IO.Path.Combine(HaloDir, name)); } catch { }
                            }

                            string outproc = System.IO.Path.Combine(HaloDir, "outproc");
                            if (System.IO.Directory.Exists(outproc))
                                System.IO.Directory.Delete(outproc, recursive: true);
                        }
                        catch { }
                    }
                    break;
                case "access.notifications":
                    Open("ms-settings:privacy-notifications");
                    break;

                case "access.startup":
                    Open(Halo.Interop.AppModel.IsPackaged ? "ms-settings:startupapps" : "taskschd.msc");
                    break;

                case "about.state":
                    Directory.CreateDirectory(HaloDir);
                    Open(HaloDir);
                    break;

                case "api.token":
                    var token = new Store().Text("api.token", "");
                    if (token.Length > 0) System.Windows.Clipboard.SetText(token);
                    break;
                case "about.repo":
                    Open("https://github.com/phoseinq/DynamicWin");
                    break;

                case "report.problem":
                    ReportWindow.Open(System.Windows.Application.Current?.MainWindow);
                    break;
            }
        }
        catch { }
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }

    private static int Hooks(string verb) => Live.Hooks(verb, 8000);
}

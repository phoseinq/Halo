using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace Halo.Settings;

public partial class App : Application
{
    private static Mutex? _instance;

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    private const int Restore = 9;

    protected override void OnStartup(StartupEventArgs e)
    {

        string forced = Environment.GetEnvironmentVariable("HALO_LANG") ?? "";
        Halo.Localization.Strings.Use(forced.Length > 0
            ? Halo.Localization.Strings.Name(forced)
            : new Store().Text("general.language", Catalog.LanguageRowFallback));

        if (e.Args.Length >= 2 && e.Args[0] == "--render-page")
        {
            Preview.Render(e.Args[1], e.Args.Length >= 3 ? e.Args[2] : "home",
                e.Args.Length >= 4 ? e.Args[3] : "");
            Shutdown();
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0] == "--render-report")
        {
            Preview.RenderReport(e.Args[1], e.Args.Length >= 3 && e.Args[2] == "filled");
            Shutdown();
            return;
        }

        if (e.Args.Length >= 1 && e.Args[0] == "--probe-launch")
        {
            bool stamped = Halo.Shell.PanelLaunch.TakeRequest();
            Halo.Shell.PanelLaunch.LogProbe(
                $"requested={(stamped ? "yes" : "no")} "
                + $"sessionAge={Halo.Shell.PanelLaunch.Age(Halo.Shell.PanelLaunch.SessionAgeSeconds())} "
                + $"show={(Halo.Shell.PanelLaunch.ShouldShow(stamped) ? "yes" : "no")}");
            Shutdown();
            return;
        }

        if (!Halo.Shell.PanelLaunch.ShouldShow(Halo.Shell.PanelLaunch.TakeRequest()))
        {
            Halo.Shell.PanelLaunch.LogRefused(Halo.Shell.PanelLaunch.SessionAgeSeconds());
            Shutdown();
            return;
        }

        _instance = new Mutex(true, "Halo.Settings.SingleInstance", out bool created);
        if (!created)
        {
            Surface();
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    private static void Surface()
    {
        try
        {
            int self = Environment.ProcessId;
            foreach (var process in Process.GetProcessesByName("Halo.Settings"))
            {
                using (process)
                {
                    if (process.Id == self || process.MainWindowHandle == IntPtr.Zero) continue;
                    ShowWindow(process.MainWindowHandle, Restore);
                    SetForegroundWindow(process.MainWindowHandle);
                    return;
                }
            }
        }
        catch { }
    }
}

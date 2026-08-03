using System;
using System.Diagnostics;
using System.IO;

namespace Halo.Settings;

internal static class Actions
{
    private static string HaloDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo");

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
                case "access.notifications":
                    Open("ms-settings:privacy-notifications");
                    break;

                case "access.startup":
                    Open("taskschd.msc");
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
}

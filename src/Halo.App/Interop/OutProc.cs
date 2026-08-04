using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Halo.Interop;

internal static class OutProc
{
    internal static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "outproc");

    private static readonly string StampPath = Path.Combine(Dir, "version.txt");

    internal static bool NeedsRefresh(string sourceVersion, string? copiedVersion)
    {
        var copied = copiedVersion?.Trim();
        if (string.IsNullOrEmpty(copied)) return true;
        return !string.Equals(sourceVersion?.Trim(), copied, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? Exe()
    {
        try
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0";
            string? copied = null;
            try { if (File.Exists(StampPath)) copied = File.ReadAllText(StampPath); } catch { }

            string exe = Path.Combine(Dir, "Halo.App.exe");
            if (File.Exists(exe) && !NeedsRefresh(version, copied)) return exe;

            Directory.CreateDirectory(Dir);

            try { File.Delete(StampPath); } catch { }
            foreach (var f in Directory.GetFiles(AppContext.BaseDirectory))
            {
                try { File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), overwrite: true); } catch { }
            }
            if (!File.Exists(exe)) return null;
            try { File.WriteAllText(StampPath, version); } catch { }
            return exe;
        }
        catch { return null; }
    }

    internal static string? Run(string verb, string stdin)
    {
        try
        {
            string? exe = Exe();
            if (exe is null) return null;
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            psi.ArgumentList.Add(verb);
            using var p = Process.Start(psi);
            if (p is null) return null;
            p.StandardInput.Write(stdin);
            p.StandardInput.Close();

            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            if (p.ExitCode != 0) return null;
            try { return output.Wait(2_000) ? output.Result : ""; } catch { return ""; }
        }
        catch { return null; }
    }
}

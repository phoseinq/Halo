using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Halo.Notifications;

internal static class BannerApply
{
    private const string Live = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    internal static string Root =>
        Environment.GetEnvironmentVariable("HALO_BANNER_ROOT") is { Length: > 0 } r ? r : Live;

    private static string Path(string subkey)
        => string.IsNullOrEmpty(subkey) ? Root : Root + "\\" + subkey;

    internal static int Apply(IEnumerable<BannerEdit> edits)
    {
        int done = 0;
        foreach (var e in edits)
        {
            if (e.Subkey is null || string.IsNullOrWhiteSpace(e.Name)) continue;
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(Path(e.Subkey), writable: true);
                if (k == null) continue;
                if (e.Value is int v) k.SetValue(e.Name, v, RegistryValueKind.DWord);
                else k.DeleteValue(e.Name, throwOnMissingValue: false);
                done++;
            }
            catch { }
        }
        return done;
    }

    internal static int? Read(string subkey, string name)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Path(subkey));
            return k?.GetValue(name) as int?;
        }
        catch { return null; }
    }
}

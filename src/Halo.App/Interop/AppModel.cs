using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Halo.Interop;

internal static class AppModel
{

    private const int ErrorInsufficientBuffer = 122;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, StringBuilder? fullName);

    private static readonly Lazy<string?> _full = new(() =>
    {
        try
        {
            int len = 0;

            if (GetCurrentPackageFullName(ref len, null) != ErrorInsufficientBuffer || len <= 0) return null;
            var sb = new StringBuilder(len);
            if (GetCurrentPackageFullName(ref len, sb) != 0) return null;
            string full = sb.ToString();
            return full.Length > 0 ? full : null;
        }

        catch { return null; }
    });

    internal static bool IsPackaged => _full.Value is not null;

    internal static string? PackageFullName => _full.Value;
}

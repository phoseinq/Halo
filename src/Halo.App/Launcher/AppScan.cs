using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Launcher;

internal static class AppScan
{

    private static readonly string[] DocumentTargets =
        [".chm", ".url", ".htm", ".html", ".pdf", ".rtf", ".txt", ".doc", ".docx", ".md", ".mht"];

    private static readonly string[] JunkNames = ["Uninstall ", "Uninstall_", "Remove "];

    internal static IReadOnlyList<AppEntry> Enumerate()
    {
        var found = new List<AppEntry>(512);
        try
        {
            if (Win32.SHGetKnownFolderItem(Win32.FOLDERID_AppsFolder, 0, IntPtr.Zero,
                    Win32.IID_IShellItem, out var folder) != 0 || folder is null)
                return found;

            try
            {
                if (folder.BindToHandler(IntPtr.Zero, Win32.BHID_EnumItems,
                        Win32.IID_IEnumShellItems, out IntPtr raw) != 0 || raw == IntPtr.Zero)
                    return found;

                var items = (Win32.IEnumShellItems)Marshal.GetObjectForIUnknown(raw);
                Marshal.Release(raw);
                try
                {
                    while (items.Next(1, out var item, out uint got) == 0 && got == 1 && item is not null)
                    {
                        try
                        {
                            if (item.GetDisplayName(Win32.SHGDN_NORMAL, out string name) != 0) continue;
                            if (item.GetDisplayName(Win32.SIGDN_PARENTRELATIVEFORADDRESSBAR, out string aumid) != 0)
                                continue;
                            if (!IsJunk(name, aumid)) found.Add(new AppEntry(name, aumid));
                        }
                        catch { }
                        finally { try { Marshal.ReleaseComObject(item); } catch { } }
                    }
                }
                finally { try { Marshal.ReleaseComObject(items); } catch { } }
            }
            finally { try { Marshal.ReleaseComObject(folder); } catch { } }
        }
        catch { }
        return AppCache.Dedupe(found);
    }

    internal static bool IsJunk(string name, string target)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target)) return true;

        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
         || target.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (string ext in DocumentTargets)
            if (target.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;

        foreach (string prefix in JunkNames)
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}

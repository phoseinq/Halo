using System;
using System.Collections.Generic;
using Halo.Interop;

namespace Halo.Shell;

internal static class TerminalTabs
{

    public static System.Collections.Generic.List<string?> TabNames(IntPtr hwnd)
    {
        var names = new System.Collections.Generic.List<string?>();
        Enumerate(hwnd, names, null);
        return names;
    }

        public static bool Select(IntPtr hwnd, string? recordedTitle)
    {
        if (string.IsNullOrWhiteSpace(recordedTitle)) return false;
        var names = new System.Collections.Generic.List<string?>();
        var items = new System.Collections.Generic.List<IUIAutomationElement?>();
        if (!Enumerate(hwnd, names, items) || names.Count <= 1) return false;
        int hit = TabMatch.Find(recordedTitle, names);
        return hit >= 0 && items[hit] is { } el && Uia.Select(el);
    }

    private static bool Enumerate(IntPtr hwnd, System.Collections.Generic.List<string?> names,
                                  System.Collections.Generic.List<IUIAutomationElement?>? items)
    {
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            var uia = Uia.Create();
            if (uia is null) return false;
            if (uia.ElementFromHandle(hwnd, out var window) != 0 || window is null) return false;
            if (uia.CreatePropertyCondition(Uia.ControlTypeProp, Uia.TabItemType, out var cond) != 0 || cond is null)
                return false;
            if (window.FindAll(Uia.TreeScopeDescendants, cond, out var found) != 0 || found is null) return false;
            if (found.get_Length(out int n) != 0 || n <= 0) return false;
            for (int i = 0; i < n; i++)
            {
                var e = found.GetElement(i, out var el) == 0 ? el : null;
                names.Add(e is null ? null : Uia.PropString(e, Uia.NameProp));
                items?.Add(e);
            }
            return true;
        }
        catch { return false; }
    }
}

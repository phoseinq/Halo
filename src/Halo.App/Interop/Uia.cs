using System;
using System.Runtime.InteropServices;

namespace Halo.Interop;

internal static class Uia
{
    public const int NameProp = 30005, ControlTypeProp = 30003, BoundingRectProp = 30001, ClassNameProp = 30012;
    public const int ValuePattern = 10002;
    public const int SliderType = 50015, TextType = 50020, TabItemType = 50019, TabType = 50018;
    public const int TreeScopeDescendants = 4;

    private static readonly Guid ClsidCUIAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");

    public static IUIAutomation? Create()
    {
        try
        {
            var t = Type.GetTypeFromCLSID(ClsidCUIAutomation);
            return t != null ? (IUIAutomation?)Activator.CreateInstance(t) : null;
        }
        catch { return null; }
    }

    public static string PropString(IUIAutomationElement e, int prop)
    {
        try { e.GetCurrentPropertyValue(prop, out var v); return v as string ?? ""; }
        catch { return ""; }
    }

    public static double[] PropRect(IUIAutomationElement e)
    {
        try { e.GetCurrentPropertyValue(BoundingRectProp, out var v); return v as double[] ?? Array.Empty<double>(); }
        catch { return Array.Empty<double>(); }
    }

    public static string? PatternValue(IUIAutomationElement e)
    {
        try
        {
            e.GetCurrentPattern(ValuePattern, out var raw);
            return raw is IUIAutomationValuePattern vp && vp.get_CurrentValue(out var s) == 0 ? s : null;
        }
        catch { return null; }
    }
}

[ComImport, Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomation
{
    [PreserveSig] int Gap01();
    [PreserveSig] int Gap02();
    [PreserveSig] int Gap03();
    [PreserveSig] int ElementFromHandle(IntPtr hwnd, out IUIAutomationElement element);
    [PreserveSig] int Gap05();
    [PreserveSig] int Gap06();
    [PreserveSig] int Gap07();
    [PreserveSig] int Gap08();
    [PreserveSig] int Gap09();
    [PreserveSig] int Gap10();
    [PreserveSig] int Gap11();
    [PreserveSig] int Gap12();
    [PreserveSig] int Gap13();
    [PreserveSig] int Gap14();
    [PreserveSig] int Gap15();
    [PreserveSig] int Gap16();
    [PreserveSig] int Gap17();
    [PreserveSig] int Gap18();
    [PreserveSig] int Gap19();
    [PreserveSig] int Gap20();
    [PreserveSig] int CreatePropertyCondition(int propertyId,
        [MarshalAs(UnmanagedType.Struct)] object value, out IUIAutomationCondition condition);
}

[ComImport, Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationElement
{
    [PreserveSig] int Gap01();
    [PreserveSig] int Gap02();
    [PreserveSig] int FindFirst(int scope, IUIAutomationCondition condition, out IUIAutomationElement? found);
    [PreserveSig] int FindAll(int scope, IUIAutomationCondition condition, out IUIAutomationElementArray? found);
    [PreserveSig] int Gap05();
    [PreserveSig] int Gap06();
    [PreserveSig] int Gap07();
    [PreserveSig] int GetCurrentPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);
    [PreserveSig] int Gap09();
    [PreserveSig] int Gap10();
    [PreserveSig] int Gap11();
    [PreserveSig] int Gap12();
    [PreserveSig] int Gap13();
    [PreserveSig] int GetCurrentPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object pattern);
}

[ComImport, Guid("14314595-B4BC-4055-95F2-58F2E42C9855"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationElementArray
{
    [PreserveSig] int get_Length(out int length);
    [PreserveSig] int GetElement(int index, out IUIAutomationElement element);
}

[ComImport, Guid("352FFBA8-0973-437C-A61F-F64CAFD81DF9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationCondition
{
}

[ComImport, Guid("A94CD8B1-0844-4CD6-9D2D-640537AB39E9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationValuePattern
{
    [PreserveSig] int SetValue([MarshalAs(UnmanagedType.BStr)] string value);
    [PreserveSig] int get_CurrentValue([MarshalAs(UnmanagedType.BStr)] out string value);
}

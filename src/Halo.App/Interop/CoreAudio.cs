using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Halo.Interop;

internal static class CoreAudio
{

    internal static IMMDevice? DefaultRender()
    {
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            return en.GetDefaultAudioEndpoint(0, 1, out var dev) == 0 ? dev : null;
        }
        catch { return null; }
    }

        internal static string? DefaultRenderId()
    {
        try
        {
            var dev = DefaultRender();
            return dev != null && dev.GetId(out var id) == 0 ? id : null;
        }
        catch { return null; }
    }

    internal static string? DefaultIdFor(int role)
    {
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (en.GetDefaultAudioEndpoint(0, role, out var dev) != 0 || dev == null) return null;
            return dev.GetId(out var id) == 0 ? id : null;
        }
        catch { return null; }
    }

        internal static List<(IMMDevice Device, string Name, string Id)> ActiveRenderEndpoints()
    {
        var found = new List<(IMMDevice, string, string)>();
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            const int ACTIVE = 1;
            if (en.EnumAudioEndpoints(0, ACTIVE, out var all) != 0 || all == null) return found;
            if (all.GetCount(out uint n) != 0) return found;
            for (uint i = 0; i < n; i++)
            {
                if (all.Item(i, out var dev) != 0 || dev == null) continue;
                if (dev.GetId(out var id) != 0) id = "?";
                found.Add((dev, FriendlyName(dev), id));
            }
        }
        catch { }
        return found;
    }

    private static string FriendlyName(IMMDevice dev)
    {
        try
        {
            const uint STGM_READ = 0;
            if (dev.OpenPropertyStore(STGM_READ, out var store) != 0 || store == null) return "?";

            var key = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
            if (store.GetValue(ref key, out var pv) != 0) return "?";
            try { return pv.Vt == 31 && pv.Data != IntPtr.Zero ? Marshal.PtrToStringUni(pv.Data) ?? "?" : "?"; }
            finally { try { PropVariantClear(ref pv); } catch { } }
        }
        catch { return "?"; }
    }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pv);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey(Guid fmtid, uint pid)
    {
        internal Guid Fmtid = fmtid;
        internal uint Pid = pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropVariant
    {
        internal ushort Vt;
        internal ushort R1, R2, R3;
        internal IntPtr Data;
        internal IntPtr Data2;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
}

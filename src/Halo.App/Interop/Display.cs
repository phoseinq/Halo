using System;
using System.Runtime.InteropServices;

namespace Halo.Interop;

internal static class Display
{
    internal readonly record struct Info(int Hz, float Dpi);

    private const int MONITOR_DEFAULTTOPRIMARY = 1;
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int CCHDEVICENAME = 32;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public Win32.RECT rcMonitor;
        public Win32.RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)] public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
    private static extern bool EnumDisplaySettings(string? device, int mode, ref DEVMODE devMode);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    internal static Info Probe(IntPtr hwnd)
    {
        int hz = 0;
        float dpi = 0f;
        try
        {
            string? device = DeviceUnder(hwnd);
            var mode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };

            if (EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref mode)) hz = (int)mode.dmDisplayFrequency;
        }
        catch { }
        try
        {

            uint raw = hwnd != IntPtr.Zero ? GetDpiForWindow(hwnd) : 0;
            if (raw == 0) raw = GetDpiForSystem();
            if (raw > 0) dpi = raw / 96f;
        }
        catch { }
        return new Info(hz, dpi);
    }

    private static string? DeviceUnder(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return null;
            IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
            if (mon == IntPtr.Zero) return null;
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = "" };
            return GetMonitorInfo(mon, ref info) && info.szDevice.Length > 0 ? info.szDevice : null;
        }
        catch { return null; }
    }
}

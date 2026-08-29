using System;
using System.Runtime.InteropServices;

namespace Halo.Widgets;

internal sealed class AudioMeter
{
    private IAudioMeterInformation? _meterI;
    private IAudioEndpointVolume? _vol;
    private static Guid _ctx = Guid.Empty;
    private string? _boundId;
    private long _nextDeviceCheck;

    public AudioMeter() => TryAcquire();

    public float Peak()
    {
        DropIfDeviceChanged();
        if (_meterI == null) { TryAcquire(); if (_meterI == null) return 0f; }
        try { _meterI!.GetPeakValue(out float p); return p; }
        catch { _meterI = null; return 0f; }
    }

    public (float L, float R) StereoPeak()
    {
        DropIfDeviceChanged();
        if (_meterI == null) { TryAcquire(); if (_meterI == null) return (0f, 0f); }
        try
        {

            if (_meterI!.GetMeteringChannelCount(out uint n) != 0 || n == 0 || n > 32) return (0f, 0f);
            var buf = _peaks is { } p && p.Length == n ? p : _peaks = new float[n];
            if (_meterI.GetChannelsPeakValues(n, buf) != 0) return (0f, 0f);
            return n == 1 ? (buf[0], buf[0]) : (buf[0], buf[1]);
        }
        catch { _meterI = null; return (0f, 0f); }
    }

    private float[]? _peaks;

    public float Volume()
    {
        DropIfDeviceChanged();
        if (_vol == null) { TryAcquire(); if (_vol == null) return 0f; }
        try { _vol!.GetMasterVolumeLevelScalar(out float v); return v; }
        catch { _vol = null; return 0f; }
    }

    public bool Muted()
    {
        DropIfDeviceChanged();
        if (_vol == null) return false;
        try { _vol!.GetMute(out bool m); return m; }
        catch { _vol = null; return false; }
    }

    public void SetVolume(float v)
    {
        DropIfDeviceChanged();
        if (_vol == null) { TryAcquire(); if (_vol == null) return; }
        try { _vol!.SetMasterVolumeLevelScalar(Math.Clamp(v, 0f, 1f), ref _ctx); }
        catch { _vol = null; }
    }

    public void Unmute()
    {
        DropIfDeviceChanged();
        if (_vol == null) { TryAcquire(); if (_vol == null) return; }
        try { _vol!.GetMute(out bool m); if (m) _vol.SetMute(false, ref _ctx); }
        catch { _vol = null; }
    }

    public void ToggleMute()
    {
        DropIfDeviceChanged();
        if (_vol == null) { TryAcquire(); if (_vol == null) return; }
        try { _vol!.GetMute(out bool m); _vol.SetMute(!m, ref _ctx); }
        catch { _vol = null; }
    }

        private void DropIfDeviceChanged()
    {
        try
        {
            if (Environment.TickCount64 < _nextDeviceCheck) return;
            _nextDeviceCheck = Environment.TickCount64 + 1000;
            if (_boundId is null) return;
            if (Halo.Interop.CoreAudio.DefaultRenderId() is not { } id || id == _boundId) return;
            _meterI = null;
            _vol = null;
            _boundId = null;
        }
        catch { }
    }

    private void TryAcquire()
    {
        try
        {
            var dev = Halo.Interop.CoreAudio.DefaultRender();
            if (dev == null) return;
            if (dev.GetId(out var id) == 0) _boundId = id;
            var mid = typeof(IAudioMeterInformation).GUID;
            if (dev.Activate(ref mid, 23, IntPtr.Zero, out var mo) == 0) _meterI = mo as IAudioMeterInformation;
            var vid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref vid, 23, IntPtr.Zero, out var vo) == 0) _vol = vo as IAudioEndpointVolume;
        }
        catch { _meterI = null; _vol = null; }
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
        [PreserveSig] int GetMeteringChannelCount(out uint count);

        [PreserveSig] int GetChannelsPeakValues(uint count, [MarshalAs(UnmanagedType.LPArray)] float[] peaks);

        [PreserveSig] int QueryHardwareSupport(out uint mask);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float levelDb, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}

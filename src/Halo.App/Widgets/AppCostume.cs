using System;
using System.Collections.Generic;

namespace Halo.Widgets;

internal static class AppCostume
{
    private static readonly Dictionary<string, FaceProp> Known = new(StringComparer.OrdinalIgnoreCase)
    {

        ["spotify"] = FaceProp.Headphones,
        ["musicbee"] = FaceProp.Headphones,
        ["foobar2000"] = FaceProp.Headphones,
        ["aimp"] = FaceProp.Headphones,
        ["itunes"] = FaceProp.Headphones,
        ["applemusic"] = FaceProp.Headphones,
        ["deezer"] = FaceProp.Headphones,
        ["tidal"] = FaceProp.Headphones,
        ["wmplayer"] = FaceProp.Headphones,
        ["groove"] = FaceProp.Headphones,

        ["vlc"] = FaceProp.Goggles,
        ["mpc-hc"] = FaceProp.Goggles,
        ["mpc-hc64"] = FaceProp.Goggles,
        ["mpv"] = FaceProp.Goggles,
        ["potplayer"] = FaceProp.Goggles,
        ["potplayermini64"] = FaceProp.Goggles,

        ["idman"] = FaceProp.Download,
        ["jdownloader2"] = FaceProp.Download,
        ["fdm"] = FaceProp.Download,
        ["qbittorrent"] = FaceProp.Download,
        ["utorrent"] = FaceProp.Download,
        ["transmission-qt"] = FaceProp.Download,
    };

        internal static FaceProp For(string? processName)
        => processName is { Length: > 0 } && Known.TryGetValue(processName, out var prop)
            ? prop : FaceProp.None;
}

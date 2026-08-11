namespace Halo.Shell;

internal static class DuplicateLaunch
{

    internal const double LogonWindowSeconds = 45.0;

        internal static bool ShouldOpenPanel(bool askedForSettings, double? winnerAgeSeconds)
    {

        if (askedForSettings) return true;

        if (winnerAgeSeconds is not { } age) return true;
        return age > LogonWindowSeconds;
    }
}

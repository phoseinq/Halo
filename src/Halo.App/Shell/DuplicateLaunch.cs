namespace Halo.Shell;

internal static class DuplicateLaunch
{

    internal const double LogonWindowSeconds = 45.0;

    internal const double SessionWindowSeconds = 180.0;

        internal static bool ShouldOpenPanel(bool askedForSettings, double? winnerAgeSeconds, double? sessionAgeSeconds)
    {

        if (askedForSettings) return true;

        if (sessionAgeSeconds is { } session && session <= SessionWindowSeconds) return false;
        if (winnerAgeSeconds is { } age && age <= LogonWindowSeconds) return false;

        return true;
    }
}

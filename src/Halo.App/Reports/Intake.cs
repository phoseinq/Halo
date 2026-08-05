using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Halo.Reports;

internal static class Intake
{
    internal const string Endpoint = "https://halo.pvboy.dev:2053/v1/reports";

    internal const string Key =
        "halo1.eyJpYXQiOjE3ODU4Nzk5MDEsImp0aSI6IjQ5YzBhMzVkNmI0M2I1OGQiLCJzdWIiOiJzaGlwcGVkIn0"
        + ".q_T9NY68-7sF82YB0aswh9YYyoirR8Da-fB0Zq2Er1kfvZ1tNONJKbV5DWa78x6igW0J_qmoCRg_WIAqcdnvBQ";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    private static string SettingsPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "settings.json");

    internal static Halo.Settings.SettingsFile Settings()
    {
        try { return Halo.Settings.SettingsFile.Read(SettingsPath); }
        catch { return Halo.Settings.SettingsFile.Empty; }
    }

    internal static bool AutoCrash(Halo.Settings.SettingsFile? file = null)
    {
        try
        {
            return (file ?? Settings())
                .Bool(Halo.Settings.SettingsKeys.AutoCrashReport,
                      Halo.Settings.SettingsKeys.AutoCrashDefault);
        }
        catch { return false; }
    }

    private static readonly TimeSpan Remember = TimeSpan.FromDays(1);

    private static string FingerprintPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "crash-sent");

    private const int RememberedCrashes = 8;

    internal static string Fingerprint(Exception? ex)
    {
        string top = (ex?.StackTrace ?? "").Split('\n', 2)[0].Trim();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes((ex?.GetType().FullName ?? "none") + "|" + top)))[..16];
    }

    internal static bool CrashIsNew(Exception? ex)
    {
        try
        {
            string hash = Fingerprint(ex);
            foreach (var line in Recent())
            {
                var parts = line.Split('\t');
                if (parts.Length != 2 || parts[0] != hash) continue;
                if (DateTimeOffset.TryParse(parts[1], null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var when)
                    && DateTimeOffset.UtcNow - when < Remember)
                    return false;
            }
            return true;
        }

        catch { return true; }
    }

    internal static void RememberSent(Exception? ex)
    {
        try
        {
            string path = FingerprintPath;
            string hash = Fingerprint(ex);
            var kept = new List<string> { hash + "\t" + DateTimeOffset.UtcNow.ToString("o") };
            foreach (var line in Recent())
                if (line.Split('\t') is { Length: 2 } p && p[0] != hash && kept.Count < RememberedCrashes)
                    kept.Add(line);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllLines(path, kept);
        }
        catch { }
    }

    private static string[] Recent()
    {
        try { return System.IO.File.Exists(FingerprintPath) ? System.IO.File.ReadAllLines(FingerprintPath) : []; }
        catch { return []; }
    }

    internal static bool TrySend(string json, Halo.Settings.SettingsFile? file = null)
    {
        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            file ??= Settings();
            string? rawEndpoint = file.Raw(Destination.EndpointKey);
            var kind = Destination.Decide(rawEndpoint, Endpoint);
            if (kind == Destination.Kind.Off) return false;
            string target = kind == Destination.Kind.Custom ? rawEndpoint!.Trim() : Endpoint;
            string? bearer = Destination.Key(kind, Key,
                file.Raw(Destination.KeyKey));
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;

            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            if (bearer is not null)
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            using var response = client.Send(request);
            return response.IsSuccessStatusCode;
        }

        catch { return false; }
    }
}

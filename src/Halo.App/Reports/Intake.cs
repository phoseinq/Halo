using System;
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

    internal static bool AutoCrash()
    {
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Halo", "settings.json");
            return Halo.Settings.SettingsFile.Read(path)
                .Bool(Halo.Settings.SettingsKeys.AutoCrashReport, false);
        }
        catch { return false; }
    }

    private static readonly TimeSpan Remember = TimeSpan.FromDays(1);

    private static string FingerprintPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "crash-sent");

    internal static bool CrashIsNew(Exception? ex)
    {
        try
        {
            string top = (ex?.StackTrace ?? "").Split('\n', 2)[0].Trim();
            string print = (ex?.GetType().FullName ?? "none") + "|" + top;

            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(print)))[..16];

            string path = FingerprintPath;
            if (System.IO.File.Exists(path))
            {
                var parts = System.IO.File.ReadAllText(path).Split('\t');
                if (parts.Length == 2 && parts[0] == hash
                    && DateTimeOffset.TryParse(parts[1], null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var when)
                    && DateTimeOffset.UtcNow - when < Remember)
                    return false;
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, hash + "\t" + DateTimeOffset.UtcNow.ToString("o"));
            return true;
        }

        catch { return true; }
    }

    internal static bool TrySend(string json)
    {
        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Key);
            using var response = client.Send(request);
            return response.IsSuccessStatusCode;
        }

        catch { return false; }
    }
}

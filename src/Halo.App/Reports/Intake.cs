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
            return Halo.Settings.SettingsFile.Read(path).Bool("report.autoCrash", false);
        }
        catch { return false; }
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

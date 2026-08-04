using System;
using System.Net.Http;
using System.Text;

namespace Halo.Reports;

internal static class Intake
{
    internal const string Endpoint = "https://halo.pvboy.dev:2053/v1/reports";

    internal const string Key =
        "halo1.eyJpYXQiOjE3ODU4NjkxMzgsImp0aSI6IjM0MTIyZDkwYzliYTVmNGYiLCJzdWIiOiJob3NlaW4ifQ"
        + ".M6XivJ3lyd3ieKWP4FJAiwqc2vNSbG4cXXAizIU6yNiugu71BQtNWW-fOLQFB8O328pDEUPl-U3ZYZ2ozggQDw";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

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

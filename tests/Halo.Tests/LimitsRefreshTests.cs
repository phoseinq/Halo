using System.Text.Json.Nodes;
using Halo.ClaudeCode;

namespace Halo.Tests;

// Halo refreshes Claude Code's OAuth token itself when nobody else has: on a desktop-app-only machine the CLI
// never runs, its token expires, and every usage read after that was refused.
public class LimitsRefreshTests
{
    [Fact]
    public void Refreshes_a_minute_before_expiry_and_after_it()
    {
        long exp = 1_000_000;
        Assert.False(Limits.NeedsRefresh(exp, exp - 61_000));
        Assert.True(Limits.NeedsRefresh(exp, exp - 59_000));
        Assert.True(Limits.NeedsRefresh(exp, exp + 5_000));
    }

    [Fact]
    public void A_token_with_no_expiry_is_left_alone()
        => Assert.False(Limits.NeedsRefresh(null, 123));

    private static JsonObject Cred() => JsonNode.Parse("""
        {"claudeAiOauth":{"accessToken":"old-a","refreshToken":"old-r","expiresAt":5,
         "scopes":["user:inference","user:profile"],"subscriptionType":"pro","rateLimitTier":"t"},
         "other":{"keep":true}}
        """)!.AsObject();

    [Fact]
    public void Folds_in_the_new_pair_and_keeps_everything_else()
    {
        var cred = Cred();
        var u = Limits.ApplyRefresh(cred, "new-a", "new-r", 3600, nowMs: 1_000);
        var o = u["claudeAiOauth"]!;
        Assert.Equal("new-a", o["accessToken"]!.GetValue<string>());
        Assert.Equal("new-r", o["refreshToken"]!.GetValue<string>());
        Assert.Equal(1_000 + 3_600_000, o["expiresAt"]!.GetValue<long>());
        Assert.Equal("pro", o["subscriptionType"]!.GetValue<string>());
        Assert.Equal(2, o["scopes"]!.AsArray().Count);
        Assert.True(u["other"]!["keep"]!.GetValue<bool>());
        Assert.Equal("old-a", cred["claudeAiOauth"]!["accessToken"]!.GetValue<string>());   // input untouched
    }

    [Fact]
    public void A_response_without_a_refresh_token_keeps_the_old_one()
    {
        var u = Limits.ApplyRefresh(Cred(), "new-a", null, 60, nowMs: 0);
        Assert.Equal("old-r", u["claudeAiOauth"]!["refreshToken"]!.GetValue<string>());
    }
}

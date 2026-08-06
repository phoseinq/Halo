extern alias settingsasm;
using Xunit;

namespace Halo.Tests;

// Where a report goes. The rows that set these were deleted from the panel with no migration, and
// SettingsFile keeps unknown keys forever, so the values are still sitting in real settings.json files
// meaning what the old row said they meant. Every one of these went wrong once.
// Serialised against the other classes that touch it: Strings.Use switches the ACTIVE LANGUAGE for
// the whole process, so a test that reads a localized string while another has just moved to
// Persian reads the wrong one. It failed one run in three before this - a settings label, a mood
// set and a report route, never the same one twice, which is what a shared global looks like.
[Collection("locale")]
public class DestinationTests
{
    private const string Built = "https://halo.pvboy.dev:2053/v1/reports";

    // The old panel rows wrote BOTH keys with the shipped defaults, so an ordinary settings.json on any
    // machine that ever opened that page carries report.endpoint pointing at Halo's own intake. Treating
    // that as "custom" would drop Halo's token for the one destination it is scoped to and 401 every send.
    [Fact]
    public void The_built_in_address_spelled_out_is_still_the_built_in_one()
    {
        var kind = Halo.Reports.Destination.Decide(Built, Built);
        Assert.Equal(Halo.Reports.Destination.Kind.BuiltIn, kind);
        // and so it still carries Halo's key, which is the half that would have 401'd
        Assert.Equal("halo1.builtin", Halo.Reports.Destination.Key(kind, "halo1.builtin", null));
    }

    [Fact]
    public void An_absent_key_means_the_built_in_intake()
        => Assert.Equal(Halo.Reports.Destination.Kind.BuiltIn, Halo.Reports.Destination.Decide(null, Built));

    // The one the first attempt at this got wrong, and the one that matters most: it read the value
    // through Text(key, Intake.Endpoint), which folds "" into the fallback - so a user who had emptied
    // the row on the strength of "leave it empty and reports never touch the network" was posted on
    // behalf of anyway. Absent and empty are different instructions.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_value_means_never_send(string raw)
        => Assert.Equal(Halo.Reports.Destination.Kind.Off, Halo.Reports.Destination.Decide(raw, Built));

    [Fact]
    public void A_set_value_means_their_host()
        => Assert.Equal(Halo.Reports.Destination.Kind.Custom,
                        Halo.Reports.Destination.Decide("https://my-intake.example/ingest", Built));

    // Halo's bearer token is scoped to Halo's intake. Attaching it to a custom endpoint would disclose the
    // shipped credential to an arbitrary host AND fail that host's auth at the same time.
    [Fact]
    public void Halos_key_never_leaves_halos_intake()
    {
        Assert.Equal("halo1.builtin",
            Halo.Reports.Destination.Key(Halo.Reports.Destination.Kind.BuiltIn, "halo1.builtin", null));
        Assert.Equal("theirs",
            Halo.Reports.Destination.Key(Halo.Reports.Destination.Kind.Custom, "halo1.builtin", " theirs "));
        Assert.Null(
            Halo.Reports.Destination.Key(Halo.Reports.Destination.Kind.Custom, "halo1.builtin", null));
        // the hole the first version left: the old panel rows wrote BOTH keys with the shipped defaults,
        // so a user who edited only the endpoint still carries Halo's token in report.key - and reading it
        // back and forwarding it is the same disclosure, through the door the fix left open
        Assert.Null(
            Halo.Reports.Destination.Key(Halo.Reports.Destination.Kind.Custom, "halo1.builtin",
                                         "  halo1.builtin  "));
        Assert.Null(
            Halo.Reports.Destination.Key(Halo.Reports.Destination.Kind.Off, "halo1.builtin", "theirs"));
    }

    // Two executables, no shared code, same rules - the same duplication IntakeContractTests pins for the
    // endpoint and the key themselves. A panel that refuses to send while the pill's crash handler still
    // posts is the worst possible split.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://my-intake.example/ingest")]
    public void Both_executables_decide_the_same_way(string? raw)
        => Assert.Equal((int)Halo.Reports.Destination.Decide(raw, Built),
                        (int)settingsasm::Halo.Settings.Destination.Decide(raw, Built));

    [Fact]
    public void Both_executables_agree_on_the_key_names()
    {
        Assert.Equal(Halo.Reports.Destination.EndpointKey,
                     settingsasm::Halo.Settings.Destination.EndpointKey);
        Assert.Equal(Halo.Reports.Destination.KeyKey, settingsasm::Halo.Settings.Destination.KeyKey);
    }

    // THROUGH THE READER, from a real file on disk. Every assertion above is a pure function, and all of
    // them passed while the shipped feature was broken: both loaders dropped an empty value at load, so
    // "" arrived at Decide as null and "never send" posted to the built-in intake anyway. Three releases
    // in a row this fix failed one layer below where it was tested. This is that layer.
    [Theory]
    [InlineData("\"\"", (int)Halo.Reports.Destination.Kind.Off)]
    [InlineData("\"   \"", (int)Halo.Reports.Destination.Kind.Off)]
    [InlineData("\"https://mine.example/in\"", (int)Halo.Reports.Destination.Kind.Custom)]
    [InlineData("\"" + Built + "\"", (int)Halo.Reports.Destination.Kind.BuiltIn)]
    public void The_decision_survives_a_round_trip_through_settings_json(string literal, int expected)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "halo-dest-" + System.Guid.NewGuid().ToString("n") + ".json");
        try
        {
            System.IO.File.WriteAllText(path,
                "{\"version\":1,\"values\":{\"report.endpoint\":" + literal + "}}");
            var file = Halo.Settings.SettingsFile.Read(path);
            Assert.Equal(expected,
                (int)Halo.Reports.Destination.Decide(file.Raw(Destination_EndpointKey), Built));
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    private const string Destination_EndpointKey = "report.endpoint";

    // A key absent from the file is still "not configured", which must stay distinct from empty.
    [Fact]
    public void An_absent_key_survives_the_round_trip_too()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "halo-dest-" + System.Guid.NewGuid().ToString("n") + ".json");
        try
        {
            System.IO.File.WriteAllText(path, """{"version":1,"values":{"appearance.scale":"100%"}}""");
            var file = Halo.Settings.SettingsFile.Read(path);
            Assert.Null(file.Raw(Destination_EndpointKey));
            Assert.Equal(Halo.Reports.Destination.Kind.BuiltIn,
                Halo.Reports.Destination.Decide(file.Raw(Destination_EndpointKey), Built));
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    // Both Key implementations, not just Decide - Key is where the credential logic lives now, and the
    // two copies had to be edited by hand.
    [Theory]
    [InlineData((int)Halo.Reports.Destination.Kind.BuiltIn, null)]
    [InlineData((int)Halo.Reports.Destination.Kind.Custom, "theirs")]
    [InlineData((int)Halo.Reports.Destination.Kind.Custom, "halo1.anything")]
    [InlineData((int)Halo.Reports.Destination.Kind.Custom, null)]
    [InlineData((int)Halo.Reports.Destination.Kind.Off, "theirs")]
    public void Both_executables_pick_the_same_key(int kind, string? raw)
        => Assert.Equal(
            Halo.Reports.Destination.Key((Halo.Reports.Destination.Kind)kind, "halo1.builtin", raw),
            settingsasm::Halo.Settings.Destination.Key(
                (settingsasm::Halo.Settings.Destination.Kind)kind, "halo1.builtin", raw));

    // a revoked Halo token from an earlier generation is still a Halo credential
    [Fact]
    public void No_halo_issued_token_of_any_generation_reaches_a_third_party()
        => Assert.Null(Halo.Reports.Destination.Key(
            Halo.Reports.Destination.Kind.Custom, "halo1.current", "halo1.revokedFirstGeneration"));

    // a trailing slash is the same intake, and demoting it would strip the token and 401
    [Fact]
    public void A_cosmetic_variant_of_the_built_in_url_is_still_the_built_in_one()
        => Assert.Equal(Halo.Reports.Destination.Kind.BuiltIn,
                        Halo.Reports.Destination.Decide(Built + "/", Built));

    // Keeping empty values at load is what makes "never send" reachable, and it must not leak into any
    // other reader. Bool was the one that noticed: a stored "" is present but not "on", so every setting
    // whose default is TRUE would have flipped off for a file carrying an empty value.
    [Fact]
    public void An_empty_value_does_not_override_a_true_default()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "halo-bool-" + System.Guid.NewGuid().ToString("n") + ".json");
        try
        {
            System.IO.File.WriteAllText(path, """{"version":1,"values":{"general.startup":""}}""");
            var file = Halo.Settings.SettingsFile.Read(path);
            Assert.True(file.Bool("general.startup", true));
            Assert.Equal("on", file.Text("general.startup", "on"));
            Assert.Equal("", file.Raw("general.startup"));   // and Raw still sees it
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    // The WIRING, not just the pieces. Every assertion above and below is a pure function, and the wiring
    // between them is where this bug landed four releases running - Send() built its own Store on the
    // default path, so Store -> Raw -> Decide -> bearer -> target could only be exercised by pressing the
    // button on a machine that happened to have the right file. Resolve is that path as a value.
    [Fact]
    public void The_off_route_carries_a_reason_and_no_target()
    {
        var r = Halo.Reports.Destination.Resolve("", null, Built, "halo1.builtin");
        Assert.Equal(Halo.Reports.Destination.Kind.Off, r.Kind);
        Assert.NotNull(r.Error);
        Assert.Null(r.Bearer);
    }

    [Fact]
    public void The_built_in_route_carries_halos_key()
    {
        var r = Halo.Reports.Destination.Resolve(null, null, Built, "halo1.builtin");
        Assert.Null(r.Error);
        Assert.Equal(Built, r.Target);
        Assert.Equal("halo1.builtin", r.Bearer);
    }

    // the credential half, end to end: their host, their key, and never Halo's whatever report.key holds
    [Fact]
    public void A_custom_route_never_carries_halos_key()
    {
        var mine = Halo.Reports.Destination.Resolve("https://mine.example/in", "mykey", Built, "halo1.builtin");
        Assert.Null(mine.Error);
        Assert.Equal("https://mine.example/in", mine.Target);
        Assert.Equal("mykey", mine.Bearer);

        var leaked = Halo.Reports.Destination.Resolve("https://mine.example/in", "halo1.builtin", Built, "halo1.builtin");
        Assert.Null(leaked.Bearer);
    }

    [Fact]
    public void A_non_https_custom_target_is_refused_by_name()
    {
        var r = Halo.Reports.Destination.Resolve("http://mine.example/in", null, Built, "halo1.builtin");
        Assert.NotNull(r.Error);
        Assert.Contains("report.endpoint", r.Error);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("https://mine.example/in", "halo1.builtin")]
    [InlineData("https://mine.example/in", "mykey")]
    public void Both_executables_resolve_the_same_route(string? endpoint, string? key)
    {
        var a = Halo.Reports.Destination.Resolve(endpoint, key, Built, "halo1.builtin");
        var b = settingsasm::Halo.Settings.Destination.Resolve(endpoint, key, Built, "halo1.builtin");
        Assert.Equal((int)a.Kind, (int)b.Kind);
        Assert.Equal(a.Target, b.Target);
        Assert.Equal(a.Bearer, b.Bearer);
        Assert.Equal(a.Error, b.Error);
    }
}
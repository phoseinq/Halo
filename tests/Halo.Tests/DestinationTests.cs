extern alias settingsasm;
using Xunit;

namespace Halo.Tests;

// Where a report goes. The rows that set these were deleted from the panel with no migration, and
// SettingsFile keeps unknown keys forever, so the values are still sitting in real settings.json files
// meaning what the old row said they meant. Every one of these went wrong once.
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
}

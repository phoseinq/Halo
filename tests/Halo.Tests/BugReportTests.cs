using System.Text.Json;
using Halo.Reports;

namespace Halo.Tests;

// The payload is the security boundary of the whole feature, so it is the part with tests. Halo mirrors
// other people's notifications, media titles, tray filenames and agent transcripts; a report that leaks
// one of those leaks a stranger's message, not the user's own data. The allowlist test below is the one
// that matters - it fails the moment a field appears that nobody named.
public class BugReportTests
{
    private static ReportFacts Facts(string? exType = null, string message = "", string[]? stack = null,
                                     string[]? inner = null)
        => new("manual", "2026-08-03T00:00:00Z", "3.3.0.0", "10.0.26200.0", "2560x1440 @ 280 Hz", 96,
               ".NET 9.0.18", "en-US", 16, 32768, 42,
               "MediaWidget", ["MediaWidget", "ClaudeWidget"], false, false, 280,
               exType, message, stack ?? [], inner ?? [], "it went wrong");

    [Fact]
    public void The_report_carries_exactly_the_fields_that_were_named()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(Facts()));
        var keys = new List<string>();
        foreach (var p in doc.RootElement.EnumerateObject()) keys.Add(p.Name);
        Assert.Equal(
            ["kind", "at", "halo", "windows", "display", "dpi", "runtime", "locale", "machine",
             "uptime_min", "surface", "description"],
            keys);
    }

    [Fact]
    public void The_surface_block_is_shape_and_never_content()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(Facts()));
        var keys = new List<string>();
        foreach (var p in doc.RootElement.GetProperty("surface").EnumerateObject()) keys.Add(p.Name);
        Assert.Equal(["primary", "live", "expanded", "heavy", "tier"], keys);
    }

    [Fact]
    public void The_machine_block_is_shape_and_never_identity()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(Facts()));
        var keys = new List<string>();
        foreach (var p in doc.RootElement.GetProperty("machine").EnumerateObject()) keys.Add(p.Name);
        Assert.Equal(["cpus", "ram_mb"], keys);
    }

    // The wrapper is usually the useless one - "one or more errors occurred" over the real failure - so
    // the chain is what makes a crash report actionable.
    [Fact]
    public void A_wrapped_exception_carries_its_inner_chain()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(
            Facts("System.AggregateException", "one or more errors occurred", ["at Halo.Frame()"],
                  ["System.IO.IOException: the file is locked"])));
        var inner = doc.RootElement.GetProperty("exception").GetProperty("inner");
        Assert.Equal("System.IO.IOException: the file is locked", inner[0].GetString());
    }

    [Fact]
    public void An_unwrapped_exception_has_no_inner_block()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(Facts("System.Exception", "boom")));
        Assert.False(doc.RootElement.GetProperty("exception").TryGetProperty("inner", out _));
    }

    [Fact]
    public void The_inner_chain_stops_rather_than_walking_a_cycle_forever()
    {
        var deep = new Exception("1", new Exception("2", new Exception("3",
                       new Exception("4", new Exception("5", new Exception("6"))))));
        Assert.True(ReportPayload.InnerChain(deep).Count <= 5);
    }

    // A manual report has no exception, and an empty exception block invites a reader to think one was
    // stripped. Absent says what happened.
    [Fact]
    public void A_report_with_no_exception_has_no_exception_block()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(Facts()));
        Assert.False(doc.RootElement.TryGetProperty("exception", out _));
    }

    [Fact]
    public void A_crash_report_carries_the_exception()
    {
        using var doc = JsonDocument.Parse(
            ReportPayload.Json(Facts("System.InvalidOperationException", "boom", ["at Halo.Frame()"])));
        var ex = doc.RootElement.GetProperty("exception");
        Assert.Equal("System.InvalidOperationException", ex.GetProperty("type").GetString());
        Assert.Equal("boom", ex.GetProperty("message").GetString());
        Assert.Equal("at Halo.Frame()", ex.GetProperty("stack")[0].GetString());
    }

    // The user typed it, in front of a preview of the file. Editing it behind their back would make the
    // preview a claim about the payload rather than the payload.
    [Fact]
    public void The_users_own_description_goes_in_verbatim()
    {
        using var doc = JsonDocument.Parse(ReportPayload.Json(Facts()));
        Assert.Equal("it went wrong", doc.RootElement.GetProperty("description").GetString());
    }

    // A stack frame's path names the account and the folder layout. The file name debugs just as well.
    [Fact]
    public void A_stack_frames_path_is_reduced_to_its_file_name()
    {
        string line = @"   at Halo.Widgets.MediaWidget.EnsureArt() in C:\Users\someone\Projects\Halo\src\Halo.App\Widgets\MediaWidget.cs:line 148";
        string clean = Scrub.Paths(line);
        Assert.Contains("MediaWidget.cs:line 148", clean);
        Assert.DoesNotContain("someone", clean);
        Assert.DoesNotContain(@"C:\", clean);
    }

    [Fact]
    public void A_unc_path_is_scrubbed_too()
    {
        string clean = Scrub.Paths(@"could not open \\fileserver\share\private\notes.txt");
        Assert.DoesNotContain("fileserver", clean);
        Assert.Contains("notes.txt", clean);
    }

    // A path with no last segment has nothing safe to keep, and returning the match would return the path.
    [Fact]
    public void A_bare_root_becomes_a_placeholder_rather_than_itself()
        => Assert.Equal("drive <path> is full", Scrub.Paths(@"drive C:\ is full"));

    [Fact]
    public void The_account_name_is_removed_even_with_no_path_around_it()
        => Assert.Equal("<user> is not permitted", Scrub.User("hosein is not permitted", "hosein"));

    // Replacing a two-letter name everywhere would shred ordinary words for no gain.
    [Fact]
    public void A_very_short_account_name_is_left_alone()
        => Assert.Equal("an ad in a badge", Scrub.User("an ad in a badge", "ad"));

    [Fact]
    public void Scrubbing_survives_null_and_empty()
    {
        Assert.Equal("", Scrub.All(null, "hosein"));
        Assert.Equal("", Scrub.All("", "hosein"));
    }

    // Both halves run: the path goes, and the account name that was not inside a path goes too.
    [Fact]
    public void All_applies_the_path_and_the_name()
    {
        string clean = Scrub.All(@"hosein could not read C:\Users\hosein\halo\tray.txt", "hosein");
        Assert.DoesNotContain("hosein", clean);
        Assert.Contains("tray.txt", clean);
    }

    [Fact]
    public void An_exception_with_no_stack_yields_no_lines()
        => Assert.Empty(ReportPayload.StackLines(new Exception("never thrown")));

    [Fact]
    public void A_thrown_exceptions_stack_comes_back_scrubbed_line_by_line()
    {
        try { throw new InvalidOperationException("boom"); }
        catch (Exception ex)
        {
            var lines = ReportPayload.StackLines(ex);
            Assert.NotEmpty(lines);
            foreach (var line in lines)
            {
                Assert.DoesNotContain(@":\", line);   // no drive-rooted path survived
                Assert.DoesNotContain("\n", line);    // one frame per entry, so the preview is readable
            }
        }
    }

    // Two caps, not one: a count alone lets a crash loop park ten 400 KB stacks on disk, and a size alone
    // lets one enormous report survive while nine useful ones are dropped for it.
    [Fact]
    public void The_store_caps_both_the_count_and_the_bytes()
    {
        Assert.Equal(10, ReportStore.MaxFiles);
        Assert.Equal(2 * 1024 * 1024, ReportStore.MaxBytes);
    }

    // The shape file is a contract between two executables, so its round trip is pinned like settings.json's.
    [Fact]
    public void The_shape_file_round_trips()
    {
        string body = ShapeReport.Format("MediaWidget", ["MediaWidget", "ClaudeWidget"], true, false, 120);
        var map = new Dictionary<string, string>();
        foreach (var line in body.Split('\n'))
        {
            int eq = line.IndexOf('=');
            if (eq > 0) map[line[..eq]] = line[(eq + 1)..];
        }
        Assert.Equal("MediaWidget", map["primary"]);
        Assert.Equal("MediaWidget,ClaudeWidget", map["live"]);
        Assert.Equal("1", map["expanded"]);
        Assert.Equal("0", map["heavy"]);
        Assert.Equal("120", map["tier"]);
    }
}

using System;
using System.Globalization;
using System.Threading;
using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The log exists to be read months later against a sign-in timestamp, so its shape is the contract.
public class LaunchLogTests
{
    private static readonly DateTime Stamp = new(2026, 8, 12, 14, 18, 31, 402);

    [Fact]
    public void A_duplicate_records_the_age_it_decided_on_and_what_it_decided()
    {
        string line = LaunchLog.LaunchLine(Stamp, 5678, won: false, askedForSettings: false, 39.44, openPanel: false);
        Assert.Contains("2026-08-12 14:18:31.402", line);
        Assert.Contains("pid=5678 lost winnerAge=39.4s asked=no panel=no", line);
        Assert.EndsWith("\r\n", line);
    }

    // An age that could not be read is the branch that opens the panel, so it has to be visible as itself
    // rather than as a zero.
    [Fact]
    public void An_unknown_age_is_written_as_a_question_mark()
        => Assert.Contains("winnerAge=? asked=no panel=yes",
            LaunchLog.LaunchLine(Stamp, 1, won: false, askedForSettings: false, null, openPanel: true));

    [Fact]
    public void The_launch_that_won_carries_no_age_to_report()
    {
        string line = LaunchLog.LaunchLine(Stamp, 1234, won: true, askedForSettings: false, null, openPanel: false);
        Assert.Contains("pid=1234 won asked=no", line);
        Assert.DoesNotContain("winnerAge", line);
    }

    [Fact]
    public void A_panel_records_which_path_opened_it()
        => Assert.Contains("panel reason=tray started=yes stamp=yes",
               LaunchLog.PanelLine(Stamp, "tray", started: true, stamped: true));

    // The stamp is the panel's only authorisation to show a window, so this pair - launched, but unstamped - is
    // a panel that will open nothing, and the log has to be able to say so. It can only happen if the state
    // directory has stopped being writable, which is worth one line rather than a silent dead icon.
    [Fact]
    public void A_panel_launched_without_its_stamp_says_so()
        => Assert.Contains("started=yes stamp=no",
            LaunchLog.PanelLine(Stamp, "tray", started: true, stamped: false));

    // The exe missing beside us hid a packaging bug for the whole life of the panel; started=no is the line
    // that would have caught it.
    [Fact]
    public void A_panel_that_never_started_says_so()
        => Assert.Contains("started=no", LaunchLog.PanelLine(Stamp, "duplicate", started: false, stamped: false));

    // This machine runs fa-IR. Under it the current culture renders 39.4 with a Persian decimal separator and
    // the log stops being greppable, which is exactly the sort of thing found while reading it for something
    // else at 3am.
    [Fact]
    public void The_line_is_the_same_under_a_persian_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("fa-IR");
            Assert.Contains("winnerAge=39.4s",
                LaunchLog.LaunchLine(Stamp, 7, won: false, askedForSettings: false, 39.44, openPanel: false));
            Assert.Contains("2026-08-12 14:18:31.402",
                LaunchLog.PanelLine(Stamp, "argv", started: true, stamped: true));
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }
}

using System;
using System.IO;
using Halo.Reports;
using Xunit;

namespace Halo.Tests;

// Nine always-on debug logs with no ceiling between them, on a program meant to run from logon to logoff for
// months. Measured on this machine after a few weeks: compact-debug 724KB, dl-debug 386KB, notif-debug 217KB.
// What is pinned here is the shape of the trim, because a cap that discards the wrong half is worse than no
// cap at all - these files exist to say what happened just before the thing you are looking at.
public sealed class DebugFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halo-debugfile-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public DebugFileTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "log.txt");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void A_log_under_the_cap_is_left_alone()
    {
        File.WriteAllText(_path, "one\ntwo\n");

        DebugFile.Trim(_path, capBytes: 4096);

        Assert.Equal("one\ntwo\n", File.ReadAllText(_path));
    }

    [Fact]
    public void A_missing_log_is_not_an_error()
    {
        DebugFile.Trim(Path.Combine(_dir, "never-written.txt"));
        Assert.False(File.Exists(Path.Combine(_dir, "never-written.txt")));
    }

    // The newest lines are the whole value of these files, so an oversized one keeps its tail. A cap that
    // truncated or deleted would reliably throw away the interesting half at the moment it filled up.
    [Fact]
    public void An_oversized_log_keeps_its_newest_lines_and_drops_its_oldest()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4000; i++) sb.Append("line ").Append(i).Append('\n');
        File.WriteAllText(_path, sb.ToString());
        long before = new FileInfo(_path).Length;

        DebugFile.Trim(_path, capBytes: 8192);

        string after = File.ReadAllText(_path);
        Assert.True(new FileInfo(_path).Length < before);
        Assert.EndsWith("line 3999\n", after);       // the newest survived
        Assert.DoesNotContain("line 0\n", after);    // the oldest did not
    }

    // The seek lands in the middle of whatever line happened to be there, and a log that opens mid-sentence
    // reads as corruption rather than as a trim.
    [Fact]
    public void The_kept_text_starts_on_a_line_boundary()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4000; i++) sb.Append("a line of some length, number ").Append(i).Append('\n');
        File.WriteAllText(_path, sb.ToString());

        DebugFile.Trim(_path, capBytes: 8192);

        Assert.StartsWith("a line of some length, number ", File.ReadAllText(_path));
    }

    // One enormous line has no boundary to cut on, so there is nothing to keep. It must not throw and must
    // not leave half a line behind.
    [Fact]
    public void A_single_line_bigger_than_the_cap_is_dropped_rather_than_halved()
    {
        File.WriteAllText(_path, new string('x', 40_000));

        DebugFile.Trim(_path, capBytes: 8192);

        Assert.Equal("", File.ReadAllText(_path));
    }

    // The counter starts at zero so the first write of a session checks - which is what trims a file a
    // previous run left oversized, rather than waiting for another fifty lines to be added to it.
    [Fact]
    public void The_first_append_of_a_session_trims_a_file_left_oversized()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4000; i++) sb.Append("old ").Append(i).Append('\n');
        File.WriteAllText(_path, sb.ToString());

        DebugFile.Append(_path, "fresh\n", capBytes: 8192);

        string after = File.ReadAllText(_path);
        Assert.True(new FileInfo(_path).Length < 9000);
        Assert.EndsWith("fresh\n", after);
        Assert.DoesNotContain("old 0\n", after);
    }

    [Fact]
    public void Appending_writes_the_line()
    {
        DebugFile.Append(_path, "hello\n");
        DebugFile.Append(_path, "again\n");

        Assert.Equal("hello\nagain\n", File.ReadAllText(_path));
    }
}

extern alias hooksasm;
using System;
using System.Collections.Generic;
using hooksasm::Halo.Hooks;

namespace Halo.Tests;

// A compact does not truncate the transcript. The pre-compact turns stay in the file and the summary is
// appended as one `user` line carrying isCompactSummary, so a backward scan for the newest usage block walks
// straight past the boundary and reads the context that was full a second ago.
//
// Measured on a real transcript that had compacted: 789,397 tokens on the last usage line before the marker,
// 43,722 on the first one after it. Between post-compact firing and the next API call there is no line after
// the marker at all - which is the whole gap the pill spent insisting the context was still full.
public sealed class TranscriptScanTests
{
    private const long Big = 789_397, Small = 43_722;

    // concatenated rather than a raw literal: the shape ends in three closing braces, which no sensible
    // number of '$' makes readable
    private static string Usage(long input, long cacheRead = 0, long cacheCreate = 0, long output = 0,
        string ts = "2026-08-02T22:00:00.000Z", string model = "claude-opus-5") =>
        "{\"timestamp\":\"" + ts + "\",\"message\":{\"model\":\"" + model + "\",\"usage\":{"
        + "\"input_tokens\":" + input
        + ",\"cache_read_input_tokens\":" + cacheRead
        + ",\"cache_creation_input_tokens\":" + cacheCreate
        + ",\"output_tokens\":" + output + "}}}";

    private const string Boundary =
        """{"type":"user","isCompactSummary":true,"timestamp":"2026-08-02T22:14:58.103Z","message":{"role":"user"}}""";

    private static TranscriptScan.Reading Read(params string[] lines) =>
        TranscriptScan.Read(lines, DateTimeOffset.MinValue);

    [Fact]
    public void ReadsTheNewestUsageWhenNothingHasCompacted()
    {
        var r = Read(Usage(1000), Usage(input: 20, cacheRead: Small - 20));
        Assert.Equal(Small, r.Latest);
        Assert.False(r.Compacted);
    }

    // the regression: the pill kept the pre-compact figure because the scan never stopped
    [Fact]
    public void StopsAtTheCompactBoundaryInsteadOfReadingThePreCompactContext()
    {
        var r = Read(Usage(input: 397, cacheRead: Big - 397), Boundary);
        Assert.Equal(0, r.Latest);
        Assert.True(r.Compacted);
    }

    [Fact]
    public void TakesTheFirstUsageWrittenAfterTheBoundary()
    {
        var r = Read(Usage(input: 397, cacheRead: Big - 397), Boundary, Usage(input: 722, cacheRead: Small - 722));
        Assert.Equal(Small, r.Latest);
        Assert.False(r.Compacted);   // a reading was found before the boundary was ever reached
    }

    // two compacts in one session: only the newest boundary may govern
    [Fact]
    public void OnlyTheNewestBoundaryCounts()
    {
        var r = Read(Usage(50_000), Boundary, Usage(input: 397, cacheRead: Big - 397), Boundary);
        Assert.Equal(0, r.Latest);
        Assert.True(r.Compacted);
    }

    [Fact]
    public void AnEmptyOrGarbageTranscriptIsNotACompact()
    {
        var r = Read("", "not json", "{}");
        Assert.Equal(0, r.Latest);
        Assert.False(r.Compacted);   // nothing to clear: the caller must leave what it had alone
    }

    [Fact]
    public void ModelComesFromTheLineTheReadingCameFrom()
    {
        var r = Read(Usage(1000, model: "claude-sonnet-5"), Usage(2000, model: "claude-opus-5"));
        Assert.Equal("claude-opus-5", r.Model);
    }

    // the turn figure is this turn's real consumption, so cache READS are excluded - they are the old
    // context being re-read, not anything this turn spent
    [Fact]
    public void TurnSumsEveryCallSinceThePromptStartedAndSkipsCacheReads()
    {
        var started = DateTimeOffset.Parse("2026-08-02T22:10:00.000Z");
        var lines = new List<string>
        {
            Usage(input: 100, cacheCreate: 10, output: 5, ts: "2026-08-02T22:05:00.000Z"),   // before the turn
            Usage(input: 200, cacheRead: 9_000, cacheCreate: 20, output: 7, ts: "2026-08-02T22:11:00.000Z"),
            Usage(input: 300, cacheRead: 9_000, cacheCreate: 30, output: 9, ts: "2026-08-02T22:12:00.000Z"),
        };
        var r = TranscriptScan.Read(lines, started);
        Assert.Equal(200 + 20 + 7 + 300 + 30 + 9, r.Turn);
    }

    // a turn that began before a compact must not have its consumption summed across the boundary either
    [Fact]
    public void TurnStopsAtTheBoundaryToo()
    {
        var started = DateTimeOffset.Parse("2026-08-02T22:00:00.000Z");
        var lines = new List<string>
        {
            Usage(input: 500, cacheCreate: 50, output: 5, ts: "2026-08-02T22:05:00.000Z"),
            Boundary,
            Usage(input: 200, cacheCreate: 20, output: 7, ts: "2026-08-02T22:20:00.000Z"),
        };
        var r = TranscriptScan.Read(lines, started);
        Assert.Equal(200 + 20 + 7, r.Turn);
    }
}

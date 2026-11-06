using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

public class ModelLabelTests
{
    // the new shape: family first, then the version
    [Theory]
    [InlineData("claude-opus-5", "opus 5")]
    [InlineData("claude-fable-5", "fable 5")]
    [InlineData("claude-sonnet-5", "sonnet 5")]
    [InlineData("claude-opus-4-1-20250805", "opus 4.1")]
    [InlineData("claude-sonnet-4-5-20250929", "sonnet 4.5")]
    [InlineData("claude-haiku-4-5-20251001", "haiku 4.5")]
    [InlineData("claude-opus-4-20250514", "opus 4")]
    public void The_id_reads_as_a_name(string id, string expected)
        => Assert.Equal(expected, ClaudeCodeWidget.ModelLabel(id));

    // the old shape put the version BEFORE the family; collecting digits in the order they appear is what
    // makes one rule cover both, instead of a table that would print nothing for the next model released
    [Theory]
    [InlineData("claude-3-5-sonnet-20241022", "sonnet 3.5")]
    [InlineData("claude-3-opus-20240229", "opus 3")]
    [InlineData("claude-3-7-sonnet-20250219", "sonnet 3.7")]
    public void The_older_ids_read_the_same_way(string id, string expected)
        => Assert.Equal(expected, ClaudeCodeWidget.ModelLabel(id));

    [Fact]
    public void An_eight_digit_run_is_a_release_date_and_not_a_version()
        => Assert.Equal("opus 5", ClaudeCodeWidget.ModelLabel("claude-opus-5-20260401"));

    // a model this does not recognise still gets shown - the panel's job is to say what is answering, and
    // a blank would be a worse answer than an unfamiliar one
    [Fact]
    public void An_unknown_family_is_shown_rather_than_dropped()
        => Assert.Equal("kingfisher-2", ClaudeCodeWidget.ModelLabel("claude-kingfisher-2"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_known_is_null_so_the_row_simply_does_not_carry_it(string? id)
        => Assert.Null(ClaudeCodeWidget.ModelLabel(id));

    [Fact]
    public void Case_and_whitespace_do_not_matter()
        => Assert.Equal("opus 5", ClaudeCodeWidget.ModelLabel("  Claude-Opus-5  "));
}

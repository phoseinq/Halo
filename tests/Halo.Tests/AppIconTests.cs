using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// Which running process an AUMID is allowed to claim is the icon chain's second tier, and getting it wrong is
// worse than answering nothing: a match returns SOME exe, the chain stops there, and the notification wears
// whatever that exe's icon is - or Windows' generic placeholder when it has none, which reads as "no icon".
public class AppIconNameMatchTests
{
    // The reported one. "gh" is the GitHub CLI, it was running, and the letters g-h sit inside "GHUB", so a
    // Logitech toast drew gh.exe's icon. Nothing about these two is related.
    [Fact]
    public void A_process_name_buried_inside_a_word_is_not_a_match()
    {
        Assert.False(AppIcon.NameMatches("Logi.GHUB.Systray", "gh"));
        Assert.False(AppIcon.NameMatches("Logi.GHUB.Systray", "hub"));
        Assert.False(AppIcon.NameMatches("Microsoft.YourPhone_8wekyb3d8bbwe!App", "our"));
    }

    [Fact]
    public void A_whole_segment_is_a_match()
    {
        Assert.True(AppIcon.NameMatches("Chrome.exe", "chrome"));
        Assert.True(AppIcon.NameMatches("Google.Chrome", "Chrome"));
        Assert.True(AppIcon.NameMatches("Spotify.exe", "Spotify"));
        Assert.True(AppIcon.NameMatches("Microsoft.YourPhone_8wekyb3d8bbwe!App", "App"));
    }

    // The segment and the process disagree about a ".exe" tail depending on which spelling the AUMID uses,
    // and that difference is not evidence of a different app.
    [Fact]
    public void An_exe_tail_on_either_side_does_not_break_the_match()
    {
        Assert.True(AppIcon.NameMatches("Spotify.exe", "Spotify.exe"));
        Assert.True(AppIcon.NameMatches("Code.exe", "Code"));
    }

    // Two-letter names carry almost no evidence and are common enough to collide constantly. Losing this tier
    // for such an app is the right trade: the shell resolver and the toast's own logo are still ahead of it
    // and behind it, and a missing icon beats another app's icon.
    [Fact]
    public void A_two_letter_process_name_is_refused_even_as_a_whole_segment()
    {
        Assert.False(AppIcon.NameMatches("gh.Something", "gh"));
        Assert.False(AppIcon.NameMatches("wt.Terminal", "wt"));
        Assert.True(AppIcon.NameMatches("wtx.Terminal", "wtx"));
    }

    [Fact]
    public void Separators_other_than_the_dot_also_split_segments()
    {
        Assert.True(AppIcon.NameMatches("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "App"));
        Assert.True(AppIcon.NameMatches(@"C:\Program Files\Foo\Bar.exe", "Bar"));
    }

    [Fact]
    public void Nothing_matches_nothing()
    {
        Assert.False(AppIcon.NameMatches("", "chrome"));
        Assert.False(AppIcon.NameMatches("Chrome.exe", ""));
    }
}

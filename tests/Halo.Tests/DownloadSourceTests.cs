using Halo.Widgets;

namespace Halo.Tests;

public sealed class DownloadSourceTests
{
    // ── partial-file classification ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(@"C:\Users\x\Downloads\ubuntu.iso.crdownload", "ubuntu.iso")]
    [InlineData(@"C:\Users\x\Downloads\movie.mkv.part", "movie.mkv")]
    [InlineData(@"C:\Users\x\Downloads\game.zip.opdownload", "game.zip")]
    [InlineData(@"C:\Users\x\Downloads\setup.exe.partial", "setup.exe")]
    [InlineData(@"C:\Users\x\Downloads\big.bin.aria2", "big.bin")]
    [InlineData(@"C:\Users\x\Downloads\linux.iso.!ut", "linux.iso")]
    public void PartialSuffixes_yield_the_real_filename(string path, string expected)
    {
        Assert.True(PartialFiles.IsPartial(path, out var name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(@"C:\Users\x\Downloads\ubuntu.iso")]
    [InlineData(@"C:\Users\x\Downloads\notes.txt")]
    [InlineData(@"C:\Users\x\Downloads\archive.partly")]   // suffix must be exact, not a prefix match
    public void OrdinaryFiles_are_not_partial(string path)
    {
        Assert.False(PartialFiles.IsPartial(path, out _));
    }

    [Fact]
    public void ChromesPlaceholderName_is_reported_as_unknown()
    {
        // "Unconfirmed 123456.crdownload" carries no real name, so the caller must fall back to the
        // browser's own record rather than showing the placeholder
        Assert.True(PartialFiles.IsPartial(@"C:\d\Unconfirmed 934617.crdownload", out var name));
        Assert.Equal("", name);
    }

    // ── steam appmanifest ─────────────────────────────────────────────────────────────────────────
    private const string Manifest = """
        "AppState"
        {
        	"appid"		"570"
        	"name"		"Dota 2"
        	"StateFlags"		"1026"
        	"BytesToDownload"		"25108240"
        	"BytesDownloaded"		"12554120"
        }
        """;

    [Fact]
    public void SteamManifest_yields_name_and_bytes()
    {
        Assert.True(SteamInstall.Parse(Manifest, out var item));
        Assert.Equal("Dota 2", item.Name);
        Assert.Equal(12554120, item.Done);
        Assert.Equal(25108240, item.Total);
    }

    [Fact]
    public void SteamManifest_without_byte_counts_is_rejected()
    {
        Assert.False(SteamInstall.Parse("\"AppState\"\n{\n\t\"name\"\t\t\"Half-Life\"\n}", out _));
    }

    [Fact]
    public void SteamManifest_never_reports_more_done_than_total()
    {
        // a finished-then-verifying manifest can briefly carry downloaded > total; a percentage above
        // 100 would be a lie, so Total is clamped up instead
        const string odd = "\"AppState\"\n{\n\t\"name\"\t\t\"X\"\n\t\"BytesToDownload\"\t\t\"100\"\n\t\"BytesDownloaded\"\t\t\"250\"\n}";
        Assert.True(SteamInstall.Parse(odd, out var item));
        Assert.True(item.Done <= item.Total);
    }

    [Fact]
    public void SteamManifest_garbage_does_not_throw()
    {
        Assert.False(SteamInstall.Parse("", out _));
        Assert.False(SteamInstall.Parse("\"\"\"\"\n{{{", out _));
        Assert.False(SteamInstall.Parse("not a manifest at all", out _));
    }

    // ── library folders ───────────────────────────────────────────────────────────────────────────
    [Fact]
    public void LibraryFolders_are_parsed_and_unescaped()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"C:\\Program Files (x86)\\Steam"
            	}
            	"1"
            	{
            		"path"		"H:\\SteamLibrary"
            	}
            }
            """;
        var libs = SteamInstall.ParseLibraries(vdf);
        Assert.Equal(2, libs.Count);
        Assert.Equal(@"C:\Program Files (x86)\Steam", libs[0]);
        Assert.Equal(@"H:\SteamLibrary", libs[1]);
    }

    [Fact]
    public void LibraryFolders_empty_input_is_empty_list()
    {
        Assert.Empty(SteamInstall.ParseLibraries(""));
        Assert.Empty(SteamInstall.ParseLibraries("{}"));
    }

    // ── when to ask Restart Manager who owns a partial file ───────────────────────────────────────
    // Measured with --probe-dlcost: RmGetList costs 70-90ms against a file a downloader is HOLDING OPEN,
    // against 8-13ms on an idle one, because it answers by walking every process's handle table. Asking
    // once per file per second for the length of a download is what made the whole machine stutter, so
    // the policy that decides whether to ask again is worth pinning down in both directions.
    [Fact]
    public void OwnerLookup_is_needed_the_first_time()
    {
        Assert.True(PartialFiles.NeedsOwnerLookup(cached: false, pid: 0, alive: false, ageMs: 0));
    }

    [Fact]
    public void OwnerLookup_is_skipped_while_the_named_owner_is_still_running()
    {
        Assert.False(PartialFiles.NeedsOwnerLookup(cached: true, pid: 4321, alive: true, ageMs: 600_000));
    }

    [Fact]
    public void OwnerLookup_repeats_once_the_named_owner_is_gone()
    {
        // a killed downloader whose partial another process picks up must not leave Cancel aimed at a pid
        // that has since been recycled onto somebody else
        Assert.True(PartialFiles.NeedsOwnerLookup(cached: true, pid: 4321, alive: false, ageMs: 0));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(14_999, false)]
    [InlineData(15_000, true)]
    public void Unnamed_owner_backs_off_instead_of_retrying_every_scan(long ageMs, bool expected)
    {
        // RM returning nothing is the worst case: it costs the same 90ms and yields no answer, so a retry
        // per second would be the stutter with none of the benefit
        Assert.Equal(expected, PartialFiles.NeedsOwnerLookup(cached: true, pid: 0, alive: false, ageMs));
    }
}

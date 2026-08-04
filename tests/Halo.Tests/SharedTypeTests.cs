using Halo.ClaudeCode;
using Halo.Interop;
using Xunit;

namespace Halo.Tests;

// These two types could not be NAMED from a test until the test project aliased the assemblies that link
// them - AppModel.cs is compiled into all three exes and HookMarks.cs into two, so an unqualified mention
// was CS0433, "exists in both". That is why every packaged branch in Autostart, Catalog, Live and Actions
// had no coverage at all: AppModel is the question all of them ask.
public class SharedTypeTests
{
    // The dangerous direction, pinned. GetCurrentPackageFullName answers ERROR_INSUFFICIENT_BUFFER when
    // there IS a package and APPMODEL_ERROR_NO_PACKAGE when there is not, and an earlier version also
    // admitted rc == 0 as success - which leaves the length at 0, builds an empty string, and an empty
    // string is not null, so IsPackaged answered TRUE on an ordinary install. A wrong true is what silently
    // stops autostart registering and makes the hook path resolve to an alias stub that is not there.
    // The test host is not packaged, so this is that exact case.
    [Fact]
    public void An_ordinary_process_is_not_packaged()
    {
        Assert.False(AppModel.IsPackaged);
        Assert.Null(AppModel.PackageFullName);
    }

    // An agent Halo has never connected has no mark, and "no mark" has to read as empty rather than as
    // either answer: HookConnect treats "done" as already-tried and "undone" as a decision never to
    // reconnect, so a missing file that parsed as either would silently disable the offer.
    [Fact]
    public void An_unknown_agent_has_no_mark()
        => Assert.Equal("", HookMarks.Of("an agent nobody has ever connected"));

    // the file is `agent=value` per line, and the two values are a wire format that outlives the build
    [Fact]
    public void The_two_marks_are_the_strings_on_disk()
    {
        Assert.Equal("done", HookMarks.Done);
        Assert.Equal("undone", HookMarks.Undone);
    }
}

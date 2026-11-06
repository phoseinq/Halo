using Halo.Agents;

namespace Halo.Tests;

// Codex's tool facts, read off the rollout's function_call: the program a shell line runs, the file a patch
// touches. The pill says "running dotnet..." instead of a mood.
public class ToolFactsTests
{
    [Theory]
    [InlineData("{\"cmd\":\"cd repo && dotnet build\"}", "dotnet build")]
    [InlineData("{\"command\":[\"bash\",\"-lc\",\"cd x; cargo test | tail\"]}", "cargo test")]
    [InlineData("{\"command\":[\"git\",\"status\"]}", "git status")]
    [InlineData("{\"path\":\"C:/repo/src/Fx.cs\"}", "Fx.cs")]
    [InlineData("{\"query\":\"layered window\"}", "layered window")]
    public void AnArgumentNamesItsTarget(string args, string expected)
        => Assert.Equal(expected, ToolFacts.CodexTarget(args, null));

    [Fact]
    public void APatchNamesItsFirstFile()
        => Assert.Equal("Fx.cs", ToolFacts.CodexTarget(null,
            "*** Begin Patch\n*** Update File: src/Halo.App/Widgets/Fx.cs\n@@\n-a\n+b\n*** End Patch"));

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"cmd\":\"cd x && echo hi\"}")]
    [InlineData("[1,2]")]
    public void NothingUsableNamesNothing(string? args)
        => Assert.Null(ToolFacts.CodexTarget(args, null));
}

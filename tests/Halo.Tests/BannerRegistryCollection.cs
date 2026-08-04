using System;
using Microsoft.Win32;
using Xunit;

namespace Halo.Tests;

// Every test that touches the notification settings key runs in this one collection, and the collection
// does not run in parallel with itself.
//
// This is not tidiness. The redirect is an environment variable, which is process-wide, so two test classes
// running at once meant one class's cleanup cleared it while the other was mid-test - and BannerApply.Root
// then fell back to the LIVE key. It really happened: a run left "app.three" sitting in the real
// notification settings of the development machine. A test that can silence the developer's own
// notifications is a worse bug than anything it was written to catch.
//
// The fixture sets the variable once for the whole collection and clears it only at the very end, so there
// is no window in which it is unset while a test is running.
public sealed class BannerRootFixture : IDisposable
{
    internal const string Scratch = @"Software\Halo\TestBannerRoot";

    public BannerRootFixture()
    {
        Environment.SetEnvironmentVariable("HALO_BANNER_ROOT", Scratch);
        Wipe();
    }

    public void Dispose()
    {
        Wipe();
        Environment.SetEnvironmentVariable("HALO_BANNER_ROOT", null);
    }

    private static void Wipe()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(Scratch, throwOnMissingSubKey: false); }
        catch { }
    }
}

[CollectionDefinition("banner-registry", DisableParallelization = true)]
public class BannerRegistryCollection : ICollectionFixture<BannerRootFixture> { }

using Halo.Widgets;

namespace Halo.Tests;

public sealed class AppCostumeTests
{
    [Fact]
    public void APlayerWearsTheHeadphonesRatherThanItsOwnIcon()
    {
        // reported as "on Spotify the app-eating one came instead of the headphone skin"
        Assert.Equal(FaceProp.Headphones, AppCostume.For("Spotify"));
    }

    [Fact]
    public void TheProcessNameIsMatchedWithoutCase()
    {
        // Process.ProcessName casing follows the exe on disk and is not something to depend on
        Assert.Equal(FaceProp.Headphones, AppCostume.For("spotify"));
        Assert.Equal(FaceProp.Headphones, AppCostume.For("SPOTIFY"));
    }

    [Fact]
    public void VlcGetsTheGogglesBecauseItIsTheVideoOne()
    {
        // and it agrees with VlcWidget.ArrivingProp on purpose: launching VLC and VLC's widget waking are
        // the same app, and two different answers for it would read as the pill changing its mind
        Assert.Equal(FaceProp.Goggles, AppCostume.For("vlc"));
    }

    [Fact]
    public void AnAppTheTableDoesNotKnowKeepsItsOwnIcon()
    {
        // None is not "no costume", it is "wear the icon" - the icon is never wrong, and a bad guess is
        // worse than it, which is why the table stays short
        Assert.Equal(FaceProp.None, AppCostume.For("chrome"));
        Assert.Equal(FaceProp.None, AppCostume.For("Code"));
    }

    [Fact]
    public void NothingIsNotAnApp()
    {
        Assert.Equal(FaceProp.None, AppCostume.For(null));
        Assert.Equal(FaceProp.None, AppCostume.For(""));
    }
}

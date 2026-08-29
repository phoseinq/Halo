using Halo.Launcher;

namespace Halo.Tests;

public sealed class TranslatorTests
{
    /// <summary>Persian "salaam" - RTL input, which is what picks the direction.</summary>
    private static readonly string Salaam = new([(char)0x0633, (char)0x0644, (char)0x0627, (char)0x0645]);

    [Fact]
    public void LangPair_GoesPersianToEnglishAndBack()
    {
        Assert.Equal("fa|en", Translator.LangPair("anything", rtl: true));
        Assert.Equal("en|fa", Translator.LangPair("anything", rtl: false));
    }

    [Fact]
    public void ParseResponse_TakesTheTranslation()
    {
        // Persian "salaam" as escapes - source files here stay ASCII
        const string Salaam = "\u0633\u0644\u0627\u0645";
        Assert.Equal(Salaam, Translator.ParseResponse(
            "{\"responseData\":{\"translatedText\":\"" + Salaam + "\"},\"responseStatus\":200}"));
    }

    [Fact]
    public void ParseResponse_AcceptsAStatusSentAsAString()
    {
        // the service sends responseStatus as a number sometimes and a string others
        Assert.Equal("hello", Translator.ParseResponse(
            """{"responseData":{"translatedText":"hello"},"responseStatus":"200"}"""));
    }

    [Fact]
    public void ParseResponse_RefusesANonSuccessBody()
    {
        // a 200 over HTTP can still carry a refusal, and putting THAT in the clipboard as if it were the
        // answer is the failure worth guarding
        Assert.Null(Translator.ParseResponse(
            """{"responseData":{"translatedText":"QUOTA EXCEEDED"},"responseStatus":429}"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{"responseStatus":200}""")]
    [InlineData("""{"responseData":{},"responseStatus":200}""")]
    [InlineData("""{"responseData":{"translatedText":""},"responseStatus":200}""")]
    [InlineData("""{"responseData":{"translatedText":"   "},"responseStatus":200}""")]
    public void ParseResponse_IsNullOnAnythingUnusable(string body)
        => Assert.Null(Translator.ParseResponse(body));

    // ---- the two languages -------------------------------------------------------------------------

    [Fact]
    public void TheLanguageTableIsUsableInBothDirections()
    {
        // the old list was nine "English to X" pairs - one column of a table pretending to be the table, so
        // German to English could not be asked for at all
        Assert.Equal("de|en", Translator.Resolve("de", "en", "hallo", rtl: false));
        Assert.Equal("en|de", Translator.Resolve("en", "de", "hello", rtl: false));
    }

    [Fact]
    public void EveryLanguageHasADistinctCodeAndAName()
    {
        Assert.Equal(Translator.Languages.Length, Translator.Languages.Select(l => l.Code).Distinct().Count());
        Assert.All(Translator.Languages, l => Assert.False(string.IsNullOrWhiteSpace(l.Name)));
        Assert.All(Translator.Languages, l => Assert.InRange(l.Code.Length, 2, 3));
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("DE", "German")]
    [InlineData("auto", "Detect")]
    [InlineData("", "Detect")]
    [InlineData(null, "Detect")]
    public void Name_ReadsACodeBackAsAWord(string? code, string expected)
        => Assert.Equal(expected, Translator.Name(code));

    [Fact]
    public void AnUnknownCodeShowsAsItself_RatherThanAsSomethingElse()
        => Assert.Equal("xx", Translator.Name("xx"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData("  ")]
    public void IsAuto_CoversEveryWayNothingHasBeenChosen(string? code)
        => Assert.True(Translator.IsAuto(code));

    [Fact]
    public void WithNothingChosen_ResolveFallsBackToTheAutoRule()
    {
        Assert.Equal("en|fa", Translator.Resolve(Translator.Auto, "", "hello", rtl: false));
        Assert.Equal("fa|en", Translator.Resolve(Translator.Auto, "", Salaam, rtl: true));
    }

    [Fact]
    public void ADetectedSourceIsFilledInFromTheTextWhenOnlyTheTargetIsChosen()
    {
        // "into German" is a complete request on its own - the left side is whatever was typed
        Assert.Equal("en|de", Translator.Resolve(Translator.Auto, "de", "hello", rtl: false));
        Assert.Equal("fa|de", Translator.Resolve(Translator.Auto, "de", Salaam, rtl: true));
    }

    [Fact]
    public void TheSameLanguageBothSidesIsNotATranslation_SoTheRuleTakesOver()
    {
        // the service answers en|en with the input echoed back, which reads as the feature being broken
        Assert.Equal("en|fa", Translator.Resolve("en", "en", "hello", rtl: false));
    }

    [Fact]
    public void Swap_ReversesAConcretePair()
        => Assert.Equal(("de", "en"), Translator.Swap("en", "de", detected: null));

    [Fact]
    public void Swap_OutOfDetect_UsesWhatTheTextWasDetectedAs()
        => Assert.Equal(("de", "fa"), Translator.Swap(Translator.Auto, "de", detected: "fa"));

    [Fact]
    public void Swap_IsRefusedRatherThanGuessed_WhenNothingHasBeenDetectedYet()
    {
        // inventing a source language is the same made-up value this project keeps rejecting
        Assert.Null(Translator.Swap(Translator.Auto, "de", detected: null));
        Assert.Null(Translator.Swap(Translator.Auto, "de", detected: "auto"));
    }

    [Fact]
    public void Swap_IsRefusedWhenThereIsNoTargetToPutOnTheLeft()
        => Assert.Null(Translator.Swap("en", "", detected: "en"));
}

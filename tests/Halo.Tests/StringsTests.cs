extern alias settingsasm;
using System;
using System.IO;
using System.Linq;
using Halo.Localization;
using Xunit;

namespace Halo.Tests;

// English is EMBEDDED rather than shipped as a loose file, alone among the locales. Every other language
// degrades to English when its file is missing or malformed; English has nowhere left to degrade to, so a
// deleted or half-copied en.json would put raw keys - "reports.off" - on screen in the one configuration
// every user starts in. The other seven stay loose so a translation can be fixed without a rebuild.
// Serialised against the other classes that touch it: Strings.Use switches the ACTIVE LANGUAGE for
// the whole process, so a test that reads a localized string while another has just moved to
// Persian reads the wrong one. It failed one run in three before this - a settings label, a mood
// set and a report route, never the same one twice, which is what a shared global looks like.
[Collection("locale")]
public class StringsTests
{
    [Fact]
    public void English_comes_from_the_assembly_and_needs_no_file_on_disk()
    {
        Strings.Use("English");
        Assert.Equal("Reports are switched off: report.endpoint in settings.json is empty.",
            Strings.Get("reports.off"));
    }

    // A key with no translation must look like a missing translation, not like a blank UI. An empty
    // string here would render as a gap nobody can attribute to anything.
    [Fact]
    public void A_key_that_does_not_exist_returns_itself()
    {
        Strings.Use("English");
        Assert.Equal("nothing.like.this", Strings.Get("nothing.like.this"));
    }

    [Fact]
    public void English_is_left_to_right()
    {
        Strings.Use("English");
        Assert.Equal("en", Strings.Lang);
        Assert.False(Strings.IsRtl);
    }

    // Seven of the eight locales are loose files, so this is the path every language but English takes.
    // Persian is the sample because it is the one unit 3 lands and the one whose direction differs.
    [Fact]
    public void A_locale_on_disk_overrides_the_embedded_English()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halo-loc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            // u062e u0627 u0645 u0648 u0634 is "khamush", "off" in Persian. Written as escapes on
            // BOTH sides - the fixture AND the assertion - because CLAUDE.md keeps every .cs in this
            // repo ASCII including test fixtures, and because an editor that resolves the escape while
            // writing puts the real character back. That is the failure this comment exists to survive.
            File.WriteAllText(Path.Combine(dir, "fa.json"),
                "{ \"reports.off\": \"\u062e\u0627\u0645\u0648\u0634\" }");

            Strings.Use("Persian", dir);
            Assert.Equal("\u062e\u0627\u0645\u0648\u0634", Strings.Get("reports.off"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // The case a half-finished translation actually produces, and the reason Get falls through twice
    // rather than once: a Persian file missing a key must show the English, not the key.
    [Fact]
    public void A_key_missing_from_the_locale_falls_back_to_English()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halo-loc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "fa.json"), "{ \"reports.off\": \"x\" }");
            Strings.Use("Persian", dir);

            // both halves, because asserting only the fallback passes just as well when the locale was
            // never loaded at all - which is exactly how this test read until it was checked against a
            // build with Persian removed from Known, where it stayed green
            Assert.Equal("x", Strings.Get("reports.off"));
            Assert.Equal("The built-in endpoint is not a valid https:// address.",
                Strings.Get("reports.builtInNotHttps"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void A_malformed_locale_file_degrades_to_English_rather_than_throwing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halo-loc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "fa.json"), "{ this is not json");
            Strings.Use("Persian", dir);

            // Persian is still the ACTIVE language - the file failed, the selection did not - and every
            // key shows English through the empty map. Asserting the language too is what separates
            // "parsed to nothing" from "never got here".
            Assert.Equal("fa", Strings.Lang);
            Assert.Equal("The built-in endpoint is not a valid https:// address.",
                Strings.Get("reports.builtInNotHttps"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Persian_is_right_to_left_and_English_is_not()
    {
        Strings.Use("Persian", null);
        Assert.Equal("fa", Strings.Lang);
        Assert.True(Strings.IsRtl);

        Strings.Use("English");
        Assert.False(Strings.IsRtl);
    }

    // An unrecognised value in settings.json - hand-edited, or written by a newer build - must not leave
    // the app with no language at all
    [Fact]
    public void An_unknown_label_reads_as_English()
    {
        Assert.Equal("en", Strings.Code("Klingon"));
        Assert.Equal("en", Strings.Code(null));
        Assert.Equal("en", Strings.Code(""));
    }

    // The linked file, asserted from the other side. Halo.Settings.csproj compiles the SAME Strings.cs
    // that Halo.App does, so this is not a contract test in the IntakeContractTests sense - there is no
    // drift to catch. It is here to fail loudly if someone "tidies up" the link into a second copy.
    [Fact]
    public void Both_executables_resolve_the_same_English_text()
    {
        Halo.Localization.Strings.Use("English");
        settingsasm::Halo.Localization.Strings.Use("English");
        Assert.Equal(Halo.Localization.Strings.Get("reports.off"),
            settingsasm::Halo.Localization.Strings.Get("reports.off"));
    }

    // Changed 2026-08-05 on the author's instruction: the default follows the Windows UI language, so a
    // Persian install opens in Persian without being told to. English is what an unrecognised one gets -
    // there are eight locales and rather more languages than that.
    [Theory]
    [InlineData("fa", "Persian")]
    [InlineData("FA", "Persian")]
    [InlineData("en", "English")]
    [InlineData("de", "English")]      // a real language Halo does not ship
    [InlineData("", "English")]
    [InlineData(null, "English")]
    public void The_default_follows_the_Windows_language_and_falls_back_to_English(string? code, string expected)
        => Assert.Equal(expected, Strings.LabelForCulture(code));

    // The row above tried fa, FA, en, de, "" and null, and every one of those codes is already two letters,
    // so the theory could not fail for the one language that is not: Known stores Chinese as "zh-Hans"
    // while CurrentUICulture.TwoLetterISOLanguageName reports "zh" for zh-CN, zh-Hans-CN and the rest.
    // A Chinese Windows therefore opened v4.0.0 in English, under a manifest that declares zh-Hans and a
    // Store listing that promises the app follows the system language.
    // Asserted through Code() rather than against the endonym, because a Chinese label written in this file
    // would break the ASCII-source rule the same way the range in Live.cs did.
    [Theory]
    [InlineData("zh")]
    [InlineData("ZH")]
    [InlineData("zh-Hans")]
    public void A_Chinese_Windows_opens_in_Chinese(string code)
        => Assert.Equal("zh-Hans", Strings.Code(Strings.LabelForCulture(code)));

    // Same mismatch reached the dev knob: Name("zh") found nothing, handed "zh" to Use, which found no
    // LABEL of that name and fell back to English - so HALO_LANG=zh rendered an English PNG and called it
    // a Chinese one, on the variable whose whole purpose is rendering the hooks in another language.
    [Fact]
    public void HALO_LANG_accepts_the_subtag_Windows_actually_reports()
        => Assert.Equal("zh-Hans", Strings.Code(Strings.Name("zh")));

    // The panel must not offer a language it cannot actually show - offering Persian with no fa.json is a
    // control the app cannot honour, which this repo has rejected twice already, for playback rate and for
    // invented percentages.
    //
    // This used to assert English ALONE, on the grounds that no locale file shipped beside the test
    // assembly. fa.json ships now, so the honest assertion is the rule itself rather than the count that
    // happened to satisfy it: English is always offered, and everything offered has a file behind it.
    [Fact]
    public void Only_languages_that_have_text_are_offered()
    {
        var shipped = Strings.Available(null);
        Assert.Contains("English", shipped);
        Assert.Contains("Persian", shipped);   // fa.json is real as of unit 3
        Assert.Equal(shipped.Count, shipped.Distinct().Count());

        var dir = Path.Combine(Path.GetTempPath(), "halo-loc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "fa.json"), "{ \"reports.off\": \"x\" }");
            Assert.Equal(["English", "Persian"], Strings.Available(dir));

            // a locale file for a language Known does not list is ignored rather than offered by its code
            File.WriteAllText(Path.Combine(dir, "zz.json"), "{ \"reports.off\": \"x\" }");
            Assert.Equal(["English", "Persian"], Strings.Available(dir));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void The_row_default_is_whatever_the_machine_is_set_to()
        => Assert.Equal(Strings.LabelForCulture(
                System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
            settingsasm::Halo.Settings.Catalog.LanguageRowFallback);

    // The locale file is read once per language and kept. ApplyLanguage runs on ANY settings change - it is
    // guarded by the settings version, not the language - so a re-read here would put file I/O behind every
    // slider in the panel. Proven by deleting the file out from under it: a second Use that still answers in
    // the loaded language cannot have gone back to disk.
    [Fact]
    public void A_language_already_loaded_is_not_read_from_disk_again()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halo-loc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "fa.json");
        try
        {
            File.WriteAllText(file, "{\"reports.off\": \"KEPT\"}");
            Strings.Forget();
            Strings.Use("Persian", dir);
            Assert.Equal("KEPT", Strings.Get("reports.off"));

            File.Delete(file);
            Strings.Use("English");
            Strings.Use("Persian", dir);
            Assert.Equal("KEPT", Strings.Get("reports.off"));   // from the cache, not from a file that is gone
        }
        finally
        {
            Strings.Forget();
            Strings.Use("English");
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // Re-selecting the language that is already active must not do work, and must not look like a change to
    // anything caching against Version.
    [Fact]
    public void Choosing_the_language_that_is_already_active_changes_nothing()
    {
        Strings.Use("English");
        int before = Strings.Version;
        Strings.Use("English");
        Strings.Use("English");
        Assert.Equal(before, Strings.Version);
    }

    // Notes for the translator live in the file itself, because JSON cannot carry a comment. They must
    // never reach a surface.
    [Fact]
    public void Underscored_keys_are_notes_and_are_not_strings()
    {
        Strings.Use("English");
        Assert.Equal("_readme", Strings.Get("_readme"));   // absent, so Get returns the key
    }

    // Word order is the first thing a translation moves, so the placeholders have to survive being
    // rearranged - and a locale whose arity is wrong must degrade, not throw, on the alert path.
    [Fact]
    public void A_format_string_survives_a_translator_moving_its_placeholders()
    {
        Strings.Use("English");
        Assert.Equal("High CPU usage \u2014 92%",
            Strings.Format("notice.load.title", Strings.Get("notice.load.cpu"), 92));
    }

    [Fact]
    public void A_locale_with_the_wrong_placeholder_count_degrades_instead_of_throwing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halo-loc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "fa.json"), "{\"notice.load.title\": \"{0} {1} {2}\"}");
            Strings.Forget();
            Strings.Use("Persian", dir);
            Assert.Equal("{0} {1} {2}", Strings.Format("notice.load.title", "CPU", 92));
        }
        finally
        {
            Strings.Forget();
            Strings.Use("English");
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // Every word on the settings pages now comes out of the locale file, and a row added without its two
    // entries would show its own key on screen - "settings.general.startup.label" in a settings window.
    // Strings.Get returning the key IS the miss, so that is what this looks for.
    [Fact]
    public void Every_settings_row_has_its_label_and_description()
    {
        settingsasm::Halo.Localization.Strings.Use("English");
        var missing = new System.Collections.Generic.List<string>();
        foreach (var page in settingsasm::Halo.Settings.Catalog.Pages)
        {
            foreach (var key in new[] { "page." + page.Id + ".label", "page." + page.Id + ".desc" })
                if (settingsasm::Halo.Localization.Strings.Get(key) == key) missing.Add(key);
            foreach (var section in page.Sections)
                foreach (var r in section.Rows)
                {
                    string label = "settings." + r.Key + ".label";
                    if (settingsasm::Halo.Localization.Strings.Get(label) == label) missing.Add(label);
                    // access.startup carries two descriptions, one per packaging shape, so a plain
                    // <key>.desc is not the only way for a row to be covered
                    string desc = "settings." + r.Key + ".desc";
                    bool has = settingsasm::Halo.Localization.Strings.Get(desc) != desc;
                    foreach (var variant in new[] { ".packaged.desc", ".task.desc" })
                    {
                        string v = "settings." + r.Key + variant;
                        has |= settingsasm::Halo.Localization.Strings.Get(v) != v;
                    }
                    if (!has) missing.Add(desc);
                }
        }
        Assert.True(missing.Count == 0, "no string for: " + string.Join(", ", missing));
    }

    // The keys are what settings.json is written with. A locale file that "translated" one would rename a
    // setting, so nothing may ever look one up by a localized name.
    [Fact]
    public void A_row_key_is_never_itself_a_localized_string()
    {
        settingsasm::Halo.Localization.Strings.Use("English");
        foreach (var page in settingsasm::Halo.Settings.Catalog.Pages)
            foreach (var section in page.Sections)
                foreach (var r in section.Rows)
                    Assert.Equal(r.Key, settingsasm::Halo.Localization.Strings.Get(r.Key));
    }

    // Every shipped locale must carry every key the FULLEST one carries, not every key English carries.
    // The six unit-4 files were generated against en.json and came out 79 keys short each, because the mood
    // slots live only in the locale files - English keeps its moods in Moods.cs. Nothing failed, nothing
    // threw; the agent status text just silently stayed English in six languages. So the bar is the widest
    // file, and this is what would have caught it.
    [Fact]
    public void Every_locale_carries_every_key_the_fullest_one_does()
    {
        var dir = Halo.Localization.Strings.LocalesDir;
        if (!Directory.Exists(dir)) return;   // a build that ships no loose locales has nothing to compare

        var files = Directory.GetFiles(dir, "*.json");
        Assert.NotEmpty(files);

        var loaded = files.ToDictionary(
            f => Path.GetFileNameWithoutExtension(f),
            f => System.Text.Json.JsonDocument.Parse(File.ReadAllText(f)).RootElement
                     .EnumerateObject().Select(x => x.Name)
                     .Where(n => !n.StartsWith('_')).ToHashSet(StringComparer.Ordinal));

        var fullest = loaded.OrderByDescending(kv => kv.Value.Count).First();
        var gaps = new System.Collections.Generic.List<string>();
        foreach (var (code, keys) in loaded)
        {
            var missing = fullest.Value.Except(keys).ToList();
            if (missing.Count > 0)
                gaps.Add($"{code} is missing {missing.Count} of {fullest.Key}'s keys, e.g. {missing[0]}");
        }
        Assert.True(gaps.Count == 0, string.Join("; ", gaps));
    }

    // A placeholder count that does not match is the one translation error that reaches the user as a raw
    // "{0}" or as a line with the number missing. Strings.Format degrades rather than throwing, so nothing
    // else would ever report it.
    [Fact]
    public void No_locale_changes_a_placeholder_count()
    {
        var dir = Halo.Localization.Strings.LocalesDir;
        if (!Directory.Exists(dir)) return;

        Halo.Localization.Strings.Use("English");

        var wrong = new System.Collections.Generic.List<string>();
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(f);
            foreach (var prop in System.Text.Json.JsonDocument.Parse(File.ReadAllText(f)).RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith('_') || prop.Value.ValueKind != System.Text.Json.JsonValueKind.String)
                    continue;
                string en = Halo.Localization.Strings.Get(prop.Name);
                if (en == prop.Name) continue;   // a key English does not have (the mood sets)
                string other = prop.Value.GetString() ?? "";
                foreach (var ph in new[] { "{0}", "{1}" })
                    if (en.Contains(ph) != other.Contains(ph))
                        wrong.Add($"{code}/{prop.Name} {ph}");
            }
        }
        Assert.True(wrong.Count == 0, string.Join(", ", wrong.Take(10)));
    }
}

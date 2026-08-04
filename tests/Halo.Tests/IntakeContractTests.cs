extern alias settingsasm;
using System.Collections.Generic;
using Xunit;

namespace Halo.Tests;

// Halo.App and Halo.Settings each carry their own copy of the intake address, the way Store and
// SettingsFile each carry their own copy of the settings shape - the two executables share no code, so
// that a change to one cannot stop the other from starting. The cost of that decision is that the copies
// can drift, and drift here means the crash handler and the send button quietly post to different places.
// This is the test that makes the duplication safe.
public class IntakeContractTests
{
    [Fact]
    public void Both_executables_agree_on_where_reports_go()
    {
        Assert.Equal(Halo.Reports.Intake.Endpoint, settingsasm::Halo.Settings.Intake.Endpoint);
        Assert.Equal(Halo.Reports.Intake.Key, settingsasm::Halo.Settings.Intake.Key);
    }

    // ReportWindow rejects a non-https endpoint at send time; a plain-http address baked in here would
    // mean that check can only ever fail, and the button would be dead on arrival.
    [Fact]
    public void The_endpoint_is_https()
        => Assert.StartsWith("https://", Halo.Reports.Intake.Endpoint, System.StringComparison.Ordinal);

    // The window's sample is what --render-report draws, so it is the picture anyone reviewing this
    // feature looks at. If it names a different set of fields than the real payload, the picture
    // documents a report that is never sent.
    [Fact]
    public void The_preview_sample_names_the_same_fields_the_real_payload_does()
    {
        var real = new List<string>();
        using (var doc = System.Text.Json.JsonDocument.Parse(
                   Halo.Reports.ReportPayload.Json(SampleFacts())))
            foreach (var p in doc.RootElement.EnumerateObject()) real.Add(p.Name);

        var sample = new List<string>();
        using (var doc = System.Text.Json.JsonDocument.Parse(
                   settingsasm::Halo.Settings.ReportWindow.SamplePreview))
            foreach (var p in doc.RootElement.EnumerateObject()) sample.Add(p.Name);

        Assert.Equal(real, sample);
    }

    private static Halo.Reports.ReportFacts SampleFacts()
        => new("manual", "2026-08-03T13:42:56Z", "3.4.0.0", "10.0.26200.0", "2560x1440 @ 280 Hz", 96,
               ".NET 9.0.18", "en-US", 20, 32492, 96,
               "MediaWidget", ["MediaWidget", "ClaudeWidget"], false, false, 280,
               null, null, [], [], "the album cover stays as the spotify logo for a whole track");

    [Fact]
    public void The_key_is_a_v1_token()
        => Assert.StartsWith("halo1.", Halo.Reports.Intake.Key, System.StringComparison.Ordinal);
}

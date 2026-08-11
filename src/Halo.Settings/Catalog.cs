using System.Collections.Generic;

namespace Halo.Settings;

internal enum PageId { Home, General, Features, Agents, Api, Access, DocsAbout }

internal enum RowKind { Toggle, Choice, Slider, Action, Status, Text }

internal sealed record Row(
    string Key,
    string Label,
    string Description,
    RowKind Kind,
    string Fallback,
    IReadOnlyList<string> Options,
    string ActionLabel = "");

internal sealed record Section(string Label, string Glyph, IReadOnlyList<Row> Rows);

internal sealed record Page(PageId Id, string Label, string Description, IReadOnlyList<Section> Sections);

internal sealed record NavGroup(string Header, IReadOnlyList<PageId> Pages);

internal static class Catalog
{

    private static string L(string key) => Halo.Localization.Strings.Get("settings." + key + ".label");
    private static string D(string key) => Halo.Localization.Strings.Get("settings." + key + ".desc");
    private static string P(string id) => Halo.Localization.Strings.Get("page." + id + ".label");
    private static string PD(string id) => Halo.Localization.Strings.Get("page." + id + ".desc");
    private static string S(string id) => Halo.Localization.Strings.Get("section." + id);
    private static string G(string id) => Halo.Localization.Strings.Get("nav." + id);

    private static Row Toggle(string key, string label, string description, bool on = true)
        => new(key, label, description, RowKind.Toggle, on ? "on" : "off", ["off", "on"]);

    private static Row Choice(string key, string label, string description, string fallback, params string[] options)
        => new(key, label, description, RowKind.Choice, fallback, options);

    private static Row Slider(string key, string label, string description, string fallback, params string[] stops)
        => new(key, label, description, RowKind.Slider, fallback, stops);

    private static Row Action(string key, string label, string description, string action)
        => new(key, label, description, RowKind.Action, "", [], action);

    internal static string LanguageRowFallback => Halo.Localization.Strings.SystemLabel;

    private static readonly object Gate = new();

    internal static NavGroup[] Nav
    {
        get
        {
            lock (Gate)
            {

                int version = Halo.Localization.Strings.Version;
                if (_navFor != version) { _nav = BuildNav(); _navFor = version; }
                return _nav!;
            }
        }
    }

    internal static Page[] Pages
    {
        get
        {
            lock (Gate)
            {
                int version = Halo.Localization.Strings.Version;
                if (_pagesFor != version) { _pages = BuildPages(); _pagesFor = version; }
                return _pages!;
            }
        }
    }

    private static NavGroup[]? _nav;
    private static Page[]? _pages;
    private static int _navFor = -1, _pagesFor = -1;

    private static NavGroup[] BuildNav() =>
    [
        new("", [PageId.Home]),
        new(G("settings"), [PageId.General, PageId.Features, PageId.Agents]),
        new(G("system"), [PageId.Api, PageId.Access]),
        new(G("reference"), [PageId.DocsAbout]),
    ];

    private static Page[] BuildPages() =>
    [

        new(PageId.Home, P("Home"), PD("Home"), []),
        new(PageId.General, P("General"), PD("General"),
        [
            new(S("appearance"), "\uE790", [
                Slider("appearance.scale", L("appearance.scale"), D("appearance.scale"),
                    "100%", "90%", "95%", "100%", "105%", "110%"),
                Choice("appearance.glass", L("appearance.glass"), D("appearance.glass"),
                    "Balanced", "Light", "Balanced", "Strong"),
                Choice("appearance.motion", L("appearance.motion"), D("appearance.motion"),
                    "Soft", "Reduced", "Soft", "Standard"),
                Choice("appearance.fps", L("appearance.fps"), D("appearance.fps"),
                    "Auto", "Auto", "280", "240", "144", "120", "60", "30"),

                new("appearance.fpsMeasured", L("appearance.fpsMeasured"), D("appearance.fpsMeasured"),
                    RowKind.Status, "", []),
            ]),
            new(S("startup"), "\uE7E8", [

                Toggle("general.startup", L("general.startup"), D("general.startup")),
                Toggle("general.fullscreen", L("general.fullscreen"), D("general.fullscreen"), true),

                Toggle("general.greeting", L("general.greeting"), D("general.greeting"), true),
            ]),
            new(S("behaviour"), "\uE945", [

                Choice("general.language", L("general.language"), D("general.language"),
                    LanguageRowFallback, [.. Halo.Localization.Strings.Available()]),
                Toggle("general.capture", L("general.capture"), D("general.capture"), false),
                Toggle("general.follow", L("general.follow"), D("general.follow")),
                Action("general.reset", L("general.reset"), D("general.reset"), "Reset position"),
            ]),
        ]),
        new(PageId.Features, P("Features"), PD("Features"),
        [
            new(S("surfaces"), "\uE7F4", [
                Toggle("feature.media", L("feature.media"), D("feature.media")),
                Toggle("media.progress", L("media.progress"), D("media.progress")),
                Toggle("feature.downloads", L("feature.downloads"), D("feature.downloads")),
                Toggle("feature.fileTray", L("feature.fileTray"), D("feature.fileTray")),
                Toggle("feature.bluetooth", L("feature.bluetooth"), D("feature.bluetooth")),
            ]),
            new(S("notifications"), "\uE8BD", [
                Toggle("feature.notifications", L("feature.notifications"), D("feature.notifications")),

                Toggle("notifications.silence", L("notifications.silence"),
                    D("notifications.silence" + (Halo.Interop.AppModel.IsPackaged ? ".packaged" : ""))),
            ]),
            new(S("alertsaboutthismachine"), "\uE9D9", [
                Toggle("alert.battery", L("alert.battery"), D("alert.battery")),
                Slider("alert.batteryAt", L("alert.batteryAt"), D("alert.batteryAt"),
                    "20%", "10%", "15%", "20%", "25%", "30%", "40%"),
                Toggle("alert.cpu", L("alert.cpu"), D("alert.cpu")),
                Slider("alert.cpuAt", L("alert.cpuAt"), D("alert.cpuAt"),
                    "50%", "40%", "50%", "60%", "70%", "80%", "90%"),
                Toggle("alert.memory", L("alert.memory"), D("alert.memory")),
                Slider("alert.memoryAt", L("alert.memoryAt"), D("alert.memoryAt"),
                    "70%", "50%", "60%", "70%", "80%", "90%"),
                Toggle("alert.internet", L("alert.internet"), D("alert.internet")),
                Toggle("alert.clipboard", L("alert.clipboard"), D("alert.clipboard")),
                Toggle("alert.language", L("alert.language"), D("alert.language")),
                Toggle("alert.hourly", L("alert.hourly"), D("alert.hourly"), false),

                Toggle("alert.weather", L("alert.weather"), D("alert.weather"), false),
            ]),
        ]),
        new(PageId.Agents, P("Agents"), PD("Agents"),
        [
            new(S("sessions"), "\uE716", [
                Toggle("feature.claudeCode", L("feature.claudeCode"), D("feature.claudeCode")),
                Toggle("feature.codex", L("feature.codex"), D("feature.codex")),
                Toggle("feature.genericAgents", L("feature.genericAgents"), D("feature.genericAgents")),
            ]),

            new(S("connection"), "\uE703", [

                new("hooks.claude", L("hooks.claude"), D("hooks.claude"),
                    RowKind.Status, "", [], "Disconnect"),
                new("hooks.codex", L("hooks.codex"), D("hooks.codex"),
                    RowKind.Status, "", [], "Disconnect"),
            ]),
            new(S("questions"), "\uE9CE", [
                Toggle("claude.ask", L("claude.ask"), D("claude.ask")),
            ]),
            new(S("alerts"), "\uEA80", [
                Toggle("alert.context", L("alert.context"), D("alert.context")),
                Slider("alert.contextAt", L("alert.contextAt"), D("alert.contextAt"),
                    "80%", "60%", "70%", "75%", "80%", "85%", "90%"),
                Toggle("alert.limit", L("alert.limit"), D("alert.limit")),
                Slider("alert.limitAt", L("alert.limitAt"), D("alert.limitAt"),
                    "80%", "60%", "70%", "80%", "90%", "95%"),
            ]),
        ]),
        new(PageId.Api, P("Api"), PD("Api"),
        [
            new(S("endpoint"), "\uE968", [

                new("api.docs", L("api.docs"), D("api.docs"), RowKind.Status, "", [], "Open"),
                Toggle("api.enabled", L("api.enabled"), D("api.enabled"), false),
                Choice("api.port", L("api.port"), D("api.port"),
                    "7317", "7317", "7318", "8317", "9317"),
                new("api.token", L("api.token"), D("api.token"),
                    RowKind.Status, "", [], "Copy"),
            ]),
            new(S("whatcallersmaydo"), "\uE8D7", [
                Toggle("api.notify", L("api.notify"), D("api.notify")),
                Toggle("api.ask", L("api.ask"), D("api.ask")),
                Toggle("api.state", L("api.state"), D("api.state"), false),
                Toggle("api.control", L("api.control"), D("api.control"), false),
                Toggle("api.settings", L("api.settings"), D("api.settings"), false),
            ]),
        ]),
        new(PageId.Access, P("Access"), PD("Access"),
        [
            new(S("permissions"), "\uE890", [
                new("access.notifications", L("access.notifications"), D("access.notifications"),
                    RowKind.Status, "", [], "Open settings"),

                new("access.startup", L("access.startup"),
                    D("access.startup." + (Halo.Interop.AppModel.IsPackaged ? "packaged" : "task")),
                    RowKind.Status, "", [],
                    Halo.Localization.Strings.Get("settings.access.startup.action."
                        + (Halo.Interop.AppModel.IsPackaged ? "packaged" : "task"))),
            ]),

            new(S("removal"), "\uE74D", [
                new("reset.everything", L("reset.everything"), D("reset.everything"),
                    RowKind.Status, "", [], "Reset"),
            ]),
        ]),
        new(PageId.DocsAbout, P("DocsAbout"), PD("DocsAbout"),
        [
            new(S("halo"), "\uE946", [
                new("about.version", L("about.version"), D("about.version"), RowKind.Status, "", []),
                new("about.state", L("about.state"), D("about.state"),
                    RowKind.Status, "", [], "Open folder"),
            ]),

            new(S("problems"), "\uE730", [
                Action("report.problem", L("report.problem"), D("report.problem"),
                    "Write a report"),
                Toggle("report.autoCrash", L("report.autoCrash"), D("report.autoCrash"),
                    false),
            ]),
            new(S("project"), "\uE943", [
                new("about.repo", L("about.repo"), D("about.repo"), RowKind.Status, "", [], "Open"),
            ]),
        ]),
    ];

    internal static Page Get(PageId id) => System.Array.Find(Pages, p => p.Id == id)!;

    internal static readonly PageId[] HomeShortcuts =
        [PageId.General, PageId.Features, PageId.Agents, PageId.Access];

    internal static string Sub(PageId page) => page switch
    {
        PageId.General => Halo.Localization.Strings.Get("home.card.general"),
        PageId.Features => Halo.Localization.Strings.Get("home.card.features"),
        PageId.Agents => Halo.Localization.Strings.Get("home.card.agents"),
        PageId.Api => Halo.Localization.Strings.Get("home.card.api"),
        _ => Halo.Localization.Strings.Get("home.card.access"),
    };

    internal static string Glyph(PageId page) => page switch
    {
        PageId.Home => "\uE80F",
        PageId.General => "\uE713",
        PageId.Features => "\uE71D",
        PageId.Agents => "\uE716",
        PageId.Api => "\uE968",
        PageId.Access => "\uE8D7",
        _ => "\uE943",
    };

    internal static (byte R, byte G, byte B) Accent(PageId page) => page switch
    {
        PageId.Home => (0x74, 0xE6, 0xC2),
        PageId.General => (0x7C, 0xB4, 0xFF),
        PageId.Features => (0xFF, 0x91, 0xC8),
        PageId.Agents => (0xD7, 0x9B, 0xFF),
        PageId.Api => (0x8B, 0xE0, 0xC8),
        PageId.Access => (0xF0, 0xAE, 0x72),
        _ => (0x5F, 0xDF, 0xE5),
    };
}

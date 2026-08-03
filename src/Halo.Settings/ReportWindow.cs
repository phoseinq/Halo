using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Halo.Settings;

internal sealed class ReportWindow : Window
{
    internal const string EndpointKey = "report.endpoint";
    internal const string KeyKey = "report.key";

    private readonly TextBox _description;
    private readonly TextBox _preview;
    private readonly TextBlock _status;
    private readonly Button _create;
    private readonly StackPanel _actions;
    private string? _path;

    internal static void Open(Window? owner = null)
    {
        try
        {
            var window = new ReportWindow();
            if (owner != null && owner.IsLoaded) window.Owner = owner;
            window.ShowDialog();
        }
        catch { }
    }

    internal static FrameworkElement PreviewTree(bool filled)
    {
        var window = new ReportWindow(preview: true);
        if (filled)
        {
            window._preview.Text = SamplePreview;
            window._preview.Visibility = Visibility.Visible;
            window._actions.Visibility = Visibility.Visible;
            window._create.IsEnabled = false;
            window._description.Text = "the album cover stays as the spotify logo for a whole track";
            window.Say("This is the report, exactly as it sits on disk. Nothing has been sent.",
                       window.Secondary);
        }
        return (FrameworkElement)window.Content;
    }

    private const string SamplePreview = """
        {
          "kind": "manual",
          "at": "2026-08-03T13:42:56Z",
          "halo": "3.4.0.0",
          "windows": "10.0.26200.0",
          "display": "2560x1440 @ 280 Hz",
          "dpi": 96,
          "runtime": ".NET 9.0.18",
          "surface": {
            "primary": "MediaWidget",
            "live": [ "MediaWidget", "ClaudeWidget" ],
            "expanded": false,
            "heavy": false,
            "tier": 280
          },
          "description": "the album cover stays as the spotify logo for a whole track"
        }
        """;

    private ReportWindow(bool preview = false)
    {
        Title = "Report a problem";
        Width = 720;
        Height = 640;
        MinWidth = 560;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x14, 0x1C));
        Foreground = Ink;

        var root = new Grid
        {
            Margin = new Thickness(20),
            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x14, 0x1C)),
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(Row(0, Label(
            "Halo mirrors other people's notifications, what you are playing and your tray file names, so a "
            + "report carries a named list of facts about this machine and nothing else. You will see the "
            + "whole file before anything happens to it.", Secondary, 12.5)));

        _description = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 96,
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(11, 8, 11, 8),
            Background = Brushes.Transparent,
            BorderBrush = FrostEdge,
            BorderThickness = new Thickness(1),
            Foreground = Ink,
            CaretBrush = Ink,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        root.Children.Add(Row(1, _description));

        _create = Glass("Create report", 150);
        _create.Margin = new Thickness(0, 12, 0, 0);
        _create.HorizontalAlignment = HorizontalAlignment.Left;
        _create.Click += (_, _) => Create();
        root.Children.Add(Row(2, _create));

        _preview = new TextBox
        {
            IsReadOnly = true,
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(11, 8, 11, 8),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x0F, 0x14, 0x1C)),
            BorderBrush = FrostEdge,
            BorderThickness = new Thickness(1),
            Foreground = Ink,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Visibility = Visibility.Collapsed,
        };
        root.Children.Add(Row(3, _preview));

        _actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 14, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _actions.Children.Add(Action("Copy", 96, Copy));
        _actions.Children.Add(Action("Save as...", 110, Save));
        _actions.Children.Add(Action("Open a GitHub issue", 170, Issue));
        _actions.Children.Add(Action("Open in Notepad", 150, Notepad));
        _actions.Children.Add(Action("Send", 96, Send));
        root.Children.Add(Row(4, _actions));

        _status = Label("", Secondary, 12);
        _status.Margin = new Thickness(0, 12, 0, 0);
        _status.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(Row(5, _status));

        Content = root;

        var crash = preview ? null : NewestReport();
        if (crash != null) Load(crash, "This is the last crash Halo recorded. Nothing has been sent.");
    }

    private Brush Ink => (Brush)FindResource("Ink");
    private Brush Secondary => (Brush)FindResource("Secondary");
    private Brush FrostEdge => (Brush)FindResource("FrostEdge");
    private Brush Coral => (Brush)FindResource("Coral");
    private Brush Mint => (Brush)FindResource("Mint");

    private static UIElement Row(int row, UIElement child) { Grid.SetRow(child, row); return child; }

    private TextBlock Label(string text, Brush brush, double size) => new()
    {
        Text = text,
        Foreground = brush,
        FontSize = size,
        TextWrapping = TextWrapping.Wrap,
    };

    private Button Glass(string content, double width)
    {
        var button = new Button
        {
            Style = (Style)FindResource("Glass"),
            Content = content,
            Width = width,
            Height = 38,
            Padding = new Thickness(13, 0, 13, 0),
        };
        Ui.SetRadius(button, new CornerRadius(10));
        return button;
    }

    private Button Action(string content, double width, Action handler)
    {
        var button = Glass(content, width);
        button.Margin = new Thickness(0, 0, 8, 0);
        button.Click += (_, _) => { try { handler(); } catch (Exception ex) { Say(ex.Message, Coral); } };
        return button;
    }

    private void Say(string text, Brush brush)
    {
        _status.Text = text;
        _status.Foreground = brush;
    }

    private static string ReportsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "reports");

    private static string? NewestReport()
    {
        try
        {
            var files = new DirectoryInfo(ReportsDir).GetFiles("crash-*.json");
            if (files.Length == 0) return null;
            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return files[0].FullName;
        }
        catch { return null; }
    }

    private void Load(string path, string message)
    {
        _path = path;
        _preview.Text = File.ReadAllText(path);
        _preview.Visibility = Visibility.Visible;
        _actions.Visibility = Visibility.Visible;
        Say(message, Secondary);
    }

    private void Create()
    {
        try
        {
            string exe = Path.Combine(AppContext.BaseDirectory, "Halo.App.exe");
            if (!File.Exists(exe)) { Say("Halo.App.exe is not beside this window.", Coral); return; }

            string desc = Path.Combine(Path.GetTempPath(), "halo-report-description.txt");
            File.WriteAllText(desc, _description.Text);
            try
            {
                var psi = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };
                psi.ArgumentList.Add("--report-new");
                psi.ArgumentList.Add(desc);
                using var process = Process.Start(psi);
                if (process is null) { Say("Could not start Halo.App.", Coral); return; }
                string printed = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(10_000);

                string? path = File.Exists(printed) ? printed : NewestAny();
                if (path is null) { Say("The report was not written.", Coral); return; }
                _create.IsEnabled = false;
                _description.IsReadOnly = true;
                Load(path, "This is the report, exactly as it sits on disk. Nothing has been sent.");
            }
            finally { try { File.Delete(desc); } catch { } }
        }
        catch (Exception ex) { Say(ex.Message, Coral); }
    }

    private static string? NewestAny()
    {
        try
        {
            var files = new DirectoryInfo(ReportsDir).GetFiles("*.json");
            if (files.Length == 0) return null;
            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return files[0].FullName;
        }
        catch { return null; }
    }

    private void Copy()
    {
        Clipboard.SetText(_preview.Text);
        Say("Copied. It never went near the network.", Mint);
    }

    private void Save()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = _path is null ? "halo-report.json" : Path.GetFileName(_path),
            Filter = "JSON|*.json|All files|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, _preview.Text);
        Say("Saved. Attach it to an email yourself if you want me to have it.", Mint);
    }

    private const int IssueBodyLimit = 6000;

    private void Issue()
    {
        string body = _preview.Text;
        bool trimmed = body.Length > IssueBodyLimit;
        if (trimmed) body = body[..IssueBodyLimit];
        string url = "https://github.com/phoseinq/Halo/issues/new?body="
            + Uri.EscapeDataString("```json\n" + body + "\n```");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        Say(trimmed
            ? "Opened GitHub. The report was too long for a URL, so attach the saved file as well."
            : "Opened GitHub. Nothing is filed until you press submit there.", trimmed ? Coral : Mint);
    }

    private void Notepad()
    {
        if (_path is null) return;
        Process.Start(new ProcessStartInfo("notepad.exe", _path) { UseShellExecute = true });
    }

    private async void Send()
    {
        var store = new Store();
        string endpoint = store.Text(EndpointKey, "");
        if (endpoint.Length == 0)
        {
            Say("No endpoint is set. Add one on this page and press Apply, or use Copy, Save or GitHub.",
                Coral);
            return;
        }
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            Say("The endpoint must be an https:// address.", Coral);
            return;
        }
        try
        {
            Say("Sending...", Secondary);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var content = new StringContent(_preview.Text, System.Text.Encoding.UTF8,
                                                  "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };

            string key = store.Text(KeyKey, "");
            if (key.Length > 0) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
                Say($"Sent. The report is still on disk at {_path}", Mint);
            else
                Say($"The endpoint answered {(int)response.StatusCode} {response.ReasonPhrase}. "
                    + "The report is still on disk.", Coral);
        }

        catch (Exception ex) { Say("Send failed: " + ex.Message + ". The report is still on disk.", Coral); }
    }
}

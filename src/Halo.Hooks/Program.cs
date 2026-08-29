using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Halo.Hooks;

internal static class Program
{
    private static readonly string ClaudeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "notch");
    private static readonly string ClaudeStatusPath = Path.Combine(ClaudeDir, "status.json");
    private static readonly string CodexDir = Environment.GetEnvironmentVariable("HALO_CODEX_STATUS_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "notch");

    private static int Main(string[] args)
    {

        if (args.Length > 0 && args[0] == "probe-startup")
        {
            try
            {
                Console.WriteLine($"packaged : {Halo.Interop.AppModel.IsPackaged}");
                Console.WriteLine($"identity : {Halo.Interop.AppModel.PackageFullName ?? "(none)"}");
                Console.WriteLine($"before   : {Autostart.Describe()}");
                if (args.Length > 1 && args[1] == "enable")
                {
                    Autostart.Install(AppContext.BaseDirectory + "Halo.App.exe");
                    Console.WriteLine($"after    : {Autostart.Describe()}");
                }
                else if (args.Length > 1 && args[1] == "disable")
                {
                    Autostart.Uninstall();
                    Console.WriteLine($"after    : {Autostart.Describe()}");
                }
                Console.WriteLine($"query    : exit {Autostart.Query()}   (0 on, 2 off and askable, 3 off and not ours)");
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine("probe-startup failed: " + e.Message); return 1; }
        }

        if (args.Length > 0 && args[0] == "probe-banner")
        {
            try
            {
                string app = Path.Combine(AppContext.BaseDirectory, "Halo.App.exe");
                if (!File.Exists(app)) { Console.Error.WriteLine("Halo.App.exe not beside me: " + app); return 1; }
                var psi = new System.Diagnostics.ProcessStartInfo(app)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                psi.ArgumentList.Add("--probe-banner");
                using var child = System.Diagnostics.Process.Start(psi);
                if (child == null) { Console.Error.WriteLine("child did not start"); return 1; }
                Console.Write(child.StandardOutput.ReadToEnd());
                child.WaitForExit(60_000);
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine("probe-banner failed: " + e.Message); return 1; }
        }

        if (args.Length > 0 && args[0] == "probe-registry-out")
        {
            try
            {
                string outDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "outproc");
                Directory.CreateDirectory(outDir);

                string src = AppContext.BaseDirectory;
                foreach (var f in Directory.GetFiles(src))
                {
                    try { File.Copy(f, Path.Combine(outDir, Path.GetFileName(f)), overwrite: true); } catch { }
                }
                string copy = Path.Combine(outDir, "Halo.Hooks.exe");
                Console.WriteLine($"parent packaged : {Halo.Interop.AppModel.IsPackaged}");
                Console.WriteLine($"child exe       : {copy}");
                var psi = new System.Diagnostics.ProcessStartInfo(copy)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                psi.ArgumentList.Add("probe-registry");
                psi.ArgumentList.Add("outproc");
                using var child = System.Diagnostics.Process.Start(psi);
                if (child == null) { Console.Error.WriteLine("child did not start"); return 1; }
                Console.WriteLine("--- child says ---");
                Console.WriteLine(child.StandardOutput.ReadToEnd().TrimEnd());
                child.WaitForExit(20_000);
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine("probe failed: " + e.Message); return 1; }
        }

        if (args.Length > 0 && args[0] == "probe-registry")
        {
            const string key = @"Software\Halo\PackagingProbe";
            string name = args.Length > 1 ? args[1] : "writtenAt";
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Console.WriteLine($"packaged : {Halo.Interop.AppModel.IsPackaged}");
            Console.WriteLine($"identity : {Halo.Interop.AppModel.PackageFullName ?? "(none)"}");
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(key);
                k.SetValue(name, stamp);
                Console.WriteLine($@"wrote    : HKCU\{key}\{name} = {stamp}");
                Console.WriteLine($@"readback : {Microsoft.Win32.Registry.GetValue($@"HKEY_CURRENT_USER\{key}", name, null) ?? "(nothing)"}");
                Console.WriteLine("now read the same value from a shell outside the package: if it is there,");
                Console.WriteLine("the write was NOT virtualized.");
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine("probe failed: " + e.Message); return 1; }
        }

        if (args.Length > 0 && args[0] is "install-autostart" or "uninstall-autostart" or "query-autostart")
        {
            try
            {
                switch (args[0])
                {
                    case "install-autostart":
                        if (args.Length != 2) throw new ArgumentException("install-autostart requires an executable path.");
                        Autostart.Install(args[1]);
                        break;
                    case "uninstall-autostart":
                        Autostart.Uninstall();
                        break;
                    default:

                        return Autostart.Query();
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        if (args.Length > 0 && args[0] is "install-claude-hooks" or "uninstall-claude-hooks" or "query-claude-hooks")
        {
            try
            {
                var settingsPath = Environment.GetEnvironmentVariable("HALO_CLAUDE_SETTINGS_PATH");
                if (string.IsNullOrWhiteSpace(settingsPath))
                    settingsPath = ClaudeHookInstaller.DefaultSettingsPath;

                switch (args[0])
                {
                    case "install-claude-hooks":
                        ClaudeHookInstaller.Install(settingsPath, args.Length >= 2 ? args[1] : OwnHookPath());
                        break;
                    case "uninstall-claude-hooks":
                        ClaudeHookInstaller.Uninstall(settingsPath);
                        break;
                    default:

                        return ClaudeHookInstaller.IsInstalled(settingsPath, OwnHookPath()) ? 0 : 2;
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        if (args.Length > 0 && args[0] is "install-codex-hooks" or "uninstall-codex-hooks" or "query-codex-hooks")
        {
            try
            {
                var settingsPath = Environment.GetEnvironmentVariable("HALO_CODEX_HOOKS_PATH");
                if (string.IsNullOrWhiteSpace(settingsPath))
                    settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "hooks.json");

                if (args[0] == "install-codex-hooks")
                {
                    CodexHookInstaller.Install(settingsPath, args.Length >= 2 ? args[1] : OwnHookPath());
                }
                else if (args[0] == "uninstall-codex-hooks")
                {
                    CodexHookInstaller.Uninstall(settingsPath);
                }
                else
                {

                    return CodexHookInstaller.IsInstalled(settingsPath, OwnHookPath()) ? 0 : 2;
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        if (args.Length > 0 && args[0].StartsWith("query-", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"unknown query: {args[0]}");
            return 4;
        }

        try
        {
            if (args.Length == 0) return 0;
            var codex = args.Length >= 2 && args[0] == "codex";
            var cmd = codex ? args[1] : args[0];

            if (cmd == "cancel")
            {
                if (args.Length >= 2 && int.TryParse(args[1], out var pid))
                    Cancel(pid);
                return 0;
            }

            CodexSurface? surface = codex ? DetectCodexSurface() : null;
            var dir = codex ? CodexDir : ClaudeDir;

            uint agentPid = 0;
            bool background = false;
            var path = codex ? CodexStatusPath(surface!.Value)
                : IsClaudeApp() ? Path.Combine(ClaudeDir, "app.json") : ClaudeSessionPath(out agentPid, out background);
            Directory.CreateDirectory(dir);
            var input = ReadInput();
            var status = LoadOrNew(path);

            if (agentPid != 0)
            {
                status["pid"] = (int)agentPid;
                if (background) status["background"] = true;
            }

            if (cmd == "session-end" && !codex && path != ClaudeStatusPath)
            {
                try { File.Delete(path); } catch { }
                try { File.Delete(ClaudeStatusPath); } catch { }
                return 0;
            }

            string? Field(string name) => input?[name]?.GetValue<string>();

            if (codex)
            {
                status["source"] = surface == CodexSurface.Desktop ? "desktop" : "cli";
                if (Field("cwd") is { } cwd) status["cwd"] = cwd;
            }

            switch (cmd)
            {
                case "session-start":
                    if (!codex) SweepDeadSessions();
                    status["sessionId"] = Field("session_id");
                    status["cwd"] = Field("cwd");
                    status["state"] = "idle";
                    if (Field("source") == "compact")
                        status["compactedAt"] = DateTimeOffset.UtcNow.ToString("o");
                    else if (Field("source") is "clear" or "startup")
                        status.Remove("session");
                    RecordProcess(status, codex);
                    break;
                case "pre-compact":
                    status["state"] = "compacting";
                    status["startedAt"] = DateTimeOffset.UtcNow.ToString("o");
                    status["message"] = null;

                    UpdateContext(status, Field("transcript_path"));
                    break;
                case "prompt":
                    status["state"] = "working";
                    status["lastPrompt"] = Truncate(Field("prompt"), 120);
                    status["currentTool"] = null;
                    status["toolTarget"] = null;
                    status["startedAt"] = DateTimeOffset.UtcNow.ToString("o");
                    status["message"] = null;
                    RecordProcess(status, codex);
                    UpdateContext(status, Field("transcript_path"));
                    break;
                case "tool":
                    status["state"] = "working";
                    status["currentTool"] = Field("tool_name");
                    status["toolTarget"] = ToolTarget(input?["tool_name"]?.GetValue<string>(),
                        AsObject(input?["tool_input"]));
                    break;
                case "tool-done":
                    status["state"] = "working";

                    status["currentTool"] = null;
                    status["toolTarget"] = null;
                    UpdateContext(status, Field("transcript_path"));
                    break;
                case "post-compact":

                    status["state"] = codex || Field("trigger") == "auto" ? "working" : "idle";
                    status["compactedAt"] = DateTimeOffset.UtcNow.ToString("o");
                    if (!codex)
                    {
                        if (Field("trigger") != "auto") status["startedAt"] = null;
                        UpdateContext(status, Field("transcript_path"));
                    }
                    break;
                case "notify":

                    var prevState = status["state"]?.GetValue<string>();
                    if (prevState is "working" or "compacting") status["state"] = "waiting_input";
                    status["message"] = Truncate(Field("message"), 160);
                    break;
                case "stop":
                    status["state"] = "idle";
                    status["currentTool"] = null;
                    status["toolTarget"] = null;
                    status["startedAt"] = null;
                    status["message"] = null;
                    UpdateContext(status, Field("transcript_path"));
                    break;
                case "session-end":
                    status["state"] = "idle";
                    status["currentTool"] = null;
                    status["toolTarget"] = null;
                    status["startedAt"] = null;
                    break;
                default:
                    return 0;
            }

            status["updatedAt"] = DateTimeOffset.UtcNow.ToString("o");
            Save(status, path);

            int askOwner = status["pid"] is JsonValue pv && pv.TryGetValue<int>(out var askPid) ? askPid : 0;
            if (cmd == "tool" && !codex)
                AskFlow.Run(ClaudeDir, input, Field("session_id"), Field("cwd"), askOwner);

            bool questionOver = cmd is "prompt" or "stop"
                || (cmd == "tool-done" && Field("tool_name") == "AskUserQuestion");
            if (questionOver && !codex) AskFlow.Clear(ClaudeDir, askOwner);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    internal static string OwnHookPath()
    {
        try
        {
            if (Halo.Interop.AppModel.IsPackaged)
            {
                string stub = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "WindowsApps", "Halo.Hooks.exe");
                if (File.Exists(stub)) return stub;
            }
        }
        catch { }
        return Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
    }

    private static JsonObject? ReadInput()
    {
        try
        {

            using var stdin = Console.OpenStandardInput();
            using var reader = new System.IO.StreamReader(stdin, new System.Text.UTF8Encoding(false));
            var text = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonNode.Parse(text) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string CodexStatusPath(CodexSurface surface) =>
        Path.Combine(CodexDir, surface == CodexSurface.Desktop ? "desktop.json" : "cli.json");

    private static void SweepDeadSessions()
    {
        try
        {
            foreach (var f in Directory.GetFiles(ClaudeDir, "status-*.json"))
            {
                try
                {
                    var pid = (JsonNode.Parse(File.ReadAllText(f)) as JsonObject)?["pid"]?.GetValue<int>() ?? 0;
                    bool alive = false;
                    if (pid > 0)
                        try { using var p = System.Diagnostics.Process.GetProcessById(pid); alive = !p.HasExited; }
                        catch { }
                    if (!alive) File.Delete(f);
                }
                catch { }
            }
        }
        catch { }
    }

    private static string ClaudeSessionPath(out uint pid, out bool background)
    {
        var map = ProcessMap();
        pid = Ancestor(map, (uint)Environment.ProcessId,
            n => n.Contains("claude") || n == "node.exe");

        background = pid != 0 && map.TryGetValue(pid, out var e)
            && map.TryGetValue(e.parent, out var par)
            && (par.name.ToLowerInvariant().Contains("claude") || par.name.ToLowerInvariant() == "node.exe");
        return pid == 0 ? ClaudeStatusPath : Path.Combine(ClaudeDir, $"status-{pid}.json");
    }

    private static JsonObject LoadOrNew(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (JsonNode.Parse(text) is JsonObject o) return o;
            }
        }
        catch
        {
        }
        return new JsonObject();
    }

    private static void Save(JsonObject status, string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, status.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }

    internal static JsonObject? AsObject(JsonNode? node)
    {
        if (node is JsonObject o) return o;
        try
        {
            if (node is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                return JsonNode.Parse(s) as JsonObject;
        }
        catch { }
        return null;
    }

    internal static string? ToolTarget(string? tool, JsonObject? input)
    {
        if (tool is null || input is null) return null;
        string? Str(string key)
        {
            try { return input[key]?.GetValue<string>()?.Trim() is { Length: > 0 } v ? v : null; }
            catch { return null; }
        }

        var raw = tool switch
        {
            "Edit" or "Write" or "MultiEdit" or "NotebookEdit" or "Read" => Leaf(Str("file_path")),
            "Bash" or "PowerShell" => Program_(Str("command")),
            "Grep" or "Glob" => Str("pattern"),
            "WebFetch" => Host(Str("url")),
            "WebSearch" => Str("query"),
            "Task" or "Agent" => Str("subagent_type"),
            "Skill" or "SlashCommand" => Str("skill") ?? Str("command"),
            _ => null,
        };
        return Truncate(raw, 24);
    }

    private static string? Leaf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var s = path.Replace('\\', '/').TrimEnd('/');
        var i = s.LastIndexOf('/');
        var leaf = i >= 0 ? s.Substring(i + 1) : s;
        return leaf.Length > 0 ? leaf : null;
    }

    private static string? Program_(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var line = command.Trim();
        if (line.IndexOfAny(new[] { '|', ';', '&' }) >= 0) return null;

        if (line[0] is '"' or '\'')
        {
            var end = line.IndexOf(line[0], 1);
            return end > 1 ? Clean(line.Substring(1, end - 1)) : null;
        }
        foreach (var word in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Contains('=')) continue;
            if (Clean(word.Trim('"', '\'', '(')) is { } name) return name;
        }
        return null;

        static string? Clean(string word)
        {
            var leaf = Leaf(word);
            if (string.IsNullOrEmpty(leaf)) return null;
            if (leaf.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) leaf = leaf[..^4];
            return leaf.Length is > 0 and <= 14 ? leaf : null;
        }
    }

    private static string? Host(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try { return new Uri(url).Host is { Length: > 0 } h ? h : null; } catch { return null; }
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max] + "…");

    private static void UpdateContext(JsonObject status, string? transcriptPath)
    {
        try
        {
            if (string.IsNullOrEmpty(transcriptPath) || !File.Exists(transcriptPath)) return;
            var lines = File.ReadAllLines(transcriptPath);

            var started = DateTimeOffset.MinValue;
            if (status["startedAt"] is JsonNode sn)
                DateTimeOffset.TryParse(sn.GetValue<string>(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out started);

            var read = TranscriptScan.Read(lines, started);
            if (read.Latest <= 0)
            {

                if (read.Compacted) status.Remove("session");
                return;
            }

            var session = status["session"] as JsonObject ?? new JsonObject();
            session["contextUsed"] = read.Latest;
            session["contextMax"] = ContextWindow(read.Model);
            session["promptTokens"] = read.Turn;
            status["session"] = session;
        }
        catch
        {
        }
    }

    private static long ContextWindow(string? model)
    {
        var m = (model ?? "").ToLowerInvariant();
        if (m.Contains("haiku")) return 200_000;
        if (m.Contains("opus") || m.Contains("fable") || m.Contains("sonnet")) return 1_000_000;
        return 200_000;
    }

    private static void RecordProcess(JsonObject status, bool codex = false)
    {
        var map = ProcessMap();
        uint start = (uint)Environment.ProcessId;

        uint agent = Ancestor(map, start, codex
            ? n => n is "codex.exe" or "codex-code-mode-host.exe" or "chatgpt.exe"
            : n => n.Contains("claude") || n == "node.exe");
        if (agent != 0) status["pid"] = (int)agent;

        uint term = Ancestor(map, start, IsTerminal);
        if (term != 0) status["consolePid"] = (int)term;

        uint host = HostWindowPid(map, start);
        if (host != 0)
        {
            status["hostPid"] = (int)host;

            long hwnd = ForegroundWindowOf(host);
            if (hwnd != 0) status["hostHwnd"] = hwnd;
        }
    }

    private static uint HostWindowPid(Dictionary<uint, (uint parent, string name)> map, uint start)
    {
        try
        {
            var owners = WindowOwners();
            uint from = start;
            IntPtr con = GetConsoleWindow();
            if (con != IntPtr.Zero)
            {
                GetWindowThreadProcessId(con, out uint conPid);
                if (conPid > 4) from = conPid;
            }

            uint? env = EnvHostPid(owners, map, from);
            if (env is uint hinted) return hinted;

            uint found = WalkToWindow(map, owners, from);

            if (found == 0 && from != start) found = WalkToWindow(map, owners, start);
            return found;
        }
        catch { }
        return 0;
    }

    private static uint? EnvHostPid(HashSet<uint> owners, Dictionary<uint, (uint parent, string name)> map,
        uint start)
    {

        string[]? want = null;
        if (string.Equals(Environment.GetEnvironmentVariable("TERM_PROGRAM"), "vscode",
                StringComparison.OrdinalIgnoreCase))

            want = ["code.exe", "cursor.exe", "codium.exe", "vscodium.exe",
                    "code - insiders.exe", "windsurf.exe"];
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")))
            want = ["windowsterminal.exe"];

        if (want == null) return null;

        uint ancestor = WalkToWindow(map, owners, start);
        bool ancestorNamed = ancestor != 0 && map.TryGetValue(ancestor, out var owner) &&
                             want.Contains(owner.name, StringComparer.OrdinalIgnoreCase);

        uint only = 0;
        foreach (var pid in owners)
        {
            if (!map.TryGetValue(pid, out var e)) continue;
            if (!want.Contains(e.name, StringComparer.OrdinalIgnoreCase)) continue;
            if (only != 0)
            {

                return ancestorNamed ? ancestor : 0;
            }
            only = pid;
        }

        if (only != 0 && ancestorNamed && ancestor != only) return 0;
        return only != 0 ? only : (ancestorNamed ? ancestor : 0);
    }

    private static uint WalkToWindow(Dictionary<uint, (uint parent, string name)> map, HashSet<uint> owners,
        uint start)
    {
        if (owners.Contains(start)) return start;
        uint cur = start;
        for (int i = 0; i < 24 && cur != 0 && map.TryGetValue(cur, out var e); i++)
        {
            cur = e.parent;
            if (cur > 4 && owners.Contains(cur)) return cur;
        }
        return 0;
    }

    private static HashSet<uint> WindowOwners()
    {
        var owners = new HashSet<uint>();
        EnumWindows((h, _) =>
        {

            if (IsWindowVisible(h) && GetWindow(h, GW_OWNER) == IntPtr.Zero && GetWindowTextLengthW(h) > 0)
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid != 0) owners.Add(pid);
            }
            return true;
        }, IntPtr.Zero);
        return owners;
    }

    private const uint GW_OWNER = 4;
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")] private static extern int GetWindowTextLengthW(IntPtr hwnd);
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    private static long ForegroundWindowOf(uint pid)
    {
        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return 0;
            GetWindowThreadProcessId(fg, out uint owner);
            return owner == pid ? fg.ToInt64() : 0;
        }
        catch { return 0; }
    }

    private enum CodexSurface { Cli, Desktop }

    private static bool IsClaudeApp()
    {
        var o = Environment.GetEnvironmentVariable("HALO_CLAUDE_SURFACE");
        if (!string.IsNullOrEmpty(o)) return o.Equals("app", StringComparison.OrdinalIgnoreCase);
        return Ancestor(ProcessMap(), (uint)Environment.ProcessId, IsTerminal) == 0;
    }

    private static CodexSurface DetectCodexSurface()
    {
        var overrideSurface = Environment.GetEnvironmentVariable("HALO_CODEX_SURFACE");
        if (string.Equals(overrideSurface, "desktop", StringComparison.OrdinalIgnoreCase))
            return CodexSurface.Desktop;
        if (string.Equals(overrideSurface, "cli", StringComparison.OrdinalIgnoreCase))
            return CodexSurface.Cli;

        var map = ProcessMap();
        uint start = (uint)Environment.ProcessId;
        if (Ancestor(map, start, n => n is "chatgpt.exe" or "codex-code-mode-host.exe") != 0)
            return CodexSurface.Desktop;
        if (Ancestor(map, start, IsTerminal) != 0)
            return CodexSurface.Cli;
        return CodexSurface.Cli;
    }

    private static bool IsTerminal(string name) => name is
        "windowsterminal.exe" or "wt.exe" or "conhost.exe" or "openconsole.exe" or
        "powershell.exe" or "pwsh.exe" or "cmd.exe" or "bash.exe" or "wsl.exe" or
        "alacritty.exe" or "wezterm-gui.exe" or "code.exe";

    private static uint Ancestor(Dictionary<uint, (uint parent, string name)> map, uint start, Func<string, bool> match)
    {
        uint cur = start;
        for (int i = 0; i < 16 && cur != 0 && map.TryGetValue(cur, out var e); i++)
        {
            if (match(e.name.ToLowerInvariant())) return cur == start ? e.parent : cur;
            cur = e.parent;
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
    private static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 pe);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
    private static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 pe);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);

    private static Dictionary<uint, (uint parent, string name)> ProcessMap()
    {
        var map = new Dictionary<uint, (uint, string)>();
        var snap = CreateToolhelp32Snapshot(0x2, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return map;
        try
        {
            var pe = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snap, ref pe))
                do { map[pe.th32ProcessID] = (pe.th32ParentProcessID, pe.szExeFile); }
                while (Process32Next(snap, ref pe));
        }
        finally { CloseHandle(snap); }
        return map;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteConsoleInput(IntPtr h, INPUT_RECORD[] buffer, uint length, out uint written);

    [StructLayout(LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public int bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public ushort UnicodeChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT_RECORD
    {
        public ushort EventType;
        public ushort _pad;
        public KEY_EVENT_RECORD Key;
    }

    private static void Cancel(int pid)
    {
        FreeConsole();
        if (!AttachConsole((uint)pid)) return;
        try
        {
            const uint GENERIC_RW = 0x80000000 | 0x40000000, SHARE_RW = 1 | 2, OPEN_EXISTING = 3;
            IntPtr hIn = CreateFile("CONIN$", GENERIC_RW, SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (hIn == IntPtr.Zero || hIn == new IntPtr(-1)) return;
            var recs = new[]
            {
                new INPUT_RECORD { EventType = 1, Key = new KEY_EVENT_RECORD { bKeyDown = 1, wRepeatCount = 1, wVirtualKeyCode = 0x1B, wVirtualScanCode = 0x01, UnicodeChar = 0x1B } },
                new INPUT_RECORD { EventType = 1, Key = new KEY_EVENT_RECORD { bKeyDown = 0, wRepeatCount = 1, wVirtualKeyCode = 0x1B, wVirtualScanCode = 0x01, UnicodeChar = 0x1B } },
            };
            WriteConsoleInput(hIn, recs, (uint)recs.Length, out _);
            CloseHandle(hIn);
        }
        finally { FreeConsole(); }
    }
}

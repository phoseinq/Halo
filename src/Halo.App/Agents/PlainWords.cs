using System;
using System.Collections.Generic;
using System.Linq;

namespace Halo.Agents;

internal static class PlainWords
{

    internal static readonly HashSet<string> WithSub = new(StringComparer.OrdinalIgnoreCase)
    {
        "git", "dotnet", "npm", "pnpm", "yarn", "cargo", "pip", "pip3", "gh", "docker", "go", "uv",
    };

    private static readonly Dictionary<string, string> Program = Build(new (string words, string[] names)[]
    {
        ("editing text", new[] { "sed", "awk", "tr", "perl" }),
        ("searching code", new[] { "grep", "rg", "findstr", "select-string", "sls" }),
        ("reading a file", new[] { "cat", "head", "tail", "less", "more", "type", "get-content", "gc", "wc" }),
        ("listing files", new[] { "ls", "dir", "find", "get-childitem", "gci", "tree", "test-path" }),
        ("copying files", new[] { "cp", "mv", "copy-item", "move-item", "robocopy", "xcopy", "rename-item" }),
        ("deleting files", new[] { "rm", "del", "rmdir", "remove-item" }),
        ("making files", new[] { "mkdir", "new-item", "touch", "set-content", "out-file" }),
        ("downloading", new[] { "curl", "wget", "invoke-webrequest", "iwr", "invoke-restmethod", "irm" }),
        ("stopping an app", new[] { "stop-process", "kill", "taskkill", "pkill" }),
        ("starting an app", new[] { "start-process", "start" }),
        ("checking processes", new[] { "get-process", "ps", "tasklist" }),
        ("running a script", new[] { "python", "python3", "py", "node", "pwsh", "powershell", "bash", "sh", "deno" }),
        ("running tests", new[] { "pytest", "jest", "vitest", "mocha" }),
        ("building", new[] { "msbuild", "make", "cmake", "tsc", "iscc" }),
        ("converting media", new[] { "ffmpeg", "magick", "convert" }),
        ("connecting", new[] { "ssh", "scp" }),
    });

    private static readonly Dictionary<string, string> Sub = Build(new (string words, string[] names)[]
    {
        ("checking git", new[] { "git status", "git diff", "git log", "git show", "git blame", "git ls-files" }),
        ("committing", new[] { "git commit" }),
        ("pushing", new[] { "git push" }),
        ("pulling", new[] { "git pull", "git fetch" }),
        ("staging files", new[] { "git add", "git rm", "git mv", "git restore", "git reset" }),
        ("switching branch", new[] { "git checkout", "git switch", "git branch", "git worktree" }),
        ("merging", new[] { "git merge", "git rebase", "git cherry-pick", "git stash" }),
        ("building", new[] { "dotnet build", "cargo build", "npm build", "go build", "dotnet publish",
                             "dotnet pack" }),
        ("running tests", new[] { "dotnet test", "cargo test", "npm test", "pnpm test", "yarn test", "go test" }),
        ("running the app", new[] { "dotnet run", "cargo run", "npm start", "go run", "npm run", "pnpm run",
                                    "yarn run", "uv run" }),
        ("installing packages", new[] { "npm install", "npm i", "npm ci", "pnpm install", "pnpm add", "yarn add",
                                        "yarn install", "pip install", "pip3 install", "cargo add",
                                        "dotnet add", "dotnet restore", "uv add", "uv pip", "go get" }),
        ("using GitHub", new[] { "gh pr", "gh issue", "gh release", "gh api", "gh run", "gh repo" }),
    });

    private static readonly Dictionary<string, string> Toolbox = new(StringComparer.OrdinalIgnoreCase)
    {
        ["git"] = "using git", ["dotnet"] = "running dotnet", ["npm"] = "running npm", ["pnpm"] = "running pnpm",
        ["yarn"] = "running yarn", ["cargo"] = "running cargo", ["pip"] = "managing packages",
        ["pip3"] = "managing packages", ["gh"] = "using GitHub", ["docker"] = "using docker", ["go"] = "running go",
        ["uv"] = "running uv",
    };

    private static Dictionary<string, string> Build((string words, string[] names)[] table)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (words, names) in table) foreach (var n in names) d[n] = words;
        return d;
    }

        internal static string? ForCommand(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        var parts = target.Trim().Split(' ', 2);
        var prog = parts[0];
        if (prog.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || prog.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)) prog = prog[..^4];
        if (parts.Length == 2 && Sub.TryGetValue(prog + " " + parts[1], out var s)) return s;
        if (Toolbox.TryGetValue(prog, out var tb)) return tb;
        return Program.TryGetValue(prog, out var p) ? p : null;
    }

    internal static string WithSubcommand(string name, string segment)
    {
        if (!WithSub.Contains(name)) return name;
        var words = segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int at = Array.FindIndex(words, w => w.Trim('"', '\'').EndsWith(name, StringComparison.OrdinalIgnoreCase)
                                             || w.Trim('"', '\'').EndsWith(name + ".exe", StringComparison.OrdinalIgnoreCase)
                                             || w.Trim('"', '\'').EndsWith(name + ".cmd", StringComparison.OrdinalIgnoreCase));
        if (at < 0) return name;

        string? sub = null;
        for (int i = at + 1; i < words.Length && sub is null; i++)
        {
            var w = words[i];
            if (w is "-C" or "-c") { i++; continue; }
            if (!w.StartsWith('-') && w.All(c => char.IsLetter(c) || c == '-')) sub = w;
        }
        return sub is null ? name : name + " " + sub.ToLowerInvariant();
    }

    internal static string NoDots(string line)
    {
        var s = line.Replace(" \u00B7 ", ", ").TrimEnd();
        while (s.EndsWith('\u2026') || s.EndsWith('.')) s = s[..^1].TrimEnd();
        return s.Length > 0 ? s : line;
    }

        internal static string ForSearch(string pattern)
    {
        var p = pattern.Trim();
        bool plain = p.Length is > 0 and <= 18 && p.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-');
        return plain ? "searching " + p : p.Contains('*') ? "finding files" : "searching code";
    }
}

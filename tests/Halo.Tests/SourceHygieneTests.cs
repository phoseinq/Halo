using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Halo.Tests;

// Two source-level rules that were kept by hand until both broke during the localization work. Neither is
// a style preference: each one silently disables a tool the repo depends on, and a silent failure here
// makes every later audit read as clean.
public class SourceHygieneTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Halo.sln"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static IEnumerable<string> Sources()
    {
        string sep = Path.DirectorySeparatorChar.ToString();
        foreach (var area in new[] { "src", "tests" })
            foreach (var path in Directory.EnumerateFiles(Path.Combine(Root, area), "*.cs",
                                                          SearchOption.AllDirectories))
                if (!path.Contains(sep + "bin" + sep) && !path.Contains(sep + "obj" + sep))
                    yield return path;
    }

    // A control character written into a string literal as a RAW BYTE compiles and behaves exactly like
    // its escape, so nothing at runtime can tell them apart. A NUL is the dangerous one: git and grep
    // classify the file as BINARY and skip it without a word. Strings.cs carried one through the whole
    // localization effort, which meant "the scan for captured strings is clean" had been computed over a
    // file set that silently excluded the string layer itself. U+0001 turned up twice as well - ripgrep
    // still reads those, but they are the same authoring slip and `file` calls them data.
    // Tab, CR and LF are the only control bytes a .cs file has any business containing.
    [Fact]
    public void No_source_file_contains_a_raw_control_character()
    {
        var guilty = new List<string>();
        foreach (var path in Sources())
            foreach (byte b in File.ReadAllBytes(path))
                if (b < 0x20 && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
                {
                    guilty.Add($"{Path.GetRelativePath(Root, path)} carries a raw 0x{b:X2}");
                    break;
                }

        Assert.True(guilty.Count == 0,
            "a raw NUL makes the file binary to grep, which then reports it clean whatever is inside:\n  "
            + string.Join("\n  ", guilty)
            + "\nwrite it as the \\uXXXX escape instead");
    }

    // CLAUDE.md: no Persian anywhere in code, and text that genuinely needs a non-Latin script goes in as
    // \uXXXX - "beware: an editor that resolves \uXXXX while writing will put the real character back".
    // That is not hypothetical. Live.cs's bidi range was written as escapes and both ends came back as
    // real characters, so the rule was being broken by the act of writing the code that follows it.
    // Typographic punctuation in comments is not what the rule is about; only actual scripts are checked.
    [Fact]
    public void Non_Latin_scripts_are_written_as_escapes_not_as_characters()
    {
        var guilty = new List<string>();
        foreach (var path in Sources())
            foreach (char c in File.ReadAllText(path))
                if (Script(c) is string script)
                {
                    guilty.Add($"{Path.GetRelativePath(Root, path)} carries {script} U+{(int)c:X4}");
                    break;
                }

        Assert.True(guilty.Count == 0,
            "write these as \\uXXXX and say in English what they mean:\n  " + string.Join("\n  ", guilty));
    }

    private static string? Script(char c) => c switch
    {
        >= '\u0590' and <= '\u08FF' => "Hebrew/Arabic",
        >= '\u0900' and <= '\u097F' => "Devanagari",
        >= '\u4E00' and <= '\u9FFF' => "CJK",
        _ => null,
    };
}

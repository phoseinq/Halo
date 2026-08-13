using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Halo.Hooks;

internal static class AskFlow
{
    private const int AckMs = 300;
    private const int AnswerMs = 20_000;

    private const int QuestionMs = 30 * 60_000;
    private const int PollMs = 25;

    internal static void Run(string dir, JsonObject? input, string? sessionId, string? cwd, int pid)
    {
        try
        {
            string? tool = input?["tool_name"]?.GetValue<string>();
            var toolInput = input?["tool_input"] as JsonObject;
            if (!AskGate.ShouldAsk(tool, toolInput, AskSettings.AllowRules(cwd))) return;

            var asks = Envelopes(tool!, toolInput!, sessionId, pid);
            if (asks.Count == 0) return;
            var ask = asks[0];

            if (ask.IsQuestion) { Publish(dir, asks, pid); return; }

            var answer = Wait(dir, ask);
            if (answer is not null) Console.Out.Write(answer.ToHookStdout());
        }
        catch { }
    }

    private static void Publish(string dir, IReadOnlyList<AskEnvelope> asks, int pid)
    {
        try
        {
            Directory.CreateDirectory(dir);
            Clear(dir, pid);
            foreach (var ask in asks)
                WriteAtomic(Path.Combine(dir, $"ask-{ask.Nonce}.json"), ask.ToJson());
        }
        catch { }
    }

    internal static void Clear(string dir, int pid)
    {
        try
        {
            if (pid <= 0 || !Directory.Exists(dir)) return;
            foreach (var path in Directory.GetFiles(dir, "ask-*.json"))
            {
                try
                {
                    if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o) continue;
                    if (o["pid"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<int>(out var p) && p == pid)
                        Delete(path);
                }
                catch { }
            }
        }
        catch { }
    }

    private static List<AskEnvelope> Envelopes(string tool, JsonObject toolInput, string? sessionId, int pid)
    {
        var asks = new List<AskEnvelope>();
        if (tool == "AskUserQuestion" && toolInput["questions"] is JsonArray qs && qs.Count >= 1)
        {
            for (int i = 0; i < qs.Count; i++)
            {
                if (qs[i] is not JsonObject q) continue;
                var options = new List<AskOption>();
                bool hasPreview = false;

                bool multiSelect = q["multiSelect"] is JsonValue mv && mv.TryGetValue<bool>(out var m) && m;
                if (q["options"] is JsonArray opts)
                    foreach (var n in opts)
                        if (n is JsonObject o && o["label"]?.GetValue<string>() is { Length: > 0 } label)
                        {
                            options.Add(new AskOption(label, o["description"]?.GetValue<string>() ?? ""));
                            if (o["preview"] is JsonValue pv && pv.TryGetValue<string>(out var p)
                                && !string.IsNullOrEmpty(p)) hasPreview = true;
                        }
                asks.Add(new AskEnvelope(
                    Guid.NewGuid().ToString("n"), pid, sessionId, tool,
                    AskGate.TargetOf(tool, toolInput), q["question"]?.GetValue<string>(), options,
                    DateTimeOffset.UtcNow.AddMilliseconds(QuestionMs),
                    multiSelect, hasPreview, i, qs.Count));
            }
            return asks;
        }

        asks.Add(new AskEnvelope(
            Guid.NewGuid().ToString("n"), pid, sessionId, tool,
            AskGate.TargetOf(tool, toolInput), null,
            [new AskOption("allow", "run it"), new AskOption("deny", "skip it")],
            DateTimeOffset.UtcNow.AddMilliseconds(AnswerMs)));
        return asks;
    }

    private static AskAnswer? Wait(string dir, AskEnvelope ask)
    {
        string askPath = Path.Combine(dir, $"ask-{ask.Nonce}.json");
        string ackPath = Path.Combine(dir, $"ack-{ask.Nonce}");
        string answerPath = Path.Combine(dir, $"answer-{ask.Nonce}.json");
        try
        {
            Directory.CreateDirectory(dir);
            WriteAtomic(askPath, ask.ToJson());

            if (!WaitForFile(ackPath, AckMs)) return null;
            if (!WaitForFile(answerPath, AnswerMs)) return null;

            var answer = AskAnswer.FromJson(ReadOrNull(answerPath));
            return answer?.Nonce == ask.Nonce ? answer : null;
        }
        catch { return null; }
        finally
        {
            Delete(askPath);
            Delete(ackPath);
            Delete(answerPath);
        }
    }

    private static bool WaitForFile(string path, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (File.Exists(path)) return true;
            System.Threading.Thread.Sleep(PollMs);
        }
        return File.Exists(path);
    }

    private static void WriteAtomic(string path, string text)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, overwrite: true);
    }

    private static string? ReadOrNull(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { System.Threading.Thread.Sleep(PollMs); }
        }
        return null;
    }

    private static void Delete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

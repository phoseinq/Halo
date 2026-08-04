using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Halo.Hooks;

internal static class ClaudeHookInstaller
{

    private static readonly (string Event, string Command)[] ManagedHooks =
    [
        ("SessionStart", "session-start"),
        ("UserPromptSubmit", "prompt"),
        ("PreToolUse", "tool"),
        ("PostToolUse", "tool-done"),
        ("Notification", "notify"),
        ("PreCompact", "pre-compact"),
        ("PostCompact", "post-compact"),
        ("Stop", "stop"),
        ("SessionEnd", "session-end"),
    ];

    internal static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    internal static void Install(string settingsPath, string hookExePath)
    {

        if (!Path.IsPathFullyQualified(hookExePath))
            throw new ArgumentException("The hook executable path must be absolute.", nameof(hookExePath));

        var settings = Load(settingsPath);
        var hooks = GetHooks(settings);
        RemoveManagedHandlers(hooks);

        foreach (var managed in ManagedHooks)
        {
            var entries = GetEntries(hooks, managed.Event);
            entries.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = $"\"{hookExePath}\" {managed.Command}",
                }),
            });
        }

        Save(settingsPath, settings);
    }

    internal static void Uninstall(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return;

        var settings = Load(settingsPath);
        if (settings["hooks"] is JsonObject hooks)
            RemoveManagedHandlers(hooks);
        else if (settings["hooks"] is not null)
            throw new JsonException("The Claude hooks property must be an object.");

        Save(settingsPath, settings, createBackup: false);
    }

    internal static bool IsInstalled(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath)) return false;
            if (Load(settingsPath)["hooks"] is not JsonObject hooks) return false;
            return ManagedHooks.All(managed =>
                hooks[managed.Event] is JsonArray entries && entries.Any(entry =>
                    entry is JsonObject o && o["hooks"] is JsonArray handlers && handlers.Any(h =>
                        h is JsonObject hj && hj["command"] is JsonValue v &&
                        v.TryGetValue<string>(out var c) && IsManagedCommand(c))));
        }

        catch { return false; }
    }

    private static JsonObject Load(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return new JsonObject();
        return JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject
            ?? throw new JsonException("The Claude hook settings root must be an object.");
    }

    private static JsonObject GetHooks(JsonObject settings)
    {
        if (settings["hooks"] is JsonObject hooks) return hooks;
        if (settings["hooks"] is not null)
            throw new JsonException("The Claude hooks property must be an object.");

        hooks = new JsonObject();
        settings["hooks"] = hooks;
        return hooks;
    }

    private static JsonArray GetEntries(JsonObject hooks, string eventName)
    {
        if (hooks[eventName] is JsonArray entries) return entries;
        if (hooks[eventName] is not null)
            throw new JsonException($"The Claude hook event '{eventName}' must be an array.");

        entries = new JsonArray();
        hooks[eventName] = entries;
        return entries;
    }

    private static void RemoveManagedHandlers(JsonObject hooks)
    {
        foreach (var managed in ManagedHooks)
        {
            if (hooks[managed.Event] is null) continue;
            if (hooks[managed.Event] is not JsonArray entries)
                throw new JsonException($"The Claude hook event '{managed.Event}' must be an array.");

            for (var entryIndex = entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                if (entries[entryIndex] is not JsonObject entry ||
                    entry["hooks"] is not JsonArray handlers)
                    continue;

                for (var handlerIndex = handlers.Count - 1; handlerIndex >= 0; handlerIndex--)
                {
                    if (handlers[handlerIndex] is JsonObject handler &&
                        handler["command"] is JsonValue commandValue &&
                        commandValue.TryGetValue<string>(out var command) &&
                        IsManagedCommand(command))
                        handlers.RemoveAt(handlerIndex);
                }

                if (handlers.Count == 0)
                    entries.RemoveAt(entryIndex);
            }
        }
    }

    private static bool IsManagedCommand(string command)
    {
        var executableEnd = command.IndexOf("Halo.Hooks.exe", StringComparison.OrdinalIgnoreCase);
        if (executableEnd < 0) return false;

        var tail = command[(executableEnd + "Halo.Hooks.exe".Length)..].TrimStart('"', ' ', '\t');
        if (tail.StartsWith("codex ", StringComparison.OrdinalIgnoreCase)) return false;
        return ManagedHooks.Any(managed =>
            tail.Equals(managed.Command, StringComparison.OrdinalIgnoreCase));
    }

    private static void Save(string settingsPath, JsonObject settings, bool createBackup = true)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException("The settings path must include a directory.", nameof(settingsPath));
        Directory.CreateDirectory(directory);

        if (createBackup && File.Exists(settingsPath))
            Backup(settingsPath);

        var temporaryPath = settingsPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath,
                settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            ClearReadOnly(settingsPath);
            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }

    internal static void Backup(string settingsPath)
    {
        string backupPath = settingsPath + ".halo-bak";
        ClearReadOnly(backupPath);
        File.Copy(settingsPath, backupPath, overwrite: true);
    }

    internal static void ClearReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        catch { }
    }
}

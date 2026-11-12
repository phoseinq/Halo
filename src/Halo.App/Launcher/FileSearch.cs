using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Halo.Launcher;

internal static class FileSearch
{
    internal const int MinQuery = 3;
    internal const int MaxHits = 3;
    internal const string FolderGlyph = "\uE8B7";
    private const int DebounceMs = 45;

    private static readonly object Gate = new();
    private static string _asked = "", _answered = "";
    private static IReadOnlyList<LauncherRow> _rows = [];
    private static int _generation;
    private static int _version;

        internal static int Version => Volatile.Read(ref _version);

    internal static string? BuildSql(string query, int top, bool foldersOnly = false)
    {
        var words = new List<string>();

        foreach (var raw in (query ?? "").Replace('\u200C', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = new StringBuilder(raw.Length);
            foreach (char c in raw)
                if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-') clean.Append(c);
            if (clean.Length > 0) words.Add(clean.ToString());
        }
        if (words.Count == 0) return null;

        string match = string.Join(" AND ", words.Select(w => "\"" + w + "*\""));
        return $"SELECT TOP {top} System.ItemPathDisplay FROM SystemIndex "
             + $"WHERE SCOPE='file:' AND CONTAINS(System.FileName, '{match}') "
             + (foldersOnly ? "AND System.ItemType = 'Directory' " : "")
             + "AND NOT System.ItemPathDisplay LIKE '%\\AppData\\%' "
             + "AND NOT System.ItemPathDisplay LIKE '%\\node_modules\\%' "
             + "ORDER BY System.DateModified DESC";
    }

    internal static LauncherRow RowFor(string path)
    {
        string name = Path.GetFileName(path.TrimEnd('\\'));
        string where = Path.GetDirectoryName(path) ?? "";
        bool folder = false;
        try { folder = Directory.Exists(path); } catch { }
        return new LauncherRow(name.Length > 0 ? name : path, null, true, LauncherRowKind.Action,
                               LauncherExtras.FilePrefix + path, Short(where), folder ? FolderGlyph : "\uE8A5",
                               Halo.Widgets.Fx.VitalStorage);
    }

    private static string Short(string dir)
    {
        const int Max = 34;
        return dir.Length <= Max ? dir : "..." + dir[^Max..];
    }

    internal static IReadOnlyList<LauncherRow> For(string query)
    {
        string q = (query ?? "").Trim();
        if (q.Length < MinQuery || q[0] == '>') return [];
        lock (Gate)
        {
            if (_answered == q) return _rows;
            if (_asked != q)
            {
                _asked = q;
                _generation++;
                EnsureWorker();
                Wake.Set();
            }
            if (_answered.Length == 0 || !q.StartsWith(_answered, StringComparison.OrdinalIgnoreCase)) return [];
            string[] words = q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return [.. _rows.Where(r => words.All(w => r.Label.Contains(w, StringComparison.OrdinalIgnoreCase)))];
        }
    }

        internal static void Warm()
    {
        lock (Gate)
        {
            _warm = true;
            EnsureWorker();
            Wake.Set();
        }
    }

    private static readonly AutoResetEvent Wake = new(false);
    private static Thread? _worker;
    private static bool _warm;

    private static void EnsureWorker()
    {
        if (_worker is not null) return;
        _worker = new Thread(Work) { IsBackground = true, Name = "halo-filesearch" };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }

    private static void Work()
    {
        while (true)
        {
            Wake.WaitOne();
            try
            {
                bool warm;
                lock (Gate) { warm = _warm; _warm = false; }
                if (warm) Connect();

                Thread.Sleep(DebounceMs);
                string q; int gen;
                lock (Gate) { q = _asked; gen = _generation; }
                if (q.Length == 0) continue;
                var rows = Search(q, MaxHits).Select(RowFor).ToList();
                lock (Gate)
                {
                    if (gen != _generation) continue;
                    _answered = q;
                    _rows = rows;
                }
                Interlocked.Increment(ref _version);
            }
            catch { }
        }
    }

    [ThreadStatic] private static object? _connection;

    private static dynamic? Connect()
    {
        if (_connection is not null) return _connection;
        Type? type = Type.GetTypeFromProgID("ADODB.Connection");
        if (type is null) return null;
        dynamic? c = Activator.CreateInstance(type);
        if (c is null) return null;
        c.Open("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");
        _connection = c;
        return c;
    }

        internal static bool IsPath(string q)
        => (q.Length >= 3 && char.IsAsciiLetter(q[0]) && q[1] == ':' && q[2] is '\\' or '/')
           || q.StartsWith(@"\\", StringComparison.Ordinal) || q.StartsWith(@"~\", StringComparison.Ordinal)
           || q.StartsWith("%", StringComparison.Ordinal);

    internal static IReadOnlyList<string> Browse(string line, int limit)
    {
        var found = new List<string>(limit);
        try
        {
            string q = Environment.ExpandEnvironmentVariables(line).Replace('/', '\\');
            if (q.StartsWith(@"~\", StringComparison.Ordinal))
                q = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + q[1..];
            int cut = q.LastIndexOf('\\');
            if (cut < 0) return found;
            string dir = q[..(cut + 1)], typed = q[(cut + 1)..];
            if (!Directory.Exists(dir)) return found;
            var info = new DirectoryInfo(dir);
            var quiet = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            found.AddRange(info.EnumerateDirectories(typed + "*", quiet).Take(limit).Select(d => d.FullName));
            found.AddRange(info.EnumerateFiles(typed + "*", quiet).Take(limit - found.Count).Select(f => f.FullName));
        }
        catch { }
        return found;
    }

    internal static IReadOnlyList<string> Search(string query, int top)
    {
        if (IsPath(query.Trim())) return Browse(query.Trim(), top * 2);
        var found = new List<string>(top * 2);
        Run(BuildSql(query, top, foldersOnly: true), found, top);
        Run(BuildSql(query, top), found, top * 2);
        return found;
    }

    private static void Run(string? sql, List<string> found, int limit)
    {
        if (sql is null) return;
        dynamic? records = null;
        try
        {
            dynamic? connection = Connect();
            if (connection is null) return;
            records = connection.Execute(sql);
            while (!(bool)records.EOF && found.Count < limit)
            {
                if (records.Fields.Item(0).Value is string path && path.Length > 0 && !found.Contains(path, StringComparer.OrdinalIgnoreCase))
                    found.Add(path);
                records.MoveNext();
            }
        }
        catch { _connection = null; }
        finally
        {
            try { records?.Close(); } catch { }
        }
    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace Halo.Notifications;

internal static class HistoryStore
{
    internal const int Max = 60;
    internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    private const int IconPx = 64;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "notif-history.bin");

    internal static string Serialize(IEnumerable<NotifItem> items)
    {
        var arr = new JsonArray();
        foreach (var it in items)
        {
            var o = new JsonObject
            {
                ["id"] = it.Id,
                ["time"] = it.Time.ToUniversalTime().ToString("o"),
                ["app"] = it.App,
                ["title"] = it.Title,
                ["body"] = it.Body,
                ["aumid"] = it.Aumid,
                ["launch"] = it.LaunchPath,
                ["kind"] = it.Kind,
                ["code"] = it.Code,
                ["stacked"] = it.Stacked,
            };
            if (IconPng(it.Icon) is { } png) o["icon"] = Convert.ToBase64String(png);
            arr.Add(o);
        }
        return arr.ToJsonString();
    }

        internal static List<NotifItem> Deserialize(string json, DateTime nowUtc)
    {
        var list = new List<NotifItem>();
        if (JsonNode.Parse(json) is not JsonArray arr) return list;
        foreach (var n in arr)
        {
            if (n is not JsonObject o) continue;
            if (!DateTime.TryParse(o["time"]?.GetValue<string>(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var t)) continue;
            if (nowUtc - t.ToUniversalTime() > MaxAge) continue;
            var it = new NotifItem
            {
                Id = o["id"]?.GetValue<uint>() ?? 0,
                Time = t.ToLocalTime(),
                App = o["app"]?.GetValue<string>() ?? "",
                Title = o["title"]?.GetValue<string>() ?? "",
                Body = o["body"]?.GetValue<string>() ?? "",
                Aumid = o["aumid"]?.GetValue<string>() ?? "",
                LaunchPath = o["launch"]?.GetValue<string>() ?? "",
                Kind = o["kind"]?.GetValue<string>() ?? "",
                Code = o["code"]?.GetValue<string>() ?? "",
                Stacked = o["stacked"]?.GetValue<int>() ?? 0,
            };
            if (o["icon"]?.GetValue<string>() is { Length: > 0 } b64)
            {
                try
                {
                    using var ms = new MemoryStream(Convert.FromBase64String(b64));
                    using var raw = new System.Drawing.Bitmap(ms);
                    it.Icon = new System.Drawing.Bitmap(raw);
                }
                catch { }
            }
            list.Add(it);
        }
        if (list.Count > Max) list.RemoveRange(0, list.Count - Max);
        return list;
    }

    private static byte[]? IconPng(System.Drawing.Bitmap? icon)
    {
        if (icon is null) return null;
        try
        {
            int s = Math.Min(IconPx, Math.Max(icon.Width, icon.Height));
            using var small = new System.Drawing.Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(icon, 0, 0, s, s);
            }
            using var ms = new MemoryStream();
            small.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        catch { return null; }
    }

    internal static List<NotifItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var plain = Unprotect(File.ReadAllBytes(FilePath));
            return plain is null ? new() : Deserialize(Encoding.UTF8.GetString(plain), DateTime.UtcNow);
        }
        catch { return new(); }
    }

    internal static void Save(IReadOnlyList<NotifItem> items)
    {
        try
        {
            var sealedBytes = Protect(Encoding.UTF8.GetBytes(Serialize(items)));
            if (sealedBytes is null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string tmp = FilePath + ".tmp";
            File.WriteAllBytes(tmp, sealedBytes);
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB { public int cbData; public IntPtr pbData; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    internal static byte[]? Protect(byte[] plain) => Crypt(plain, protect: true);
    internal static byte[]? Unprotect(byte[] sealedBytes) => Crypt(sealedBytes, protect: false);

    private static byte[]? Crypt(byte[] input, bool protect)
    {
        var inBlob = new DATA_BLOB { cbData = input.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, input.Length)) };
        var outBlob = new DATA_BLOB();
        try
        {
            Marshal.Copy(input, 0, inBlob.pbData, input.Length);
            bool ok = protect
                ? CryptProtectData(ref inBlob, "Halo notification history", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                   CRYPTPROTECT_UI_FORBIDDEN, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                     CRYPTPROTECT_UI_FORBIDDEN, ref outBlob);
            if (!ok || outBlob.pbData == IntPtr.Zero) return null;
            var result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        catch { return null; }
        finally
        {
            Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }
}

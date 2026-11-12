using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;

namespace Halo.Launcher;

internal static class TextShelf
{
    internal static byte[] Pack(IEnumerable<string> texts)
    {
        var b = new StringBuilder();
        foreach (var t in texts) b.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(t))).Append('\n');
        return Encoding.UTF8.GetBytes(b.ToString());
    }

    internal static List<string> Unpack(byte[] bytes)
    {
        var list = new List<string>();
        foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            list.Add(Encoding.UTF8.GetString(Convert.FromBase64String(line)));
        return list;
    }
}

internal static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Size; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved,
                                                IntPtr prompt, int flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved,
                                                  IntPtr prompt, int flags, out Blob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    private const int NoUi = 1;

    internal static byte[]? Protect(byte[] plain) => Run(plain, protect: true);
    internal static byte[]? Unprotect(byte[] sealedBytes) => Run(sealedBytes, protect: false);

    private static byte[]? Run(byte[] data, bool protect)
    {
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var input = new Blob { Size = data.Length, Data = pin.AddrOfPinnedObject() };
            bool ok = protect
                ? CryptProtectData(ref input, "Halo", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, NoUi, out var output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, NoUi, out output);
            if (!ok) return null;
            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            LocalFree(output.Data);
            return result;
        }
        catch { return null; }
        finally { pin.Free(); }
    }
}

internal static class ClipPins
{
    internal const int Max = 40;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "clip-pins.dat");

    internal static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return Dpapi.Unprotect(File.ReadAllBytes(FilePath)) is { } plain ? TextShelf.Unpack(plain) : [];
        }
        catch { return []; }
    }

    private static void Save(List<string> texts)
    {
        try
        {
            if (Dpapi.Protect(TextShelf.Pack(texts)) is not { } sealedBytes) return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, sealedBytes);
        }
        catch { }
    }

    internal static void Add(string text)
    {
        var all = Load();
        if (all.Contains(text) || all.Count >= Max) return;
        all.Add(text);
        Save(all);
    }

    internal static void RemoveAt(int index)
    {
        var all = Load();
        if (index < 0 || index >= all.Count) return;
        all.RemoveAt(index);
        Save(all);
    }
}

internal static class LauncherVault
{
    private const string CredentialName = "Halo.Vault";
    private const int NonceSize = 12, TagSize = 16;

    private static readonly byte[] Challenge = Encoding.ASCII.GetBytes("halo-vault-key-derivation-v1");

    private static byte[]? _key;
    private static List<string> _texts = [];

    internal static bool IsUnlocked => _key is not null;
    internal static IReadOnlyList<string> Texts => _texts;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "vault.dat");

    internal static byte[] Seal(byte[] plain, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];
        using (var gcm = new AesGcm(key, TagSize)) gcm.Encrypt(nonce, plain, cipher, tag);
        var all = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(all, 0);
        tag.CopyTo(all, NonceSize);
        cipher.CopyTo(all, NonceSize + TagSize);
        return all;
    }

        internal static byte[]? Open(byte[] sealedBytes, byte[] key)
    {
        if (sealedBytes.Length < NonceSize + TagSize) return null;
        try
        {
            var cipher = sealedBytes.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            using var gcm = new AesGcm(key, TagSize);
            gcm.Decrypt(sealedBytes.AsSpan(0, NonceSize), cipher, sealedBytes.AsSpan(NonceSize, TagSize), plain);
            return plain;
        }
        catch (CryptographicException) { return null; }
    }

    private static async Task<byte[]?> SignatureAsync()
    {
        if (!await KeyCredentialManager.IsSupportedAsync()) return null;
        var opened = await KeyCredentialManager.OpenAsync(CredentialName);
        var credential = opened.Status == KeyCredentialStatus.Success
            ? opened.Credential
            : opened.Status == KeyCredentialStatus.NotFound
                ? (await KeyCredentialManager.RequestCreateAsync(CredentialName, KeyCredentialCreationOption.FailIfExists)).Credential
                : null;
        if (credential is null) return null;
        var signed = await credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(Challenge));
        if (signed.Status != KeyCredentialStatus.Success) return null;
        CryptographicBuffer.CopyToByteArray(signed.Result, out byte[] signature);
        return signature;
    }

        internal static async Task<bool> UnlockAsync()
    {
        try
        {
            if (await SignatureAsync() is not { } signature) return false;
            var key = SHA256.HashData(signature);
            var texts = new List<string>();
            if (File.Exists(FilePath))
            {

                if (Open(File.ReadAllBytes(FilePath), key) is not { } plain) return false;
                texts = TextShelf.Unpack(plain);
            }
            _key = key;
            _texts = texts;
            return true;
        }
        catch { return false; }
    }

        internal static async Task<string> ProbeAsync()
    {
        var first = await SignatureAsync();
        if (first is null) return "first signature failed";
        var second = await SignatureAsync();
        if (second is null) return "second signature failed";
        return $"length={first.Length} repeatable={CryptographicOperations.FixedTimeEquals(first, second)}";
    }

    internal static void Lock()
    {
        if (_key is not null) CryptographicOperations.ZeroMemory(_key);
        _key = null;
        _texts = [];
    }

    private static void Save()
    {
        if (_key is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, Seal(TextShelf.Pack(_texts), _key));
    }

    internal static void Add(string text)
    {
        if (_key is null || _texts.Contains(text)) return;
        _texts.Add(text);
        try { Save(); } catch { }
    }

    internal static void RemoveAt(int index)
    {
        if (_key is null || index < 0 || index >= _texts.Count) return;
        _texts.RemoveAt(index);
        try { Save(); } catch { }
    }
}

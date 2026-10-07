using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NoxVault;

internal sealed class RememberedLogin
{
    readonly string path;
    readonly string revokedPath;
    readonly byte[] entropy;
    readonly bool permanent;
    internal RememberedLogin(string vaultPath, bool permanent = false)
    {
        this.permanent = permanent;
        path = vaultPath + (permanent ? ".quickfill" : ".session");
        revokedPath = path + ".revoked";
        entropy = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(vaultPath).ToUpperInvariant()));
    }
    internal DateTime Save(Vault vault, DateTime now)
    {
        var expires = permanent ? DateTime.MaxValue : now.Date.AddDays(1);
        var key = vault.SessionKey();
        var payload = new byte[80];
        try
        {
            BitConverter.GetBytes(now.Ticks).CopyTo(payload, 0);
            BitConverter.GetBytes(expires.Ticks).CopyTo(payload, 8);
            key.CopyTo(payload, 16);
            Vault.AtomicWrite(path, ProtectedData.Protect(payload, entropy, DataProtectionScope.CurrentUser));
            // Re-enable only after a freshly authenticated session was persisted.
            if (File.Exists(revokedPath)) File.Delete(revokedPath);
            return expires;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(payload); }
    }
    internal VaultData? Restore(Vault vault, DateTime now, out DateTime expires)
    {
        expires = default;
        vault.Dispose();
        if (File.Exists(revokedPath) || !File.Exists(path)) return null;
        byte[]? payload = null, key = null;
        try
        {
            if (new FileInfo(path).Length > 4096) throw new InvalidDataException();
            payload = ProtectedData.Unprotect(File.ReadAllBytes(path), entropy, DataProtectionScope.CurrentUser);
            if (payload.Length != 80) throw new InvalidDataException();
            var issued = new DateTime(BitConverter.ToInt64(payload, 0));
            var deadline = new DateTime(BitConverter.ToInt64(payload, 8));
            if (now < issued || now >= deadline || deadline != (permanent ? DateTime.MaxValue : issued.Date.AddDays(1))) throw new InvalidDataException();
            key = payload.AsSpan(16, 64).ToArray();
            var data = vault.OpenSession(key); expires = deadline; return data;
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        {
            try { Clear(); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            return null;
        }
        finally
        {
            if (payload != null) CryptographicOperations.ZeroMemory(payload);
            if (key != null) CryptographicOperations.ZeroMemory(key);
        }
    }
    internal void Clear()
    {
        if (!File.Exists(path)) return;
        // A separate revocation marker survives a sharing violation on the
        // protected session file. Never restore that file on a subsequent run.
        Vault.AtomicWrite(revokedPath, Array.Empty<byte>());
        File.Delete(path);
    }
}

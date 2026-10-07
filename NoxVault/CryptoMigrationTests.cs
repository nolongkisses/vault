using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoxVault;

internal static class CryptoMigrationTests
{
    internal static void Run(string folder, Action<bool, string> check, Action<Action, string> reject)
    {
        // Independently generated with argon2-cffi (reference libargon2), v19,
        // m=65536 KiB, t=3, p=4, 32-byte output; checks our production parameters.
        var derived = Vault.DeriveKey("Independent test password!", Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        check(Convert.ToHexString(derived).Equals("fdd4b34537f5c369ae5f8e5defb6c90e88bd7386eff70ba25fdd4dc73d2c9fb5", StringComparison.OrdinalIgnoreCase),
            "Argon2id matches independent libargon2 reference vector");
        CryptographicOperations.ZeroMemory(derived);

        const string password = "Legacy synthetic master phrase!";
        var data = new VaultData();
        data.Accounts.Add(new Account { Title = "Migration", Category = data.Categories[0], Password = "Only synthetic 🔐 äöü", Notes = "Preserve all fields", Favorite = true });
        var json = JsonSerializer.SerializeToUtf8Bytes(data);
        var legacy = new byte[68 + json.Length];
        Encoding.ASCII.GetBytes("NOXVL001").CopyTo(legacy, 0);
        RandomNumberGenerator.Fill(legacy.AsSpan(8, 32));
        RandomNumberGenerator.Fill(legacy.AsSpan(40, 12));
        var oldKey = Rfc2898DeriveBytes.Pbkdf2(password, legacy.AsSpan(8, 32), 600000, HashAlgorithmName.SHA256, 32);
        var oldSession = legacy.AsSpan(8, 32).ToArray().Concat(oldKey).ToArray();
        using (var aes = new AesGcm(oldKey, 16))
            aes.Encrypt(legacy.AsSpan(40, 12), json, legacy.AsSpan(68), legacy.AsSpan(52, 16), legacy.AsSpan(0, 40));
        CryptographicOperations.ZeroMemory(oldKey);
        CryptographicOperations.ZeroMemory(json);
        var path = Path.Combine(folder, "legacy.nox");
        File.WriteAllBytes(path, legacy);
        using var vault = new Vault(path);
        reject(() => vault.Open("Wrong master phrase!"), "Legacy migration requires correct master password");
        check(!vault.Unlocked && File.ReadAllBytes(path).SequenceEqual(legacy), "Failed legacy authentication preserves file and locked state");
        check(JsonSerializer.Serialize(Vault.ReadBackup(path, password)) == JsonSerializer.Serialize(data) && File.ReadAllBytes(path).SequenceEqual(legacy),
            "Legacy backup reads all fields without modifying backup");
        reject(() => vault.OpenSession(oldSession), "Legacy remembered login requires password for migration");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            reject(() => vault.Open(password), "Migration rejects blocked atomic replacement");
        check(!vault.Unlocked && File.ReadAllBytes(path).SequenceEqual(legacy), "Failed migration preserves original encrypted vault and releases key");
        check(JsonSerializer.Serialize(vault.Open(password)) == JsonSerializer.Serialize(data), "Migration preserves every account field including Unicode");
        var migrated = File.ReadAllBytes(path);
        check(Encoding.ASCII.GetString(migrated, 0, 8) == "NOXVL004" && !migrated.AsSpan(8, 32).SequenceEqual(legacy.AsSpan(8, 32)),
            "Migration writes v4 with fresh salt");
        reject(() => vault.OpenSession(oldSession), "Pre-migration session cannot open upgraded vault");
        CryptographicOperations.ZeroMemory(oldSession);
        check(JsonSerializer.Serialize(vault.Open(password)) == JsonSerializer.Serialize(data) && File.ReadAllBytes(path).SequenceEqual(migrated),
            "Migrated vault reopens without another rewrite");
        var currentSession = vault.SessionKey();
        try { check(vault.OpenSession(currentSession).Accounts[0].Password == data.Accounts[0].Password, "Upgraded vault supports new remembered session"); }
        finally { CryptographicOperations.ZeroMemory(currentSession); }
        vault.Save(data);
        check(vault.Open(password).Accounts.Count == 1, "Saving after migration retains new key and format");
        migrated[7] = (byte)'1'; File.WriteAllBytes(path, migrated);
        reject(() => vault.Open(password), "Format downgrade tampering fails authentication");
        File.WriteAllBytes(path, new byte[67]);
        reject(() => vault.Open(password), "Truncated vault rejected before key derivation");
        check(!Directory.GetFiles(folder, "*.tmp").Any(), "Migration leaves no temporary files");
    }
}

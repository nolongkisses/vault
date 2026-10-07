using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace NoxVault;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Website { get; set; } = "";
    public RiotFillProfile? Autofill { get; set; }
    public List<SecretField> Fields { get; set; } = new();
    public DateTime? DeletedUtc { get; set; }
    public bool Favorite { get; set; }
    public DateTime Updated { get; set; } = DateTime.Now;
}

public sealed record RiotFillProfile(string Path, string Hash, bool UseEmail);

public sealed class SecretField
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class VaultData
{
    public List<string> Categories { get; set; } = new() { "Valorant", "League of Legends", "Andere Spiele", "Sonstige Accounts" };
    public Dictionary<string, string> CategoryColors { get; set; } = new();
    public List<Account> Accounts { get; set; } = new();
    public VaultData Clone() => new()
    {
        Categories = new List<string>(Categories),
        CategoryColors = new Dictionary<string, string>(CategoryColors),
        Accounts = Accounts.Select(a => new Account { Id = a.Id, Title = a.Title, Category = a.Category, Email = a.Email,
            Username = a.Username, Password = a.Password, Notes = a.Notes, Website = a.Website, Autofill = a.Autofill, DeletedUtc = a.DeletedUtc, Fields = a.Fields.Select(f => new SecretField { Label = f.Label, Value = f.Value }).ToList(), Favorite = a.Favorite, Updated = a.Updated }).ToList()
    };
    public int PurgeExpired(DateTime utcNow) => Accounts.RemoveAll(a => a.DeletedUtc.HasValue && a.DeletedUtc.Value <= utcNow.AddDays(-30));
    public void Validate()
    {
        CategoryColors ??= new();
        if (CategoryColors.Count > 200 || CategoryColors.Any(p => Categories == null || !Categories.Contains(p.Key) || p.Value == null || p.Value.Length != 7 || p.Value[0] != '#' || !p.Value.Skip(1).All(Uri.IsHexDigit))) throw new InvalidDataException("Ungültige Kategoriefarbe.");
        if (Accounts != null && Accounts.Any(a => a?.Autofill is { } f &&
            (string.IsNullOrWhiteSpace(f.Path) || !Path.IsPathFullyQualified(f.Path) || f.Path.Length > 1024 ||
             f.Hash == null || f.Hash.Length != 64 || !f.Hash.All(Uri.IsHexDigit))))
            throw new InvalidDataException("Ungültige Ausfüllzuordnung.");
        if (Categories == null || Accounts == null || Categories.Count == 0 || Categories.Count > 200 || Accounts.Count > 100000 ||
            Categories.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 80) || Categories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Categories.Count ||
            Accounts.Any(a => a == null || string.IsNullOrWhiteSpace(a.Title) || !Categories.Contains(a.Category) || a.Email == null || a.Username == null || a.Password == null || a.Notes == null || a.Website == null || a.Fields == null || a.Fields.Count > 30 || a.Fields.Any(f => f == null || string.IsNullOrWhiteSpace(f.Label) || f.Label.Length > 80 || f.Value == null || f.Value.Length > 10000)) ||
            Accounts.Select(a => a.Id).Distinct().Count() != Accounts.Count)
            throw new InvalidDataException("Der Tresor enthält ungültige Einträge.");
    }
}

// Format: magic (8), salt (32), nonce (12), authentication tag (16), ciphertext.
// Magic + salt are authenticated as AAD; GCM authenticates nonce and ciphertext.
// v1: PBKDF2-SHA256/600000. v2: Argon2id v1.3, 64 MiB, t=3, p=4.
// v3 uses the same cryptography and supports trash, websites and custom fields.
// v4 additionally persists explicit app bindings inside the encrypted payload.
// Parameters are fixed by the format version, never controlled by untrusted input.
public sealed class Vault : IDisposable
{
    const int Iterations = 600000;
    static readonly byte[] LegacyMagic = Encoding.ASCII.GetBytes("NOXVL001");
    static readonly byte[] PreviousMagic = Encoding.ASCII.GetBytes("NOXVL002");
    static readonly byte[] VersionThreeMagic = Encoding.ASCII.GetBytes("NOXVL003");
    static readonly byte[] Magic = Encoding.ASCII.GetBytes("NOXVL004");
    byte[]? key;
    byte[]? salt;
    public string FilePath { get; }
    bool previouslyExisted;
    public bool Exists
    {
        get
        {
            if (StoragePaths.FilePresent(FilePath)) { previouslyExisted = true; return true; }
            if (previouslyExisted || StoragePaths.FilePresent(FilePath + ".initialized"))
                throw new IOException("Der bestehende Tresor ist momentan nicht erreichbar. Bitte erneut versuchen.");
            return false;
        }
    }
    public bool Unlocked => key != null;
    public Vault(string path) => FilePath = path;
    internal VaultData CreateForWindows()
    {
        if (Exists) throw new IOException("Es existiert bereits ein Tresor.");
        Dispose();
        salt = RandomNumberGenerator.GetBytes(32);
        key = RandomNumberGenerator.GetBytes(32);
        var data = new VaultData();
        try
        {
            // Persist the Windows-protected key before committing the vault.
            new RememberedLogin(FilePath, permanent: true).Save(this, DateTime.Now);
            SaveCore(data, overwrite: false); previouslyExisted = true;
            AtomicWrite(FilePath + ".initialized", Array.Empty<byte>(), overwrite: false);
            return data;
        }
        catch { Dispose(); throw; }
    }
    internal VaultData ReadBackupForWindows(string path)
    {
        var session = SessionKey();
        try { using var backup = new Vault(path); return backup.OpenSession(session); }
        finally { CryptographicOperations.ZeroMemory(session); }
    }
    public VaultData Create(string password)
    {
        if (Exists) throw new IOException("Es existiert bereits ein Tresor.");
        if (password.Length < 12) throw new ArgumentException("Bitte mindestens 12 Zeichen verwenden.");
        Dispose();
        salt = RandomNumberGenerator.GetBytes(32);
        key = DeriveKey(password, salt);
        var data = new VaultData();
        try
        {
            SaveCore(data, overwrite: false); previouslyExisted = true;
            AtomicWrite(FilePath + ".initialized", Array.Empty<byte>(), overwrite: false);
            return data;
        }
        catch { Dispose(); throw; }
    }
    public VaultData Open(string password)
    {
        Dispose();
        var bytes = ReadBounded(FilePath);
        var (data, newKey, newSalt) = Decode(bytes, password);
        bool purged = data.PurgeExpired(DateTime.UtcNow) > 0;
        if (!bytes.AsSpan(0, 8).SequenceEqual(Magic))
        {
            // Authenticate first; retain the original file if deriving or writing fails.
            CryptographicOperations.ZeroMemory(newKey);
            newSalt = RandomNumberGenerator.GetBytes(32);
            newKey = DeriveKey(password, newSalt);
            using var upgraded = new Vault(FilePath) { key = newKey, salt = newSalt };
            upgraded.Save(data);
            upgraded.key = null; // Ownership transfers only after the atomic write succeeds.
        }
        Dispose(); key = newKey; salt = newSalt;
        if (purged) { try { Save(data); } catch { Dispose(); throw; } }
        return data;
    }
    public static VaultData ReadBackup(string path, string password)
    {
        var (data, backupKey, _) = Decode(ReadBounded(path), password);
        CryptographicOperations.ZeroMemory(backupKey);
        return data;
    }
    internal byte[] SessionKey()
    {
        if (key == null || salt == null) throw new InvalidOperationException("Tresor ist gesperrt.");
        return salt.Concat(key).ToArray();
    }
    internal VaultData OpenSession(byte[] session)
    {
        Dispose();
        var bytes = ReadBounded(FilePath);
        if (session.Length != 64 || !bytes.AsSpan(0, 8).SequenceEqual(Magic) ||
            !bytes.AsSpan(8, 32).SequenceEqual(session.AsSpan(0, 32))) throw new CryptographicException();
        var plain = new byte[bytes.Length - 68];
        try
        {
            using var aes = new AesGcm(session.AsSpan(32, 32), 16);
            aes.Decrypt(bytes.AsSpan(40, 12), bytes.AsSpan(68), bytes.AsSpan(52, 16), plain, bytes.AsSpan(0, 40));
            var data = JsonSerializer.Deserialize<VaultData>(plain) ?? throw new InvalidDataException();
            data.Validate();
            key = session.AsSpan(32, 32).ToArray(); salt = session.AsSpan(0, 32).ToArray();
            return data;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    internal static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var size = stream.Length;
        if (size < 68 || size > 32 * 1024 * 1024) throw new InvalidDataException("Ungültige Tresorgröße.");
        var bytes = new byte[(int)size];
        stream.ReadExactly(bytes);
        return bytes;
    }
    internal static byte[] DeriveKey(string password, byte[] salt)
    {
        var encoded = Encoding.UTF8.GetBytes(password);
        var result = new byte[32];
        var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13).WithMemoryAsKB(65536)
            .WithIterations(3).WithParallelism(4).WithSalt(salt).Build();
        try
        {
            var generator = new Argon2BytesGenerator();
            generator.Init(parameters);
            generator.GenerateBytes(encoded, result);
            return result;
        }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
        finally { CryptographicOperations.ZeroMemory(encoded); parameters.Clear(); }
    }
    static (VaultData, byte[], byte[]) Decode(byte[] bytes, string password)
    {
        if (bytes.Length < 68 || (!bytes.AsSpan(0, 8).SequenceEqual(Magic) && !bytes.AsSpan(0, 8).SequenceEqual(VersionThreeMagic) && !bytes.AsSpan(0, 8).SequenceEqual(PreviousMagic) && !bytes.AsSpan(0, 8).SequenceEqual(LegacyMagic)))
            throw new InvalidDataException("Keine unterstützte Nox-Tresordatei.");
        var s = bytes.AsSpan(8, 32).ToArray();
        var k = bytes.AsSpan(0, 8).SequenceEqual(LegacyMagic)
            ? Rfc2898DeriveBytes.Pbkdf2(password, s, Iterations, HashAlgorithmName.SHA256, 32)
            : DeriveKey(password, s);
        var plain = new byte[bytes.Length - 68];
        try
        {
            using var aes = new AesGcm(k, 16);
            aes.Decrypt(bytes.AsSpan(40, 12), bytes.AsSpan(68), bytes.AsSpan(52, 16), plain, bytes.AsSpan(0, 40));
            var data = JsonSerializer.Deserialize<VaultData>(plain) ?? throw new InvalidDataException();
            data.Validate();
            return (data, k, s);
        }
        catch { CryptographicOperations.ZeroMemory(k); throw; }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void Save(VaultData data) => SaveCore(data, overwrite: true);
    void SaveCore(VaultData data, bool overwrite)
    {
        if (key == null || salt == null) throw new InvalidOperationException("Tresor ist gesperrt.");
        data.Validate();
        var plain = JsonSerializer.SerializeToUtf8Bytes(data);
        try
        {
            if (plain.Length > 32 * 1024 * 1024 - 68) throw new InvalidDataException("Der Tresor überschreitet die maximale Größe von 32 MB.");
            var bytes = new byte[68 + plain.Length];
            Magic.CopyTo(bytes, 0); salt.CopyTo(bytes, 8);
            RandomNumberGenerator.Fill(bytes.AsSpan(40, 12));
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(bytes.AsSpan(40, 12), plain, bytes.AsSpan(68), bytes.AsSpan(52, 16), bytes.AsSpan(0, 40));
            AtomicWrite(FilePath, bytes, overwrite);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void ChangePassword(string currentPassword, string newPassword)
    {
        if (key == null || salt == null) throw new InvalidOperationException("Tresor ist gesperrt.");
        if (newPassword.Length < 12) throw new ArgumentException("Bitte mindestens 12 Zeichen verwenden.");
        var (data, verifiedKey, _) = Decode(ReadBounded(FilePath), currentPassword);
        CryptographicOperations.ZeroMemory(verifiedKey);
        var nextSalt = RandomNumberGenerator.GetBytes(32);
        var nextKey = DeriveKey(newPassword, nextSalt);
        // Keep the active key intact until the atomic file replacement succeeds.
        using var replacement = new Vault(FilePath) { key = nextKey, salt = nextSalt };
        replacement.Save(data);
        CryptographicOperations.ZeroMemory(key);
        key = nextKey; salt = nextSalt; replacement.key = null;
    }
    public void Backup(string path)
    {
        if (Path.GetFullPath(path).Equals(Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase)) throw new IOException("Bitte einen anderen Speicherort wählen.");
        AtomicWrite(path, ReadBounded(FilePath));
    }
    public static void AtomicWrite(string path, byte[] bytes, bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes); f.Flush(true); }
            File.Move(temp, path, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose()
    {
        if (key != null) CryptographicOperations.ZeroMemory(key);
        key = null; salt = null;
    }

}

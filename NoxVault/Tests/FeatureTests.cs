using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoxVault;

// Preferences, generator, search, trash, custom fields, v2 migration and automatic backups, all with synthetic data.
internal static class FeatureTests
{
    const string Password = "Feature test master phrase!";

    internal static void Run(string folder, Action<bool, string> check, Action<Action, string> reject)
    {
        PreferenceAndGeneratorTests(check, reject);
        var data = QueryTests(check, reject);
        var path = Path.Combine(folder, "features.nox");
        using var vault = new Vault(path);
        vault.Create(Password);
        vault.Save(data);
        PersistenceTests(vault, data, check, reject);
        MigrationTest(folder, check);
        BackupTests(folder, vault, check, reject);
    }

    static void PreferenceAndGeneratorTests(Action<bool, string> check, Action<Action, string> reject)
    {
        var prefs = JsonSerializer.Deserialize<Preferences>(
            "{\"IdleMinutes\":-1,\"ClipboardSeconds\":999,\"RevealSeconds\":0,\"BackupRetention\":-1,\"AccountSort\":null}")!;
        prefs.Normalize();
        check(prefs.IdleMinutes == 5 && prefs.ClipboardSeconds == 30 && prefs.RevealSeconds == 15 && prefs.BackupRetention == 7
            && prefs.AccountSort == "Name", "Invalid preferences fall back to compatible defaults");
        prefs.IdleMinutes = 15;
        prefs.ClipboardSeconds = 60;
        prefs.RevealSeconds = 10;
        var loaded = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;
        loaded.Normalize();
        check(loaded.IdleMinutes == 15 && loaded.ClipboardSeconds == 60
            && loaded.RevealSeconds == 10, "Security preferences survive serialization");
        for (int mask = 1; mask < 16; mask++)
        {
            var options = new GeneratorOptions
            {
                Length = 32, Upper = (mask & 1) != 0, Lower = (mask & 2) != 0, Digits = (mask & 4) != 0, Symbols = (mask & 8) != 0,
            };
            var groups = options.Groups();
            var values = Enumerable.Range(0, 10).Select(_ => options.Generate()).ToArray();
            check(values.All(value => value.Length == 32 && value.All(string.Concat(groups).Contains)
                && groups.All(g => value.Any(g.Contains))),
                "Generator honors character groups " + mask);
        }
        reject(() => new GeneratorOptions { Upper = false, Lower = false, Digits = false, Symbols = false }.Generate(),
            "Generator rejects empty alphabet");
        reject(() => new GeneratorOptions { Length = 10000 }.Generate(), "Generator rejects out-of-range length");
        check(new GeneratorOptions { Length = 128 }.Generate().Length == 128, "Generator supports long passwords");
        check(AccountQueries.ValidWebsite("https://example.invalid/path") && AccountQueries.ValidWebsite("")
            && !AccountQueries.ValidWebsite("file:///C:/Windows") && !AccountQueries.ValidWebsite("javascript:alert(1)")
            && !AccountQueries.ValidWebsite("https://user:pass@example.invalid"),
            "Website validation restricts browser launch to HTTP(S) without credentials");
    }

    static VaultData QueryTests(Action<bool, string> check, Action<Action, string> reject)
    {
        var data = new VaultData();
        data.CategoryColors[data.Categories[0]] = "#9E8CFC";
        var colorClone = data.Clone();
        colorClone.CategoryColors[data.Categories[0]] = "#3B82F6";
        check(data.CategoryColors[data.Categories[0]] == "#9E8CFC", "Category colors are deep-cloned before mutation");
        colorClone.CategoryColors[data.Categories[0]] = "invalid";
        reject(() => colorClone.Validate(), "Invalid category colors are rejected");
        var recovery = new SecretField { Label = "Recovery", Value = "Synthetic recovery code" };
        data.Accounts.Add(new Account
        {
            Title = "First", Category = data.Categories[0], Password = "Synthetic duplicate", Website = "https://example.invalid",
            Fields = new() { recovery },
        });
        data.Accounts.Add(new Account { Title = "Second", Category = data.Categories[0], Password = "Synthetic duplicate" });
        data.Accounts.Add(new Account { Title = "Empty", Category = data.Categories[0] });
        data.Accounts.Add(new Account
        {
            Title = "Deleted", Category = data.Categories[0], Password = "Hidden trash password",
            DeletedUtc = DateTime.UtcNow
        });
        var clone = data.Clone();
        clone.Accounts[0].Fields[0].Value = "Changed";
        check(data.Accounts[0].Fields[0].Value == "Synthetic recovery code", "Custom fields are deep-cloned before mutation");
        check(AccountQueries.PasswordIssues(data).Count == 3, "Local password check detects empty and reused passwords and ignores trash");
        check(AccountQueries.Filter(data, null, false, "example.invalid", "Name").Count == 1
            && AccountQueries.Filter(data, null, false, "Hidden trash password", "Name").Count == 0
            && AccountQueries.Filter(data, null, false, "Synthetic recovery code", "Name").Count == 0,
            "Search includes websites but excludes trash and secret field values");
        return data;
    }

    static void PersistenceTests(Vault vault, VaultData data, Action<bool, string> check, Action<Action, string> reject)
    {
        var path = vault.FilePath;
        var restored = vault.Open(Password);
        check(restored.CategoryColors[data.Categories[0]] == "#9E8CFC"
            && Vault.ReadBackup(path, Password).CategoryColors[data.Categories[0]] == "#9E8CFC",
            "Category colors survive encrypted save and backup round trip");
        check(restored.Accounts[0].Website == data.Accounts[0].Website
            && restored.Accounts[0].Fields[0].Value == data.Accounts[0].Fields[0].Value
            && restored.Accounts[3].DeletedUtc.HasValue, "Website, custom fields and trash survive encrypted round trip");
        check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("Synthetic recovery"),
            "Custom fields are absent from persisted plaintext");
        restored.Accounts[3].DeletedUtc = null;
        vault.Save(restored);
        check(vault.Open(Password).Accounts.All(a => a.DeletedUtc == null), "Trash restoration preserves encrypted account");
        restored.Accounts[3].DeletedUtc = DateTime.UtcNow.AddDays(-31);
        vault.Save(restored);
        check(vault.Open(Password).Accounts.Count == 3 && Vault.ReadBackup(path, Password).Accounts.Count == 3,
            "Expired trash is purged and persisted at unlock");
        var invalid = data.Clone();
        invalid.Accounts[0].Fields[0].Label = "";
        var before = File.ReadAllBytes(path);
        reject(() => vault.Save(invalid), "Unnamed custom field rejected");
        check(before.SequenceEqual(File.ReadAllBytes(path)), "Invalid custom fields preserve prior encrypted file");
    }

    // Builds an independent v2 fixture using its original format and production KDF.
    static void MigrationTest(string folder, Action<bool, string> check)
    {
        var v2json = Encoding.UTF8.GetBytes(
            "{\"Categories\":[\"Old\"],\"Accounts\":[{\"Title\":\"v2 account\",\"Category\":\"Old\","
            + "\"Password\":\"Synthetic old secret\"}]}");
        var v2 = new byte[68 + v2json.Length];
        Encoding.ASCII.GetBytes("NOXVL002").CopyTo(v2, 0);
        RandomNumberGenerator.Fill(v2.AsSpan(8, 32));
        RandomNumberGenerator.Fill(v2.AsSpan(40, 12));
        var v2key = Vault.DeriveKey(Password, v2.AsSpan(8, 32).ToArray());
        using (var aes = new AesGcm(v2key, 16)) aes.Encrypt(v2.AsSpan(40, 12), v2json, v2.AsSpan(68), v2.AsSpan(52, 16), v2.AsSpan(0, 40));
        CryptographicOperations.ZeroMemory(v2key);
        CryptographicOperations.ZeroMemory(v2json);
        var v2path = Path.Combine(folder, "v2.nox");
        File.WriteAllBytes(v2path, v2);
        using var previous = new Vault(v2path);
        var upgraded = previous.Open(Password);
        check(upgraded.Accounts[0].Password == "Synthetic old secret" && upgraded.Accounts[0].Website == ""
            && upgraded.Accounts[0].Fields.Count == 0
            && Encoding.ASCII.GetString(File.ReadAllBytes(v2path), 0, 8) == "NOXVL004",
                "v2 migration preserves passwords and supplies new field defaults");
    }

    static void BackupTests(string folder, Vault vault, Action<bool, string> check, Action<Action, string> reject)
    {
        var path = vault.FilePath;
        var destination = Path.Combine(folder, "automatic");
        Directory.CreateDirectory(destination);
        DateTime start = new(2026, 1, 1, 12, 0, 0);
        check(AutomaticBackupService.Run(path, destination, 7, start), "Automatic backup creates first encrypted snapshot");
        check(!AutomaticBackupService.Run(path, destination, 7, start.AddHours(1)), "Automatic backups limited to once per local day");
        var owned = AutomaticBackupService.OwnedDirectory(path, destination);
        File.WriteAllText(Path.Combine(owned, "manual.nox"), "synthetic sentinel");
        for (int day = 1; day < 10; day++) AutomaticBackupService.Run(path, destination, 7, start.AddDays(day));
        var backups = Directory.GetFiles(owned, "auto-*.nox");
        check(backups.Length == 7
            && File.Exists(Path.Combine(owned, "manual.nox")), "Retention keeps seven automatic versions and preserves manual files");
        check(Vault.ReadBackup(backups[0], Password).Accounts.Count == 3, "Automatic snapshot can be fully restored");
        check(AutomaticBackupService.LastSuccess(path, destination) == start.AddDays(9),
            "Last successful backup uses completed snapshot date");
        reject(() => AutomaticBackupService.Run(path, Path.Combine(folder, "missing-drive"), 7, start),
            "Unavailable destination produces explicit backup error");
        check(vault.Open(Password).Accounts.Count == 3, "Backup failure leaves active vault intact");
        reject(() => AutomaticBackupService.Run(path, destination, 0, start), "Invalid retention cannot delete backups");
        var expired = vault.Open(Password);
        expired.Accounts.Add(new Account
        {
            Title = "Expired", Category = expired.Categories[0],
            DeletedUtc = DateTime.UtcNow.AddDays(-31)
        });
        vault.Save(expired);
        var beforePurge = File.ReadAllBytes(path);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            reject(() => vault.Open(Password), "Failed expiry persistence rejects unlock");
        check(!vault.Unlocked
            && beforePurge.SequenceEqual(File.ReadAllBytes(path)), "Failed expiry write preserves original file and clears key");
        check(vault.Open(Password).Accounts.Count == 3, "Expiry cleanup recovers after blocked file is released");
    }

    internal static void Benchmark(string outDir, Action<bool, string> check)
    {
        var report = new List<string> { "Synthetic data only. Times are local measurements, not performance guarantees.",
            "accounts,filter_ms,build_and_last_page_ms" };
        foreach (int size in new[] { 100, 1000, 10000 })
        {
            var data = new VaultData();
            for (int i = 0; i < size; i++)
                data.Accounts.Add(new Account
                {
                    Title = "Account " + i.ToString("D5"),
                    Category = data.Categories[i % data.Categories.Count], Password = "Synthetic benchmark secret"
                });
            var watch = Stopwatch.StartNew();
            var results = AccountQueries.Filter(data, null, false, "Account", "Name");
            watch.Stop();
            double filtering = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var window = new MainWindow(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nox"), data);
            window.TestPagedAccounts(size);
            watch.Stop();
            report.Add(FormattableString.Invariant($"{size},{filtering:F2},{watch.Elapsed.TotalMilliseconds:F2}"));
            check(results.Count == size, "Search and bounded account presentation cover " + size + " accounts");
            report.Add(window.MeasureSelection(size));
            window.Close();
        }
        File.WriteAllLines(Path.Combine(outDir, "performance.txt"), report);
    }
}

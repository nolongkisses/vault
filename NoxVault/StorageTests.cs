using System;
using System.IO;
using System.Linq;

namespace NoxVault;

internal static class StorageTests
{
    internal static void Run(string folder, Action<bool, string> check, Action<Action, string> reject)
    {
        var root = Path.Combine(folder, "storage-tests");
        var legacy = Path.Combine(root, "legacy");
        var packaged = Path.Combine(root, "package", "LocalCache", "Local", "NoxVault");
        var target = Path.Combine(root, "profile", ".noxvault");
        Directory.CreateDirectory(legacy); Directory.CreateDirectory(packaged);
        var source = Path.Combine(packaged, "vault.nox");
        using (var vault = new Vault(source)) { vault.Create("Synthetic storage migration!"); }
        var original = File.ReadAllBytes(source);
        File.WriteAllText(Path.Combine(packaged, "settings.json"), "{\"HotkeyF\":9}");
        File.WriteAllBytes(source + ".session", new byte[] { 1, 2, 3 });
        StoragePaths.Initialize(target, new[] { legacy, packaged });
        var migrated = Path.Combine(target, "vault.nox");
        check(File.ReadAllBytes(migrated).SequenceEqual(original) && File.ReadAllBytes(source).SequenceEqual(original),
            "Packaged-only vault is migrated byte-for-byte without modifying original");
        check(File.Exists(Path.Combine(target, "settings.json")) && !File.Exists(migrated + ".session"),
            "Storage migration preserves preferences without copying remembered sessions");
        using (var reopened = new Vault(migrated))
            check(reopened.Open("Synthetic storage migration!").Accounts.Count == 0, "Migrated vault opens with original password");
        using (var other = new Vault(Path.Combine(legacy, "vault.nox"))) { other.Create("Different synthetic vault!"); }
        StoragePaths.Initialize(target, new[] { legacy, packaged });
        check(File.ReadAllBytes(migrated).SequenceEqual(original), "Established canonical vault wins over stale legacy copies");
        var conflictTarget = Path.Combine(root, "conflict");
        reject(() => StoragePaths.Initialize(conflictTarget, new[] { legacy, packaged }), "Different legacy vaults stop migration instead of guessing");
        check(!File.Exists(Path.Combine(conflictTarget, "vault.nox")), "Conflicting migration creates no replacement vault");
        File.Delete(migrated);
        reject(() => StoragePaths.Initialize(target, new[] { packaged }), "Missing established vault never rolls back to legacy automatically");
        using (var missing = new Vault(migrated))
            reject(() => missing.Create("Synthetic replacement forbidden!"), "Persistent marker blocks creation over a missing established vault");
        reject(() => StoragePaths.ProfileRoot(""), "Unavailable user profile cannot become a relative data path");
        reject(() => StoragePaths.ProfileRoot("relative"), "Relative user profile rejected");
        var fresh = Path.Combine(root, "fresh", "vault.nox");
        using (var vault = new Vault(fresh))
        {
            check(!vault.Exists, "Fresh installation can still create a vault");
            vault.Create("Synthetic disappearing vault!"); File.Delete(fresh);
            reject(() => { _ = vault.Exists; }, "Disappearing vault is an access error instead of first-run state");
        }
        var occupied = Path.Combine(root, "occupied.nox"); Directory.CreateDirectory(occupied);
        reject(() => StoragePaths.FilePresent(occupied), "Directory at vault path is not treated as a new installation");
        var protectedFile = Path.Combine(root, "atomic.nox"); File.WriteAllBytes(protectedFile, original);
        reject(() => Vault.AtomicWrite(protectedFile, new byte[] { 1 }, overwrite: false), "Create-only atomic writes reject destination races");
        check(File.ReadAllBytes(protectedFile).SequenceEqual(original), "Rejected create-only write preserves existing encrypted bytes");
        var duplicate = Path.Combine(root, "duplicate"); Directory.CreateDirectory(duplicate);
        File.WriteAllBytes(Path.Combine(duplicate, "vault.nox"), original);
        var duplicateTarget = Path.Combine(root, "duplicate-target");
        StoragePaths.Initialize(duplicateTarget, new[] { packaged, duplicate });
        check(File.ReadAllBytes(Path.Combine(duplicateTarget, "vault.nox")).SequenceEqual(original), "Identical legacy views do not cause a false migration conflict");
        using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
            reject(() => StoragePaths.Initialize(Path.Combine(root, "locked-target"), new[] { packaged }), "Unreadable legacy vault blocks onboarding instead of being ignored");
        var emptyTarget = Path.Combine(root, "empty-target");
        StoragePaths.Initialize(emptyTarget, new[] { Path.Combine(root, "missing-legacy") });
        check(!File.Exists(Path.Combine(emptyTarget, "vault.nox")), "New installation stays empty until explicit creation");

        var gatePath = Path.Combine(root, "late-available.nox");
        var window = new MainWindow(gatePath, designMode: true, startInTray: true);
        try
        {
            check(window.StartupDiagnostics().Contains("Gate=Create"), "Synthetic first-run gate is initially available");
            using (var late = new Vault(gatePath)) { late.Create("Synthetic late available vault!"); }
            window.RefreshGateForTest();
            check(window.StartupDiagnostics().Contains("Gate=Unlock"), "Opening vault refreshes stale first-run gate");
            File.Delete(gatePath); window.RefreshGateForTest();
            check(window.StartupDiagnostics().Contains("Gate=Unavailable"), "Missing known vault shows retry instead of creation");
            File.WriteAllBytes(gatePath, original); window.RefreshGateForTest();
            check(window.StartupDiagnostics().Contains("Gate=Unlock"), "Unavailable vault recovers when storage returns");
        }
        finally { window.Close(); }
    }
}

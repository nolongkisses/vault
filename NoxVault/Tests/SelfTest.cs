using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoxVault;

// Shared by every test module: a throwaway folder, the render output folder, and the two assertion forms.
internal sealed record TestContext(string Folder, string OutDir, Action<bool, string> Check, Action<Action, string> Reject);

// `NoxVault.exe --self-test <report>`: synthetic data only, never the real vault, the real clipboard or real input.
internal static class SelfTest
{
    internal static void Run(string reportPath, bool includeNative = false)
    {
        var folder = Path.Combine(Path.GetTempPath(), "NoxVault-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var outDir = Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? folder;
        Directory.CreateDirectory(outDir);
        var passed = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAILED: " + name);
            passed.Add("PASS: " + name);
        }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException)
            { passed.Add("PASS: " + name); return; }
            throw new Exception("FAILED: " + name);
        }
        var test = new TestContext(folder, outDir, Check, Reject);
        Motion.Instant = true;
        try
        {
            MotionTests(test);
            CryptoMigrationTests.Run(folder, Check, Reject);
            StorageTests.Run(folder, Check, Reject);
            StartupTests(test);
            FeatureTests.Run(folder, Check, Reject);
            AutofillTests.Run(folder, Check, Reject);
            var (restored, backup) = VaultTests(test);
            ClipboardAndGeneratorTests(test);
            SessionTests(test);
            WindowsAccessTests(test);
            RotationTests(test, restored, backup);
            PreferenceTests(test);
            FeatureTests.Benchmark(outDir, Check);
            BrandExport.Save(outDir);
            Renders.Run(test, restored, includeNative);
            File.WriteAllText(reportPath, string.Join(Environment.NewLine, passed));
        }
        finally { Directory.Delete(folder, true); }
    }

    static void StartupTests(TestContext test)
    {
        var testLink = Path.Combine(test.Folder, "vault test.lnk");
        StartupRegistration.WriteLink(testLink, "--tray");
        test.Check(StartupRegistration.Matches(testLink, "--tray"), "Startup shortcut resolves to this executable with tray arguments");
        test.Check(!StartupRegistration.Matches(testLink, ""), "Startup shortcut validation rejects missing tray arguments");
        StartupRegistration.WriteLink(testLink, "");
        test.Check(StartupRegistration.Matches(testLink, ""), "Start menu shortcut opens the same executable normally");
        var startupVaultPath = Path.Combine(test.Folder, "startup.nox");
        using (var startupVault = new Vault(startupVaultPath))
        {
            startupVault.Create("Synthetic startup master phrase!");
            new RememberedLogin(startupVaultPath).Save(startupVault, DateTime.Now);
        }
        var backgroundWindow = new MainWindow(startupVaultPath, startInTray: true);
        try
        {
            backgroundWindow.StartInTray();
            var diagnostics = backgroundWindow.StartupDiagnostics();
            test.Check(diagnostics.Contains("Visible=False") && diagnostics.Contains("Locked=True")
                && diagnostics.Contains("TrayVisible=True"),
                "Autostart initializes tray without showing or unlocking a remembered vault");
            test.Check(Preferences.AutoStartCommand() == "\"" + Path.Combine(AppContext.BaseDirectory, "NoxVault.exe") + "\" --tray",
                "Autostart quotes apphost path including spaces and passes tray argument");
        }
        finally { backgroundWindow.QuitForTest(); }
    }

    static (VaultData Restored, string Backup) VaultTests(TestContext test)
    {
        string file = Path.Combine(test.Folder, "vault.nox"), backup = Path.Combine(test.Folder, "backup.nox");
        using var vault = new Vault(file);
        test.Reject(() => vault.Create("short"), "Weak master password rejected");
        var data = vault.Create("Test master phrase 2026!");
        data.Accounts.Add(new Account
        {
            Title = "Main Account", Category = "Valorant", Email = "demo@example.invalid", Username = "Nightfall#EUW",
            Password = "Synthetic-Secret-123!", Notes = "Nur synthetische Testdaten.", Favorite = true,
        });
        data.Accounts.Add(new Account
        {
            Title = "Ranked · EU West", Category = "League of Legends", Username = "Moonlight", Email = "league@example.invalid",
            Password = "Another-Test-Password",
        });
        vault.Save(data);
        var firstBytes = File.ReadAllBytes(file);
        test.Check(!Encoding.UTF8.GetString(firstBytes).Contains("Synthetic-Secret")
            && !Encoding.UTF8.GetString(firstBytes).Contains("example.invalid"),
            "No credentials in persisted plaintext");
        vault.Save(data);
        test.Check(!firstBytes.SequenceEqual(File.ReadAllBytes(file)), "Unique nonce on each save");
        vault.Backup(backup);
        test.Reject(() => vault.Backup(file), "Backup cannot overwrite active vault");
        vault.Dispose();
        test.Check(!vault.Unlocked, "Lock releases key");
        test.Reject(() => vault.Save(data), "Locked vault rejects writes");
        test.Reject(() => vault.Open("Wrong password 123"), "Wrong password rejected");
        test.Check(!vault.Unlocked, "Failed unlock leaves vault locked");
        data = vault.Open("Test master phrase 2026!");
        test.Check(data.Accounts.Count == 2 && data.Accounts[0].Password == "Synthetic-Secret-123!", "Encrypted round trip");
        data.Categories.Add("E-Mail");
        data.Accounts[0].Category = "E-Mail";
        data.Accounts[0].Title = "Edited";
        vault.Save(data);
        test.Check(vault.Open("Test master phrase 2026!").Accounts[0].Category == "E-Mail", "Account edits and category survive reopen");
        data.Accounts.RemoveAt(0);
        vault.Save(data);
        test.Check(vault.Open("Test master phrase 2026!").Accounts.Count == 1, "Deletion persists");
        var restored = Vault.ReadBackup(backup, "Test master phrase 2026!");
        vault.Save(restored);
        test.Check(vault.Open("Test master phrase 2026!").Accounts.Count == 2, "Backup restoration");
        TamperTests(test, vault, restored, backup);
        return (restored, backup);
    }

    static void TamperTests(TestContext test, Vault vault, VaultData restored, string backup)
    {
        using var other = new Vault(Path.Combine(test.Folder, "other.nox"));
        other.Create("Different master phrase!");
        other.Save(restored);
        test.Check(other.Open("Different master phrase!").Accounts.Count == 2, "Restoration re-encrypts using destination key");
        test.Reject(() => other.Open("Test master phrase 2026!"), "Source password cannot unlock re-encrypted destination");
        foreach (var index in new[] { 0, 9, 42, 55, 75 })
        {
            var corrupt = File.ReadAllBytes(backup);
            corrupt[index] ^= 0x40;
            var corruptPath = Path.Combine(test.Folder, "corrupt.nox");
            File.WriteAllBytes(corruptPath, corrupt);
            test.Reject(() => Vault.ReadBackup(corruptPath, "Test master phrase 2026!"), "Tamper detection at byte " + index);
        }
        var invalid = restored.Clone();
        invalid.Categories.Clear();
        var before = File.ReadAllBytes(vault.FilePath);
        test.Reject(() => vault.Save(invalid), "Invalid data rejected before write");
        test.Check(before.SequenceEqual(File.ReadAllBytes(vault.FilePath)), "Failed validation preserves original file");
        test.Check(!Directory.GetFiles(test.Folder, "*.tmp").Any(), "No temporary files left behind");
    }

    static void ClipboardAndGeneratorTests(TestContext test)
    {
        var passwords = Enumerable.Range(0, 100).Select(_ => new GeneratorOptions().Generate()).ToArray();
        test.Check(passwords.All(p => p.Length == 24)
            && passwords.Distinct().Count() == 100, "Password generator produces distinct 24-character values");
        uint seq = 0;
        string clip = "";
        int clears = 0;
        using var guard = new ClipboardGuard(v => { clip = v; seq++; }, () => seq, () => { clip = ""; seq++; clears++; });
        guard.Copy("synthetic");
        guard.Clear();
        test.Check(clip == "" && clears == 1, "Clipboard guard clears owned content");
        guard.Copy("synthetic");
        clip = "new user content";
        seq++;
        guard.Clear();
        test.Check(clip == "new user content" && clears == 1, "Clipboard guard preserves newer content");
        guard.Copy("first");
        guard.Copy("second");
        guard.Clear();
        test.Check(clip == "" && clears == 2, "Repeated copy tracks latest content");
    }

    static void MotionTests(TestContext test)
    {
        foreach (var spring in new[] { Motion.Glide, Motion.Settle })
        {
            var start = spring.State(40, 0, 0);
            double settle = spring.SettleTime(40, 0, 0.08);
            var end = spring.State(40, 0, settle);
            test.Check(start.X == 40 && start.V == 0 && Math.Abs(end.X) < 0.08 && settle > 0.1 && settle < 1.5,
                "Spring starts exactly at its offset and settles within the visible precision");
            var middle = spring.State(40, 0, 0.1);
            var retarget = spring.State(middle.X, middle.V, 0.05);
            var direct = spring.State(40, 0, 0.15);
            test.Check(Math.Abs(retarget.X - direct.X) < 1e-9 && Math.Abs(retarget.V - direct.V) < 1e-9,
                "Spring retarget from sampled position and velocity continues the same path");
        }
    }

    static void PreferenceTests(TestContext test)
    {
        var legacyPrefs = JsonSerializer.Deserialize<Preferences>("{\"HotkeyF\":10}")!;
        test.Check(legacyPrefs.EffectiveKey == 0x79 && legacyPrefs.HotkeyModifiers == 8, "Legacy shortcut preferences migrate correctly");
        var customPrefs = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences
        {
            HotkeyKey = 0x4E,
            HotkeyModifiers = 3
        }))!;
        test.Check(customPrefs.EffectiveKey == 0x4E && customPrefs.HotkeyModifiers == 3, "Custom shortcut preferences round trip");
        var themed = JsonSerializer.Deserialize<Preferences>("{\"Theme\":\"Neon\"}")!;
        themed.Normalize();
        test.Check(themed.Theme == "System", "Unknown theme preference falls back to following Windows");
    }

    static void SessionTests(TestContext test)
    {
        using var sessionVault = new Vault(Path.Combine(test.Folder, "session.nox"));
        var session = new RememberedLogin(sessionVault.FilePath);
        var noon = new DateTime(2026, 9, 11, 12, 0, 0);
        test.Check(session.Restore(sessionVault, noon, out _) == null, "Remember login is off by default");
        sessionVault.Create("Session test master phrase!");
        test.Check(session.Save(sessionVault, noon) == noon.Date.AddDays(1), "Remembered login expires at next local midnight");
        sessionVault.Dispose();
        test.Check(session.Restore(sessionVault, noon.AddHours(11), out _) != null, "Protected session reopens vault without password");
        test.Check(session.Restore(sessionVault, noon.Date.AddDays(1), out _) == null && !File.Exists(sessionVault.FilePath + ".session"),
            "Midnight rejects and removes remembered login");
        sessionVault.Open("Session test master phrase!");
        session.Save(sessionVault, noon);
        test.Check(session.Restore(sessionVault, noon.AddMinutes(-1), out _) == null, "Clock rollback rejects remembered login");
        sessionVault.Open("Session test master phrase!");
        session.Save(sessionVault, noon);
        var protectedSession = File.ReadAllBytes(sessionVault.FilePath + ".session");
        protectedSession[^1] ^= 1;
        File.WriteAllBytes(sessionVault.FilePath + ".session", protectedSession);
        test.Check(session.Restore(sessionVault, noon, out _) == null, "Tampered remembered login is rejected");
        sessionVault.Open("Session test master phrase!");
        session.Save(sessionVault, noon);
        session.Clear();
        test.Check(session.Restore(sessionVault, noon, out _) == null, "Explicit session removal prevents automatic unlock");
        sessionVault.Open("Session test master phrase!");
        session.Save(sessionVault, noon);
        sessionVault.ChangePassword("Session test master phrase!", "Replacement session phrase!");
        test.Check(session.Restore(sessionVault, noon, out _) == null
            && !sessionVault.Unlocked, "Password rotation invalidates previous session key");
    }

    static void WindowsAccessTests(TestContext test)
    {
        var windowsPath = Path.Combine(test.Folder, "windows.nox");
        using (var windowsVault = new Vault(windowsPath))
        {
            var windowsData = windowsVault.CreateForWindows();
            windowsData.Accounts.Add(new Account
            {
                Title = "Windows-only account", Category = windowsData.Categories[0],
                Password = "Synthetic only"
            });
            windowsVault.Save(windowsData);
            var windowsBackup = Path.Combine(test.Folder, "windows-backup.nox");
            windowsVault.Backup(windowsBackup);
            test.Check(windowsVault.ReadBackupForWindows(windowsBackup).Accounts.Single().Title == "Windows-only account",
                "Windows-only backups restore without a master password");
            windowsVault.Dispose();
            test.Check(new RememberedLogin(windowsPath, permanent: true).Restore(windowsVault, DateTime.Now, out _)?.Accounts.Count == 1,
                "New Windows-only vault creates and reopens without a master password");
            var originalWindowsVault = File.ReadAllBytes(windowsPath);
            test.Reject(() => windowsVault.CreateForWindows(), "Windows-only creation cannot replace an existing vault");
            test.Check(originalWindowsVault.SequenceEqual(File.ReadAllBytes(windowsPath)),
                "Rejected Windows-only creation preserves existing accounts");
        }
        var newWindowsWindow = new MainWindow(Path.Combine(test.Folder, "new-windows.nox"));
        test.Check(newWindowsWindow.StartupDiagnostics().Contains("Locked=False"),
            "First app start opens a new Windows-only vault directly without setup");
        newWindowsWindow.TestDirectQuickFill();
        QuickFillAccessTests(test);
    }

    static void QuickFillAccessTests(TestContext test)
    {
        var quickPath = Path.Combine(test.Folder, "quickfill.nox");
        using (var quickVault = new Vault(quickPath))
        {
            var access = new RememberedLogin(quickPath, permanent: true);
            var noon = DateTime.Now;
            test.Check(access.Restore(quickVault, noon, out _) == null, "Direct quick fill requires one initial authenticated unlock");
            quickVault.Create("Synthetic quick fill master phrase!");
            test.Check(access.Save(quickVault, noon) == DateTime.MaxValue, "Direct quick-fill access has no midnight expiry");
            quickVault.Dispose();
            test.Check(access.Restore(quickVault, noon.AddDays(30), out _) != null,
                "Direct quick fill restores after restart and across midnight");
            quickVault.ChangePassword("Synthetic quick fill master phrase!", "Replacement quick fill master phrase!");
            test.Check(access.Restore(quickVault, noon, out _) == null
                && !quickVault.Unlocked, "Password rotation rejects stale direct quick-fill key");
            quickVault.Open("Replacement quick fill master phrase!");
            access.Save(quickVault, noon);
            var protectedAccess = File.ReadAllBytes(quickPath + ".quickfill");
            protectedAccess[^1] ^= 1;
            File.WriteAllBytes(quickPath + ".quickfill", protectedAccess);
            test.Check(access.Restore(quickVault, noon, out _) == null, "Tampered direct quick-fill access is rejected");
            quickVault.Open("Replacement quick fill master phrase!");
            access.Save(quickVault, noon);
        }
        new MainWindow(quickPath, startInTray: true).TestDirectQuickFill();
        test.Check(true, "Main window and Win+F8 restore Windows access without password after locking and restart");
        test.Check(true, "Quick fill resumes the picker automatically after initial Windows access setup");
        var automaticWindow = new MainWindow(quickPath);
        test.Check(automaticWindow.StartupDiagnostics().Contains("Locked=False"),
            "Normal app startup restores Windows access without a password gate");
        automaticWindow.TestDirectQuickFill();
    }

    static void RotationTests(TestContext test, VaultData restored, string backup)
    {
        using var rotation = new Vault(Path.Combine(test.Folder, "rotation.nox"));
        rotation.Create("Original master phrase!");
        rotation.Save(restored);
        var original = File.ReadAllBytes(rotation.FilePath);
        test.Reject(() => rotation.ChangePassword("Wrong current password!", "Replacement master phrase!"),
            "Password change rejects wrong current password");
        test.Reject(() => rotation.ChangePassword("Original master phrase!", "short"), "Password change rejects weak replacement");
        test.Check(rotation.Unlocked && original.SequenceEqual(File.ReadAllBytes(rotation.FilePath)),
            "Rejected password changes preserve file and unlocked state");
        using (var held = new FileStream(rotation.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            test.Reject(() => rotation.ChangePassword("Original master phrase!", "Replacement master phrase!"),
                "Password change survives blocked file replacement");
        test.Check(original.SequenceEqual(File.ReadAllBytes(rotation.FilePath)), "Failed password write preserves original file");
        rotation.Save(restored);
        test.Check(Vault.ReadBackup(rotation.FilePath, "Original master phrase!").Accounts.Count == restored.Accounts.Count,
            "Failed password write preserves active encryption key");
        rotation.ChangePassword("Original master phrase!", "Replacement master phrase!");
        test.Check(!original.AsSpan(8, 32).SequenceEqual(File.ReadAllBytes(rotation.FilePath).AsSpan(8, 32)),
            "Password change generates fresh salt");
        rotation.Save(restored);
        rotation.Dispose();
        test.Reject(() => rotation.Open("Original master phrase!"), "Previous password no longer unlocks changed vault");
        test.Check(rotation.Open("Replacement master phrase!").Accounts.Count == restored.Accounts.Count,
            "New password preserves all accounts and survives subsequent save");
        test.Check(Vault.ReadBackup(backup, "Test master phrase 2026!").Accounts.Count == 2,
            "Existing backup remains readable with its original password");
    }
}

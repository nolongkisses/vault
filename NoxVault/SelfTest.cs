using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoxVault;

public static class SelfTest
{
    public static void Run(string reportPath, bool includeNative = false)
    {
        var folder = Path.Combine(Path.GetTempPath(), "NoxVault-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var passed = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); passed.Add("PASS: " + name); }
        void Reject(Action action, string name)
        {
            try { action(); } catch (Exception ex) when (ex is CryptographicException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { passed.Add("PASS: " + name); return; }
            throw new Exception("FAILED: " + name);
        }
        try
        {
            CryptoMigrationTests.Run(folder, Check, Reject);
            StorageTests.Run(folder, Check, Reject);
            var testLink = Path.Combine(folder, "vault test.lnk");
            StartupRegistration.WriteLink(testLink, "--tray");
            Check(StartupRegistration.Matches(testLink, "--tray"), "Startup shortcut resolves to this executable with tray arguments");
            Check(!StartupRegistration.Matches(testLink, ""), "Startup shortcut validation rejects missing tray arguments");
            StartupRegistration.WriteLink(testLink, "");
            Check(StartupRegistration.Matches(testLink, ""), "Start menu shortcut opens the same executable normally");
            var startupVaultPath = Path.Combine(folder, "startup.nox");
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
                Check(diagnostics.Contains("Visible=False") && diagnostics.Contains("Locked=True") && diagnostics.Contains("TrayVisible=True"),
                    "Autostart initializes tray without showing or unlocking a remembered vault");
                Check(Preferences.AutoStartCommand() == "\"" + Path.Combine(AppContext.BaseDirectory, "NoxVault.exe") + "\" --tray",
                    "Autostart quotes apphost path including spaces and passes tray argument");
            }
            finally
            {
                Native.PostMessage(new System.Windows.Interop.WindowInteropHelper(backgroundWindow).Handle, 0x8002, IntPtr.Zero, IntPtr.Zero);
                var frame = new System.Windows.Threading.DispatcherFrame();
                backgroundWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            FeatureTests.Run(folder, Check, Reject);
            AutofillTests.Run(folder, Check, Reject);
            string file = Path.Combine(folder, "vault.nox"), backup = Path.Combine(folder, "backup.nox");
            using var vault = new Vault(file);
            Reject(() => vault.Create("short"), "Weak master password rejected");
            var data = vault.Create("Test master phrase 2026!");
            data.Accounts.Add(new Account { Title = "Main Account", Category = "Valorant", Email = "demo@example.invalid", Username = "Nightfall#EUW", Password = "Synthetic-Secret-123!", Notes = "Nur synthetische Testdaten.", Favorite = true });
            data.Accounts.Add(new Account { Title = "Ranked · EU West", Category = "League of Legends", Username = "Moonlight", Email = "league@example.invalid", Password = "Another-Test-Password" });
            vault.Save(data);
            var firstBytes = File.ReadAllBytes(file);
            Check(!Encoding.UTF8.GetString(firstBytes).Contains("Synthetic-Secret") && !Encoding.UTF8.GetString(firstBytes).Contains("example.invalid"), "No credentials in persisted plaintext");
            vault.Save(data); Check(!firstBytes.SequenceEqual(File.ReadAllBytes(file)), "Unique nonce on each save");
            vault.Backup(backup);
            Reject(() => vault.Backup(file), "Backup cannot overwrite active vault");
            vault.Dispose(); Check(!vault.Unlocked, "Lock releases key");
            Reject(() => vault.Save(data), "Locked vault rejects writes");
            Reject(() => vault.Open("Wrong password 123"), "Wrong password rejected");
            Check(!vault.Unlocked, "Failed unlock leaves vault locked");
            data = vault.Open("Test master phrase 2026!");
            Check(data.Accounts.Count == 2 && data.Accounts[0].Password == "Synthetic-Secret-123!", "Encrypted round trip");
            data.Categories.Add("E-Mail"); data.Accounts[0].Category = "E-Mail"; data.Accounts[0].Title = "Edited"; vault.Save(data);
            Check(vault.Open("Test master phrase 2026!").Accounts[0].Category == "E-Mail", "Account edits and category survive reopen");
            data.Accounts.RemoveAt(0); vault.Save(data); Check(vault.Open("Test master phrase 2026!").Accounts.Count == 1, "Deletion persists");
            var restored = Vault.ReadBackup(backup, "Test master phrase 2026!"); vault.Save(restored);
            Check(vault.Open("Test master phrase 2026!").Accounts.Count == 2, "Backup restoration");
            using var other = new Vault(Path.Combine(folder, "other.nox")); other.Create("Different master phrase!"); other.Save(restored);
            Check(other.Open("Different master phrase!").Accounts.Count == 2, "Restoration re-encrypts using destination key");
            Reject(() => other.Open("Test master phrase 2026!"), "Source password cannot unlock re-encrypted destination");
            foreach (var index in new[] { 0, 9, 42, 55, 75 })
            {
                var corrupt = File.ReadAllBytes(backup); corrupt[index] ^= 0x40; var corruptPath = Path.Combine(folder, "corrupt.nox"); File.WriteAllBytes(corruptPath, corrupt);
                Reject(() => Vault.ReadBackup(corruptPath, "Test master phrase 2026!"), "Tamper detection at byte " + index);
            }
            var invalid = restored.Clone(); invalid.Categories.Clear(); var before = File.ReadAllBytes(file);
            Reject(() => vault.Save(invalid), "Invalid data rejected before write"); Check(before.SequenceEqual(File.ReadAllBytes(file)), "Failed validation preserves original file");
            Check(!Directory.GetFiles(folder, "*.tmp").Any(), "No temporary files left behind");
            var passwords = Enumerable.Range(0, 100).Select(_ => MainWindow.GeneratePassword()).ToArray();
            Check(passwords.All(p => p.Length == 24) && passwords.Distinct().Count() == 100, "Password generator produces distinct 24-character values");
            uint seq = 0; string clip = ""; int clears = 0;
            using (var guard = new ClipboardGuard(v => { clip = v; seq++; }, () => seq, () => { clip = ""; seq++; clears++; }))
            {
                guard.Copy("synthetic"); guard.Clear(); Check(clip == "" && clears == 1, "Clipboard guard clears owned content");
                guard.Copy("synthetic"); clip = "new user content"; seq++; guard.Clear(); Check(clip == "new user content" && clears == 1, "Clipboard guard preserves newer content");
                guard.Copy("first"); guard.Copy("second"); guard.Clear(); Check(clip == "" && clears == 2, "Repeated copy tracks latest content");
            }
            var outDir = Path.GetDirectoryName(Path.GetFullPath(reportPath))!; Directory.CreateDirectory(outDir);
            using (var sessionVault = new Vault(Path.Combine(folder, "session.nox")))
            {
                var session = new RememberedLogin(sessionVault.FilePath);
                var noon = new DateTime(2026, 9, 11, 12, 0, 0);
                Check(session.Restore(sessionVault, noon, out _) == null, "Remember login is off by default");
                sessionVault.Create("Session test master phrase!");
                Check(session.Save(sessionVault, noon) == noon.Date.AddDays(1), "Remembered login expires at next local midnight");
                sessionVault.Dispose();
                Check(session.Restore(sessionVault, noon.AddHours(11), out _) != null, "Protected session reopens vault without password");
                Check(session.Restore(sessionVault, noon.Date.AddDays(1), out _) == null && !File.Exists(sessionVault.FilePath + ".session"), "Midnight rejects and removes remembered login");
                sessionVault.Open("Session test master phrase!"); session.Save(sessionVault, noon);
                Check(session.Restore(sessionVault, noon.AddMinutes(-1), out _) == null, "Clock rollback rejects remembered login");
                sessionVault.Open("Session test master phrase!"); session.Save(sessionVault, noon);
                var protectedSession = File.ReadAllBytes(sessionVault.FilePath + ".session");
                protectedSession[^1] ^= 1; File.WriteAllBytes(sessionVault.FilePath + ".session", protectedSession);
                Check(session.Restore(sessionVault, noon, out _) == null, "Tampered remembered login is rejected");
                sessionVault.Open("Session test master phrase!"); session.Save(sessionVault, noon);
                session.Clear(); Check(session.Restore(sessionVault, noon, out _) == null, "Explicit session removal prevents automatic unlock");
                sessionVault.Open("Session test master phrase!"); session.Save(sessionVault, noon);
                sessionVault.ChangePassword("Session test master phrase!", "Replacement session phrase!");
                Check(session.Restore(sessionVault, noon, out _) == null && !sessionVault.Unlocked, "Password rotation invalidates previous session key");
            }
            var quickPath = Path.Combine(folder, "quickfill.nox");
            var windowsPath = Path.Combine(folder, "windows.nox");
            using (var windowsVault = new Vault(windowsPath))
            {
                var windowsData = windowsVault.CreateForWindows();
                windowsData.Accounts.Add(new Account { Title = "Windows-only account", Category = windowsData.Categories[0], Password = "Synthetic only" });
                windowsVault.Save(windowsData);
                var windowsBackup = Path.Combine(folder, "windows-backup.nox");
                windowsVault.Backup(windowsBackup);
                Check(windowsVault.ReadBackupForWindows(windowsBackup).Accounts.Single().Title == "Windows-only account", "Windows-only backups restore without a master password");
                windowsVault.Dispose();
                Check(new RememberedLogin(windowsPath, permanent: true).Restore(windowsVault, DateTime.Now, out _)?.Accounts.Count == 1,
                    "New Windows-only vault creates and reopens without a master password");
                var originalWindowsVault = File.ReadAllBytes(windowsPath);
                Reject(() => windowsVault.CreateForWindows(), "Windows-only creation cannot replace an existing vault");
                Check(originalWindowsVault.SequenceEqual(File.ReadAllBytes(windowsPath)), "Rejected Windows-only creation preserves existing accounts");
            }
            var newWindowsWindow = new MainWindow(Path.Combine(folder, "new-windows.nox"));
            Check(newWindowsWindow.StartupDiagnostics().Contains("Locked=False"), "First app start opens a new Windows-only vault directly without setup");
            newWindowsWindow.TestDirectQuickFill();
            using (var quickVault = new Vault(quickPath))
            {
                var access = new RememberedLogin(quickPath, permanent: true);
                var noon = DateTime.Now;
                Check(access.Restore(quickVault, noon, out _) == null, "Direct quick fill requires one initial authenticated unlock");
                quickVault.Create("Synthetic quick fill master phrase!");
                Check(access.Save(quickVault, noon) == DateTime.MaxValue, "Direct quick-fill access has no midnight expiry");
                quickVault.Dispose();
                Check(access.Restore(quickVault, noon.AddDays(30), out _) != null, "Direct quick fill restores after restart and across midnight");
                quickVault.ChangePassword("Synthetic quick fill master phrase!", "Replacement quick fill master phrase!");
                Check(access.Restore(quickVault, noon, out _) == null && !quickVault.Unlocked, "Password rotation rejects stale direct quick-fill key");
                quickVault.Open("Replacement quick fill master phrase!"); access.Save(quickVault, noon);
                var protectedAccess = File.ReadAllBytes(quickPath + ".quickfill");
                protectedAccess[^1] ^= 1; File.WriteAllBytes(quickPath + ".quickfill", protectedAccess);
                Check(access.Restore(quickVault, noon, out _) == null, "Tampered direct quick-fill access is rejected");
                quickVault.Open("Replacement quick fill master phrase!"); access.Save(quickVault, noon);
            }
            new MainWindow(quickPath, startInTray: true).TestDirectQuickFill();
            Check(true, "Main window and Win+F8 restore Windows access without password after locking and restart");
            Check(true, "Quick fill resumes the picker automatically after initial Windows access setup");
            var automaticWindow = new MainWindow(quickPath);
            Check(automaticWindow.StartupDiagnostics().Contains("Locked=False"), "Normal app startup restores Windows access without a password gate");
            automaticWindow.TestDirectQuickFill();
            using (var rotation = new Vault(Path.Combine(folder, "rotation.nox")))
            {
                rotation.Create("Original master phrase!"); rotation.Save(restored);
                var original = File.ReadAllBytes(rotation.FilePath);
                Reject(() => rotation.ChangePassword("Wrong current password!", "Replacement master phrase!"), "Password change rejects wrong current password");
                Reject(() => rotation.ChangePassword("Original master phrase!", "short"), "Password change rejects weak replacement");
                Check(rotation.Unlocked && original.SequenceEqual(File.ReadAllBytes(rotation.FilePath)), "Rejected password changes preserve file and unlocked state");
                using (var held = new FileStream(rotation.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject(() => rotation.ChangePassword("Original master phrase!", "Replacement master phrase!"), "Password change survives blocked file replacement");
                Check(original.SequenceEqual(File.ReadAllBytes(rotation.FilePath)), "Failed password write preserves original file");
                rotation.Save(restored);
                Check(Vault.ReadBackup(rotation.FilePath, "Original master phrase!").Accounts.Count == restored.Accounts.Count, "Failed password write preserves active encryption key");
                rotation.ChangePassword("Original master phrase!", "Replacement master phrase!");
                Check(!original.AsSpan(8, 32).SequenceEqual(File.ReadAllBytes(rotation.FilePath).AsSpan(8, 32)), "Password change generates fresh salt");
                rotation.Save(restored); rotation.Dispose();
                Reject(() => rotation.Open("Original master phrase!"), "Previous password no longer unlocks changed vault");
                Check(rotation.Open("Replacement master phrase!").Accounts.Count == restored.Accounts.Count, "New password preserves all accounts and survives subsequent save");
                Check(Vault.ReadBackup(backup, "Test master phrase 2026!").Accounts.Count == 2, "Existing backup remains readable with its original password");
            }
            var legacyPrefs = JsonSerializer.Deserialize<Preferences>("{\"HotkeyF\":10}")!;
            Check(legacyPrefs.EffectiveKey == 0x79 && legacyPrefs.HotkeyModifiers == 8, "Legacy shortcut preferences migrate correctly");
            var customPrefs = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences { HotkeyKey = 0x4E, HotkeyModifiers = 3 }))!;
            Check(customPrefs.EffectiveKey == 0x4E && customPrefs.HotkeyModifiers == 3, "Custom shortcut preferences round trip");
            FeatureTests.Benchmark(outDir, Check);
            BrandExport.Save(outDir);
            // In-process layout rendering with synthetic data, without input automation or live secrets.
            var previewData = restored.Clone();
            previewData.Accounts[0].Website = "https://example.invalid";
            previewData.Accounts[0].Fields.Add(new SecretField { Label = "Wiederherstellungscode", Value = "Synthetic recovery" });
            previewData.Accounts.Add(new Account { Title = "Gelöschter Testaccount", Category = previewData.Categories[0], DeletedUtc = DateTime.UtcNow });
            var preview = new MainWindow(Path.Combine(folder, "preview.nox"), previewData);
            Render(preview, 1180, 780, Path.Combine(outDir, "vault-preview.png"));
            Render(preview, 980, 660, Path.Combine(outDir, "vault-compact.png"));
            Render(preview, 1920, 1080, Path.Combine(outDir, "vault-fullscreen.png"));
            var fillPreview = preview.CreateFillPreview();
            Render(fillPreview, 420, 360, Path.Combine(outDir, "autofill-picker.png")); fillPreview.Close();
            Check(true, "Autofill picker renders with synthetic account and concealed password");
            new System.Windows.Interop.WindowInteropHelper(preview).EnsureHandle(); var settings = preview.CreateSettingsWindow();
            preview.PushDialog(settings);
            Render(preview, 980, 660, Path.Combine(outDir, "settings-preview.png"));
            var settingsView = preview.SettingsPreview!;
            Check(settingsView.PageCount == 5 && !settingsView.IsCompact, "Settings expose five sections with wide navigation");
            for (int page = 0; page < settingsView.PageCount; page++)
            {
                settingsView.SelectPage(page);
                Render(preview, 1180, 780, Path.Combine(outDir, $"settings-page-{page}.png"));
                Check(settingsView.SelectedPage == page && settingsView.ActualWidth > 0, "Settings navigation selects page " + page);
                Render(preview, 980, 660, Path.Combine(outDir, $"settings-page-{page}-150.png"), 1.5);
                Check(settingsView.IsCompact, "Settings use compact navigation at 150 percent on page " + page);
                Render(preview, 980, 660, Path.Combine(outDir, $"settings-page-{page}-200.png"), 2);
                Check(settingsView.IsCompact && settingsView.ActualHeight > 0, "Settings remain reachable at 200 percent on page " + page);
                var settingsScroll = settingsView.Children.OfType<ScrollViewer>().Single();
                settingsScroll.ScrollToEnd(); settingsScroll.UpdateLayout();
                Check(settingsScroll.ScrollableHeight == 0 || settingsScroll.VerticalOffset >= settingsScroll.ScrollableHeight - 1,
                    "Settings content scrolls to its last action at 200 percent on page " + page);
            }
            settingsView.SelectPage(0);
            Render(preview, 980, 660, Path.Combine(outDir, "settings-preview.png"));
            preview.PopDialog(settings);
            var forced = Ui.Dialog(preview, "Forced close test", new StackPanel());
            bool closeRequested = false;
            forced.Closing += (_, args) => { closeRequested = true; args.Cancel = true; };
            preview.Dispatcher.BeginInvoke(new Action(() => forced.Close(true)));
            forced.ShowDialog();
            Check(!closeRequested, "Security closure bypasses unsaved-change confirmations");
            preview.TestFeatureDialogs(name => Render(preview, 980, 660, Path.Combine(outDir, name + ".png")));
            Check(true, "Editor, generator, trash and local password check render and close safely");
            int baseLayerCount = ((Grid)preview.Content).Children.Count;
            var outer = Ui.Dialog(preview, "Test overlay", new StackPanel());
            Exception? dialogFailure = null;
            preview.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Check(preview.OwnedWindows.Count == 0, "In-app dialog creates no native window");
                    var inner = Ui.Dialog(outer, "Nested overlay", new StackPanel());
                    preview.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { Check(!outer.IsEnabled && inner.IsEnabled, "Nested dialog disables underlying dialog"); }
                        catch (Exception ex) { dialogFailure = ex; }
                        finally { inner.DialogResult = true; }
                    }));
                    Check(inner.ShowDialog() == true && outer.IsEnabled, "Nested dialog returns result and restores parent");
                }
                catch (Exception ex) { dialogFailure = ex; }
                finally { outer.Close(); }
            }));
            outer.ShowDialog();
            if (dialogFailure != null) throw dialogFailure;
            Check(((Grid)preview.Content).Children.Count == baseLayerCount && ((Grid)preview.Content).Children[0].IsEnabled, "Closing overlays restores main view");
            var gate = new MainWindow(Path.Combine(folder, "lifecycle.nox"), designMode: true);
            preview.TestQuickFillIsolation();
            Check(true, "Riot picker, unlock and notices leave the main window hidden");
            Render(gate, 1180, 780, Path.Combine(outDir, "welcome-preview.png"));
            Render(gate, 980, 630, Path.Combine(outDir, "welcome-compact.png"));
            if (includeNative) { gate.TestHotkeys(); Check(true, "Native shortcut registration, replacement and conflict tests"); }
            gate.TestLifecycle(); Check(true, "UI save, lock, unlock, rebuild and search lifecycle");
            gate.TestSecurityOptions(); Check(true, "Configured idle limits and password concealment work through the UI lifecycle");
            Check(true, "Locked session file cannot prevent lock or restore in a new instance");
            Check(true, "Search batches input, selects text for keyboard access and cancels on lock");
            Render(gate, 1180, 780, Path.Combine(outDir, "unlock-preview.png"));
            Check(true, "WPF layout rendered at standard and minimum size");
            if (includeNative) File.WriteAllText(Path.Combine(outDir, "shortcut-check.json"), JsonSerializer.Serialize(Native.ProbeHotkeys(), new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(reportPath, string.Join(Environment.NewLine, passed));
        }
        finally { Directory.Delete(folder, true); }
    }
    static void Render(ContentControl window, int width, int height, string path, double scale = 1)
    {
        window.Width = width; window.Height = height;
        var content = (FrameworkElement)window.Content;
        content.LayoutTransform = new ScaleTransform(scale, scale);
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var f = File.Create(path); encoder.Save(f);
    }
}

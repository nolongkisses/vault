using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using static NoxVault.Elements;

namespace NoxVault;

// Manual export and restore, and the daily automatic copy after changes.
internal sealed partial class MainWindow
{
    bool automaticBackupRunning;
    string automaticBackupMessage = "";

    void Backup(TextBlock? feedback = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "vault-Tresor (*.nox)|*.nox",
            FileName = "vault-Sicherung-" + DateTime.Now.ToString("yyyy-MM-dd") + ".nox"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            session.Vault.Backup(dialog.FileName);
            status.Text = "Verschlüsselte Sicherung erstellt.";
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { status.Text = "Sicherung fehlgeschlagen: " + ex.Message; }
        if (feedback != null) feedback.Text = status.Text;
    }

    string? ChooseBackup()
    {
        var dialog = new OpenFileDialog { Filter = "vault-Tresor (*.nox)|*.nox" };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    VaultData? UnlockBackup(string path)
    {
        try { return session.Vault.ReadBackupForWindows(path); }
        catch (CryptographicException) { } // Older password-protected imports may use another key.
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        {
            status.Text = "Sicherung nicht lesbar: " + ex.Message;
            return null;
        }
        return UnlockBackupWithPassword(path);
    }

    VaultData? UnlockBackupWithPassword(string path)
    {
        var form = new StackPanel();
        var password = Secret("Passwort der Sicherung");
        Field(form, "Passwort der Sicherung", password);
        var error = Text("", TextRole.Small, Theme.Fg, bold: true);
        error.Margin = new Thickness(0, Theme.S5, 0, 0);
        form.Children.Add(error);
        var dialog = InAppDialog.Create(this, "Sicherung öffnen", form);
        VaultData? backup = null;
        var check = Primary("Sicherung prüfen", () => { });
        form.Children.Add(InAppDialog.Actions(check));
        dialog.Closing += (_, e) => { if (busy) e.Cancel = true; };
        check.Click += async (_, _) =>
        {
            if (busy) return;
            busy = true;
            check.IsEnabled = false;
            error.Text = "Sicherung wird geprüft …";
            string secret = password.Password;
            try { backup = await Task.Run(() => Vault.ReadBackup(path, secret)); }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or IOException
                or UnauthorizedAccessException or System.Text.Json.JsonException)
            { error.Text = "Passwort falsch oder Sicherung nicht lesbar."; }
            finally { secret = ""; busy = false; check.IsEnabled = true; }
            if (lockPending) { backup = null; lockPending = false; Lock(); return; }
            if (backup != null) dialog.DialogResult = true;
        };
        dialog.ShowDialog();
        password.Clear();
        return backup;
    }

    void Restore(TextBlock? feedback = null)
    {
        var path = ChooseBackup();
        if (path == null || Data == null) return;
        var backup = UnlockBackup(path);
        if (backup == null) return;
        int active = backup.Accounts.Count(a => a.DeletedUtc == null), trashed = backup.Accounts.Count(a => a.DeletedUtc != null);
        if (!InAppDialog.Confirm(this, "Tresor ersetzen?",
            $"Die Sicherung enthält {active} aktive Accounts und {trashed} Einträge im Papierkorb. "
            + $"Dateidatum: {File.GetLastWriteTime(path):dd.MM.yyyy HH:mm}. Die aktuellen Einträge werden ersetzt; zuvor wird eine "
            + "verschlüsselte Sicherung im Tresorordner erstellt. Dein Windows-Zugriff bleibt erhalten.")) return;
        try
        {
            session.Vault.Backup(Path.Combine(Preferences.Root,
                "vor-wiederherstellung-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".nox"));
            if (Mutate(d => { d.Categories = backup.Categories; d.CategoryColors = backup.CategoryColors; d.Accounts = backup.Accounts; }))
                SelectCategory(null, false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { status.Text = "Wiederherstellung abgebrochen: " + ex.Message; }
        if (feedback != null) feedback.Text = status.Text;
    }

    async Task QueueAutomaticBackup()
    {
        if (preview || !prefs.AutomaticBackups || automaticBackupRunning || !session.Vault.Exists) return;
        automaticBackupRunning = true;
        string folder = prefs.BackupDirectory, source = session.Vault.FilePath;
        int retain = prefs.BackupRetention;
        try
        {
            bool created = await Task.Run(() => AutomaticBackupService.Run(source, folder, retain, DateTime.Now));
            if (created) automaticBackupMessage = "Automatische Sicherung erstellt.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            automaticBackupMessage = "Tresor gespeichert, automatische Sicherung fehlgeschlagen: " + ex.Message;
            if (Data != null) status.Text = automaticBackupMessage;
        }
        finally
        {
            automaticBackupRunning = false;
            if (lockPending && !busy) { lockPending = false; Lock(); }
        }
    }
}

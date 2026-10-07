using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// Automatic daily copies, manual export and restore, and the trash.
internal sealed partial class MainWindow
{
    void BackupsPage(SettingsView view)
    {
        var backups = view.AddPage("Sicherungen", "Eine verschlüsselte Kopie für den Fall der Fälle.");
        AutomaticBackupSection(view, backups);
        var exportSection = SettingsView.Section(backups, "Sicherung erstellen",
            "Speichere eine verschlüsselte Kopie als .nox-Datei an einem Ort deiner Wahl.");
        TextBlock? exportFeedback = null;
        var export = Button("Sicherung erstellen …", () => { if (!preview) Backup(exportFeedback); });
        export.HorizontalAlignment = HorizontalAlignment.Left;
        exportSection.Children.Add(export);
        exportFeedback = SettingsView.Feedback(exportSection);
        var restoreSection = SettingsView.Section(backups, "Aus Sicherung wiederherstellen",
            "Ersetzt deine aktuellen Accounts. Vorher erstellt vault eine Sicherung des bestehenden Tresors. "
            + "Dein Windows-Zugriff bleibt erhalten.");
        TextBlock? restoreFeedback = null;
        var restore = Button("Wiederherstellen …", () => { if (!preview) Restore(restoreFeedback); });
        restore.HorizontalAlignment = HorizontalAlignment.Left;
        restoreSection.Children.Add(restore);
        restoreFeedback = SettingsView.Feedback(restoreSection);
        var trash = SettingsView.Section(backups, "Papierkorb",
            "Gelöschte Accounts 30 Tage lang wiederherstellen oder endgültig entfernen.");
        var trashButton = Button("Papierkorb öffnen …", ShowTrash);
        trashButton.HorizontalAlignment = HorizontalAlignment.Left;
        trash.Children.Add(trashButton);
    }

    void AutomaticBackupSection(SettingsView view, StackPanel page)
    {
        var automatic = SettingsView.Section(page, "Automatisch sichern",
            "Nach Änderungen höchstens einmal täglich, während vault läuft. Manuelle Sicherungen bleiben erhalten.");
        var path = Text(prefs.BackupDirectory.Length == 0
            ? "Wähle einen Zielordner, um automatische Sicherungen zu aktivieren." : prefs.BackupDirectory,
            TextRole.Small, Theme.Faint);
        path.Margin = new Thickness(0, Theme.S5, 0, 0);
        automatic.Children.Add(path);
        var state = Text("", TextRole.Small, Theme.Sub);
        state.Margin = new Thickness(0, Theme.S4, 0, Theme.S4);
        automatic.Children.Add(state);
        var toggle = Button("", () => { });
        void Refresh() => RefreshBackupState(toggle, state);
        Refresh();
        var choose = Button("Ordner wählen …", () => ChooseBackupFolder(path, state, Refresh));
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, Theme.S5),
            Children = { choose, toggle }
        };
        toggle.Margin = new Thickness(Theme.S4, 0, 0, 0);
        automatic.Children.Add(controls);
        toggle.Click += (_, _) =>
        {
            bool old = prefs.AutomaticBackups;
            prefs.AutomaticBackups = !old;
            try { if (!preview) prefs.Save(); Refresh(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { prefs.AutomaticBackups = old; state.Text = "Nicht gespeichert: " + ex.Message; }
        };
        automatic.Children.Add(Text("Anzahl aufbewahrter Sicherungen", TextRole.Small, Theme.Sub));
        AddTimeSetting(automatic, "Automatische Sicherungen behalten", new[] { 7, 14, 30 },
            (() => prefs.BackupRetention, v => prefs.BackupRetention = v), "Versionen");
        var previous = view.PageChanged;
        view.PageChanged = () => { previous?.Invoke(); Refresh(); };
    }

    void RefreshBackupState(Button toggle, TextBlock state)
    {
        toggle.Content = prefs.AutomaticBackups ? "Pausieren" : "Aktivieren";
        AutomationProperties.SetName(toggle, "Automatische Sicherungen " + (prefs.AutomaticBackups ? "pausieren" : "aktivieren"));
        toggle.IsEnabled = prefs.AutomaticBackups || prefs.BackupDirectory.Length > 0;
        try
        {
            var last = AutomaticBackupService.LastSuccess(session.Vault.FilePath, prefs.BackupDirectory);
            state.Text = (prefs.AutomaticBackups ? "Aktiv" : "Ausgeschaltet") + " · "
                + (last.HasValue ? "Letzte Sicherung: " + last.Value.ToString("dd.MM.yyyy HH:mm") : "Noch keine automatische Sicherung")
                + (automaticBackupMessage.Length > 0 ? "\n" + automaticBackupMessage : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { state.Text = "Sicherungsordner nicht erreichbar."; }
    }

    void ChooseBackupFolder(TextBlock path, TextBlock state, Action refresh)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner für verschlüsselte Sicherungen" };
        if (picker.ShowDialog(this) != true) return;
        var (oldDirectory, wasEnabled) = (prefs.BackupDirectory, prefs.AutomaticBackups);
        prefs.BackupDirectory = picker.FolderName;
        prefs.AutomaticBackups = true;
        try
        {
            if (!preview) prefs.Save();
            path.Text = prefs.BackupDirectory;
            refresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            (prefs.BackupDirectory, prefs.AutomaticBackups) = (oldDirectory, wasEnabled);
            state.Text = "Nicht gespeichert: " + ex.Message;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static NoxVault.Ui;

namespace NoxVault;

// Keeps the settings layout independent of the vault window and its storage logic.
internal sealed class SettingsView : Grid
{
    readonly StackPanel navigation = new();
    readonly ComboBox compactNavigation = new() { Margin = new Thickness(0, 0, 0, 16), MinHeight = 38 };
    readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    readonly Border divider = new() { Background = Brush("#252A31"), Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 18, 0) };
    readonly List<(string title, StackPanel page, Button button)> pages = new();
    bool compact;
    internal int SelectedPage { get; private set; } = -1;
    internal int PageCount => pages.Count;
    internal bool IsCompact => compact;
    internal Action? PageChanged { get; set; }

    internal SettingsView()
    {
        MinHeight = 0; MaxHeight = 540;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(164) });
        ColumnDefinitions.Add(new ColumnDefinition());
        SetRow(navigation, 1); SetRow(divider, 1); SetRow(scroll, 1); SetColumn(scroll, 1);
        SetColumnSpan(compactNavigation, 2);
        Children.Add(navigation); Children.Add(divider); Children.Add(compactNavigation); Children.Add(scroll);
        compactNavigation.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(compactNavigation, "Einstellungsbereich");
        compactNavigation.SelectionChanged += (_, _) => { if (compactNavigation.SelectedIndex >= 0) SelectPage(compactNavigation.SelectedIndex); };
        var actionStyle = new Style(typeof(Button), (Style)Application.Current.FindResource("SettingsAction"));
        actionStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#8D96A3")));
        Resources[typeof(Button)] = actionStyle;
        scroll.Style = (Style)Application.Current.FindResource("OverlayScrollViewer");
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        compact = availableSize.Width < 600;
        navigation.Visibility = divider.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        compactNavigation.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        ColumnDefinitions[0].Width = new GridLength(compact ? 0 : 164);
        var height = Math.Min(540, availableSize.Height);
        var measured = base.MeasureOverride(new Size(availableSize.Width, height));
        return new Size(measured.Width, height);
    }
    internal StackPanel AddPage(string title, string description)
    {
        var page = new StackPanel { Margin = new Thickness(8, 2, 8, 8) };
        page.Children.Add(Text(title, 23, bold: true));
        var subtitle = Text(description, 12, "#818A98"); subtitle.Margin = new Thickness(0, 6, 0, 26); page.Children.Add(subtitle);
        int index = pages.Count;
        var button = Button(title, () => SelectPage(index));
        button.SetResourceReference(StyleProperty, "SidebarItem");
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = Brushes.Transparent; button.Padding = new Thickness(12, 10, 12, 10);
        button.Margin = new Thickness(0, 0, 30, 5);
        pages.Add((title, page, button)); navigation.Children.Add(button); compactNavigation.Items.Add(title);
        if (SelectedPage < 0) SelectPage(0);
        return page;
    }
    internal void SelectPage(int index)
    {
        if (index < 0 || index >= pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (SelectedPage == index) return;
        SelectedPage = index;
        scroll.Content = pages[index].page; scroll.ScrollToTop();
        foreach (var item in pages)
        {
            bool selected = item.page == pages[index].page;
            DesktopVisuals.SetIsSelected(item.button, selected);
            item.button.Foreground = Brush(selected ? "#F1F3F5" : "#8D96A3");
            item.button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            AutomationProperties.SetItemStatus(item.button, selected ? "Ausgewählt" : "");
        }
        compactNavigation.SelectedIndex = index;
        PageChanged?.Invoke();
    }
    internal static StackPanel Section(StackPanel page, string title, string description)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        section.Children.Add(Text(title, 14, "#E2E6EC", true));
        if (description.Length > 0)
        {
            var hint = Text(description, 12, "#8D96A3"); hint.Margin = new Thickness(0, 6, 0, 0); section.Children.Add(hint); section.Tag = hint;
        }
        page.Children.Add(section); return section;
    }
    internal static void Separator(StackPanel page) => page.Children.Add(new Border { Height = 1, Background = Brush("#252A31"), Margin = new Thickness(0, 0, 0, 20) });
    internal void ArrangeOptions()
    {
        foreach (var page in pages)
        foreach (var section in page.page.Children.OfType<StackPanel>().Where(s => s.Tag is TextBlock))
        {
            var control = section.Children.OfType<FrameworkElement>().FirstOrDefault(c => c is ComboBox or System.Windows.Controls.Button);
            if (control == null || section.Children.OfType<FrameworkElement>().Any(c => c is Panel)) continue;
            var labels = new StackPanel();
            for (int i = 0; i < 2; i++) { var label = section.Children[0]; section.Children.RemoveAt(0); labels.Children.Add(label); }
            section.Children.Remove(control);
            section.Children.Insert(0, new SettingsOptionRow(labels, control));
        }
    }
    internal static TextBlock Feedback(StackPanel section)
    {
        var feedback = Text("", 12, "#BDC2CC"); feedback.Margin = new Thickness(0, 9, 0, 0);
        var style = new Style(typeof(TextBlock));
        var empty = new DataTrigger { Binding = new System.Windows.Data.Binding("Text") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self) }, Value = "" };
        empty.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed)); style.Triggers.Add(empty); feedback.Style = style;
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        section.Children.Add(feedback); return feedback;
    }
}

internal sealed class SettingsOptionRow : Grid
{
    readonly FrameworkElement labels, choice;
    internal SettingsOptionRow(FrameworkElement labels, FrameworkElement choice)
    {
        this.labels = labels; this.choice = choice;
        ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        choice.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(labels); Children.Add(choice);
    }
    protected override Size MeasureOverride(Size available)
    {
        bool stacked = available.Width < 440;
        SetColumnSpan(labels, stacked ? 2 : 1); SetRow(choice, stacked ? 1 : 0); SetColumn(choice, stacked ? 0 : 1);
        choice.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        choice.Margin = stacked ? new Thickness(0, 12, 0, 0) : new Thickness(20, 0, 0, 0);
        return base.MeasureOverride(available);
    }
}

public sealed partial class MainWindow
{
    void AddTimeSetting(StackPanel section, string label, int[] values, Func<int> read, Action<int> write, string unit)
    {
        var choice = new ComboBox { ItemsSource = values.Select(v => new { Value = v, Label = v + " " + unit }).ToArray(),
            DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = read(), MinHeight = 38, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 150 };
        AutomationProperties.SetName(choice, label + " (" + unit + ")"); section.Children.Add(choice);
        var feedback = SettingsView.Feedback(section);
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedValue is not int value || value == read()) return;
            int old = read(); write(value);
            try { if (!preview) prefs.Save(); feedback.Text = "Gespeichert · " + value + " " + unit; SettingsPreview?.PageChanged?.Invoke(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { write(old); choice.SelectedValue = old; feedback.Text = "Nicht gespeichert: " + ex.Message; }
        };
    }
    internal SettingsView? SettingsPreview { get; private set; }
    internal InAppDialog CreateSettingsWindow()
    {
        var view = new SettingsView(); SettingsPreview = view;
        var w = new InAppDialog(this, "Einstellungen", view, 920, scrollContent: false);
        var general = view.AddPage("Allgemein", "So verhält sich vault im Alltag.");
        var autoSection = new StackPanel { Margin = new Thickness(0, 0, 0, 20) }; general.Children.Add(autoSection);
        var autoRow = new Grid(); autoRow.ColumnDefinitions.Add(new ColumnDefinition()); autoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var autoLabel = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        autoLabel.Children.Add(Text("Mit Windows starten", 14, "#E2E6EC", true));
        var autoHint = Text("Startet gesperrt im Hintergrund.", 12, "#8D96A3"); autoHint.Margin = new Thickness(0, 5, 0, 0); autoLabel.Children.Add(autoHint);
        autoRow.Children.Add(autoLabel); autoSection.Children.Add(autoRow);
        var auto = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = !preview && Preferences.AutoStart, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(auto, "Mit Windows starten"); Grid.SetColumn(auto, 1); autoRow.Children.Add(auto);
        var autoFeedback = SettingsView.Feedback(autoSection);
        auto.Click += (_, _) =>
        {
            bool old = auto.IsChecked != true;
            try { if (!preview) Preferences.AutoStart = auto.IsChecked == true; autoFeedback.Text = auto.IsChecked == true ? "Autostart aktiviert." : "Autostart deaktiviert."; }
            catch (Exception ex) { auto.IsChecked = old; autoFeedback.Text = "Nicht gespeichert: " + ex.Message; }
        };
        SettingsView.Separator(general);
        var shortcuts = SettingsView.Section(general, "vault öffnen", "Feld anklicken, Tastenkombination drücken und übernehmen.");
        uint candidateModifiers = prefs.HotkeyModifiers, candidateKey = prefs.EffectiveKey;
        var capture = Input("Neue Tastenkombination", ShortcutLabel(candidateModifiers, candidateKey)); capture.IsReadOnly = true; capture.Height = 38; capture.MinHeight = 38; capture.Padding = new Thickness(12, 8, 12, 8); capture.FontSize = 13;
        capture.Margin = new Thickness(0, 12, 0, 0); shortcuts.Children.Add(capture);
        var shortcutActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        shortcuts.Children.Add(shortcutActions);
        var apply = Button("Übernehmen", () => { });
        var discard = Button("Verwerfen", () => { }); discard.Background = Brushes.Transparent;
        shortcutActions.Children.Add(apply); shortcutActions.Children.Add(discard);
        var shortcutFeedback = SettingsView.Feedback(shortcuts);
        bool dirty = false;
        void RefreshCandidate()
        {
            dirty = candidateModifiers != prefs.HotkeyModifiers || candidateKey != prefs.EffectiveKey;
            apply.IsEnabled = dirty || (!preview && !hotkeyRegistered);
            discard.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
            shortcutActions.Visibility = dirty || (!preview && !hotkeyRegistered) ? Visibility.Visible : Visibility.Collapsed;
        }
        RefreshCandidate();
        discard.Click += (_, _) => { candidateModifiers = prefs.HotkeyModifiers; candidateKey = prefs.EffectiveKey; capture.Text = ShortcutLabel(candidateModifiers, candidateKey); shortcutFeedback.Text = "Änderung verworfen."; RefreshCandidate(); };
        capture.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Tab) return;
            e.Handled = true;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            var modifiers = (uint)Keyboard.Modifiers;
            if (modifiers == 0 || key is Key.Escape or Key.None) { shortcutFeedback.Text = "Nutze Strg, Alt, Shift oder Win zusammen mit einer weiteren Taste."; return; }
            candidateModifiers = modifiers; candidateKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            capture.Text = ShortcutLabel(candidateModifiers, candidateKey); RefreshCandidate();
            shortcutFeedback.Text = dirty ? "Noch nicht gespeichert. Übernehmen oder verwerfen." : "Diese Kombination ist bereits ausgewählt.";
        };
        apply.Click += (_, _) =>
        {
            if (preview) { shortcutFeedback.Text = "Vorschau: Tastenkombination wird nicht registriert."; return; }
            var oldModifiers = prefs.HotkeyModifiers; var oldKey = prefs.HotkeyKey;
            int candidateId = activeHotkeyId == 42 ? 43 : 42;
            if (!Native.RegisterHotKey(handle, candidateId, candidateModifiers | 0x4000, candidateKey)) { shortcutFeedback.Text = "Diese Kombination ist belegt oder nicht verfügbar."; return; }
            prefs.HotkeyModifiers = candidateModifiers; prefs.HotkeyKey = candidateKey;
            try { prefs.Save(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Native.UnregisterHotKey(handle, candidateId); prefs.HotkeyModifiers = oldModifiers; prefs.HotkeyKey = oldKey; shortcutFeedback.Text = "Nicht gespeichert: " + ex.Message; return; }
            if (hotkeyRegistered) Native.UnregisterHotKey(handle, activeHotkeyId);
            activeHotkeyId = candidateId; hotkeyRegistered = true; shortcut = ShortcutLabel(candidateModifiers, candidateKey);
            if (tray != null) tray.Text = "vault · " + shortcut;
            shortcutFeedback.Text = "Tastenkombination gespeichert."; RefreshCandidate();
        };
        if (!preview && !hotkeyRegistered) shortcutFeedback.Text = "Gespeicherte Kombination nicht verfügbar. Wähle eine andere.";
        SettingsView.Separator(general);
        SettingsView.Section(general, "Beim Schließen", "vault sperrt den Tresor und bleibt im Infobereich. Über das Tray-Menü kannst du die App vollständig beenden.");
        w.Closing += (_, e) =>
        {
            if (!dirty) return;
            if (!Confirm(w, "Änderung verwerfen?", "Die neue Tastenkombination wurde noch nicht übernommen. Möchtest du die Einstellungen trotzdem schließen?")) e.Cancel = true;
        };

        var security = view.AddPage("Sicherheit", "Windows-Zugriff und Schutz deiner Zugangsdaten.");
        SettingsView.Section(security, "Direkt öffnen", "vault startet ohne Master-Passwort. Dein Windows-Benutzerkonto öffnet die Zugangsdaten automatisch, auch nach einem Neustart. Bei Inaktivität bleibt vault geöffnet.");
        SettingsView.Separator(security);
        var clipboardSection = SettingsView.Section(security, "Kopierte Daten entfernen", "Leert nur von vault kopierte Inhalte. Gilt ab dem nächsten Kopieren.");
        AddTimeSetting(clipboardSection, "Zwischenablage leeren nach", new[] { 15, 30, 60 }, () => prefs.ClipboardSeconds, v => prefs.ClipboardSeconds = v, "Sekunden");
        SettingsView.Separator(security);
        var revealSection = SettingsView.Section(security, "Passwörter wieder verbergen", "Blendet sichtbare Passwörter nach dieser Zeit wieder aus.");
        AddTimeSetting(revealSection, "Passwort verbergen nach", new[] { 10, 15, 30 }, () => prefs.RevealSeconds, v => prefs.RevealSeconds = v, "Sekunden");
        SettingsView.Separator(security);
        var audit = SettingsView.Section(security, "Lokaler Passwortcheck", "Leere und mehrfach verwendete Passwörter finden.");
        var check = Button("Passwörter prüfen …", ShowPasswordCheck); check.HorizontalAlignment = HorizontalAlignment.Left; audit.Children.Add(check);
        var backups = view.AddPage("Sicherungen", "Eine verschlüsselte Kopie für den Fall der Fälle.");
        var automatic = SettingsView.Section(backups, "Automatisch sichern", "Nach Änderungen höchstens einmal täglich, während vault läuft. Manuelle Sicherungen bleiben erhalten.");
        var backupPath = Text(prefs.BackupDirectory.Length == 0 ? "Wähle einen Zielordner, um automatische Sicherungen zu aktivieren." : prefs.BackupDirectory, 12, "#818A98"); backupPath.Margin = new Thickness(0, 12, 0, 0); automatic.Children.Add(backupPath);
        var backupState = Text("", 12, "#8D96A3"); backupState.Margin = new Thickness(0, 8, 0, 8); automatic.Children.Add(backupState);
        var backupToggle = Button("", () => { });
        void RefreshBackupState()
        {
            backupToggle.Content = prefs.AutomaticBackups ? "Pausieren" : "Aktivieren";
            AutomationProperties.SetName(backupToggle, "Automatische Sicherungen " + (prefs.AutomaticBackups ? "pausieren" : "aktivieren"));
            backupToggle.IsEnabled = prefs.AutomaticBackups || prefs.BackupDirectory.Length > 0;
            try { var last = AutomaticBackupService.LastSuccess(vault.FilePath, prefs.BackupDirectory); backupState.Text = (prefs.AutomaticBackups ? "Aktiv" : "Ausgeschaltet") + " · " + (last.HasValue ? "Letzte Sicherung: " + last.Value.ToString("dd.MM.yyyy HH:mm") : "Noch keine automatische Sicherung") + (automaticBackupMessage.Length > 0 ? "\n" + automaticBackupMessage : ""); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { backupState.Text = "Sicherungsordner nicht erreichbar."; }
        }
        RefreshBackupState();
        var backupControls = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) }; automatic.Children.Add(backupControls);
        backupControls.Children.Add(Button("Ordner wählen …", () =>
        {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner für verschlüsselte Sicherungen" };
            if (picker.ShowDialog(this) != true) return;
            var old = prefs.BackupDirectory; bool enabled = prefs.AutomaticBackups;
            prefs.BackupDirectory = picker.FolderName; prefs.AutomaticBackups = true;
            try { if (!preview) prefs.Save(); backupPath.Text = prefs.BackupDirectory; RefreshBackupState(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { prefs.BackupDirectory = old; prefs.AutomaticBackups = enabled; backupState.Text = "Nicht gespeichert: " + ex.Message; }
        }));
        backupControls.Children.Add(backupToggle);
        backupToggle.Click += (_, _) =>
        {
            bool old = prefs.AutomaticBackups; prefs.AutomaticBackups = !old;
            try { if (!preview) prefs.Save(); RefreshBackupState(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { prefs.AutomaticBackups = old; backupState.Text = "Nicht gespeichert: " + ex.Message; }
        };
        var retentionLabel = Text("Anzahl aufbewahrter Sicherungen", 12, "#8D96A3"); retentionLabel.Margin = new Thickness(0, 8, 0, 8); automatic.Children.Add(retentionLabel);
        AddTimeSetting(automatic, "Automatische Sicherungen behalten", new[] { 7, 14, 30 }, () => prefs.BackupRetention, v => prefs.BackupRetention = v, "Versionen");
        var refreshSecurityPage = view.PageChanged;
        view.PageChanged = () => { refreshSecurityPage?.Invoke(); RefreshBackupState(); };
        SettingsView.Separator(backups);
        var exportSection = SettingsView.Section(backups, "Sicherung erstellen", "Speichere eine verschlüsselte Kopie als .nox-Datei an einem Ort deiner Wahl.");
        var exportFeedback = Text("", 12);
        var export = Button("Sicherung erstellen …", () => { if (!preview) Backup(exportFeedback); }); export.HorizontalAlignment = HorizontalAlignment.Left; exportSection.Children.Add(export);
        exportFeedback = SettingsView.Feedback(exportSection);
        SettingsView.Separator(backups);
        var restoreSection = SettingsView.Section(backups, "Aus Sicherung wiederherstellen", "Ersetzt deine aktuellen Accounts. Vorher erstellt vault eine Sicherung des bestehenden Tresors. Dein Windows-Zugriff bleibt erhalten.");
        var restoreFeedback = Text("", 12);
        var restore = Button("Wiederherstellen …", () => { if (!preview) Restore(restoreFeedback); }); restore.HorizontalAlignment = HorizontalAlignment.Left; restore.Background = Brushes.Transparent; restoreSection.Children.Add(restore);
        restoreFeedback = SettingsView.Feedback(restoreSection);

        SettingsView.Separator(backups);
        var trash = SettingsView.Section(backups, "Papierkorb", "Gelöschte Accounts 30 Tage lang wiederherstellen oder endgültig entfernen.");
        var trashButton = Button("Papierkorb öffnen …", ShowTrash); trashButton.HorizontalAlignment = HorizontalAlignment.Left; trash.Children.Add(trashButton);
        var fill = view.AddPage("Ausfüllen", "Zugangsdaten gezielt in den Riot-Login einfügen.");
        var fillShortcut = SettingsView.Section(fill, "Account-Auswahl öffnen", "Diese Tastenkombination öffnet die Auswahl im Riot-Login.");
        var fillChoice = new ComboBox { ItemsSource = new[] { 8, 9, 10, 11, 12 }.Select(f => new { Value = f, Label = "Win + F" + f }).ToArray(),
            DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = prefs.FillShortcutF, MinWidth = 150 };
        AutomationProperties.SetName(fillChoice, "Tastenkombination für Account-Auswahl");
        fillShortcut.Children.Add(fillChoice);
        var fillFeedback = SettingsView.Feedback(fillShortcut);
        fillFeedback.Text = preview || fillShortcutRegistered ? "Bereit · " + FillShortcutLabel : "Shortcut ist belegt. Bitte eine andere Kombination wählen.";
        fillChoice.SelectionChanged += (_, _) =>
        {
            if (fillChoice.SelectedValue is not int next || next == prefs.FillShortcutF) return;
            int old = prefs.FillShortcutF;
            if (!preview && !Native.RegisterHotKey(handle, 75, 0x4008, (uint)(0x6F + next)))
            { fillChoice.SelectedValue = old; fillFeedback.Text = "Diese Tastenkombination ist bereits belegt."; return; }
            prefs.FillShortcutF = next;
            try { if (!preview) prefs.Save(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { prefs.FillShortcutF = old; fillChoice.SelectedValue = old; if (!preview) Native.UnregisterHotKey(handle, 75); fillFeedback.Text = "Nicht gespeichert. Bitte Dateizugriff prüfen."; return; }
            if (!preview) { Native.UnregisterHotKey(handle, 75); RegisterFillShortcut(); }
            fillFeedback.Text = preview || fillShortcutRegistered ? "Gespeichert · " + FillShortcutLabel : "Shortcut inzwischen belegt. Bitte erneut wählen.";
        };
        SettingsView.Separator(fill);
        SettingsView.Section(fill, "Einmal pro Account einrichten", "1. Account öffnen und „Ausfüllen einrichten“ wählen.\n2. Den Riot-Client zuordnen.\n3. Im Login die Tastenkombination drücken und den Account auswählen.");
        SettingsView.Separator(fill);
        SettingsView.Section(fill, "Du bestätigst die Anmeldung", "vault trägt die Zugangsdaten ein. Den Login startest du selbst. Nach einem Clientupdate die Zuordnung erneut prüfen.");
        var about = view.AddPage("Über vault", "Deine Accounts. Lokal auf deinem Gerät.");
        SettingsView.Section(about, "vault", "Version " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        SettingsView.Separator(about);
        SettingsView.Section(about, "Lokale Speicherung", "Dein Tresor bleibt auf diesem Windows-Gerät. Keine Cloud-Synchronisierung und keine Telemetrie.");
        var technical = new Expander { Header = "Technische Details", Foreground = Brush("#8D96A3"), Margin = new Thickness(0, 4, 0, 0) };
        technical.SetResourceReference(StyleProperty, "SettingsDisclosure");
        technical.Content = Text("AES-256-GCM · Argon2id\n.NET " + Environment.Version + "\nTresorformat v3", 12, "#818A98");
        about.Children.Add(technical);
        view.ArrangeOptions();
        return w;
    }
}

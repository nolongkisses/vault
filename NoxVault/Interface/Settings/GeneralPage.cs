using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using static NoxVault.Elements;

namespace NoxVault;

// Autostart, appearance and the shortcut that opens vault.
internal sealed partial class MainWindow
{
    void GeneralPage(SettingsView view, InAppDialog dialog)
    {
        var general = view.AddPage("Allgemein", "So verhält sich vault im Alltag.");
        AutostartSection(general);
        AppearanceSection(general);
        ShortcutSection(general, dialog);
        SettingsView.Section(general, "Beim Schließen",
            "vault sperrt den Tresor und bleibt im Infobereich. Über das Tray-Menü kannst du die App vollständig beenden.");
    }

    void AutostartSection(StackPanel page)
    {
        var section = SettingsView.Section(page, "Mit Windows starten", "Startet gesperrt im Hintergrund.");
        var autostart = new Switch { IsChecked = !preview && Preferences.AutoStart };
        AutomationProperties.SetName(autostart, "Mit Windows starten");
        section.Children.Add(autostart);
        var feedback = SettingsView.Feedback(section);
        autostart.Click += (_, _) =>
        {
            bool old = autostart.IsChecked != true;
            try
            {
                if (!preview) Preferences.AutoStart = autostart.IsChecked == true;
                feedback.Text = autostart.IsChecked == true ? "Autostart aktiviert." : "Autostart deaktiviert.";
            }
            catch (Exception ex) // Registry, shortcut or COM: any failure keeps the previous state and says why.
            {
                autostart.IsChecked = old;
                feedback.Text = "Nicht gespeichert: " + ex.Message;
            }
        };
    }

    void AppearanceSection(StackPanel page)
    {
        var section = SettingsView.Section(page, "Darstellung", "Hell, dunkel oder wie Windows.");
        var modes = new[] { "System", "Dunkel", "Hell" };
        var choice = new Segmented("Darstellung", modes);
        choice.Select(Array.IndexOf(modes, prefs.Theme) is var index and >= 0 ? index : 0, notify: false);
        section.Children.Add(choice);
        var feedback = SettingsView.Feedback(section);
        choice.Changed += chosen =>
        {
            var old = prefs.Theme;
            prefs.Theme = modes[chosen];
            try { if (!preview) prefs.Save(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                prefs.Theme = old;
                choice.Select(Array.IndexOf(modes, old), notify: false);
                feedback.Text = "Nicht gespeichert: " + ex.Message;
                return;
            }
            Theme.Apply(prefs.Theme);
        };
    }

    // The new combination is only registered and saved with "Übernehmen"; closing with a pending change asks first.
    void ShortcutSection(StackPanel page, InAppDialog dialog)
    {
        var section = SettingsView.Section(page, "vault öffnen", "Feld anklicken, Tastenkombination drücken und übernehmen.");
        uint modifiers = prefs.HotkeyModifiers, key = prefs.EffectiveKey;
        var capture = Input("Neue Tastenkombination", Hotkeys.Label(modifiers, key));
        capture.IsReadOnly = true;
        capture.Margin = new Thickness(0, Theme.S5, 0, 0);
        section.Children.Add(capture);
        var apply = Button("Übernehmen", () => { });
        var discard = Ghost("Verwerfen", () => { });
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, Theme.S4, 0, 0),
            Children = { apply, discard }
        };
        section.Children.Add(actions);
        var feedback = SettingsView.Feedback(section);
        bool dirty = false;
        void Refresh()
        {
            dirty = modifiers != prefs.HotkeyModifiers || key != prefs.EffectiveKey;
            apply.IsEnabled = dirty || (!preview && !hotkeys.OpenRegistered);
            discard.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
            actions.Visibility = dirty || (!preview && !hotkeys.OpenRegistered) ? Visibility.Visible : Visibility.Collapsed;
        }
        Refresh();
        discard.Click += (_, _) =>
        {
            (modifiers, key) = (prefs.HotkeyModifiers, prefs.EffectiveKey);
            capture.Text = Hotkeys.Label(modifiers, key);
            feedback.Text = "Änderung verworfen.";
            Refresh();
        };
        capture.PreviewKeyDown += (_, e) =>
        {
            if (CaptureShortcut(e, out bool invalid) is not { } combination)
            {
                if (invalid) feedback.Text = "Nutze Strg, Alt, Shift oder Win zusammen mit einer weiteren Taste.";
                return;
            }
            (modifiers, key) = combination;
            capture.Text = Hotkeys.Label(modifiers, key);
            Refresh();
            feedback.Text = dirty ? "Noch nicht gespeichert. Übernehmen oder verwerfen." : "Diese Kombination ist bereits ausgewählt.";
        };
        apply.Click += (_, _) => { feedback.Text = ApplyShortcut(modifiers, key); Refresh(); };
        if (!preview && !hotkeys.OpenRegistered) feedback.Text = "Gespeicherte Kombination nicht verfügbar. Wähle eine andere.";
        dialog.Closing += (_, e) =>
        {
            if (dirty && !InAppDialog.Confirm(dialog, "Änderung verwerfen?",
                "Die neue Tastenkombination wurde noch nicht übernommen. Möchtest du die Einstellungen trotzdem schließen?"))
                e.Cancel = true;
        };
    }

    // Tab keeps moving focus; a lone modifier waits for the next key; a key without modifier is invalid.
    static (uint Modifiers, uint Key)? CaptureShortcut(KeyEventArgs e, out bool invalid)
    {
        invalid = false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab) return null;
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return null;
        var modifiers = (uint)Keyboard.Modifiers;
        invalid = modifiers == 0 || key is Key.Escape or Key.None;
        return invalid ? null : (modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    string ApplyShortcut(uint modifiers, uint key)
    {
        if (preview) return "Vorschau: Tastenkombination wird nicht registriert.";
        var (oldModifiers, oldKey) = (prefs.HotkeyModifiers, prefs.HotkeyKey);
        try
        {
            bool registered = hotkeys.ReplaceOpen(modifiers, key, () =>
            {
                (prefs.HotkeyModifiers, prefs.HotkeyKey) = (modifiers, key);
                prefs.Save();
            });
            return registered ? "Tastenkombination gespeichert." : "Diese Kombination ist belegt oder nicht verfügbar.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            (prefs.HotkeyModifiers, prefs.HotkeyKey) = (oldModifiers, oldKey);
            return "Nicht gespeichert: " + ex.Message;
        }
    }
}

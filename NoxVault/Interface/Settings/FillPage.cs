using System;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;

namespace NoxVault;

// The Riot fill shortcut and how the fill works.
internal sealed partial class MainWindow
{
    void FillPage(SettingsView view)
    {
        var fill = view.AddPage("Ausfüllen", "Zugangsdaten gezielt in den Riot-Login einfügen.");
        var shortcut = SettingsView.Section(fill, "Account-Auswahl öffnen", "Diese Tastenkombination öffnet die Auswahl im Riot-Login.");
        var choice = new ComboBox
        {
            ItemsSource = new[] { 8, 9, 10, 11, 12 }.Select(f => new { Value = f, Label = Hotkeys.FillLabel(f) }).ToArray(),
            DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = prefs.FillShortcutF, MinWidth = 150,
        };
        AutomationProperties.SetName(choice, "Tastenkombination für Account-Auswahl");
        shortcut.Children.Add(choice);
        var feedback = SettingsView.Feedback(shortcut);
        feedback.Text = preview
            || hotkeys.FillRegistered ? "Bereit · " + FillShortcutLabel : "Shortcut ist belegt. Bitte eine andere Kombination wählen.";
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedValue is not int next || next == prefs.FillShortcutF) return;
            feedback.Text = ChangeFillShortcut(next);
            choice.SelectedValue = prefs.FillShortcutF;
        };
        SettingsView.Section(fill, "Einmal pro Account einrichten",
            "1. Account öffnen und „Riot verbinden“ wählen.\n2. Den Riot-Client zuordnen.\n"
            + "3. Im Login die Tastenkombination drücken und den Account auswählen.");
        SettingsView.Section(fill, "Du bestätigst die Anmeldung",
            "vault trägt die Zugangsdaten ein. Den Login startest du selbst. Nach einem Clientupdate die Zuordnung erneut prüfen.");
    }

    // The new key is reserved before saving, so a taken combination never replaces a working one.
    string ChangeFillShortcut(int next)
    {
        int old = prefs.FillShortcutF;
        if (!preview && !hotkeys.ProbeFill(next)) return "Diese Tastenkombination ist bereits belegt.";
        prefs.FillShortcutF = next;
        try { if (!preview) prefs.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            prefs.FillShortcutF = old;
            if (!preview) hotkeys.ReleaseFillProbe();
            return "Nicht gespeichert. Bitte Dateizugriff prüfen.";
        }
        if (!preview) { hotkeys.ReleaseFillProbe(); hotkeys.RegisterFill(prefs.FillShortcutF); }
        return preview || hotkeys.FillRegistered ? "Gespeichert · "
            + FillShortcutLabel : "Shortcut inzwischen belegt. Bitte erneut wählen.";
    }
}

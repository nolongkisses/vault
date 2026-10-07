using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// Builds the settings sheet from its pages. Every change is saved immediately except the open shortcut.
internal sealed partial class MainWindow
{
    internal SettingsView? SettingsPreview { get; private set; }

    void Settings() => CreateSettingsWindow().ShowDialog();

    internal InAppDialog CreateSettingsWindow()
    {
        var view = new SettingsView();
        SettingsPreview = view;
        var dialog = new InAppDialog(this, "Einstellungen", view, 920, scrollContent: false);
        GeneralPage(view, dialog);
        SecurityPage(view);
        BackupsPage(view);
        FillPage(view);
        AboutPage(view);
        view.ArrangeOptions();
        return dialog;
    }

    void AboutPage(SettingsView view)
    {
        var about = view.AddPage("Über vault", "Deine Accounts. Lokal auf deinem Gerät.");
        SettingsView.Section(about, "vault", "Version " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        SettingsView.Section(about, "Lokale Speicherung",
            "Dein Tresor bleibt auf diesem Windows-Gerät. Keine Cloud-Synchronisierung und keine Telemetrie.");
        var technical = new Expander { Header = "Technische Details" };
        technical.Content = Text("AES-256-GCM · Argon2id\n.NET " + Environment.Version + "\nTresorformat v4", TextRole.Small, Theme.Sub);
        about.Children.Add(technical);
    }

    // A choice from fixed values, saved on change; a failed save restores the previous value.
    void AddTimeSetting(StackPanel section, string label, int[] values, (Func<int> Read, Action<int> Write) value, string unit)
    {
        var choice = new ComboBox
        {
            ItemsSource = values.Select(v => new { Value = v, Label = v + " " + unit }).ToArray(), DisplayMemberPath = "Label",
            SelectedValuePath = "Value", SelectedValue = value.Read(), MinWidth = 150, HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(choice, label + " (" + unit + ")");
        section.Children.Add(choice);
        var feedback = SettingsView.Feedback(section);
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedValue is not int chosen || chosen == value.Read()) return;
            int old = value.Read();
            value.Write(chosen);
            try
            {
                if (!preview) prefs.Save();
                feedback.Text = "Gespeichert · " + chosen + " " + unit;
                SettingsPreview?.PageChanged?.Invoke();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                value.Write(old);
                choice.SelectedValue = old;
                feedback.Text = "Nicht gespeichert: " + ex.Message;
            }
        };
    }
}

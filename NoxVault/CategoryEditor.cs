using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow
{
    FrameworkElement ColorPalette(string initial, Action<string> changed)
    {
        var panel = new WrapPanel();
        var colors = new[] { ("Blau", "#3B82F6"), ("Indigo", "#6E77D6"), ("Violett", "#9E8CFC"), ("Pink", "#D474B0"),
            ("Rot", "#E5484D"), ("Orange", "#F09A50"), ("Gold", "#D6B56E"), ("Grün", "#65BA74"), ("Türkis", "#53B9AB"), ("Grau", "#8B8D98") };
        void Select(string value)
        {
            foreach (Button button in panel.Children)
                button.Content = Text((string)button.Tag == value ? "✓" : "", 16, "#FFFFFF", true);
        }
        foreach (var (name, hex) in colors)
        {
            var button = Button("", () => { Select(hex); changed(hex); });
            button.Tag = hex; button.Width = 32; button.Height = 32; button.MinWidth = 0;
            button.Padding = new Thickness(0); button.Margin = new Thickness(0, 0, 8, 8);
            button.Background = Brush(hex); button.ToolTip = name;
            System.Windows.Automation.AutomationProperties.SetName(button, "Kategoriefarbe: " + name);
            panel.Children.Add(button);
        }
        Select(initial); return panel;
    }

    void EditCategory(string? old)
    {
        if (data == null) return;
        var form = new StackPanel();
        var name = Input("Name der Kategorie", old ?? ""); name.MaxLength = 80;
        Field(form, "Name", name);
        string color = old == null ? "#3B82F6" : CategoryColor(old);
        Field(form, "Farbe", ColorPalette(color, value => color = value));
        form.Children.Add(Text("Für das Kategoriesymbol und die zugehörigen Einträge.", 11, "#8D96A3"));
        var error = Text("", 12, "#F49098"); form.Children.Add(error);
        var dialog = Dialog(this, old == null ? "Kategorie hinzufügen" : "Kategorie bearbeiten", form);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        actions.Children.Add(Button("Abbrechen", () => dialog.Close()));
        actions.Children.Add(Button("Speichern", () =>
        {
            string value = name.Text.Trim();
            if (value.Length == 0) { error.Text = "Bitte einen Namen eingeben."; return; }
            if (data.Categories.Any(c => c != old && c.Equals(value, StringComparison.OrdinalIgnoreCase)))
            { error.Text = "Diese Kategorie existiert bereits."; return; }
            if (Mutate(d =>
            {
                if (old == null) d.Categories.Add(value);
                else
                {
                    d.Categories[d.Categories.IndexOf(old)] = value;
                    d.CategoryColors.Remove(old);
                    foreach (var account in d.Accounts.Where(a => a.Category == old)) account.Category = value;
                }
                d.CategoryColors[value] = color;
            })) { SelectCategory(value, false); dialog.Close(); }
            else error.Text = "Speichern fehlgeschlagen.";
        }, true));
        form.Children.Add(actions); dialog.ShowDialog();
    }
}

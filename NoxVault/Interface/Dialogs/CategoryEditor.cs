using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static NoxVault.Elements;

namespace NoxVault;

// Add, rename, recolour and delete categories. Deleting moves the accounts into a category the user picks.
internal sealed partial class MainWindow
{
    static WrapPanel ColorPalette(string initial, Action<string> changed)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, Theme.S4, 0, Theme.S4) };
        void Select(string value)
        {
            foreach (Button swatch in panel.Children)
                swatch.Content = (string)swatch.Tag == value ? new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(Icons.Check), Width = Theme.IconSize, Height = Theme.IconSize, Stretch = Stretch.Uniform,
                    Fill = Brushes.White,
                } : null;
        }
        foreach (var (name, hex) in Theme.CategoryPalette)
        {
            var swatch = Ghost("", () => { Select(hex); changed(hex); });
            swatch.Tag = hex;
            swatch.Width = swatch.Height = Theme.ControlHeight;
            swatch.Padding = new Thickness(0);
            swatch.Margin = new Thickness(0, 0, Theme.S4, Theme.S4);
            swatch.Background = Theme.Color(hex);
            swatch.ToolTip = name;
            System.Windows.Automation.AutomationProperties.SetName(swatch, "Kategoriefarbe: " + name);
            panel.Children.Add(swatch);
        }
        Select(initial);
        return panel;
    }

    void EditCategory(string? old)
    {
        if (Data == null) return;
        var data = Data;
        var form = new StackPanel();
        var name = Input("Name der Kategorie", old ?? "");
        name.MaxLength = 80;
        Field(form, "Name", name);
        string color = old == null ? Theme.DefaultCategoryColor : CategoryColor(old);
        Field(form, "Farbe", ColorPalette(color, value => color = value));
        form.Children.Add(Text("Für das Kategoriesymbol und die zugehörigen Einträge.", TextRole.Small, Theme.Sub));
        var error = Text("", TextRole.Small, Theme.Fg, bold: true);
        form.Children.Add(error);
        var dialog = InAppDialog.Create(this, old == null ? "Kategorie hinzufügen" : "Kategorie bearbeiten", form);
        void Save()
        {
            string value = name.Text.Trim();
            if (value.Length == 0) { error.Text = "Bitte einen Namen eingeben."; return; }
            if (data.Categories.Any(c => c != old && c.Equals(value, StringComparison.OrdinalIgnoreCase)))
            { error.Text = "Diese Kategorie existiert bereits."; return; }
            if (Mutate(d => RenameOrAdd(d, old, value, color))) { SelectCategory(value, false); dialog.Close(); }
            else error.Text = "Speichern fehlgeschlagen.";
        }
        form.Children.Add(InAppDialog.Actions(Ghost("Abbrechen", dialog.Close), Primary("Speichern", Save)));
        dialog.ShowDialog();
    }

    static void RenameOrAdd(VaultData data, string? old, string value, string color)
    {
        if (old == null) data.Categories.Add(value);
        else
        {
            data.Categories[data.Categories.IndexOf(old)] = value;
            data.CategoryColors.Remove(old);
            foreach (var account in data.Accounts.Where(a => a.Category == old)) account.Category = value;
        }
        data.CategoryColors[value] = color;
    }

    void DeleteCategory(string? old)
    {
        if (Data == null || old == null || !Data.Categories.Contains(old)) return;
        if (Data.Categories.Count == 1) { status.Text = "Mindestens eine Kategorie muss erhalten bleiben."; return; }
        var form = new StackPanel();
        int number = Data.Accounts.Count(a => a.Category == old);
        form.Children.Add(Text(number == 0 ? "„" + old + "“ ist leer und wird gelöscht."
            : $"{number} Accounts aus „{old}“ bleiben erhalten. Wähle ihre neue Kategorie.", TextRole.Body, Theme.Sub));
        var destination = new ComboBox { ItemsSource = Data.Categories.Where(c => c != old).ToList(), SelectedIndex = 0 };
        if (number > 0) Field(form, "Accounts verschieben nach", destination);
        var dialog = InAppDialog.Create(this, "Kategorie löschen", form);
        var delete = Primary("Kategorie löschen", () =>
        {
            var target = (string)destination.SelectedItem;
            bool deleted = Mutate(d =>
            {
                d.Categories.Remove(old);
                d.CategoryColors.Remove(old);
                foreach (var account in d.Accounts.Where(a => a.Category == old)) account.Category = target;
            });
            if (deleted) { SelectCategory(target, false); dialog.Close(); }
        });
        form.Children.Add(InAppDialog.Actions(Ghost("Abbrechen", dialog.Close), delete));
        dialog.ShowDialog();
    }
}

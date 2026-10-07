using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// Add, edit or duplicate one account. Nothing is written until "Account speichern"; closing with changes asks first.
internal sealed partial class MainWindow
{
    sealed class AccountForm
    {
        internal TextBox Title = new(), Email = new(), Username = new(), Website = new(), Notes = new();
        internal ComboBox Category = new();
        internal PasswordBox Password = new();
        internal PasswordEditor? PasswordEditor;
        internal TextBlock Error = new();
        internal readonly List<(TextBox Label, PasswordBox Value, FrameworkElement Row)> Fields = new();
        internal readonly Dictionary<string, string> Colors = new();
        internal string InitialTitle = "", InitialCategory = "";
    }

    void Edit(Account? existing, bool duplicate = false)
    {
        if (Data == null) return;
        var form = new AccountForm();
        var content = new StackPanel();
        content.Children.Add(Text("Nur die Bezeichnung ist erforderlich.", TextRole.Small, Theme.Sub));
        content.Children.Add(IdentitySection(form, existing, duplicate));
        content.Children.Add(CredentialSection(form, existing));
        content.Children.Add(ExtrasSection(form, existing));
        form.Error = Text("", TextRole.Small, Theme.Fg, bold: true);
        form.Error.Margin = new Thickness(0, Theme.S5, 0, 0);
        content.Children.Add(form.Error);
        var layout = new Grid { MaxHeight = 620 };
        layout.RowDefinitions.Add(new RowDefinition());
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroll = new ScrollViewer { Content = content };
        scroll.SetResourceReference(StyleProperty, "GutterScrollViewer");
        layout.Children.Add(scroll);
        string title = duplicate ? "Account duplizieren" : existing == null ? "Account hinzufügen" : "Account bearbeiten";
        var dialog = new InAppDialog(this, title, layout, 650, false);
        bool saved = false;
        var footer = InAppDialog.Actions(Ghost("Abbrechen", dialog.Close),
            Primary("Account speichern", () =>
            {
                if (!SaveAccount(form, existing, duplicate)) return;
                saved = true;
                dialog.Close();
            }));
        Grid.SetRow(footer, 1);
        layout.Children.Add(footer);
        dialog.Closing += (_, e) =>
        {
            if (!saved && Dirty(form, existing)
                && !InAppDialog.Confirm(dialog, "Änderungen verwerfen?", "Die Änderungen an diesem Account wurden noch nicht gespeichert."))
                e.Cancel = true;
        };
        dialog.Closed += (_, _) =>
        {
            form.PasswordEditor?.Clear();
            form.Email.Clear();
            form.Username.Clear();
            form.Notes.Clear();
            foreach (var field in form.Fields) field.Value.Clear();
        };
        dialog.ShowDialog();
    }

    static StackPanel Section(string heading)
    {
        var section = new StackPanel { Margin = new Thickness(0, Theme.S7, 0, 0) };
        section.Children.Add(Text(heading, TextRole.Title, bold: true));
        return section;
    }

    static Grid Pair(string leftLabel, UIElement left, string rightLabel, UIElement right)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var first = new StackPanel { Margin = new Thickness(0, 0, Theme.S4, 0) };
        var second = new StackPanel { Margin = new Thickness(Theme.S4, 0, 0, 0) };
        Field(first, leftLabel, left);
        Field(second, rightLabel, right);
        Grid.SetColumn(second, 1);
        row.Children.Add(first);
        row.Children.Add(second);
        return row;
    }

    StackPanel IdentitySection(AccountForm form, Account? existing, bool duplicate)
    {
        var data = Data ?? throw new InvalidOperationException("Tresor ist gesperrt.");
        var section = Section("Account");
        form.InitialTitle = (existing?.Title ?? "") + (duplicate ? " – Kopie" : "");
        form.Title = Input("Bezeichnung", form.InitialTitle);
        form.Title.MaxLength = 120;
        form.InitialCategory = existing?.Category ?? category ?? data.Categories[0];
        form.Category = new ComboBox { ItemsSource = data.Categories, SelectedItem = form.InitialCategory };
        section.Children.Add(Pair("Bezeichnung *", form.Title, "Kategorie", form.Category));
        var palette = new StackPanel();
        void RefreshPalette()
        {
            if (form.Category.SelectedItem is not string name) return;
            palette.Children.Clear();
            var current = form.Colors.TryGetValue(name, out var value) ? value : CategoryColor(name);
            palette.Children.Add(ColorPalette(current, chosen => form.Colors[name] = chosen));
            palette.Children.Add(Text("Gilt für alle Einträge dieser Kategorie.", TextRole.Small, Theme.Sub));
        }
        form.Category.SelectionChanged += (_, _) => RefreshPalette();
        RefreshPalette();
        section.Children.Add(new Expander { Header = "Kategoriefarbe", Content = palette, Margin = new Thickness(0, Theme.S5, 0, 0) });
        return section;
    }

    StackPanel CredentialSection(AccountForm form, Account? existing)
    {
        var section = Section("Zugangsdaten");
        form.Email = Input("E-Mail", existing?.Email ?? "");
        form.Username = Input("Benutzername", existing?.Username ?? "");
        section.Children.Add(Pair("E-Mail", form.Email, "Benutzername", form.Username));
        form.Password = Secret("Passwort");
        form.Password.Password = existing?.Password ?? "";
        var header = new DockPanel { Margin = new Thickness(0, Theme.S5, 0, Theme.S3) };
        var generate = Ghost("Passwort generieren", () => ShowGenerator(form.Password));
        generate.SetResourceReference(ForegroundProperty, Theme.Sub);
        DockPanel.SetDock(generate, Dock.Right);
        header.Children.Add(generate);
        header.Children.Add(Text("Passwort", TextRole.Small, Theme.Sub));
        section.Children.Add(header);
        form.PasswordEditor = EditorPassword(form.Password);
        section.Children.Add(form.PasswordEditor);
        return section;
    }

    Expander ExtrasSection(AccountForm form, Account? existing)
    {
        var extras = new StackPanel();
        form.Website = Input("Login-Seite", existing?.Website ?? "");
        form.Website.ToolTip = "Zum Beispiel https://account.riotgames.com";
        Field(extras, "Login-Seite (optional)", form.Website);
        var help = Text("Die Webadresse deiner Anmeldung. Damit öffnest du die Seite später direkt aus den Account-Details. "
            + "Für Apps kannst du das Feld leer lassen.", TextRole.Small, Theme.Sub);
        help.Margin = new Thickness(0, Theme.S3, 0, 0);
        extras.Children.Add(help);
        form.Notes = Input("Notiz", existing?.Notes ?? "");
        form.Notes.AcceptsReturn = true;
        form.Notes.TextWrapping = TextWrapping.Wrap;
        form.Notes.Height = 72;
        form.Notes.Padding = new Thickness(Theme.S4);
        form.Notes.VerticalContentAlignment = VerticalAlignment.Top;
        form.Notes.MaxLength = 10000;
        Field(extras, "Notiz", form.Notes);
        var fieldsPanel = new StackPanel();
        Field(extras, "Eigene geschützte Felder", fieldsPanel);
        foreach (var field in existing?.Fields ?? new List<SecretField>()) AddField(form, fieldsPanel, field.Label, field.Value);
        var add = Button("Geschütztes Feld hinzufügen", () => { if (form.Fields.Count < 30) AddField(form, fieldsPanel, "", ""); });
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Margin = new Thickness(0, Theme.S4, 0, 0);
        extras.Children.Add(add);
        return new Expander
        {
            Header = "Weitere Angaben · Login-Seite, Notizen & eigene Felder", Content = extras, Margin = new Thickness(0, Theme.S7, 0, 0),
            IsExpanded = !string.IsNullOrEmpty(existing?.Website) || !string.IsNullOrEmpty(existing?.Notes) || existing?.Fields.Count > 0,
        };
    }

    static void AddField(AccountForm form, StackPanel panel, string label, string value)
    {
        var row = new StackPanel { Margin = new Thickness(0, 0, 0, Theme.S5) };
        var name = Input("Feldname", label);
        name.MaxLength = 80;
        var secret = Secret("Feldinhalt");
        secret.MaxLength = 10000;
        secret.Password = value;
        secret.Margin = new Thickness(0, Theme.S3, 0, Theme.S3);
        var remove = Ghost("Feld entfernen",
            () => { secret.Clear(); form.Fields.RemoveAll(f => f.Row == row); panel.Children.Remove(row); });
        remove.HorizontalAlignment = HorizontalAlignment.Left;
        row.Children.Add(name);
        row.Children.Add(secret);
        row.Children.Add(remove);
        panel.Children.Add(row);
        form.Fields.Add((name, secret, row));
    }

    bool SaveAccount(AccountForm form, Account? existing, bool duplicate)
    {
        if (string.IsNullOrWhiteSpace(form.Title.Text)) { form.Error.Text = "Bitte eine Bezeichnung eingeben."; return false; }
        if (!AccountQueries.ValidWebsite(form.Website.Text.Trim()))
        { form.Error.Text = "Website muss eine vollständige http- oder https-Adresse ohne Zugangsdaten sein."; return false; }
        if (form.Fields.Any(f => string.IsNullOrWhiteSpace(f.Label.Text)))
        { form.Error.Text = "Bitte alle geschützten Felder benennen oder leere Felder entfernen."; return false; }
        var item = new Account
        {
            Id = duplicate ? Guid.NewGuid() : existing?.Id ?? Guid.NewGuid(), Title = form.Title.Text.Trim(),
            Category = (string)form.Category.SelectedItem, Email = form.Email.Text.Trim(), Username = form.Username.Text.Trim(),
            Password = form.Password.Password, Website = form.Website.Text.Trim(), Notes = form.Notes.Text,
            Fields = form.Fields.Select(f => new SecretField { Label = f.Label.Text.Trim(), Value = f.Value.Password }).ToList(),
            Favorite = existing?.Favorite ?? false, Autofill = duplicate ? null : existing?.Autofill,
        };
        bool written = Mutate(d =>
        {
            d.Accounts.RemoveAll(a => a.Id == item.Id);
            d.Accounts.Add(item);
            foreach (var pair in form.Colors) d.CategoryColors[pair.Key] = pair.Value;
        });
        if (!written) { form.Error.Text = "Speichern fehlgeschlagen. Bitte Speicherplatz und Zugriffsrechte prüfen."; return false; }
        selected = item.Id;
        RefreshNavigation();
        RefreshAccounts();
        return true;
    }

    bool Dirty(AccountForm form, Account? existing)
    {
        static string Join(IEnumerable<(string Label, string Value)> fields) => string.Join("\u001F",
            fields.Select(f => f.Label + "\u001E" + f.Value));
        var initialFields = Join((existing?.Fields ?? new List<SecretField>()).Select(f => (f.Label, f.Value)));
        return form.Colors.Any(p => p.Value != CategoryColor(p.Key)) || form.Title.Text != form.InitialTitle
            || form.Category.SelectedItem as string != form.InitialCategory || form.Email.Text != (existing?.Email ?? "")
            || form.Username.Text != (existing?.Username ?? "") || form.Password.Password != (existing?.Password ?? "")
            || form.Website.Text != (existing?.Website ?? "") || form.Notes.Text != (existing?.Notes ?? "")
            || Join(form.Fields.Select(f => (f.Label.Text, f.Value.Password))) != initialFields;
    }
}

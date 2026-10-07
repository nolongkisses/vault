using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow
{
    void Edit(Account? existing, bool duplicate = false)
    {
        if (data == null) return;
        var form = new StackPanel();
        var intro = Text("Nur die Bezeichnung ist erforderlich.", 11, "#818A98");
        intro.Margin = new Thickness(8, 0, 8, 20); form.Children.Add(intro);
        StackPanel Section(string heading)
        {
            var content = new StackPanel { Margin = new Thickness(8, 0, 8, 0) };
            content.Children.Add(Text(heading.ToUpperInvariant(), 10, "#8D96A3", true));
            var section = new Border { Child = content, BorderBrush = Brush("#252A31"), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 18), Margin = new Thickness(0, 0, 0, 18) };
            form.Children.Add(section); return content;
        }
        var identity = Section("Account");
        string initialTitle = (existing?.Title ?? "") + (duplicate ? " – Kopie" : "");
        var title = Input("Bezeichnung", initialTitle); title.MaxLength = 120;
        string initialCategory = existing?.Category ?? category ?? data.Categories[0];
        var cat = new ComboBox { ItemsSource = data.Categories, SelectedItem = initialCategory };
        void Pair(Panel parent, string leftLabel, UIElement left, string rightLabel, UIElement right)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
            var a = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; var b = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            Field(a, leftLabel, left); Field(b, rightLabel, right);
            ((FrameworkElement)a.Children[0]).Margin = ((FrameworkElement)b.Children[0]).Margin = new Thickness(0, 9, 0, 7);
            ((TextBlock)a.Children[0]).Foreground = ((TextBlock)b.Children[0]).Foreground = Brush("#8D96A3");
            Grid.SetColumn(b, 1); row.Children.Add(a); row.Children.Add(b); parent.Children.Add(row);
        }
        Pair(identity, "Bezeichnung *", title, "Kategorie", cat);
        var categoryColors = new Dictionary<string, string>();
        var paletteHost = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var paletteDisclosure = new Expander { Header = "Kategoriefarbe", Content = paletteHost, Margin = new Thickness(0, 12, 0, 0) };
        paletteDisclosure.Foreground = Brush("#8D96A3");
        void RefreshPalette()
        {
            if (cat.SelectedItem is not string name) return;
            paletteHost.Children.Clear();
            paletteHost.Children.Add(ColorPalette(categoryColors.TryGetValue(name, out var value) ? value : CategoryColor(name), value => categoryColors[name] = value));
            paletteHost.Children.Add(Text("Gilt für alle Einträge dieser Kategorie.", 11, "#8D96A3"));
        }
        cat.SelectionChanged += (_, _) => RefreshPalette(); RefreshPalette(); identity.Children.Add(paletteDisclosure);
        var credentials = Section("Zugangsdaten");
        var email = Input("E-Mail", existing?.Email ?? "");
        var username = Input("Benutzername", existing?.Username ?? "");
        Pair(credentials, "E-Mail", email, "Benutzername", username);
        var password = Secret("Passwort"); password.Password = existing?.Password ?? "";
        var passwordHeader = new DockPanel { Margin = new Thickness(0, 10, 0, 7) };
        var generator = Button("Passwort generieren", () => ShowGenerator(password));
        generator.SetResourceReference(StyleProperty, "GhostButton"); generator.Foreground = Brush("#8D96A3");
        generator.Margin = new Thickness(0); generator.Padding = new Thickness(12, 6, 12, 6);
        generator.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(generator, Dock.Right);
        passwordHeader.Children.Add(generator); passwordHeader.Children.Add(Text("Passwort", 12, "#8D96A3")); credentials.Children.Add(passwordHeader);
        var passwordRow = EditorPassword(password, out var clearPassword);
        credentials.Children.Add(passwordRow);
        var extras = new StackPanel();
        var website = Input("Login-Seite", existing?.Website ?? ""); Field(extras, "Login-Seite (optional)", website);
        website.ToolTip = "Zum Beispiel https://account.riotgames.com";
        var websiteHelp = Text("Die Webadresse deiner Anmeldung. Damit öffnest du die Seite später direkt aus den Account-Details. Für Apps kannst du das Feld leer lassen.", 11, "#929CAA");
        websiteHelp.Margin = new Thickness(0, 7, 0, 0); extras.Children.Add(websiteHelp);
        var notes = Input("Notiz", existing?.Notes ?? ""); notes.AcceptsReturn = true; notes.Height = 70; notes.MaxLength = 10000; Field(extras, "Notiz", notes);
        var fields = new List<(TextBox label, PasswordBox value, FrameworkElement row)>();
        var fieldsPanel = new StackPanel(); Field(extras, "Eigene geschützte Felder", fieldsPanel);
        void AddField(string label, string value)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var name = Input("Feldname", label); name.MaxLength = 80;
            var secret = Secret("Feldinhalt"); secret.MaxLength = 10000; secret.Password = value;
            row.Children.Add(name); secret.Margin = new Thickness(0, 6, 0, 6); row.Children.Add(secret);
            var remove = Button("Feld entfernen", () => { secret.Clear(); fields.RemoveAll(f => f.row == row); fieldsPanel.Children.Remove(row); });
            remove.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(remove); fieldsPanel.Children.Add(row); fields.Add((name, secret, row));
        }
        foreach (var field in existing?.Fields ?? new List<SecretField>()) AddField(field.Label, field.Value);
        var addField = Button("+ Geschütztes Feld", () => { if (fields.Count < 30) AddField("", ""); }); addField.HorizontalAlignment = HorizontalAlignment.Left; extras.Children.Add(addField);
        var additional = new Expander { Header = "Weitere Angaben · Login-Seite, Notizen & eigene Felder", Content = extras, Foreground = Brush("#A5ABB5"),
            IsExpanded = !string.IsNullOrEmpty(existing?.Website) || !string.IsNullOrEmpty(existing?.Notes) || existing?.Fields.Count > 0 };
        additional.SetResourceReference(StyleProperty, "AccountEditorDisclosure"); form.Children.Add(additional);
        var error = Text("", 12, "#FF929A"); error.Margin = new Thickness(0, 12, 0, 0); form.Children.Add(error);
        var layout = new Grid { MaxHeight = 620 }; layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(new ScrollViewer { Content = form, Style = (Style)FindResource("OverlayScrollViewer") });
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var footer = new Border { Child = buttons, BorderBrush = Brush("#252A31"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16, 0, 0), Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(footer, 1); layout.Children.Add(footer);
        var w = new InAppDialog(this, duplicate ? "Account duplizieren" : existing == null ? "Account hinzufügen" : "Account bearbeiten", layout, 650, false);
        bool saved = false;
        string InitialFields() => string.Join("\u001F", (existing?.Fields ?? new List<SecretField>()).Select(f => f.Label + "\u001E" + f.Value));
        bool Dirty() => categoryColors.Any(p => p.Value != CategoryColor(p.Key)) || title.Text != initialTitle || cat.SelectedItem as string != initialCategory || email.Text != (existing?.Email ?? "") || username.Text != (existing?.Username ?? "") ||
            password.Password != (existing?.Password ?? "") || website.Text != (existing?.Website ?? "") || notes.Text != (existing?.Notes ?? "") || string.Join("\u001F", fields.Select(f => f.label.Text + "\u001E" + f.value.Password)) != InitialFields();
        var cancel = Button("Abbrechen", () => w.Close()); cancel.SetResourceReference(StyleProperty, "GhostButton"); cancel.Foreground = Brush("#8D96A3"); buttons.Children.Add(cancel);
        buttons.Children.Add(Button("Account speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { error.Text = "Bitte eine Bezeichnung eingeben."; return; }
            if (!AccountQueries.ValidWebsite(website.Text.Trim())) { error.Text = "Website muss eine vollständige http- oder https-Adresse ohne Zugangsdaten sein."; return; }
            if (fields.Any(f => string.IsNullOrWhiteSpace(f.label.Text))) { error.Text = "Bitte alle geschützten Felder benennen oder leere Felder entfernen."; return; }
            var item = new Account { Id = duplicate ? Guid.NewGuid() : existing?.Id ?? Guid.NewGuid(), Title = title.Text.Trim(), Category = (string)cat.SelectedItem,
                Email = email.Text.Trim(), Username = username.Text.Trim(), Password = password.Password, Website = website.Text.Trim(), Notes = notes.Text,
                Fields = fields.Select(f => new SecretField { Label = f.label.Text.Trim(), Value = f.value.Password }).ToList(), Favorite = existing?.Favorite ?? false,
                Autofill = duplicate ? null : existing?.Autofill };
            if (Mutate(d => { d.Accounts.RemoveAll(a => a.Id == item.Id); d.Accounts.Add(item); foreach (var pair in categoryColors) d.CategoryColors[pair.Key] = pair.Value; })) { selected = item.Id; RefreshNavigation(); RefreshAccounts(); saved = true; w.Close(); }
            else error.Text = "Speichern fehlgeschlagen. Bitte Speicherplatz und Zugriffsrechte prüfen.";
        }, true));
        w.Closing += (_, e) => { if (!saved && Dirty() && !Confirm(w, "Änderungen verwerfen?", "Die Änderungen an diesem Account wurden noch nicht gespeichert.")) e.Cancel = true; };
        w.Closed += (_, _) => { clearPassword(); email.Clear(); username.Clear(); notes.Clear(); foreach (var field in fields) field.value.Clear(); };
        w.ShowDialog();
    }
    FrameworkElement EditorPassword(PasswordBox secret, out Action clear, bool clipboardActions = true)
    {
        var panel = new StackPanel();
        var row = new Grid(); panel.Children.Add(row);
        var visible = Input("Passwort sichtbar"); visible.MaxLength = secret.MaxLength;
        visible.Visibility = Visibility.Collapsed;
        secret.Padding = visible.Padding = new Thickness(12, 8, clipboardActions ? 112 : 44, 8);
        row.Children.Add(secret); row.Children.Add(visible);
        var feedback = Text("", 11, "#929CAA"); feedback.Visibility = Visibility.Collapsed;
        feedback.Margin = new Thickness(0, 6, 0, 0); panel.Children.Add(feedback);
        void Report(string message) { feedback.Text = message; feedback.Visibility = Visibility.Visible; }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }; row.Children.Add(actions);
        bool syncing = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(prefs.RevealSeconds) };
        var eye = IconButton(EyeGeometry, "Passwort anzeigen", () => { });
        void Hide()
        {
            timer.Stop(); visible.Visibility = Visibility.Collapsed; secret.Visibility = Visibility.Visible;
            syncing = true; visible.Clear(); syncing = false;
            SetButtonIcon(eye, EyeGeometry, "Passwort anzeigen");
        }
        timer.Tick += (_, _) => Hide();
        eye.Click += (_, _) =>
        {
            if (visible.IsVisible) { Hide(); secret.Focus(); }
            else
            {
                syncing = true; visible.Text = secret.Password; syncing = false;
                secret.Visibility = Visibility.Collapsed; visible.Visibility = Visibility.Visible;
                SetButtonIcon(eye, EyeOffGeometry, "Passwort verbergen"); visible.Focus(); visible.CaretIndex = visible.Text.Length;
                timer.Start();
            }
        };
        var copy = IconButton(CopyGeometry, "Passwort kopieren", () =>
        {
            try { clipboard.ClearAfterSeconds = prefs.ClipboardSeconds; clipboard.Copy(secret.Password); Report("Kopiert · Zwischenablage wird nach " + prefs.ClipboardSeconds + " Sekunden geleert."); }
            catch (System.Runtime.InteropServices.COMException) { Report("Zwischenablage gerade belegt. Bitte erneut versuchen."); }
        });
        var paste = IconButton("M8,4 L4,4 L4,22 L20,22 L20,4 L16,4 M8,2 L16,2 L16,6 L8,6 Z M8,11 L16,11 M8,15 L16,15", "Passwort einfügen", () =>
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText()) { Report("Die Zwischenablage enthält keinen Text."); return; }
                string value = System.Windows.Clipboard.GetText();
                if (value.Length > secret.MaxLength) { Report("Das Passwort darf höchstens " + secret.MaxLength + " Zeichen enthalten."); return; }
                secret.Password = value; Report("Passwort eingefügt.");
            }
            catch (System.Runtime.InteropServices.COMException) { Report("Zwischenablage gerade belegt. Bitte erneut versuchen."); }
        });
        actions.Children.Add(eye); if (clipboardActions) { actions.Children.Add(copy); actions.Children.Add(paste); }
        secret.PasswordChanged += (_, _) =>
        {
            copy.IsEnabled = secret.Password.Length > 0;
            if (syncing || visible.Visibility != Visibility.Visible) return;
            syncing = true; visible.Text = secret.Password; syncing = false;
        };
        visible.TextChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true; secret.Password = visible.Text; syncing = false;
        };
        copy.IsEnabled = secret.Password.Length > 0;
        concealSecrets.Add(Hide);
        clear = () => { Hide(); secret.Clear(); concealSecrets.Remove(Hide); };
        return panel;
    }
    void ShowGenerator(PasswordBox target)
    {
        var form = new StackPanel(); var options = new GeneratorOptions();
        var length = new ComboBox { ItemsSource = new[] { 8, 12, 16, 20, 24, 32, 48, 64, 128 }, SelectedItem = 24 }; Field(form, "Länge", length);
        CheckBox Toggle(string name, bool value)
        {
            var box = new CheckBox { Content = name, IsChecked = value, Style = (Style)FindResource("RememberLogin"), Margin = new Thickness(0, 10, 0, 0) };
            form.Children.Add(box); return box;
        }
        var upper = Toggle("Großbuchstaben", true); var lower = Toggle("Kleinbuchstaben", true); var digits = Toggle("Ziffern", true); var symbols = Toggle("Sonderzeichen", true); var similar = Toggle("Ähnliche Zeichen vermeiden", true);
        var result = Secret("Erzeugtes Passwort"); result.MaxLength = 128; Field(form, "Erzeugtes Passwort", EditorPassword(result, out var clearResult));
        var error = Text("", 12, "#AAAAAA"); error.Margin = new Thickness(0, 12, 0, 12); form.Children.Add(error);
        var w = Dialog(this, "Passwortgenerator", form, 440);
        var actions = new WrapPanel(); form.Children.Add(actions);
        var use = Button("Übernehmen", () => { target.Password = result.Password; w.Close(); }, true);
        void Generate()
        {
            options.Length = (int)length.SelectedItem; options.Upper = upper.IsChecked == true; options.Lower = lower.IsChecked == true;
            options.Digits = digits.IsChecked == true; options.Symbols = symbols.IsChecked == true; options.AvoidSimilar = similar.IsChecked == true;
            try { result.Password = options.Generate(); use.IsEnabled = true; error.Text = options.Length + " Zeichen · alle gewählten Gruppen enthalten"; }
            catch (ArgumentException ex) { result.Clear(); use.IsEnabled = false; error.Text = ex.Message; }
        }
        actions.Children.Add(Button("Neu erzeugen", Generate)); actions.Children.Add(use);
        length.SelectionChanged += (_, _) => Generate();
        foreach (var box in new[] { upper, lower, digits, symbols, similar }) box.Click += (_, _) => Generate();
        w.Closed += (_, _) => clearResult(); Generate(); w.ShowDialog();
    }
}

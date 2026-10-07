using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static NoxVault.Elements;

namespace NoxVault;

// A password field that can be shown for a few seconds, copied and pasted; both controls always hold the same value.
internal sealed class PasswordEditor : StackPanel
{
    internal PasswordEditor(PasswordBox secret, TextBox visible, Button reveal)
    {
        Secret = secret;
        Visible = visible;
        Reveal = reveal;
    }

    internal PasswordBox Secret { get; }
    internal TextBox Visible { get; }
    internal Button Reveal { get; }
    internal Action Clear { get; set; } = () => { };
}

internal sealed partial class MainWindow
{
    PasswordEditor EditorPassword(PasswordBox secret, bool clipboardActions = true)
    {
        var visible = Input("Passwort sichtbar");
        visible.MaxLength = secret.MaxLength;
        visible.Visibility = Visibility.Collapsed;
        secret.Padding = visible.Padding = new Thickness(Theme.S4, 0, clipboardActions ? Theme.S9 * 2 + Theme.S5 : Theme.S9, 0);
        var eye = IconButton(Icons.Eye, "Passwort anzeigen", () => { }, small: true);
        var editor = new PasswordEditor(secret, visible, eye);
        var row = new Grid { Children = { secret, visible } };
        editor.Children.Add(row);
        var feedback = Text("", TextRole.Small, Theme.Sub);
        feedback.Visibility = Visibility.Collapsed;
        feedback.Margin = new Thickness(0, Theme.S3, 0, 0);
        editor.Children.Add(feedback);
        void Report(string message) { feedback.Text = message; feedback.Visibility = Visibility.Visible; }
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Theme.S1, 0),
        };
        row.Children.Add(actions);
        actions.Children.Add(eye);
        var copy = IconButton(Icons.Copy, "Passwort kopieren", () => CopyEditorPassword(secret, Report), small: true);
        if (clipboardActions)
        {
            actions.Children.Add(copy);
            actions.Children.Add(IconButton(Icons.Paste, "Passwort einfügen", () => PastePassword(secret, Report), small: true));
        }
        BindReveal(editor, copy);
        return editor;
    }

    void BindReveal(PasswordEditor editor, Button copy)
    {
        var (secret, visible, eye) = (editor.Secret, editor.Visible, editor.Reveal);
        bool syncing = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(prefs.RevealSeconds) };
        void Hide()
        {
            timer.Stop();
            visible.Visibility = Visibility.Collapsed;
            secret.Visibility = Visibility.Visible;
            syncing = true; visible.Clear(); syncing = false;
            SetIcon(eye, Icons.Eye, "Passwort anzeigen");
        }
        timer.Tick += (_, _) => Hide();
        eye.Click += (_, _) =>
        {
            if (visible.IsVisible) { Hide(); secret.Focus(); return; }
            syncing = true; visible.Text = secret.Password; syncing = false;
            secret.Visibility = Visibility.Collapsed;
            visible.Visibility = Visibility.Visible;
            SetIcon(eye, Icons.EyeOff, "Passwort verbergen");
            visible.Focus();
            visible.CaretIndex = visible.Text.Length;
            timer.Start();
        };
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
        editor.Clear = () => { Hide(); secret.Clear(); concealSecrets.Remove(Hide); };
    }

    void CopyEditorPassword(PasswordBox secret, Action<string> report)
    {
        try
        {
            clipboard.ClearAfterSeconds = prefs.ClipboardSeconds;
            clipboard.Copy(secret.Password);
            report("Kopiert · Zwischenablage wird nach " + prefs.ClipboardSeconds + " Sekunden geleert.");
        }
        catch (System.Runtime.InteropServices.COMException) { report("Zwischenablage gerade belegt. Bitte erneut versuchen."); }
    }

    static void PastePassword(PasswordBox secret, Action<string> report)
    {
        try
        {
            if (!Clipboard.ContainsText()) { report("Die Zwischenablage enthält keinen Text."); return; }
            string value = Clipboard.GetText();
            if (value.Length > secret.MaxLength)
            {
                report("Das Passwort darf höchstens " + secret.MaxLength
                + " Zeichen enthalten."); return;
            }
            secret.Password = value;
            report("Passwort eingefügt.");
        }
        catch (System.Runtime.InteropServices.COMException) { report("Zwischenablage gerade belegt. Bitte erneut versuchen."); }
    }

    void ShowGenerator(PasswordBox target)
    {
        var form = new StackPanel();
        var options = new GeneratorOptions();
        var length = new ComboBox { ItemsSource = new[] { 8, 12, 16, 20, 24, 32, 48, 64, 128 }, SelectedItem = 24 };
        Field(form, "Länge", length);
        CheckBox Toggle(string name)
        {
            var box = new CheckBox { Content = name, IsChecked = true, Margin = new Thickness(0, Theme.S4, 0, 0) };
            form.Children.Add(box);
            return box;
        }
        var groups = new[] { Toggle("Großbuchstaben"), Toggle("Kleinbuchstaben"), Toggle("Ziffern"), Toggle("Sonderzeichen"),
            Toggle("Ähnliche Zeichen vermeiden") };
        var result = Secret("Erzeugtes Passwort");
        result.MaxLength = 128;
        var editor = EditorPassword(result);
        Field(form, "Erzeugtes Passwort", editor);
        var info = Text("", TextRole.Small, Theme.Sub);
        info.Margin = new Thickness(0, Theme.S5, 0, 0);
        form.Children.Add(info);
        var dialog = InAppDialog.Create(this, "Passwortgenerator", form, 440);
        var use = Primary("Übernehmen", () => { target.Password = result.Password; dialog.Close(); });
        void Generate()
        {
            options.Length = (int)length.SelectedItem;
            (options.Upper, options.Lower, options.Digits, options.Symbols, options.AvoidSimilar) =
                (groups[0].IsChecked == true, groups[1].IsChecked == true, groups[2].IsChecked == true, groups[3].IsChecked == true,
                    groups[4].IsChecked == true);
            try
            {
                result.Password = options.Generate(); use.IsEnabled = true; info.Text = options.Length
                + " Zeichen · alle gewählten Gruppen enthalten";
            }
            catch (ArgumentException ex) { result.Clear(); use.IsEnabled = false; info.Text = ex.Message; }
        }
        form.Children.Add(InAppDialog.Actions(Button("Neu erzeugen", Generate), use));
        length.SelectionChanged += (_, _) => Generate();
        foreach (var box in groups) box.Click += (_, _) => Generate();
        dialog.Closed += (_, _) => editor.Clear();
        Generate();
        dialog.ShowDialog();
    }
}

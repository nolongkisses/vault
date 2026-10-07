using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using static NoxVault.Elements;

namespace NoxVault;

// The selected account: header with actions, one surface of credential rows, and the note.
internal sealed partial class MainWindow
{
    const string Masked = "••••••••••••";
    StackPanel detail = new();
    readonly List<DetailRow> detailRows = new();

    void ShowDetail()
    {
        ConcealSecrets();
        detail.Children.Clear();
        detailRows.Clear();
        var account = Data?.Accounts.FirstOrDefault(a => a.Id == selected && a.DeletedUtc == null);
        if (account == null) { detail.Children.Add(EmptyDetail()); return; }
        detail.Children.Add(DetailHeader(account));
        var rows = new StackPanel();
        rows.Children.Add(Credential("E-Mail", account.Email));
        rows.Children.Add(Credential("Benutzername", account.Username));
        rows.Children.Add(Credential("Passwort", account.Password, secret: true));
        if (!string.IsNullOrWhiteSpace(account.Website)) rows.Children.Add(Website(account.Website));
        foreach (var field in account.Fields) rows.Children.Add(Credential(field.Label, field.Value, secret: true));
        detail.Children.Add(Surface(rows, Theme.Raise1, Theme.S2));
        if (!string.IsNullOrWhiteSpace(account.Notes)) detail.Children.Add(Note(account.Notes));
    }

    static StackPanel EmptyDetail()
    {
        var empty = new StackPanel { Margin = new Thickness(0, Theme.S6, 0, 0) };
        empty.Children.Add(Text("Platz für deine Zugangsdaten.", TextRole.Title, bold: true));
        var help = Text("Wähle links einen Account aus oder füge einen neuen hinzu. Deine Zugangsdaten bleiben lokal verschlüsselt.",
            TextRole.Body, Theme.Sub);
        help.Margin = new Thickness(0, Theme.S4, 0, 0);
        empty.Children.Add(help);
        return empty;
    }

    Grid DetailHeader(Account account)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, Theme.S6) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel { Margin = new Thickness(0, 0, Theme.S5, 0) };
        var meta = new StackPanel { Orientation = Orientation.Horizontal };
        meta.Children.Add(Dot(CategoryColor(account.Category), Theme.S3));
        var categoryName = Text(account.Category, TextRole.Small, Theme.Sub);
        categoryName.Margin = new Thickness(Theme.S3, 0, 0, 0);
        meta.Children.Add(categoryName);
        labels.Children.Add(meta);
        var title = Text(account.Title, TextRole.Head, bold: true);
        title.Margin = new Thickness(0, Theme.S2, 0, Theme.S1);
        labels.Children.Add(title);
        labels.Children.Add(Text("Zuletzt geändert " + account.Updated.ToString("dd.MM.yyyy"), TextRole.Micro, Theme.Faint));
        header.Children.Add(labels);
        var actions = DetailActions(account);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        return header;
    }

    StackPanel DetailActions(Account account)
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var star = IconButton(account.Favorite ? Icons.StarFilled : Icons.Star, account.Favorite ? "Favorit entfernen" : "Als Favorit",
            () => Mutate(d => d.Accounts.First(item => item.Id == account.Id).Favorite = !account.Favorite));
        actions.Children.Add(star);
        var edit = Button("Bearbeiten", () => Edit(account));
        edit.Margin = new Thickness(Theme.S2, 0, Theme.S2, 0);
        actions.Children.Add(edit);
        actions.Children.Add(CreateAccountMenu(account));
        return actions;
    }

    void DeleteAccount(Account account)
    {
        if (InAppDialog.Confirm(this, "In den Papierkorb?", "„" + account.Title + "“ wird 30 Tage verschlüsselt im Papierkorb aufbewahrt."))
            Mutate(d => d.Accounts.First(item => item.Id == account.Id).DeletedUtc = DateTime.UtcNow);
    }

    DetailRow Credential(string label, string value, bool secret = false)
    {
        var row = new DetailRow(label, value.Length == 0 ? "Nicht hinterlegt" : secret ? Masked : value, value.Length == 0);
        if (secret) row.Actions.Children.Add(RevealButton(row, value));
        var copyFeedback = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        Button copy = IconButton(Icons.Copy, label + " kopieren", () => { });
        copy.Click += (_, _) => Copy(label, value, copy, copyFeedback);
        copy.IsEnabled = value.Length > 0;
        void ResetCopy() { copyFeedback.Stop(); SetIcon(copy, Icons.Copy, label + " kopieren"); }
        copyFeedback.Tick += (_, _) => ResetCopy();
        concealSecrets.Add(ResetCopy);
        row.Actions.Children.Add(copy);
        detailRows.Add(row);
        return row;
    }

    Button RevealButton(DetailRow row, string value)
    {
        var show = IconButton(Icons.Eye, "Passwort anzeigen", () => { });
        bool revealed = false;
        var concealTimer = new DispatcherTimer();
        void HideSecret()
        {
            concealTimer.Stop();
            revealed = false;
            row.Value.Text = value.Length == 0 ? "Nicht hinterlegt" : Masked;
            SetIcon(show, Icons.Eye, "Passwort anzeigen");
        }
        concealTimer.Tick += (_, _) => HideSecret();
        concealSecrets.Add(HideSecret);
        show.Click += (_, _) =>
        {
            revealed = !revealed;
            row.Value.Text = revealed ? value : Masked;
            SetIcon(show, revealed ? Icons.EyeOff : Icons.Eye, revealed ? "Passwort verbergen" : "Passwort anzeigen");
            concealTimer.Stop();
            if (revealed) { concealTimer.Interval = TimeSpan.FromSeconds(prefs.RevealSeconds); concealTimer.Start(); }
        };
        show.IsEnabled = value.Length > 0;
        return show;
    }

    void Copy(string label, string value, Button copy, DispatcherTimer feedback)
    {
        try
        {
            clipboard.ClearAfterSeconds = prefs.ClipboardSeconds;
            clipboard.Copy(value);
            SetIcon(copy, Icons.Check, label + " kopiert");
            feedback.Stop();
            feedback.Start();
            status.Text = label + " kopiert · Wird nach " + prefs.ClipboardSeconds + " Sekunden automatisch entfernt.";
        }
        catch (System.Runtime.InteropServices.COMException) { status.Text = "Zwischenablage gerade belegt. Bitte erneut versuchen."; }
    }

    DetailRow Website(string address)
    {
        var row = new DetailRow("Login-Seite", address, missing: false);
        row.Actions.Children.Add(IconButton(Icons.OpenExternal, "Website öffnen", () => OpenWebsite(address)));
        return row;
    }

    void OpenWebsite(string address)
    {
        if (!AccountQueries.ValidWebsite(address))
        {
            status.Text = "Nur gültige http- oder https-Adressen können geöffnet werden.";
            return;
        }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { status.Text = "Website konnte nicht geöffnet werden."; }
    }

    static Border Note(string notes)
    {
        var content = new StackPanel();
        content.Children.Add(Text("Notiz", TextRole.Small, Theme.Faint));
        var text = Text(notes, TextRole.Body, Theme.Fg);
        text.Margin = new Thickness(0, Theme.S2, 0, 0);
        content.Children.Add(text);
        var note = Surface(content, Theme.Raise1, Theme.S5);
        note.Margin = new Thickness(0, Theme.S5, 0, 0);
        return note;
    }
}

// One credential line: label above the value, actions on the right, hover one step up. Timers and clipboard stay in MainWindow.
internal sealed class DetailRow : Grid
{
    internal DetailRow(string label, string value, bool missing)
    {
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hover = new Border { CornerRadius = new CornerRadius(Theme.Radius - Theme.S2), Opacity = 0 };
        hover.SetResourceReference(Border.BackgroundProperty, Theme.Raise2);
        SetColumnSpan(hover, 2);
        Children.Add(hover);
        var text = new StackPanel { Margin = new Thickness(Theme.S4, Theme.S3, Theme.S4, Theme.S3) };
        text.Children.Add(Line(label, TextRole.Small, Theme.Faint));
        Value = Line(value, TextRole.Body, missing ? Theme.Faint : Theme.Fg);
        Value.Margin = new Thickness(0, Theme.S1, 0, 0);
        text.Children.Add(Value);
        Children.Add(text);
        Actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Actions.Margin = new Thickness(0, 0, Theme.S2, 0);
        SetColumn(Actions, 1);
        Children.Add(Actions);
        AutomationProperties.SetName(this, label);
        MouseEnter += (_, _) => Motion.Fade(hover, 1);
        MouseLeave += (_, _) => Motion.Fade(hover, 0);
    }

    internal TextBlock Value { get; }
    internal StackPanel Actions { get; }
}

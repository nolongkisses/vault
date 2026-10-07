using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow
{
    void ShowTrash()
    {
        if (data == null) return;
        var form = new StackPanel();
        form.Children.Add(Text("Gelöschte Accounts bleiben 30 Tage verschlüsselt erhalten. Danach werden sie beim nächsten Entsperren oder Speichern entfernt. Ältere Sicherungen können sie weiterhin enthalten.", 12, "#AAAAAA"));
        var results = new StackPanel { Margin = new Thickness(0, 18, 0, 0) }; form.Children.Add(results);
        var feedback = SettingsView.Feedback(form);
        var w = Dialog(this, "Papierkorb", form, 580); int page = 0;
        void Refresh()
        {
            results.Children.Clear(); if (data == null) return;
            var items = data.Accounts.Where(a => a.DeletedUtc.HasValue).OrderByDescending(a => a.DeletedUtc).ToList();
            page = Math.Clamp(page, 0, Math.Max(0, (items.Count - 1) / 50));
            if (items.Count == 0) { results.Children.Add(Text("Der Papierkorb ist leer.", 14, "#AAAAAA")); return; }
            foreach (var a in items.Skip(page * 50).Take(50))
            {
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
                row.Children.Add(Text(a.Title, 15, bold: true));
                row.Children.Add(Text("Entfernung ab " + a.DeletedUtc!.Value.AddDays(30).ToLocalTime().ToString("dd.MM.yyyy HH:mm"), 11, "#999999"));
                var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; row.Children.Add(actions);
                actions.Children.Add(Button("Wiederherstellen", () =>
                {
                    if (Mutate(d => d.Accounts.First(x => x.Id == a.Id).DeletedUtc = null)) { feedback.Text = "Account wiederhergestellt."; Refresh(); }
                    else feedback.Text = status.Text;
                }));
                var delete = Button("Endgültig löschen", () =>
                {
                    if (!Confirm(w, "Endgültig löschen?", "„" + a.Title + "“ wird aus diesem Tresor endgültig entfernt. Vorhandene Sicherungen bleiben unverändert.")) return;
                    if (Mutate(d => d.Accounts.RemoveAll(x => x.Id == a.Id))) { feedback.Text = "Account endgültig gelöscht."; Refresh(); }
                    else feedback.Text = status.Text;
                }); delete.Foreground = Brush("#F49098"); actions.Children.Add(delete); results.Children.Add(row);
            }
            if (items.Count > 50)
            {
                var paging = new WrapPanel();
                var back = Button("Zurück", () => { page--; Refresh(); }); back.IsEnabled = page > 0; paging.Children.Add(back);
                paging.Children.Add(Text($"{page + 1} / {(items.Count + 49) / 50}  ", 12));
                var next = Button("Weiter", () => { page++; Refresh(); }); next.IsEnabled = (page + 1) * 50 < items.Count; paging.Children.Add(next); results.Children.Add(paging);
            }
        }
        Refresh(); w.ShowDialog();
    }
    void ShowPasswordCheck()
    {
        if (data == null) return;
        var form = new StackPanel();
        form.Children.Add(Text("Prüft lokal auf leere und mehrfach verwendete Passwörter. Es werden keine Passwörter übertragen oder in dieser Liste angezeigt. Das ist keine vollständige Sicherheitsbewertung.", 12, "#AAAAAA"));
        var results = new StackPanel { Margin = new Thickness(0, 18, 0, 0) }; form.Children.Add(results);
        var issues = AccountQueries.PasswordIssues(data); var w = Dialog(this, "Passwörter prüfen", form, 550); int page = 0;
        void Refresh()
        {
            results.Children.Clear();
            if (issues.Count == 0) { results.Children.Add(Text("Keine leeren oder mehrfach verwendeten Passwörter gefunden.", 14)); return; }
            results.Children.Add(Text(issues.Count + " Accounts mit Hinweisen", 14, bold: true));
            foreach (var issue in issues.Skip(page * 50).Take(50))
            {
                var button = Button(issue.title + " · " + issue.issue, () => { w.Close(); foreach (var dialog in dialogs.ToArray().Reverse()) { dialog.Close(); if (dialogs.Contains(dialog)) return; } if (data == null) return; category = null; favorites = false; query = ""; search.Text = ""; selected = issue.id; RefreshNavigation(); RefreshAccounts(); });
                button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0, 8, 0, 0); results.Children.Add(button);
            }
            if (issues.Count > 50)
            {
                var paging = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
                var back = Button("Zurück", () => { page--; Refresh(); }); back.IsEnabled = page > 0; paging.Children.Add(back);
                var next = Button("Weiter", () => { page++; Refresh(); }); next.IsEnabled = (page + 1) * 50 < issues.Count; paging.Children.Add(next); results.Children.Add(paging);
            }
        }
        Refresh(); w.Closed += (_, _) => issues.Clear(); w.ShowDialog();
    }
}

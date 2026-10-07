using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// Lists accounts with an empty or reused password; passwords themselves are never shown here.
internal sealed partial class MainWindow
{
    void ShowPasswordCheck()
    {
        if (Data == null) return;
        var form = new StackPanel();
        form.Children.Add(Text("Prüft lokal auf leere und mehrfach verwendete Passwörter. Es werden keine Passwörter übertragen oder in "
            + "dieser Liste angezeigt. Das ist keine vollständige Sicherheitsbewertung.", TextRole.Small, Theme.Sub));
        var results = new StackPanel { Margin = new Thickness(0, Theme.S6, 0, 0) };
        form.Children.Add(results);
        var issues = AccountQueries.PasswordIssues(Data);
        var dialog = InAppDialog.Create(this, "Passwörter prüfen", form, 550);
        int page = 0;
        void Refresh()
        {
            results.Children.Clear();
            if (issues.Count == 0) { results.Children.Add(Text("Keine leeren oder mehrfach verwendeten Passwörter gefunden.")); return; }
            results.Children.Add(Text(issues.Count + " Accounts mit Hinweisen", TextRole.Title, bold: true));
            foreach (var issue in issues.Skip(page * TrashPageSize).Take(TrashPageSize))
            {
                var row = Button("", () => OpenIssue(issue.Id, dialog), "ListRow");
                var line = new DockPanel();
                var reason = Text(issue.Issue, TextRole.Small, Theme.Sub);
                DockPanel.SetDock(reason, Dock.Right);
                line.Children.Add(reason);
                line.Children.Add(Line(issue.Title));
                row.Content = line;
                System.Windows.Automation.AutomationProperties.SetName(row, issue.Title + " · " + issue.Issue);
                results.Children.Add(row);
            }
            if (issues.Count > TrashPageSize) results.Children.Add(Pager(page, issues.Count, TrashPageSize,
                delta => { page += delta; Refresh(); }));
        }
        Refresh();
        dialog.Closed += (_, _) => issues.Clear();
        dialog.ShowDialog();
    }

    // Closes every dialog above the vault, then selects the account in "Alle Zugangsdaten".
    void OpenIssue(Guid id, InAppDialog dialog)
    {
        dialog.Close();
        foreach (var open in dialogs.ToArray().Reverse())
        {
            open.Close();
            if (dialogs.Contains(open)) return;
        }
        if (Data == null) return;
        category = null;
        favorites = false;
        query = "";
        search.Text = "";
        selected = id;
        RefreshNavigation();
        RefreshAccounts();
    }
}

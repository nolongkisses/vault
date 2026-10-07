using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// Deleted accounts stay encrypted for 30 days; here they can be restored or removed for good.
internal sealed partial class MainWindow
{
    const int TrashPageSize = 50;

    void ShowTrash()
    {
        if (Data == null) return;
        var form = new StackPanel();
        form.Children.Add(Text("Gelöschte Accounts bleiben 30 Tage verschlüsselt erhalten. Danach werden sie beim nächsten Entsperren "
            + "oder Speichern entfernt. Ältere Sicherungen können sie weiterhin enthalten.", TextRole.Small, Theme.Sub));
        var results = new StackPanel { Margin = new Thickness(0, Theme.S6, 0, 0) };
        form.Children.Add(results);
        var feedback = SettingsView.Feedback(form);
        var dialog = InAppDialog.Create(this, "Papierkorb", form, 580);
        int page = 0;
        void Refresh()
        {
            results.Children.Clear();
            if (Data == null) return;
            var items = Data.Accounts.Where(a => a.DeletedUtc.HasValue).OrderByDescending(a => a.DeletedUtc).ToList();
            page = Math.Clamp(page, 0, Math.Max(0, (items.Count - 1) / TrashPageSize));
            if (items.Count == 0) { results.Children.Add(Text("Der Papierkorb ist leer.", TextRole.Body, Theme.Sub)); return; }
            foreach (var account in items.Skip(page * TrashPageSize).Take(TrashPageSize))
                results.Children.Add(TrashRow(account, dialog, feedback, Refresh));
            if (items.Count > TrashPageSize)
                results.Children.Add(Pager(page, items.Count, TrashPageSize, delta => { page += delta; Refresh(); }));
        }
        Refresh();
        dialog.ShowDialog();
    }

    Border TrashRow(Account account, InAppDialog dialog, TextBlock feedback, Action refresh)
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel();
        labels.Children.Add(Line(account.Title, TextRole.Body, bold: true));
        var deletedAt = account.DeletedUtc.GetValueOrDefault();
        labels.Children.Add(Text("Entfernung ab " + deletedAt.AddDays(30).ToLocalTime().ToString("dd.MM.yyyy HH:mm"), TextRole.Small,
            Theme.Faint));
        content.Children.Add(labels);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var restore = Button("Wiederherstellen", () =>
        {
            feedback.Text = Mutate(d => d.Accounts.First(x => x.Id == account.Id).DeletedUtc = null)
                ? "Account wiederhergestellt." : status.Text;
            refresh();
        });
        // On a raise1 row the button sits one step brighter, otherwise it would vanish into the row.
        restore.SetResourceReference(BackgroundProperty, Theme.Raise2);
        restore.SetResourceReference(Visuals.HoverProperty, Theme.Raise3);
        actions.Children.Add(restore);
        var delete = Ghost("Endgültig löschen", () =>
        {
            if (!InAppDialog.Confirm(dialog, "Endgültig löschen?",
                "„" + account.Title + "“ wird aus diesem Tresor endgültig entfernt. Vorhandene Sicherungen bleiben unverändert.")) return;
            feedback.Text = Mutate(d => d.Accounts.RemoveAll(x => x.Id == account.Id)) ? "Account endgültig gelöscht." : status.Text;
            refresh();
        });
        delete.FontWeight = FontWeights.Bold;
        delete.Margin = new Thickness(Theme.S2, 0, 0, 0);
        actions.Children.Add(delete);
        Grid.SetColumn(actions, 1);
        content.Children.Add(actions);
        var row = Surface(content, Theme.Raise1, Theme.S5);
        row.Margin = new Thickness(0, 0, 0, Theme.S2);
        return row;
    }

    static StackPanel Pager(int page, int total, int size, Action<int> go)
    {
        var paging = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Theme.S5, 0, 0) };
        var back = Button("Zurück", () => go(-1));
        back.IsEnabled = page > 0;
        var label = Text($"{page + 1} / {(total + size - 1) / size}", TextRole.Small, Theme.Sub);
        label.Margin = new Thickness(Theme.S4, 0, Theme.S4, 0);
        var next = Button("Weiter", () => go(1));
        next.IsEnabled = (page + 1) * size < total;
        paging.Children.Add(back);
        paging.Children.Add(label);
        paging.Children.Add(next);
        return paging;
    }
}

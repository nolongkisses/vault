using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using static NoxVault.Elements;

namespace NoxVault;

// Search field and the account list, at most one page of 100 rows at a time.
internal sealed partial class MainWindow
{
    const int PageSize = 100;
    StackPanel accountList = new();
    TextBox search = new();
    ScrollViewer? accountScroller;
    Guid? selected;
    string? category;
    bool favorites;
    int accountPage;
    string query = "";

    Grid ListPane()
    {
        var pane = new Grid();
        pane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        pane.RowDefinitions.Add(new RowDefinition());
        pane.Children.Add(SearchField());
        accountList = new StackPanel();
        accountScroller = new ScrollViewer { Content = accountList, Margin = new Thickness(0, Theme.S5, 0, 0) };
        accountScroller.SetResourceReference(StyleProperty, "GutterScrollViewer");
        accountScroller.Margin = new Thickness(0, Theme.S5, -Theme.S6, 0);
        Grid.SetRow(accountScroller, 1);
        pane.Children.Add(accountScroller);
        return pane;
    }

    Grid SearchField()
    {
        search = Input("Zugangsdaten durchsuchen", query);
        search.Padding = new Thickness(Theme.S8, 0, Theme.S8, 0);
        search.ToolTip = "Nach Name, E-Mail, Benutzername, Kategorie oder Website suchen";
        var area = new Grid { Children = { search } };
        var magnifier = Icon(Icons.Search, Theme.Size(TextRole.Body), Theme.Faint);
        magnifier.HorizontalAlignment = HorizontalAlignment.Left;
        magnifier.Margin = new Thickness(Theme.S4, 0, 0, 0);
        magnifier.IsHitTestVisible = false;
        area.Children.Add(magnifier);
        var placeholder = Line("Zugangsdaten suchen …", TextRole.Body, Theme.Faint);
        placeholder.Margin = new Thickness(Theme.S8, 0, Theme.S8, 0);
        placeholder.IsHitTestVisible = false;
        area.Children.Add(placeholder);
        var clear = IconButton(Icons.Close, "Suche leeren", () => { search.Clear(); search.Focus(); }, small: true);
        clear.HorizontalAlignment = HorizontalAlignment.Right;
        clear.Margin = new Thickness(0, 0, Theme.S1, 0);
        area.Children.Add(clear);
        void UpdateHint()
        {
            placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            clear.Visibility = search.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        search.TextChanged += (_, _) =>
        {
            UpdateHint();
            query = search.Text;
            accountPage = 0;
            selected = null;
            accountScroller?.ScrollToTop();
            searchDelay.Stop();
            searchDelay.Start();
        };
        UpdateHint();
        return area;
    }

    void SelectCategory(string? value, bool favourites)
    {
        category = value;
        favorites = favourites;
        accountPage = 0;
        selected = null;
        RefreshNavigation();
        RefreshAccounts();
        accountScroller?.ScrollToTop();
    }

    void FocusSearch()
    {
        FocusManager.SetFocusedElement(this, search);
        search.Focus();
        search.SelectAll();
    }

    void SelectAccount(Guid id)
    {
        if (selected == id) return;
        selected = id;
        foreach (var button in accountList.Children.OfType<Button>())
            Visuals.SetIsSelected(button, button.Tag is Guid entryId && entryId == id);
        ShowDetail();
    }

    void RefreshAccounts()
    {
        searchDelay.Stop();
        if (Data == null) return;
        heading.Text = favorites ? "Favoriten" : category ?? "Alle Zugangsdaten";
        var accounts = AccountQueries.Filter(Data, category, favorites, query, prefs.AccountSort);
        count.Text = accounts.Count.ToString();
        AutomationProperties.SetName(count, accounts.Count == 1 ? "1 Eintrag" : $"{accounts.Count} Einträge");
        int selectedIndex = accounts.FindIndex(a => a.Id == selected);
        if (selectedIndex >= 0) accountPage = selectedIndex / PageSize;
        accountPage = Math.Clamp(accountPage, 0, Math.Max(0, (accounts.Count - 1) / PageSize));
        if (selectedIndex < 0) selected = accounts.Skip(accountPage * PageSize).FirstOrDefault()?.Id;
        accountList.Children.Clear();
        if (accounts.Count == 0) accountList.Children.Add(EmptyList());
        foreach (var account in accounts.Skip(accountPage * PageSize).Take(PageSize)) accountList.Children.Add(AccountRow(account));
        if (accounts.Count > PageSize) accountList.Children.Add(Paging(accounts.Count));
        ShowDetail();
    }

    StackPanel EmptyList()
    {
        bool searching = !string.IsNullOrWhiteSpace(query);
        var empty = new StackPanel { Margin = new Thickness(Theme.S5, Theme.S6, Theme.S5, 0) };
        empty.Children.Add(Text(searching ? "Keine Treffer." : "Noch ganz für dich.", TextRole.Title, bold: true));
        var description = Text(searching ? "Versuche einen anderen Suchbegriff."
            : "Lege deine ersten Zugangsdaten an. E-Mail, Benutzername und Passwort sind dann nur einen Klick entfernt.",
                TextRole.Body, Theme.Sub);
        description.Margin = new Thickness(0, Theme.S4, 0, Theme.S6);
        empty.Children.Add(description);
        var action = searching ? Button("Suche zurücksetzen", () => search.Clear()) : Primary("Hinzufügen", () => Edit(null));
        action.HorizontalAlignment = HorizontalAlignment.Left;
        empty.Children.Add(action);
        return empty;
    }

    Button AccountRow(Account account)
    {
        var content = new StackPanel();
        var titleLine = new DockPanel();
        var dot = Dot(CategoryColor(account.Category), Theme.S3);
        dot.Margin = new Thickness(0, 0, Theme.S4, 0);
        titleLine.Children.Add(dot);
        if (account.Favorite)
        {
            var star = Icon(Icons.StarFilled, Theme.Size(TextRole.Small), Theme.Faint);
            DockPanel.SetDock(star, Dock.Right);
            titleLine.Children.Add(star);
        }
        titleLine.Children.Add(Line(account.Title, TextRole.Body, bold: true));
        content.Children.Add(titleLine);
        var login = Line(string.IsNullOrEmpty(account.Username) ? account.Email : account.Username, TextRole.Small, Theme.Sub);
        login.Margin = new Thickness(Theme.S6 - Theme.S1, Theme.S1, 0, 0);
        content.Children.Add(login);
        var row = Button("", () => SelectAccount(account.Id), "ListRow");
        row.Tag = account.Id;
        row.Content = content;
        Visuals.SetIsSelected(row, account.Id == selected);
        AutomationProperties.SetName(row, "Account " + account.Title);
        AccountDrag.Source(row, account.Id, account.Title,
            () => !busy && dialogs.Count == 0 && Data?.Accounts.Any(item => item.Id == account.Id && item.DeletedUtc == null) == true);
        return row;
    }

    StackPanel Paging(int total)
    {
        var paging = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Theme.S4, 0, 0) };
        void Go(int delta) { accountPage += delta; selected = null; RefreshAccounts(); accountScroller?.ScrollToTop(); }
        var back = IconButton(Icons.ChevronLeft, "Vorherige Seite", () => Go(-1));
        back.IsEnabled = accountPage > 0;
        var next = IconButton(Icons.ChevronRight, "Nächste Seite", () => Go(1));
        next.IsEnabled = (accountPage + 1) * PageSize < total;
        var label = Text($"{accountPage + 1} / {(total + PageSize - 1) / PageSize}", TextRole.Small, Theme.Sub);
        label.Margin = new Thickness(Theme.S4, 0, Theme.S4, 0);
        paging.Children.Add(back);
        paging.Children.Add(label);
        paging.Children.Add(next);
        return paging;
    }
}

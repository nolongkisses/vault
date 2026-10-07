using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using static NoxVault.Elements;

namespace NoxVault;

// The open vault: sidebar on the frame, and the stage with the header, the account list and the details.
internal sealed partial class MainWindow
{
    TextBlock status = new();
    TextBlock heading = new();
    TextBlock count = new();

    void BuildShell()
    {
        ResumeQuickFill();
        searchDelay.Stop();
        ClearRoot();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SidebarWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(BuildSidebar());
        var stage = Stage(Workspace());
        Grid.SetColumn(stage, 1);
        root.Children.Add(stage);
        captionContent.Content = SidebarCaption();
        if (session.Warning.Length > 0) { status.Text = session.Warning; session.Warning = ""; }
        RefreshNavigation();
        RefreshAccounts();
    }

    Grid Workspace()
    {
        var workspace = new Grid();
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition());
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.Children.Add(Header());
        var body = new Grid { Margin = new Thickness(0, Theme.S7, 0, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(0.72, GridUnitType.Star), MinWidth = 260,
            MaxWidth = 340
        });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Theme.S7) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.28, GridUnitType.Star) });
        body.Children.Add(ListPane());
        detail = new StackPanel();
        var detailPane = new DetailViewport(detail);
        Grid.SetColumn(detailPane, 2);
        body.Children.Add(detailPane);
        Grid.SetRow(body, 1);
        workspace.Children.Add(body);
        status = Text("", TextRole.Small, Theme.Sub);
        status.Margin = new Thickness(0, Theme.S5, 0, 0);
        status.SetBinding(VisibilityProperty, new System.Windows.Data.Binding("Text.Length")
        {
            Source = status,
            Converter = new ShownWhenText()
        });
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        Grid.SetRow(status, 2);
        workspace.Children.Add(status);
        return workspace;
    }

    DockPanel Header()
    {
        var header = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(SortControl());
        var add = Primary("Hinzufügen", () => Edit(null));
        add.Content = IconLabel(Icons.Add, "Hinzufügen");
        add.Margin = new Thickness(Theme.S4, 0, 0, 0);
        actions.Children.Add(add);
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        heading = Line("Alle Zugangsdaten", TextRole.Display, bold: true);
        count = Text("", TextRole.Display, Theme.Sub);
        count.Margin = new Thickness(Theme.S5, 0, 0, 0);
        Typography.SetNumeralAlignment(count, FontNumeralAlignment.Tabular);
        title.Children.Add(heading);
        title.Children.Add(count);
        header.Children.Add(title);
        return header;
    }

    Segmented SortControl()
    {
        var sort = new Segmented("Sortieren", "Name A–Z", "Neueste");
        sort.Select(prefs.AccountSort == AccountQueries.SortByName ? 0 : 1, notify: false);
        sort.Changed += index =>
        {
            var old = prefs.AccountSort;
            prefs.AccountSort = index == 0 ? AccountQueries.SortByName : AccountQueries.SortByUpdated;
            try { if (!preview) prefs.Save(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                prefs.AccountSort = old;
                sort.Select(old == AccountQueries.SortByName ? 0 : 1, notify: false);
                status.Text = "Sortierung nicht gespeichert.";
                return;
            }
            selected = null;
            accountPage = 0;
            RefreshAccounts();
            accountScroller?.ScrollToTop();
        };
        return sort;
    }

    sealed class ShownWhenText : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is int length && length > 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

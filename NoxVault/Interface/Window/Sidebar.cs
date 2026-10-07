using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shell;
using static NoxVault.Elements;

namespace NoxVault;

// Navigation on the frame: all entries, favourites, categories, and the lock and settings buttons at the bottom.
internal sealed partial class MainWindow
{
    static readonly DependencyProperty SidebarSizeProperty = DependencyProperty.Register(nameof(SidebarSize), typeof(double),
        typeof(MainWindow), new PropertyMetadata(Theme.Sidebar, (owner, _) => ((MainWindow)owner).ApplySidebarSize()));

    NavigationList navigation = new();
    Grid? sidebar;
    bool sidebarCollapsed;

    double SidebarWidth => sidebarCollapsed ? Theme.SidebarFolded : Theme.Sidebar;
    double SidebarSize { get => (double)GetValue(SidebarSizeProperty); set => SetValue(SidebarSizeProperty, value); }

    Grid BuildSidebar()
    {
        navigation = new NavigationList();
        sidebar = new Grid { Margin = new Thickness(Theme.Inset, 0, Theme.Inset, Theme.Inset) };
        sidebar.RowDefinitions.Add(new RowDefinition());
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.Children.Add(new ScrollViewer { Content = navigation });
        var bottom = new StackPanel { Orientation = sidebarCollapsed ? Orientation.Vertical : Orientation.Horizontal };
        bottom.Children.Add(IconButton(Icons.Lock, "vault ausblenden", Lock));
        bottom.Children.Add(IconButton(Icons.Settings, "Einstellungen", Settings));
        bottom.HorizontalAlignment = sidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Grid.SetRow(bottom, 1);
        sidebar.Children.Add(bottom);
        return sidebar;
    }

    FrameworkElement SidebarCaption()
    {
        var caption = sidebarCollapsed ? new Grid { Width = Theme.SidebarFolded } : (Grid)CaptionBrand(Theme.Sidebar);
        var toggle = IconButton(Icons.Sidebar, sidebarCollapsed ? "Seitenleiste ausklappen" : "Seitenleiste einklappen", ToggleSidebar);
        toggle.HorizontalAlignment = sidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        toggle.Margin = new Thickness(0, 0, sidebarCollapsed ? 0 : Theme.Inset, 0);
        WindowChrome.SetIsHitTestVisibleInChrome(toggle, true);
        caption.Children.Add(toggle);
        return caption;
    }

    void ToggleSidebar()
    {
        sidebarCollapsed = !sidebarCollapsed;
        if (sidebar == null) return;
        var replacement = BuildSidebar();
        root.Children.Remove(sidebar);
        root.Children.Insert(0, replacement);
        sidebar = replacement;
        captionContent.Content = SidebarCaption();
        RefreshNavigation();
        Motion.Animate(this, SidebarSizeProperty, SidebarWidth, Motion.Glide);
    }

    void ApplySidebarSize()
    {
        if (root.ColumnDefinitions.Count == 2) root.ColumnDefinitions[0].Width = new GridLength(SidebarSize);
    }

    void RefreshNavigation()
    {
        if (Data == null) return;
        var data = Data;
        var rows = navigation.Rows;
        rows.Clear();
        var active = data.Accounts.Where(a => a.DeletedUtc == null).ToList();
        var all = NavRow("Alle Zugangsdaten", Icon(Icons.All), active.Count, () => SelectCategory(null, false));
        var starred = NavRow("Favoriten", Icon(Icons.Star), active.Count(a => a.Favorite), () => SelectCategory(null, true));
        rows.Add(all);
        rows.Add(starred);
        if (!sidebarCollapsed) rows.Add(CategoryHeader());
        else rows.Add(new Border { Height = Theme.S6 });
        Button? current = category == null ? (favorites ? starred : all) : null;
        foreach (var name in data.Categories)
        {
            var row = CategoryRow(name, active.Count(a => a.Category == name));
            rows.Add(row);
            if (category == name) current = row;
        }
        navigation.Select(current);
    }

    Button NavRow(string text, FrameworkElement icon, int total, Action action)
    {
        var button = Button("", action, "NavRow");
        AutomationProperties.SetName(button, text + ", " + total + " Einträge");
        icon.Width = icon.Height = sidebarCollapsed ? Theme.IconSize : Theme.Size(TextRole.Body);
        if (icon is System.Windows.Shapes.Shape shape)
            shape.SetBinding(System.Windows.Shapes.Shape.FillProperty,
                new System.Windows.Data.Binding(nameof(Foreground)) { Source = button });
        if (sidebarCollapsed)
        {
            button.Content = icon;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.ToolTip = text;
            return button;
        }
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Theme.S6 + Theme.S4) });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        line.Children.Add(icon);
        var label = Line(text);
        label.ClearValue(TextBlock.ForegroundProperty);
        Grid.SetColumn(label, 1);
        line.Children.Add(label);
        var number = Text(total == 0 ? "" : total.ToString(), TextRole.Small, Theme.Faint);
        Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
        Grid.SetColumn(number, 2);
        line.Children.Add(number);
        button.Content = line;
        return button;
    }

    DockPanel CategoryHeader()
    {
        var header = new DockPanel { Margin = new Thickness(Theme.S4, Theme.S6, 0, Theme.S2) };
        var add = IconButton(Icons.Add, "Neue Kategorie", () => EditCategory(null), small: true);
        DockPanel.SetDock(add, Dock.Right);
        header.Children.Add(add);
        header.Children.Add(Text("Kategorien", TextRole.Small, Theme.Faint));
        return header;
    }

    Button CategoryRow(string name, int total)
    {
        var dot = new Grid { Children = { Dot(CategoryColor(name)) } };
        var row = NavRow(name, dot, total, () => SelectCategory(name, false));
        row.Tag = name;
        row.ContextMenu = CategoryMenu(name);
        row.ToolTip = sidebarCollapsed ? name : "Ziehen zum Verschieben · Rechtsklick zum Bearbeiten";
        AccountDrag.Target(row,
            id => !busy && dialogs.Count == 0 && Data?.Categories.Contains(name) == true
                && Data.Accounts.Any(a => a.Id == id && a.DeletedUtc == null && a.Category != name),
            id =>
            {
                if (Mutate(d =>
                {
                    var account = d.Accounts.First(a => a.Id == id
                    && a.DeletedUtc == null); account.Category = name; account.Updated = DateTime.Now;
                }))
                    status.Text = "Zugangsdaten verschoben nach „" + name + "“.";
            });
        CategoryDrag.Attach(row, name, MoveCategory);
        return row;
    }

    ContextMenu CategoryMenu(string name)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "Kategorie bearbeiten" };
        edit.Click += (_, _) => EditCategory(name);
        var delete = new MenuItem { Header = "Kategorie löschen", IsEnabled = Data?.Categories.Count > 1 };
        Visuals.SetIsDestructive(delete, true);
        delete.Click += (_, _) => DeleteCategory(name);
        menu.Items.Add(edit);
        menu.Items.Add(delete);
        return menu;
    }

    // Reorders, then lets every moved row glide from where it was to its new slot.
    void MoveCategory(string source, string target, bool after)
    {
        if (Data == null || !Data.Categories.Contains(source) || !Data.Categories.Contains(target)) return;
        int oldIndex = Data.Categories.IndexOf(source), newIndex = Data.Categories.IndexOf(target) + (after ? 1 : 0);
        if (oldIndex < newIndex) newIndex--;
        if (oldIndex == newIndex) return;
        var before = CategoryPositions();
        if (!Mutate(d =>
        {
            d.Categories.Remove(source); d.Categories.Insert(d.Categories.IndexOf(target) + (after ? 1 : 0),
            source);
        })) return;
        navigation.UpdateLayout();
        foreach (var row in navigation.Rows.OfType<Button>().Where(r => r.Tag is string))
        {
            if (!before.TryGetValue((string)row.Tag, out var previousY)) continue;
            double offset = previousY - row.TranslatePoint(new Point(), navigation).Y;
            if (Math.Abs(offset) < 0.5) continue;
            var shift = new TranslateTransform(0, offset);
            row.RenderTransform = shift;
            Motion.Animate(shift, TranslateTransform.YProperty, 0, Motion.Glide);
        }
        status.Text = "Reihenfolge gespeichert.";
    }

    Dictionary<string, double> CategoryPositions() =>
        navigation.Rows.OfType<Button>().Where(r => r.Tag is string).ToDictionary(r => (string)r.Tag,
            r => r.TranslatePoint(new Point(), navigation).Y);

    string CategoryColor(string name) => Data != null
        && Data.CategoryColors.TryGetValue(name, out var color) ? color : Theme.DefaultCategoryColor;
}

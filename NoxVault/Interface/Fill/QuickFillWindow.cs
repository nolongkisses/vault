using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using static NoxVault.Elements;

namespace NoxVault;

// The small picker that Win+F8 opens over the Riot login, without showing the main window.
internal sealed partial class MainWindow
{
    const double PickerWidth = 420;
    FillTarget? pendingFillTarget;
    bool quickFillAfterUnlock;
    Window? quickFillWindow;
    Action? refreshQuickFill;

    void OpenQuickFill()
    {
        FillTestTrace?.Invoke("shortcut received");
        if (preview || fillCancellation != null || dialogs.Count != 0 || busy) return;
        CheckIdle();
        if (quickFillWindow != null) { quickFillWindow.Activate(); return; }
        // Choosing/unlocking an account does not require Riot to be ready.
        // Discover and validate the current login only when the user fills it.
        if (Data == null)
        {
            if (TryAutomaticLogin()) BuildShell();
            else { quickFillAfterUnlock = true; ShowVault(); return; }
        }
        ShowQuickFill(null);
    }

    void ResumeQuickFill()
    {
        if (Data == null || (pendingFillTarget == null && !quickFillAfterUnlock)) return;
        var target = pendingFillTarget;
        pendingFillTarget = null;
        quickFillAfterUnlock = false;
        Dispatcher.BeginInvoke(() => { if (Data != null) ShowQuickFill(target); });
    }

    Window PickerWindow(UIElement content)
    {
        var window = new Window
        {
            Title = "vault · In Riot ausfüllen", Width = PickerWidth, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, WindowStyle = WindowStyle.None, Icon = Icon, Content = content,
            FontFamily = Theme.Font, UseLayoutRounding = true, ShowInTaskbar = false,
        };
        window.SetResourceReference(BackgroundProperty, Theme.Page);
        window.SetResourceReference(ForegroundProperty, Theme.Fg);
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Theme.PopoverRadius), UseAeroCaptionButtons = false,
        });
        window.SourceInitialized += (_, _) => Native.Round(window, Theme.Dark);
        window.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is not TextBox
            && e.ButtonState == MouseButtonState.Pressed) window.DragMove();
        };
        quickFillWindow = window;
        return window;
    }

    static DockPanel PickerHeader(Action close)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, Theme.S6) };
        var closeButton = IconButton(Icons.Close, "Schnellauswahl schließen", close, small: true);
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(Brand.Icon(Theme.IconSize));
        var name = Text("vault", TextRole.Body, bold: true);
        name.Margin = new Thickness(Theme.S4, 0, 0, 0);
        brand.Children.Add(name);
        header.Children.Add(brand);
        return header;
    }

    void ShowQuickFillNotice(string message)
    {
        var form = new StackPanel { Margin = new Thickness(Theme.S7) };
        var window = PickerWindow(form);
        form.Children.Add(PickerHeader(window.Close));
        form.Children.Add(Text("In Riot ausfüllen", TextRole.Head, bold: true));
        var note = Text(message, TextRole.Body, Theme.Sub);
        note.Margin = new Thickness(0, Theme.S4, 0, Theme.S6);
        form.Children.Add(note);
        var open = Primary("vault öffnen", () => { window.Close(); ShowVault(); });
        open.HorizontalAlignment = HorizontalAlignment.Left;
        form.Children.Add(open);
        window.KeyDown += (_, e) => { if (e.Key == Key.Escape) window.Close(); };
        window.Closed += (_, _) => quickFillWindow = null;
        window.Show();
        window.Activate();
    }

    void ShowQuickFill(FillTarget? target, bool show = true)
    {
        if (Data == null || quickFillWindow != null) return;
        var form = new StackPanel { Margin = new Thickness(Theme.S7) };
        var surface = new Border { Child = form, CornerRadius = new CornerRadius(Theme.PopoverRadius) };
        surface.SetResourceReference(Border.BackgroundProperty, Theme.Page);
        var window = PickerWindow(surface);
        // Expose the isolated synthetic test popup to external UI test tools.
        window.ShowInTaskbar = FillTestObserver != null;
        form.Children.Add(PickerHeader(window.Close));
        form.Children.Add(Text("In Riot ausfüllen", TextRole.Head, bold: true));
        var hint = Text("Wähle den Account für deinen Login.", TextRole.Small, Theme.Sub);
        hint.Margin = new Thickness(0, Theme.S2, 0, Theme.S6);
        form.Children.Add(hint);
        var searchBox = Input("Account suchen");
        var placeholder = Line("Accounts suchen …", TextRole.Body, Theme.Faint);
        placeholder.Margin = new Thickness(Theme.S4, 0, Theme.S4, 0);
        placeholder.IsHitTestVisible = false;
        form.Children.Add(new Grid { Children = { searchBox, placeholder } });
        var filter = new Segmented("Spiel filtern", "Alle", "League", "Valorant") { Margin = new Thickness(0, Theme.S4, 0, 0) };
        form.Children.Add(filter);
        var list = new StackPanel();
        form.Children.Add(new ScrollViewer { Content = list, MaxHeight = 240, Margin = new Thickness(0, Theme.S5, 0, 0) });
        var feedback = Text("", TextRole.Small, Theme.Sub);
        form.Children.Add(feedback);
        var footer = Text("Enter zum Einfügen · Esc zum Schließen", TextRole.Micro, Theme.Faint);
        footer.Margin = new Thickness(0, Theme.S5, 0, 0);
        form.Children.Add(footer);
        void Refresh() => FillPickerRows(list, searchBox.Text, filter.SelectedIndex, account => FillAccount(account, target, feedback));
        filter.Changed += _ => Refresh();
        searchBox.TextChanged += (_, _) =>
        {
            placeholder.Visibility = searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        };
        Refresh();
        BindPickerKeys(window, searchBox, list);
        refreshQuickFill = Refresh;
        window.Closed += (_, _) =>
        {
            refreshQuickFill = null;
            fillCancellation?.Cancel();
            quickFillWindow = null;
            searchBox.Clear();
            list.Children.Clear();
        };
        if (show) { window.Show(); window.Activate(); searchBox.Focus(); }
    }

    void FillPickerRows(StackPanel list, string text, int game, Func<Guid, System.Threading.Tasks.Task> fill)
    {
        list.Children.Clear();
        string? riotGame = game switch { 1 => "League of Legends", 2 => "Valorant", _ => null };
        var matches = Data?.Accounts.Where(a => a.DeletedUtc == null && AccountQueries.SupportsRiotFill(a)
                && (riotGame == null || string.Equals(a.Category, riotGame, StringComparison.OrdinalIgnoreCase))
                && (a.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || a.Username.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.Favorite).ThenBy(a => a.Title).Take(50).ToArray() ?? Array.Empty<Account>();
        foreach (var account in matches)
        {
            var row = Button(account.Title, async () => await fill(account.Id), "ListRow");
            row.Tag = account.Id;
            var labels = new StackPanel();
            labels.Children.Add(Line(account.Title, TextRole.Body, bold: true));
            bool email = account.Autofill?.UseEmail ?? string.IsNullOrWhiteSpace(account.Username);
            labels.Children.Add(Line(email ? account.Email : account.Username, TextRole.Small, Theme.Sub));
            var line = new DockPanel();
            var go = Icon(Icons.Go, Theme.IconSize, Theme.Sub);
            DockPanel.SetDock(go, Dock.Right);
            line.Children.Add(go);
            line.Children.Add(labels);
            row.Content = line;
            list.Children.Add(row);
        }
        if (matches.Length == 0)
            list.Children.Add(Text("Keine passenden Zugangsdaten. Einträge aus Valorant und League of Legends erscheinen hier automatisch.",
                TextRole.Body, Theme.Sub));
    }

    void BindPickerKeys(Window window, TextBox searchBox, StackPanel list)
    {
        searchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || list.Children.OfType<Button>().FirstOrDefault() is not { } first) return;
            e.Handled = true;
            first.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        };
        window.PreviewKeyDown += (_, e) =>
        {
            lastActivity = DateTime.UtcNow;
            if (e.Key == Key.Escape) { fillCancellation?.Cancel(); window.Close(); }
        };
        window.PreviewMouseDown += (_, _) => lastActivity = DateTime.UtcNow;
    }
}

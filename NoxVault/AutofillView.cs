using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow
{
    CancellationTokenSource? fillCancellation;
    FillTarget? pendingFillTarget;
    bool quickFillAfterUnlock;
    Window? quickFillWindow;
    Action? refreshQuickFill;
    bool fillShortcutRegistered;
    System.Windows.Threading.DispatcherTimer? fillStatusTimer;
    internal Action<FillResult>? FillTestObserver;
    internal Action<string>? FillTestTrace;
    string FillShortcutLabel => "Win + F" + prefs.FillShortcutF;
    internal static bool SupportsRiotFill(Account account) =>
        string.Equals(account.Category, "Valorant", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(account.Category, "League of Legends", StringComparison.OrdinalIgnoreCase);

    void RegisterFillShortcut()
    {
        Native.UnregisterHotKey(handle, 74);
        fillShortcutRegistered = Native.RegisterHotKey(handle, 74, 0x4008, (uint)(0x6F + prefs.FillShortcutF));
    }
    void CancelFill()
    {
        fillStatusTimer?.Stop(); fillStatusTimer = null;
        pendingFillTarget = null;
        quickFillAfterUnlock = false;
        fillCancellation?.Cancel();
        quickFillWindow?.Close(); quickFillWindow = null;
    }
    async Task<FillResult> FillOperation(FillRequest request)
    {
        if (fillCancellation != null) return new(false, "Eine Ausfüllprüfung läuft bereits.");
        using var cancellation = new CancellationTokenSource(); fillCancellation = cancellation;
        bool escape = request.Mode == "fill" && Native.RegisterHotKey(handle, 76, 0x4000, 0x1B);
        try
        {
            if (request.Mode == "fill" && !escape) return new(false, "Abbruch mit Esc ist gerade nicht verfügbar. Bitte erneut versuchen.");
            return await Autofill.RunAsync(request, cancellation.Token);
        }
        finally { if (escape) Native.UnregisterHotKey(handle, 76); fillCancellation = null; }
    }
    Button CreateFillAction(Account account)
    {
        var connected = account.Autofill != null;
        return IconButton("M5,8 L5,3 L21,3 L21,21 L5,21 L5,16 M2,12 L14,12 M10,8 L14,12 L10,16 M17,7 L18,7 M17,17 L18,17",
            connected ? "In Riot ausfüllen · " + FillShortcutLabel : "Ausfüllen einrichten", async () =>
            {
                if (connected) await FillAccount(account.Id);
                else ConfigureFill(account.Id);
            });
    }
    void ConfigureFill(Guid accountId)
    {
        if (data == null || preview) return;
        var account = data.Accounts.FirstOrDefault(a => a.Id == accountId && a.DeletedUtc == null);
        if (account == null || !SupportsRiotFill(account)) return;
        var form = new StackPanel();
        form.Children.Add(Text("Ein Klick für deinen Riot-Login", 16, bold: true));
        var introduction = Text("Öffne den Anmeldebildschirm im Riot Client und wähle, welche Zugangsdaten vault einfügen soll.", 13, "#8D96A3");
        introduction.Margin = new Thickness(0, 8, 0, 8); form.Children.Add(introduction);
        var source = new ComboBox { ItemsSource = new[] { "Benutzername", "E-Mail" }, SelectedIndex = account.Autofill?.UseEmail == true ? 1 : 0 };
        Field(form, "Für das Login verwenden", source);
        var hint = Text("Verwende deinen Riot-Loginnamen. Dein Spielername mit #Tag kann davon abweichen. Nach einem Clientupdate kann eine erneute Zuordnung nötig sein.", 12, "#8D96A3");
        hint.Margin = new Thickness(0, 12, 0, 12); form.Children.Add(hint);
        var feedback = Text("", 12, "#AAAAAA"); form.Children.Add(feedback);
        var dialog = new InAppDialog(this, "Riot verbinden", form, 510, false);
        var check = Button("Login prüfen & verbinden", () => { }, true); check.Margin = new Thickness(0, 16, 0, 8); form.Children.Add(check);
        check.Click += async (_, _) =>
        {
            bool useEmail = source.SelectedIndex == 1;
            var current = data?.Accounts.FirstOrDefault(a => a.Id == accountId && a.DeletedUtc == null);
            if (current == null || !SupportsRiotFill(current)) return;
            if (string.IsNullOrWhiteSpace(useEmail ? current.Email : current.Username) || string.IsNullOrEmpty(current.Password))
            { feedback.Text = "Bitte zuerst Loginname und Passwort im Account hinterlegen."; return; }
            check.IsEnabled = false; source.IsEnabled = false; feedback.Text = "Riot-Login wird geprüft …";
            var result = await FillOperation(new("probe"));
            check.IsEnabled = true; source.IsEnabled = true;
            if (data == null || !dialog.IsVisible) return;
            feedback.Text = result.Message;
            if (result.Ok && result.Target is { } target && Mutate(d => d.Accounts.First(a => a.Id == accountId).Autofill = new(target.Path, target.Hash, useEmail)))
            { dialog.Close(); status.Text = "Riot zugeordnet · Schnellauswahl: " + (fillShortcutRegistered ? FillShortcutLabel : "Shortcut belegt – Einstellungen öffnen"); }
        };
        if (account.Autofill != null)
            form.Children.Add(Button("Zuordnung entfernen", () => { if (Mutate(d => d.Accounts.First(a => a.Id == accountId).Autofill = null)) dialog.Close(); }));
        dialog.Closed += (_, _) => fillCancellation?.Cancel();
        dialog.ShowDialog();
    }
    async Task FillAccount(Guid id, FillTarget? target = null, TextBlock? feedback = null)
    {
        if (fillCancellation != null || data == null || preview) return;
        var account = data.Accounts.FirstOrDefault(a => a.Id == id && a.DeletedUtc == null);
        if (account == null || !SupportsRiotFill(account)) return;
        var savedProfile = account.Autofill;
        lastActivity = DateTime.UtcNow;
        (feedback ?? status).Text = "Riot-Login wird geprüft …";
        // Always obtain a fresh worker verification before renewing a binding.
        // A captured target must pass the same signature, identity and field checks.
        var probe = await FillOperation(new("probe", target));
        FillTestTrace?.Invoke("probe=" + probe.Ok + ";diagnostic=" + probe.Diagnostic);
        if (data == null) return;
        account = data.Accounts.FirstOrDefault(a => a.Id == id && a.DeletedUtc == null);
        if (account == null || account.Autofill != savedProfile || !SupportsRiotFill(account)) return;
        if (!probe.Ok || probe.Target is not { } found) { (feedback ?? status).Text = probe.Message; return; }
        var profile = savedProfile ?? new RiotFillProfile(found.Path, found.Hash, string.IsNullOrWhiteSpace(account.Username));
        if (string.IsNullOrWhiteSpace(profile.UseEmail ? account.Email : account.Username) || string.IsNullOrEmpty(account.Password))
        { (feedback ?? status).Text = "Bitte zuerst Benutzername oder E-Mail und Passwort ergänzen."; return; }
        var refreshedProfile = ProfileForVerifiedTarget(profile, probe);
        if (refreshedProfile == null)
        { (feedback ?? status).Text = "Riot wurde an einen anderen Ort verschoben. Bitte die Riot-Zuordnung erneut einrichten."; return; }
        if (savedProfile != null && refreshedProfile != savedProfile)
        {
            if (!Mutate(d => d.Accounts.First(a => a.Id == id).Autofill = refreshedProfile))
            { (feedback ?? status).Text = "Die aktualisierte Riot-Zuordnung konnte nicht gespeichert werden. Bitte erneut versuchen."; return; }
            savedProfile = refreshedProfile;
        }
        profile = refreshedProfile;
        if (!await Autofill.ActivateTarget(found))
        { (feedback ?? status).Text = "Riot konnte nicht aktiviert werden. Bitte die Ausfüllaktion erneut wählen."; return; }
        var latest = data?.Accounts.FirstOrDefault(a => a.Id == id && a.DeletedUtc == null);
        if (latest == null || latest.Autofill != savedProfile || !SupportsRiotFill(latest)) return;
        var result = await FillOperation(new("fill", found, profile.UseEmail ? latest.Email : latest.Username, latest.Password));
        FillTestObserver?.Invoke(result);
        if (data == null) return;
        (feedback ?? status).Text = result.Message;
        if (result.Ok)
        {
            quickFillWindow?.Close();
            fillStatusTimer?.Stop();
            var messageTarget = status;
            messageTarget.Text = result.Message;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            fillStatusTimer = timer;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (ReferenceEquals(fillStatusTimer, timer))
                {
                    fillStatusTimer = null;
                    if (messageTarget.Text == result.Message) messageTarget.Text = "";
                }
            };
            timer.Start();
        }
    }
    internal static bool ProfileMatches(RiotFillProfile profile, FillTarget target) =>
        string.Equals(profile.Path, target.Path, StringComparison.OrdinalIgnoreCase) && profile.Hash == target.Hash;
    internal static RiotFillProfile? ProfileForVerifiedTarget(RiotFillProfile profile, FillResult verification)
    {
        if (!verification.Ok || verification.Target is not { } target ||
            !string.Equals(profile.Path, target.Path, StringComparison.OrdinalIgnoreCase)) return null;
        // Only the successful worker probe authorizes an updated hash. The fill
        // worker pins that new hash again, rejecting changes after the probe.
        return profile with { Hash = target.Hash };
    }

    void OpenQuickFill()
    {
        FillTestTrace?.Invoke("shortcut received");
        if (preview || fillCancellation != null || dialogs.Count != 0 || busy) return;
        CheckIdle();
        if (quickFillWindow != null) { quickFillWindow.Activate(); return; }
        // Choosing/unlocking an account does not require Riot to be ready.
        // Discover and validate the current login only when the user fills it.
        if (data == null)
        {
            if (TryAutomaticLogin()) BuildShell();
            else { quickFillAfterUnlock = true; ShowVault(); return; }
        }
        ShowQuickFill(null);
    }
    void ShowQuickFillNotice(string message)
    {
        var form = new StackPanel { Margin = new Thickness(22) };
        var window = new Window { Title = "vault · In Riot ausfüllen", Width = 420, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None, Background = Brush("#111419"), ShowInTaskbar = false,
            Icon = Icon, Content = form, UseLayoutRounding = true };
        quickFillWindow = window;
        var header = new DockPanel();
        var close = IconButton("M3,3 L21,21 M21,3 L3,21", "Schließen", () => window.Close());
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close); header.Children.Add(Wordmark(24)); form.Children.Add(header);
        form.Children.Add(Text("In Riot ausfüllen", 23, bold: true));
        var note = Text(message, 13, "#8D96A3"); note.Margin = new Thickness(0, 12, 0, 16); form.Children.Add(note);
        header.MouseLeftButtonDown += (_, e) => { if (!close.IsMouseOver) { e.Handled = true; window.DragMove(); } };
        form.Children.Add(Button("vault öffnen", () => { window.Close(); ShowVault(); }, true));
        window.KeyDown += (_, e) => { if (e.Key == Key.Escape) window.Close(); };
        window.Closed += (_, _) => quickFillWindow = null;
        window.SourceInitialized += (_, _) => Native.Round(window);
        window.Show(); window.Activate();
    }
    void ResumeQuickFill()
    {
        if (data == null || (pendingFillTarget == null && !quickFillAfterUnlock)) return;
        var target = pendingFillTarget;
        pendingFillTarget = null; quickFillAfterUnlock = false;
        Dispatcher.BeginInvoke(() => { if (data != null) ShowQuickFill(target); });
    }
    void ShowQuickFill(FillTarget? target, bool show = true)
    {
        if (data == null || quickFillWindow != null) return;
        var form = new StackPanel { Margin = new Thickness(22) };
        Window? picker = null;
        var header = new DockPanel { Background = System.Windows.Media.Brushes.Transparent, Margin = new Thickness(0, 0, 0, 18) };
        var close = IconButton("M3,3 L21,21 M21,3 L3,21", "Schnellauswahl schließen", () => picker?.Close());
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(Wordmark(24)); form.Children.Add(header);
        form.Children.Add(Text("In Riot ausfüllen", 23, bold: true));
        var hint = Text("Wähle den Account für deinen Login.", 12, "#8D96A3"); hint.Margin = new Thickness(0, 6, 0, 18); form.Children.Add(hint);
        var searchBox = Input("Account suchen", "");
        searchBox.Padding = new Thickness(12, 8, 154, 8);
        var searchRow = new Grid(); searchRow.Children.Add(searchBox);
        var placeholder = Text("Accounts suchen …", 13, "#747C89"); placeholder.Margin = new Thickness(12, 0, 154, 0); placeholder.IsHitTestVisible = false; searchRow.Children.Add(placeholder); form.Children.Add(searchRow);
        string? gameFilter = null;
        var filters = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
        filters.Children.Add(new Border { Width = 1, Height = 18, Background = Brush("#303741"), Margin = new Thickness(0, 0, 5, 0) });
        Button league = null!, valorant = null!;
        void SetFilter(string value)
        {
            gameFilter = gameFilter == value ? null : value;
            foreach (var b in new[] { league, valorant })
            {
                bool active = (string)b.Tag == gameFilter;
                b.Background = Brush(active ? "#243247" : "#00000000"); b.Foreground = Brush(active ? "#E2E6EC" : "#8D96A3");
                System.Windows.Automation.AutomationProperties.SetItemStatus(b, active ? "Filter aktiv" : "Filter aus");
            }
            Refresh();
        }
        Button GameButton(string label, string category)
        {
            var b = Button(label, () => SetFilter(category)); b.Tag = category;
            b.SetResourceReference(StyleProperty, "GhostButton"); b.FontSize = 11; b.Padding = new Thickness(8, 6, 8, 6); b.Margin = new Thickness(0);
            b.Foreground = Brush("#8D96A3"); b.ToolTip = category + " filtern · Erneut klicken zeigt alle";
            System.Windows.Automation.AutomationProperties.SetName(b, category + " filtern"); filters.Children.Add(b); return b;
        }

        var list = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var listViewport = new ScrollViewer { Content = list, MaxHeight = 230, Style = (Style)FindResource("OverlayScrollViewer") };
        form.Children.Add(listViewport);
        var feedback = Text("", 12, "#8D96A3"); feedback.Margin = new Thickness(0, 10, 0, 0);
        feedback.SetBinding(VisibilityProperty, new System.Windows.Data.Binding("Text.Length") { Source = feedback, Converter = new EmptyMessageVisibility() }); form.Children.Add(feedback);
        league = GameButton("League", "League of Legends"); valorant = GameButton("Valorant", "Valorant"); searchRow.Children.Add(filters);
        var footer = Text("Enter zum Einfügen · Esc zum Schließen", 11, "#818A98"); footer.Margin = new Thickness(0, 16, 0, 0); form.Children.Add(footer);
        var window = new Window { Title = "vault · In Riot ausfüllen", Width = 420, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brush("#111419"), Foreground = Brush("#F1F3F5"), Content = new Border { Background = Brush("#111419"), CornerRadius = new CornerRadius(12), Child = form },
            WindowStyle = WindowStyle.None, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), UseLayoutRounding = true,
            // Expose the isolated synthetic test popup to external UI test tools.
            ShowInTaskbar = FillTestObserver != null };
        picker = window; quickFillWindow = window;
        window.MouseLeftButtonDown += (_, e) =>
        {
            if (close.IsMouseOver || searchRow.IsMouseOver || listViewport.IsMouseOver || e.LeftButton != MouseButtonState.Pressed) return;
            e.Handled = true; window.DragMove();
        };
        System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(12), UseAeroCaptionButtons = false });
        void Refresh()
        {
            list.Children.Clear();
            var matches = data?.Accounts.Where(a => a.DeletedUtc == null && SupportsRiotFill(a) && (gameFilter == null || string.Equals(a.Category, gameFilter, StringComparison.OrdinalIgnoreCase)) &&
                (a.Title.Contains(searchBox.Text, StringComparison.OrdinalIgnoreCase) || a.Username.Contains(searchBox.Text, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.Favorite).ThenBy(a => a.Title).Take(50).ToArray() ?? Array.Empty<Account>();
            foreach (var account in matches)
            {
                var row = Button(account.Title, async () => await FillAccount(account.Id, target, feedback)); row.Tag = account.Id;
                row.Style = (Style)FindResource("AccountListItem"); row.Margin = new Thickness(0, 0, 0, 4); row.Padding = new Thickness(12, 10, 12, 10);
                var label = new StackPanel(); label.Children.Add(Text(account.Title, 14, bold: true));
                var login = Text((account.Autofill?.UseEmail ?? string.IsNullOrWhiteSpace(account.Username)) ? account.Email : account.Username, 12, "#8D96A3"); login.Margin = new Thickness(0, 4, 0, 0); label.Children.Add(login);
                var rowLayout = new DockPanel(); var arrow = Text("↗", 16, "#8D96A3"); arrow.Margin = new Thickness(12, 0, 0, 0); DockPanel.SetDock(arrow, Dock.Right); rowLayout.Children.Add(arrow); rowLayout.Children.Add(label);
                row.Content = rowLayout; row.HorizontalContentAlignment = HorizontalAlignment.Stretch; list.Children.Add(row);
            }
            if (matches.Length == 0) list.Children.Add(Text("Keine passenden Zugangsdaten. Einträge aus Valorant und League of Legends erscheinen hier automatisch.", 13, "#AAAAAA"));
        }
        searchBox.TextChanged += (_, _) => { placeholder.Visibility = searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Refresh(); }; Refresh();
        searchBox.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && list.Children.OfType<Button>().FirstOrDefault() is { } first) { e.Handled = true; first.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); } };
        window.PreviewKeyDown += (_, e) => { lastActivity = DateTime.UtcNow; if (e.Key == Key.Escape) { fillCancellation?.Cancel(); window.Close(); } };
        window.PreviewMouseDown += (_, _) => lastActivity = DateTime.UtcNow;
        refreshQuickFill = Refresh;
        window.Closed += (_, _) => { refreshQuickFill = null; fillCancellation?.Cancel(); quickFillWindow = null; searchBox.Clear(); list.Children.Clear(); };
        window.SourceInitialized += (_, _) => Native.Round(window);
        if (show) { window.Show(); window.Activate(); searchBox.Focus(); }
    }
    internal Window CreateFillPreview()
    {
        if (!preview || data == null) throw new InvalidOperationException();
        var target = new FillTarget(1, 1, 1, @"C:\Synthetic\Riot Client.exe", new string('A', 64));
        data.Accounts[0].Autofill = new(target.Path, target.Hash, false);
        ShowQuickFill(target, false);
        return quickFillWindow!;
    }
    internal void TestQuickFillIsolation()
    {
        Hide();
        ShowQuickFillNotice("Synthetischer Test: Riot-Login aktivieren.");
        if (IsVisible || quickFillWindow?.IsVisible != true) throw new InvalidOperationException("Riot notice opened the main window.");
        quickFillWindow.Close();
        var target = new FillTarget(1, 1, 1, @"C:\Synthetic\Riot Client.exe", new string('A', 64));
        ShowQuickFillNotice("Synthetischer Test: Windows-Zugriff einrichten.");
        if (IsVisible || quickFillWindow?.IsVisible != true) throw new InvalidOperationException("Riot unlock opened the main window.");
        if (((StackPanel)quickFillWindow.Content).Children.OfType<Button>().Any(b => Equals(b.Content, "Entsperren")))
            throw new InvalidOperationException("Quick fill still offers password entry.");
        quickFillWindow.Close();
        ShowQuickFill(null);
        if (IsVisible || quickFillWindow?.IsVisible != true) throw new InvalidOperationException("Riot picker opened the main window.");
        var form = (StackPanel)((Border)quickFillWindow.Content).Child;
        var list = (StackPanel)form.Children.OfType<ScrollViewer>().Single().Content;
        var added = new Account { Title = "Synthetic new Riot entry", Category = "Valorant", Username = "Synthetic", Password = "Synthetic" };
        data!.Accounts.Add(added); refreshQuickFill!();
        if (!list.Children.OfType<Button>().Any(b => Equals(b.Tag, added.Id))) throw new InvalidOperationException("New unbound Riot account missing from picker.");
        added.Category = "Unrelated"; refreshQuickFill();
        if (list.Children.OfType<Button>().Any(b => Equals(b.Tag, added.Id))) throw new InvalidOperationException("Picker retained account moved out of Riot categories.");
        data.Accounts.Remove(added);
        quickFillWindow.Close();
        if (refreshQuickFill != null) throw new InvalidOperationException("Closed picker retained refresh callback.");
    }
    internal void TestDirectQuickFill()
    {
        try
        {
            OpenQuickFill();
            if (IsVisible || data == null || quickFillWindow?.Content is not Border)
                throw new InvalidOperationException("Quick fill did not restore the account picker directly.");
            quickFillWindow.Close();
            quickFillAfterUnlock = true; BuildShell();
            var frame = new System.Windows.Threading.DispatcherFrame();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            if (quickFillAfterUnlock || quickFillWindow?.Content is not Border)
                throw new InvalidOperationException("Quick fill did not resume automatically after initial setup.");
            Lock();
            if (data != null || vault.Unlocked || !File.Exists(vault.FilePath + ".quickfill"))
                throw new InvalidOperationException("Lock failed to release data or removed direct quick-fill access.");
            if (gateState != "WindowsAccess") throw new InvalidOperationException("Main window still offers a password gate.");
            if (!TryAutomaticLogin() || !vault.Unlocked) throw new InvalidOperationException("Main window did not restore Windows access.");
            BuildShell(); LockCore(false);
            OpenQuickFill();
            if (data == null || quickFillWindow?.Content is not Border)
                throw new InvalidOperationException("Quick fill required a password after locking.");
        }
        finally { quitting = true; Close(); }
    }
    internal async void PrepareFillIntegrationTest(string report, bool automatic = false)
    {
        quitting = true; // Closing the isolated test window exits instead of hiding.
        data ??= vault.CreateForWindows();
        var probe = await FillOperation(new("probe"));
        if (!probe.Ok || probe.Target is not { } target) { File.WriteAllText(report, "FAIL: " + probe.Message + ";diagnostic=" + probe.Diagnostic); Close(); return; }
        data.Accounts.Add(new Account { Title = "Autofill-Testaccount", Category = data.Categories[0], Username = "vault-autofill-test", Password = "Synthetic-Only-42!",
            Autofill = new(target.Path, target.Hash, false) });
        vault.Save(data); BuildShell(); Title = "vault · Ausfülltest";
        FillTestObserver = result => File.AppendAllText(report, (result.Ok ? "PASS: " : "FAIL: ") + result.Message + Environment.NewLine);
        FillTestTrace = message => File.AppendAllText(report, "TRACE: " + message + Environment.NewLine);
        File.WriteAllText(report, fillShortcutRegistered ? "READY: synthetic account; Win+F" + prefs.FillShortcutF + Environment.NewLine : "FAIL: shortcut registration" + Environment.NewLine);
        if (automatic)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            await FillAccount(data.Accounts.Single().Id);
            File.AppendAllText(report, "STATUS: " + status.Text + Environment.NewLine + "elapsed_ms=" + elapsed.ElapsedMilliseconds + Environment.NewLine);
            var cleanup = await FillOperation(new("clear-test", target));
            File.AppendAllText(report, "cleanup=" + cleanup.Ok + ";" + cleanup.Message + Environment.NewLine);
            Close();
        }
    }
}

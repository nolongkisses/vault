using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow : Window
{
    readonly Vault vault;
    readonly RememberedLogin rememberedLogin;
    readonly RememberedLogin quickFillLogin;
    DateTime rememberedUntil;
    bool rememberedLoginBlocked;
    VaultData? data;
    readonly Preferences prefs;
    readonly System.Collections.Generic.List<Action> concealSecrets = new();
    readonly ClipboardGuard clipboard = new();
    readonly DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly DispatcherTimer searchDelay = new() { Interval = TimeSpan.FromMilliseconds(150) };
    DateTime lastActivity = DateTime.UtcNow;
    readonly Grid root = new();
    readonly Grid layers = new();
    readonly ContentControl titleContent;
    readonly System.Collections.Generic.List<InAppDialog> dialogs = new();
    StackPanel navigation = new();
    StackPanel accountList = new();
    StackPanel detail = new();
    TextBlock status = Text("", 12, "#A5ABB5");
    TextBlock heading = Text("Alle Zugangsdaten", 30, bold: true);
    TextBlock count = Text("", 13, "#9299A5");
    TextBox search = null!;
    Guid? selected;
    string? category;
    bool favorites;
    int accountPage;
    ScrollViewer? accountScroller;
    bool sidebarCollapsed;
    string query = "";
    bool quitting;
    bool busy;
    bool lockPending;
    IntPtr handle;
    bool hotkeyRegistered;
    int activeHotkeyId = 42;
    string shortcut = "Nicht registriert";
    System.Windows.Forms.NotifyIcon? tray;
    readonly bool preview;
    public MainWindow(string? testPath = null, VaultData? previewData = null, bool designMode = false, bool startInTray = false)
    {
        preview = previewData != null || designMode;
        prefs = preview ? new Preferences { HotkeyModifiers = 3 } : Preferences.Load();
        vault = new Vault(testPath ?? Path.Combine(Preferences.Root, "vault.nox"));
        rememberedLogin = new RememberedLogin(vault.FilePath);
        quickFillLogin = new RememberedLogin(vault.FilePath, permanent: true);
        Title = "vault"; Width = 1180; Height = 780; MinWidth = 980; MinHeight = 660;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/vault.ico"));
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0D0F12"); Foreground = Brush("#F1F3F5"); FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        root.Background = Background; layers.Children.Add(root); Content = layers;
        titleContent = WindowFrame.Attach(this, layers, root);
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle; Native.Round(this);
            HwndSource.FromHwnd(handle)?.AddHook(Hook);
            if (!preview)
            {
                RegisterShortcut(prefs.HotkeyModifiers, prefs.EffectiveKey);
                RegisterFillShortcut();
                using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/vault.ico")).Stream;
                tray = new System.Windows.Forms.NotifyIcon { Icon = new System.Drawing.Icon(iconStream), Text = "vault · " + shortcut, Visible = true };
                var menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("vault öffnen", null, (_, _) => Dispatcher.Invoke(ShowVault));
                menu.Items.Add("vault ausblenden", null, (_, _) => Dispatcher.Invoke(Lock));
                menu.Items.Add("Beenden", null, (_, _) => Dispatcher.Invoke(() => { quitting = true; Close(); }));
                tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowVault);
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            lastActivity = DateTime.UtcNow;
            if (data == null || dialogs.Count != 0) return;
            if (e.Key == Key.Escape) { e.Handled = true; Lock(); }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            { e.Handled = true; FocusSearch(); }
        };
        PreviewMouseDown += (_, _) => lastActivity = DateTime.UtcNow;
        PreviewMouseMove += (_, _) => lastActivity = DateTime.UtcNow;
        idle.Tick += (_, _) => CheckIdle();
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); if (data != null) RefreshAccounts(); };
        if (!preview) { idle.Start(); SystemEvents.SessionSwitch += SessionChanged; }
        Closing += OnClosing;
        if (previewData != null) { data = previewData; BuildShell(); }
        else if (!preview && !startInTray && TryAutomaticLogin()) BuildShell();
        else BuildGate();
    }
    internal void StartInTray()
    {
        // Initialize the native handle, tray icon and shortcuts without showing
        // or activating a window during Windows sign-in.
        new WindowInteropHelper(this).EnsureHandle();
    }
    void SessionChanged(object sender, SessionSwitchEventArgs e)
    { if (e.Reason == SessionSwitchReason.SessionLock) Dispatcher.BeginInvoke(Lock); }
    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy || (quitting && automaticBackupRunning)) { e.Cancel = true; lockPending = true; quitting = false; return; }
        if (!quitting && !preview) { e.Cancel = true; LockCore(false); Hide(); return; }
        ConcealSecrets(); idle.Stop(); searchDelay.Stop(); clipboard.Dispose(); vault.Dispose(); tray?.Dispose();
        CancelFill(); Native.UnregisterHotKey(handle, 74);
        SystemEvents.SessionSwitch -= SessionChanged;
        if (hotkeyRegistered) Native.UnregisterHotKey(handle, activeHotkeyId);
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x312 && w.ToInt32() == 76) { handled = true; fillCancellation?.Cancel(); return IntPtr.Zero; }
        if (msg == 0x312 && w.ToInt32() == 74) { handled = true; OpenQuickFill(); return IntPtr.Zero; }
        if (msg == 0x8002)
        {
            handled = true;
            if (busy || automaticBackupRunning || dialogs.Count != 0 || fillCancellation != null) { status.Text = "Update wartet: Bitte geöffnete Dialoge abschließen und das Update erneut starten."; return IntPtr.Zero; }
            quitting = true; Close(); return IntPtr.Zero;
        }
        if ((msg == 0x312 && w.ToInt32() == activeHotkeyId) || msg == 0x8001) { ShowVault(); handled = true; }
        return IntPtr.Zero;
    }
    internal void BeginHotkeyCheck(string reportPath)
    {
        Show();
        bool registered = RegisterShortcut(3, 0x77); // Ctrl+Alt+F8; isolated test, no preference writes.
        File.WriteAllText(reportPath, registered ? "READY: Ctrl+Alt+F8 registered" : "FAIL: registration " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        Hide();
        int stage = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) =>
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (!IsActive) { File.WriteAllText(reportPath, "FAIL: reopened but not active"); timer.Stop(); return; }
            if (stage++ == 0)
            {
                File.WriteAllText(reportPath, "PASS: hidden vault reopened and activated\nREADY: press Ctrl+Alt+F8 again to restore minimized vault");
                WindowState = WindowState.Minimized;
            }
            else { File.AppendAllText(reportPath, "\nPASS: minimized vault restored and activated"); timer.Stop(); }
        };
        timer.Start();
    }
    bool TryRememberedLogin()
    {
        if (rememberedLoginBlocked) return false;
        data = rememberedLogin.Restore(vault, DateTime.Now, out rememberedUntil);
        lastActivity = DateTime.UtcNow;
        if (data != null) SaveQuickFillLogin();
        return data != null;
    }
    void SaveQuickFillLogin()
    {
        try { quickFillLogin.Save(vault, DateTime.Now); }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        { status.Text = "Schnellzugriff konnte nicht gespeichert werden. Bitte Dateizugriff prüfen."; }
    }
    bool TryAutomaticLogin()
    {
        try
        {
            data = quickFillLogin.Restore(vault, DateTime.Now, out _);
            lastActivity = DateTime.UtcNow;
            if (data != null || TryRememberedLogin()) return true;
            if (!vault.Exists && !preview) { data = vault.CreateForWindows(); return true; }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        { accessError = ex.Message; }
        return false;
    }
    void ShowVault()
    {
        CheckIdle();
        if (data == null && !busy && dialogs.Count == 0)
        {
            if (TryAutomaticLogin()) BuildShell();
            else BuildGate();
        }
        Show(); WindowState = WindowState.Normal; Activate();
    }
    internal void RefreshGateForTest() => ShowVault();
    internal string StartupDiagnostics() => $"Visible={IsVisible}\nLocked={data == null}\nTrayVisible={tray?.Visible == true}\nOpenShortcut={shortcut}\nOpenRegistered={hotkeyRegistered}\nFillShortcut={FillShortcutLabel}\nFillRegistered={fillShortcutRegistered}\nVaultPath={vault.FilePath}\nGate={gateState}\n";
    void CheckIdle()
    {
        if (!preview) return;
        if (rememberedUntil != default)
        {
            if (DateTime.Now >= rememberedUntil || DateTime.Now.Date != rememberedUntil.AddDays(-1).Date) Lock();
            return;
        }
        if (data != null && DateTime.UtcNow - lastActivity > TimeSpan.FromMinutes(prefs.IdleMinutes)) Lock();
    }
    internal void TestHotkeys()
    {
        new WindowInteropHelper(this).EnsureHandle();
        uint testKey = 0;
        foreach (uint candidate in new uint[] { 0x78, 0x79, 0x7A })
            if (RegisterShortcut(7, candidate)) { testKey = candidate; break; }
        if (testKey == 0) throw new Exception("No free shortcut for registration test");
        int savedId = activeHotkeyId;
        if (Native.RegisterHotKey(IntPtr.Zero, 1951, 7 | 0x4000, testKey))
        { Native.UnregisterHotKey(IntPtr.Zero, 1951); throw new Exception("Registered shortcut did not reserve combination"); }
        // A conflicting replacement must leave the original registration active.
        uint blockedKey = testKey == 0x78 ? 0x79u : 0x78u;
        if (!Native.RegisterHotKey(IntPtr.Zero, 1952, 7 | 0x4000, blockedKey)) throw new Exception("Conflict-test key unavailable");
        try
        {
            if (RegisterShortcut(7, blockedKey) || activeHotkeyId != savedId) throw new Exception("Conflict replaced active shortcut");
        }
        finally { Native.UnregisterHotKey(IntPtr.Zero, 1952); }
        if (!RegisterShortcut(7, blockedKey)) throw new Exception("Shortcut replacement failed");
        Native.UnregisterHotKey(handle, activeHotkeyId); hotkeyRegistered = false;
    }
    internal void TestLifecycle()
    {
        data = vault.Create("Synthetic lifecycle test!"); BuildShell();
        if (!Mutate(d => d.Accounts.Add(new Account { Title = "Lifecycle", Category = d.Categories[0] }))) throw new Exception("UI save failed");
        var lockOverlay = Dialog(this, "Lock test", new StackPanel());
        Dispatcher.BeginInvoke(new Action(Lock));
        lockOverlay.ShowDialog();
        if (dialogs.Count != 0 || !root.IsEnabled) throw new Exception("Lock retained overlay state");
        if (data != null || vault.Unlocked || accountList.Children.Count != 0 || detail.Children.Count != 0) throw new Exception("Lock retained UI data");
        data = vault.Open("Synthetic lifecycle test!"); BuildShell();
        if (data.Accounts.Count != 1) throw new Exception("Reopen failed");
        SelectCategory("Valorant", false); query = "NO-MATCH"; RefreshAccounts();
        if (selected != null) throw new Exception("Search filter failed");
        lastActivity = DateTime.UtcNow.AddMinutes(-6); CheckIdle();
        if (data != null || vault.Unlocked) throw new Exception("Idle lock failed");
        data = vault.Open("Synthetic lifecycle test!"); BuildShell();
        rememberedUntil = rememberedLogin.Save(vault, DateTime.Now); rememberedLoginBlocked = false;
        lastActivity = DateTime.UtcNow.AddMinutes(-6); CheckIdle();
        if (data == null) throw new Exception("Remembered login should survive inactivity");
        LockCore(false);
        if (data != null || vault.Unlocked || !TryRememberedLogin()) throw new Exception("Remembered login failed after hiding");
        BuildShell(); Lock();
        if (TryRememberedLogin()) throw new Exception("Explicit lock failed to revoke remembered login");
        data = vault.Open("Synthetic lifecycle test!"); BuildShell();
        rememberedUntil = rememberedLogin.Save(vault, DateTime.Now); rememberedLoginBlocked = false;
        rememberedUntil = DateTime.Now.Date; CheckIdle();
        if (data != null || vault.Unlocked || TryRememberedLogin()) throw new Exception("Midnight failed to lock and revoke session");
        data = vault.Open("Synthetic lifecycle test!"); BuildShell();
        rememberedUntil = rememberedLogin.Save(vault, DateTime.Now); rememberedLoginBlocked = false;
        using (var heldSession = new FileStream(vault.FilePath + ".session", FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Lock();
            if (data != null || vault.Unlocked || TryRememberedLogin()) throw new Exception("Session file error prevented locking");
            using var reopened = new Vault(vault.FilePath);
            if (new RememberedLogin(vault.FilePath).Restore(reopened, DateTime.Now, out _) != null)
                throw new Exception("Revoked session restored in a new instance");
        }
        data = vault.Open("Synthetic lifecycle test!"); category = null; query = ""; BuildShell();
        search.Text = "NO"; search.Text = "NO-MATCH";
        if (!searchDelay.IsEnabled || accountList.Children.Count != 1) throw new Exception("Search was not deferred");
        FocusSearch();
        if (search.SelectionLength != search.Text.Length || FocusManager.GetFocusedElement(this) != search)
            throw new Exception("Search shortcut did not select input");
        var searchFrame = new DispatcherFrame();
        var searchWait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        searchWait.Tick += (_, _) => { searchWait.Stop(); searchFrame.Continue = false; };
        searchWait.Start(); Dispatcher.PushFrame(searchFrame);
        if (searchDelay.IsEnabled || selected != null || count.Text != "0 Einträge") throw new Exception("Deferred search returned incorrect results");
        search.Text = "Lifecycle"; Lock();
        if (searchDelay.IsEnabled || accountList.Children.Count != 0) throw new Exception("Pending search survived lock");
        vault.Dispose();
    }
    static string ShortcutLabel(uint modifiers, uint key)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & 2) != 0) parts.Add("Strg");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)key).ToString());
        return string.Join(" + ", parts);
    }
    bool RegisterShortcut(uint modifiers, uint key)
    {
        int candidate = activeHotkeyId == 42 ? 43 : 42;
        if (!Native.RegisterHotKey(handle, candidate, modifiers | 0x4000, key)) return false;
        if (hotkeyRegistered) Native.UnregisterHotKey(handle, activeHotkeyId);
        activeHotkeyId = candidate; hotkeyRegistered = true;
        shortcut = ShortcutLabel(modifiers, key);
        if (tray != null) tray.Text = "vault · " + shortcut;
        return true;
    }
    internal void PushDialog(InAppDialog dialog)
    {
        if (dialogs.Count > 0) dialogs[^1].IsEnabled = false;
        root.IsEnabled = false; dialogs.Add(dialog); Grid.SetRow(dialog, 1); layers.Children.Add(dialog);
    }
    internal void PopDialog(InAppDialog dialog)
    {
        layers.Children.Remove(dialog); dialogs.Remove(dialog);
        if (dialogs.Count > 0) dialogs[^1].IsEnabled = true;
        else root.IsEnabled = true;
    }
    void ClearRoot() { root.Children.Clear(); root.RowDefinitions.Clear(); root.ColumnDefinitions.Clear(); }
    string gateState = "Unknown";
    string accessError = "";
    void BuildGate(string? notice = null)
    {
        titleContent.Content = null;
        ClearRoot();
        bool creating = false;
        try { creating = !vault.Exists; gateState = creating ? "Create" : "Unlock"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { gateState = "Unavailable"; accessError = ex.Message; }
        bool available = !creating && gateState != "Unavailable" && File.Exists(vault.FilePath + ".quickfill");
        if (available && !preview) gateState = "WindowsAccess";
        var form = new StackPanel { Width = 420, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        form.Children.Add(Wordmark(30));
        form.Children.Add(Text(available ? "Deine Zugangsdaten sind bereit." : "Windows-Zugriff nicht verfügbar", 22, bold: true));
        if (!available)
            form.Children.Add(Text(notice ?? (accessError.Length > 0 ? accessError : "Der Windows-Schlüssel fehlt. Dein vorhandener Tresor bleibt erhalten."), 13, "#9299A5"));
        form.Children.Add(Button(available ? "Zugangsdaten öffnen" : "Erneut versuchen", ShowVault, true));
        root.Children.Add(form);
    }
    sealed class EmptyMessageVisibility : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is int length && length > 0 ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
    void Lock() { LockCore(true); if (!preview && !busy) Hide(); }
    void LockCore(bool forget)
    {
        CancelFill();
        ConcealSecrets();
        string? notice = null;
        searchDelay.Stop();
        if (forget)
        {
            rememberedUntil = default; rememberedLoginBlocked = true;
            try { rememberedLogin.Clear(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { notice = "Tresor gesperrt. Die gespeicherte Anmeldung konnte nicht vollständig entfernt werden. Bitte Dateizugriff prüfen."; }
        }
        if (busy) { lockPending = true; return; }
        foreach (var dialog in dialogs.ToArray().Reverse()) dialog.Close(true);
        foreach (Window w in OwnedWindows.Cast<Window>().ToArray()) w.Close();
        clipboard.Clear(); vault.Dispose(); data = null; selected = null; query = ""; category = null; favorites = false;
        accountList.Children.Clear(); detail.Children.Clear(); navigation.Children.Clear(); BuildGate(notice);
    }
    void BuildShell()
    {
        ResumeQuickFill();
        searchDelay.Stop();
        ClearRoot();
        navigation = new StackPanel(); accountList = new StackPanel(); detail = new StackPanel();
        status = Text("", 12, "#A5ABB5"); heading = Text("Alle Zugangsdaten", 30, bold: true); count = Text("", 13, "#9299A5");
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var side = new Grid { Background = Brush("#0A0C0F") };
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); side.RowDefinitions.Add(new RowDefinition()); side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var titleBarLeft = new Grid { Width = 209, Height = WindowFrame.CaptionHeight, Background = Brush("#0A0C0F") };
        var titleDivider = new Border { Width = 1, Background = Brush("#252A31"), HorizontalAlignment = HorizontalAlignment.Right, IsHitTestVisible = false };
        titleBarLeft.Children.Add(titleDivider);
        titleContent.Content = titleBarLeft;
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 16, 12, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        brand.Children.Add(Wordmark(34)); side.Children.Add(brand);
        navigation.Margin = new Thickness(12, 0, 12, 0); var ns = new ScrollViewer { Content = navigation }; Grid.SetRow(ns, 1); side.Children.Add(ns);
        var bottom = new StackPanel { Margin = new Thickness(14, 12, 14, 20) };
        bottom.Children.Add(new Border { Height = 1, Background = Brush("#292929"), Margin = new Thickness(10, 0, 10, 12) });
        Button SidebarAction(string label, string geometry, Action action)
        {
            var button = Button(label, action);
            button.Style = (Style)FindResource("GhostButton");
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(12, 10, 12, 10); button.Margin = new Thickness(0);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse(geometry), Stroke = Brush("#BDBDBD"),
                StrokeThickness = 1.3, Width = 16, Height = 16, Stretch = System.Windows.Media.Stretch.Uniform,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeLineJoin = System.Windows.Media.PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
            });
            var text = Text(label, 13); Grid.SetColumn(text, 1); row.Children.Add(text);
            button.Content = row; return button;
        }
        var settingsButton = SidebarAction("Einstellungen",
            "M9,1 L11,1 L11.6,3.3 L13.2,4.2 L15.5,3.6 L16.5,5.4 L14.8,7.1 L14.8,8.9 L16.5,10.6 L15.5,12.4 L13.2,11.8 L11.6,12.7 L11,15 L9,15 L8.4,12.7 L6.8,11.8 L4.5,12.4 L3.5,10.6 L5.2,8.9 L5.2,7.1 L3.5,5.4 L4.5,3.6 L6.8,4.2 L8.4,3.3 Z M12.5,8 A2.5,2.5 0 1 1 7.5,8 A2.5,2.5 0 1 1 12.5,8", Settings);
        settingsButton.Background = System.Windows.Media.Brushes.Transparent;
        var lockButton = SidebarAction("vault ausblenden",
            "M4,7 L12,7 Q14,7 14,9 L14,14 Q14,16 12,16 L4,16 Q2,16 2,14 L2,9 Q2,7 4,7 Z M4,7 L4,5 A4,4 0 0 1 12,5 L12,7 M8,11 L8,13", Lock);
        lockButton.Background = System.Windows.Media.Brushes.Transparent;
        lockButton.Margin = new Thickness(0, 0, 0, 4); bottom.Children.Add(lockButton);
        bottom.Children.Add(settingsButton); Grid.SetRow(bottom, 2); side.Children.Add(bottom); root.Children.Add(side);
        var sidebarHost = new Grid { Background = Brush("#0A0C0F") };
        root.Children.Remove(side); sidebarHost.Children.Add(side); root.Children.Add(sidebarHost);
        var toggle = Button("‹", () => { });
        toggle.Style = (Style)FindResource("GhostButton");
        toggle.Width = 30; toggle.Height = WindowFrame.CaptionButtonHeight; toggle.Padding = new Thickness(0);
        toggle.Margin = new Thickness(12, 3, 0, 3); toggle.HorizontalAlignment = HorizontalAlignment.Left;
        toggle.VerticalAlignment = VerticalAlignment.Center; toggle.Background = System.Windows.Media.Brushes.Transparent;
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(toggle, true);
        titleBarLeft.Children.Add(toggle);
        Border? mainSurface = null;
        void UpdateSidebar()
        {
            root.ColumnDefinitions[0].Width = new GridLength(sidebarCollapsed ? 0 : 208);
            side.Visibility = sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            titleBarLeft.Width = sidebarCollapsed ? 54 : 209;
            titleBarLeft.Background = Brush(sidebarCollapsed ? "#0D0F12" : "#0A0C0F");
            titleDivider.Visibility = sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            if (mainSurface != null) mainSurface.BorderThickness = new Thickness(sidebarCollapsed ? 0 : 1, 0, 0, 0);
            toggle.Content = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse("M4,1 L12,1 Q15,1 15,4 L15,10 Q15,13 12,13 L4,13 Q1,13 1,10 L1,4 Q1,1 4,1 Z " +
                    (sidebarCollapsed ? "M5,4 L5,10" : "M5,1 L5,13")),
                Stroke = Brush("#BDBDBD"), StrokeThickness = 1,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeLineJoin = System.Windows.Media.PenLineJoin.Round,
                Width = 14, Height = 12, Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            toggle.ToolTip = sidebarCollapsed ? "Seitenleiste ausklappen" : "Seitenleiste einklappen";
            System.Windows.Automation.AutomationProperties.SetName(toggle, (string)toggle.ToolTip);
        }
        toggle.Click += (_, _) => { sidebarCollapsed = !sidebarCollapsed; UpdateSidebar(); };
        UpdateSidebar();
        var main = new Grid { Margin = new Thickness(24, 16, 24, 16) };
        mainSurface = new Border { Background = Brush("#0D0F12"),
            CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(sidebarCollapsed ? 0 : 1, 0, 0, 0), BorderBrush = Brush("#252A31"), Child = main };
        Grid.SetColumn(mainSurface, 1); root.Children.Add(mainSurface);
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); main.RowDefinitions.Add(new RowDefinition()); main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel(); var add = Button("+  Zugangsdaten hinzufügen", () => Edit(null), true); add.HorizontalAlignment = HorizontalAlignment.Right; add.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(add, Dock.Right); header.Children.Add(add);
        var labels = new StackPanel(); heading.FontSize = 26; heading.Margin = new Thickness(0, 0, 0, 5); labels.Children.Add(heading); labels.Children.Add(count); header.Children.Add(labels); main.Children.Add(header);
        // Match the header and search-row spacing inside the continuous surface.
        var navigationOffset = new Border { Margin = new Thickness(0, 0, 0, 34), IsHitTestVisible = false };
        navigationOffset.SetBinding(HeightProperty, new System.Windows.Data.Binding("ActualHeight") { Source = header });
        side.Children.Add(navigationOffset);
        search = Input("Zugangsdaten durchsuchen", query); search.Height = 38; search.MinHeight = 38; search.FontSize = 13; search.Padding = new Thickness(34, 8, 32, 8); search.ToolTip = "Nach Name, E-Mail, Benutzername, Kategorie oder Website suchen";
        var searchArea = new Grid(); searchArea.Children.Add(search);
        var magnifier = new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse("M9,5 A4,4 0 1 1 1,5 A4,4 0 1 1 9,5 M8,8 L12,12"), Stroke = Brush("#929292"), StrokeThickness = 1.4, Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), IsHitTestVisible = false }; searchArea.Children.Add(magnifier);
        var placeholder = Text("Zugangsdaten suchen …", 13, "#858585"); placeholder.TextWrapping = TextWrapping.NoWrap; placeholder.TextTrimming = TextTrimming.CharacterEllipsis; placeholder.Margin = new Thickness(35, 0, 32, 0); placeholder.IsHitTestVisible = false; searchArea.Children.Add(placeholder);
        var clearSearch = Button("×", () => { search.Clear(); search.Focus(); }); clearSearch.Content = Ui.CloseIcon(); clearSearch.Width = 26; clearSearch.Height = 26; clearSearch.Padding = new Thickness(0); clearSearch.Margin = new Thickness(0, 0, 6, 0); clearSearch.HorizontalAlignment = HorizontalAlignment.Right; clearSearch.VerticalAlignment = VerticalAlignment.Center; clearSearch.Background = System.Windows.Media.Brushes.Transparent; clearSearch.ToolTip = "Suche leeren";
        System.Windows.Automation.AutomationProperties.SetName(clearSearch, "Suche leeren"); searchArea.Children.Add(clearSearch);
        void UpdateSearchHint() { placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; clearSearch.Visibility = search.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible; }
        search.TextChanged += (_, _) => { UpdateSearchHint(); query = search.Text; accountPage = 0; selected = null; accountScroller?.ScrollToTop(); searchDelay.Stop(); searchDelay.Start(); }; UpdateSearchHint();
        var body = new Grid { Margin = new Thickness(0, 18, 0, 0) }; body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.72, GridUnitType.Star), MinWidth = 270, MaxWidth = 340 }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.28, GridUnitType.Star) });
        var listPane = new Grid(); listPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); listPane.RowDefinitions.Add(new RowDefinition());
        var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition()); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.Children.Add(searchArea); listPane.Children.Add(searchRow);
        var sort = CreateSortButton();
        Grid.SetColumn(sort, 1); searchRow.Children.Add(sort);
        var listScroll = new ScrollViewer { Content = accountList }; accountScroller = listScroll; Grid.SetRow(listScroll, 1); listPane.Children.Add(listScroll); body.Children.Add(listPane);
        var detailCard = new Border { Child = new DetailViewport(detail), BorderBrush = Brush("#252A31"), BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(24, 0, 0, 0) }; Grid.SetColumn(detailCard, 2); body.Children.Add(detailCard); Grid.SetRow(body, 2); main.Children.Add(body);
        status.Margin = new Thickness(0, 16, 0, 0);
        status.SetBinding(VisibilityProperty, new System.Windows.Data.Binding("Text.Length") { Source = status, Converter = new EmptyMessageVisibility() });
        Grid.SetRow(status, 3); main.Children.Add(status);
        RefreshNavigation(); RefreshAccounts();
    }
    void RefreshNavigation()
    {
        if (data == null) return;
        navigation.Children.Clear();
        Button Nav(string text, string icon, int total, bool active, Action action)
        {
            var b = Button(text, action); b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.Style = (Style)FindResource("SidebarItem"); DesktopVisuals.SetIsSelected(b, active); b.Margin = new Thickness(0, 0, 0, 4); b.Padding = new Thickness(12, 8, 8, 8);
            b.Background = active ? Brush("#2D2D2D") : System.Windows.Media.Brushes.Transparent;
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var geometry = icon == "▦" ? "M1,1 L6,1 L6,6 L1,6 Z M10,1 L15,1 L15,6 L10,6 Z M1,10 L6,10 L6,15 L1,15 Z M10,10 L15,10 L15,15 L10,15 Z" : icon == "☆" ? "M8,1 L10,6 L15,6.5 L11,10 L12,15 L8,12.5 L4,15 L5,10 L1,6.5 L6,6 Z" : "M1,4 L6,4 L8,6 L15,6 L15,14 L1,14 Z";
            row.Children.Add(new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse(geometry), Stroke = Brush(icon == "·" ? CategoryColor(text) : active ? "#D0D0D0" : "#808080"), StrokeThickness = 1.2, Width = 15, Height = 15, Stretch = System.Windows.Media.Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, StrokeLineJoin = System.Windows.Media.PenLineJoin.Round });
            var label = Text(text, 13, active ? "#F1F3F5" : "#8D96A3", active); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis; label.Margin = new Thickness(0, 0, 6, 0); Grid.SetColumn(label, 1); row.Children.Add(label);
            var number = Text(total == 0 ? "" : total.ToString(), 10, "#697382"); number.HorizontalAlignment = HorizontalAlignment.Center;
            var badge = new Border { Child = number, MinWidth = 22, Padding = new Thickness(5, 2, 5, 2), CornerRadius = new CornerRadius(5), Background = System.Windows.Media.Brushes.Transparent }; Grid.SetColumn(badge, 2); row.Children.Add(badge); b.Content = row;
            System.Windows.Automation.AutomationProperties.SetName(b, text + ", " + total + " Einträge");
            navigation.Children.Add(b);
            return b;
        }
        Nav("Alle Zugangsdaten", "▦", data.Accounts.Count(a => a.DeletedUtc == null), category == null && !favorites, () => SelectCategory(null, false));
        Nav("Favoriten", "☆", data.Accounts.Count(a => a.DeletedUtc == null && a.Favorite), favorites, () => SelectCategory(null, true));
        var categoryHeader = new DockPanel { Margin = new Thickness(12, 22, 4, 8) };
        var addCategory = Button("+", AddCategory);
        addCategory.Style = (Style)FindResource("GhostButton");
        addCategory.Width = 28; addCategory.Height = 28; addCategory.FontSize = 10; addCategory.Foreground = Brush("#777F8C");
        addCategory.Padding = new Thickness(0); addCategory.Margin = new Thickness(0);
        addCategory.FocusVisualStyle = null;
        addCategory.VerticalAlignment = VerticalAlignment.Center;
        addCategory.Content = Text("+", 17, "#929292");
        addCategory.Background = System.Windows.Media.Brushes.Transparent;
        addCategory.ToolTip = "Neue Kategorie";
        System.Windows.Automation.AutomationProperties.SetName(addCategory, "Neue Kategorie");
        DockPanel.SetDock(addCategory, Dock.Right); categoryHeader.Children.Add(addCategory);
        categoryHeader.Children.Add(Text("Kategorien", 11, "#858585"));
        navigation.Children.Add(categoryHeader);
        foreach (var c in data.Categories)
        {
            var b = Nav(c, "·", data.Accounts.Count(a => a.DeletedUtc == null && a.Category == c), category == c, () => SelectCategory(c, false));
            var menu = new ContextMenu();
            var renameItem = new MenuItem { Header = "Kategorie bearbeiten" };
            renameItem.Click += (_, _) => RenameCategory(c);
            var deleteItem = new MenuItem { Header = "Kategorie löschen", Foreground = Brush("#F49098"), IsEnabled = data.Categories.Count > 1 };
            deleteItem.Click += (_, _) => DeleteCategory(c);
            menu.Items.Add(renameItem); menu.Items.Add(deleteItem); b.ContextMenu = menu;
            b.Tag = c;
            AccountDrag.Target(b,
                id => !busy && dialogs.Count == 0 && data?.Categories.Contains(c) == true && data.Accounts.Any(a => a.Id == id && a.DeletedUtc == null && a.Category != c),
                id =>
                {
                    if (Mutate(d =>
                    {
                        var account = d.Accounts.First(a => a.Id == id && a.DeletedUtc == null);
                        account.Category = c; account.Updated = DateTime.Now;
                    })) status.Text = "Zugangsdaten verschoben nach „" + c + "“.";
                });
            b.ToolTip = "Ziehen zum Verschieben · Rechtsklick zum Bearbeiten";
            CategoryDrag.Attach(b, c, (source, target, after) =>
            {
                if (data == null || !data.Categories.Contains(source) || !data.Categories.Contains(target)) return;
                var oldIndex = data.Categories.IndexOf(source);
                var newIndex = data.Categories.IndexOf(target) + (after ? 1 : 0);
                if (oldIndex < newIndex) newIndex--;
                if (oldIndex == newIndex) return;
                var positions = navigation.Children.OfType<Button>().Where(x => x.Tag is string)
                    .ToDictionary(x => (string)x.Tag, x => x.TranslatePoint(new Point(), navigation).Y);
                if (!Mutate(d =>
                {
                    d.Categories.Remove(source);
                    d.Categories.Insert(d.Categories.IndexOf(target) + (after ? 1 : 0), source);
                })) return;
                navigation.UpdateLayout();
                foreach (var moved in navigation.Children.OfType<Button>().Where(x => x.Tag is string))
                {
                    if (!SystemParameters.ClientAreaAnimation || !positions.TryGetValue((string)moved.Tag, out var previousY)) continue;
                    var offset = previousY - moved.TranslatePoint(new Point(), navigation).Y;
                    if (Math.Abs(offset) < 0.5) continue;
                    var shift = new System.Windows.Media.TranslateTransform(); moved.RenderTransform = shift;
                    shift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(180))
                        { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop,
                            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
                }
                status.Text = "Reihenfolge gespeichert.";
            });
        }
    }
    void SelectCategory(string? value, bool fav) { category = value; favorites = fav; accountPage = 0; selected = null; RefreshNavigation(); RefreshAccounts(); accountScroller?.ScrollToTop(); }
    void FocusSearch() { FocusManager.SetFocusedElement(this, search); search.Focus(); search.SelectAll(); }
    void SelectAccount(Guid id)
    {
        if (selected == id) return;
        selected = id;
        foreach (var button in accountList.Children.OfType<Button>())
            DesktopVisuals.SetIsSelected(button, button.Tag is Guid entryId && entryId == id);
        ShowDetail();
    }
    void RefreshAccounts()
    {
        searchDelay.Stop();
        if (data == null) return;
        heading.Text = favorites ? "Favoriten" : category ?? "Alle Zugangsdaten";
        var accounts = AccountQueries.Filter(data, category, favorites, query, prefs.AccountSort);
        count.Text = accounts.Count == 1 ? "1 Eintrag" : $"{accounts.Count} Einträge";
        int selectedIndex = accounts.FindIndex(a => a.Id == selected);
        if (selectedIndex >= 0) accountPage = selectedIndex / 100;
        accountPage = Math.Clamp(accountPage, 0, Math.Max(0, (accounts.Count - 1) / 100));
        if (selectedIndex < 0) selected = accounts.Skip(accountPage * 100).FirstOrDefault()?.Id;
        accountList.Children.Clear();
        if (accounts.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(16, 35, 16, 0) };
            empty.Children.Add(Text("Noch ganz für dich.", 21, bold: true));
            var desc = Text(string.IsNullOrWhiteSpace(query) ? "Lege deine ersten Zugangsdaten an. E-Mail, Benutzername und Passwort sind dann nur einen Klick entfernt." : "Keine Treffer. Versuche einen anderen Suchbegriff.", 14, "#9299A5"); desc.Margin = new Thickness(0, 14, 0, 20); empty.Children.Add(desc);
            empty.Children.Add(string.IsNullOrWhiteSpace(query) ? Button("+  Zugangsdaten hinzufügen", () => Edit(null), true) : Button("Suche zurücksetzen", () => search.Clear())); accountList.Children.Add(empty);
        }
        foreach (var a in accounts.Skip(accountPage * 100).Take(100))
        {
            var row = new StackPanel();
            var title = Text(a.Title + (a.Favorite ? "  ★" : ""), 14, bold: true); title.Margin = new Thickness(0, 0, 0, 4);
            title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis; row.Children.Add(title);
            var sub = Text(string.IsNullOrEmpty(a.Username) ? a.Email : a.Username, 12, "#9299A5"); sub.TextTrimming = TextTrimming.CharacterEllipsis; sub.TextWrapping = TextWrapping.NoWrap; row.Children.Add(sub);
            var b = Button("", () => SelectAccount(a.Id)); b.Tag = a.Id; b.Content = row; b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.Style = (Style)FindResource("AccountListItem"); DesktopVisuals.SetIsSelected(b, a.Id == selected);
            b.Padding = new Thickness(12, 10, 12, 10); b.Margin = new Thickness(0, 0, 0, 4);
            System.Windows.Automation.AutomationProperties.SetName(b, "Account " + a.Title); accountList.Children.Add(b);
            AccountDrag.Source(b, a.Id, a.Title,
                () => !busy && dialogs.Count == 0 && data?.Accounts.Any(item => item.Id == a.Id && item.DeletedUtc == null) == true);
        }
        if (accounts.Count > 100)
        {
            var paging = new WrapPanel();
            var back = Button("‹", () => { accountPage--; selected = null; RefreshAccounts(); accountScroller?.ScrollToTop(); }); back.IsEnabled = accountPage > 0; back.ToolTip = "Vorherige Seite"; paging.Children.Add(back);
            paging.Children.Add(Text($"{accountPage + 1} / {(accounts.Count + 99) / 100}  ", 12));
            var next = Button("›", () => { accountPage++; selected = null; RefreshAccounts(); accountScroller?.ScrollToTop(); }); next.IsEnabled = (accountPage + 1) * 100 < accounts.Count; next.ToolTip = "Nächste Seite"; paging.Children.Add(next); accountList.Children.Add(paging);
        }
        ShowDetail();
    }
    string CategoryColor(string c) => data != null && data.CategoryColors.TryGetValue(c, out var color) ? color : "#3B82F6";
    void ConcealSecrets() { foreach (var conceal in concealSecrets) conceal(); concealSecrets.Clear(); }
    void ShowDetail()
    {
        ConcealSecrets();
        detail.Children.Clear();
        var a = data?.Accounts.FirstOrDefault(a => a.Id == selected && a.DeletedUtc == null);
        if (a == null)
        {
            var symbol = Text("◇", 64, "#F5F5F5"); symbol.Margin = new Thickness(0, 48, 0, 24); detail.Children.Add(symbol);
            detail.Children.Add(Text("Platz für deine Zugangsdaten.", 23, bold: true));
            var help = Text("Wähle links einen Account aus oder füge einen neuen hinzu. Deine Zugangsdaten bleiben lokal verschlüsselt.", 14, "#979EAA"); help.Margin = new Thickness(0, 16, 0, 0); detail.Children.Add(help); return;
        }
        var heading = new Grid(); heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        labels.Children.Add(Text(a.Category.ToUpperInvariant(), 10, CategoryColor(a.Category), true));
        var title = Text(a.Title, 26, bold: true); title.Margin = new Thickness(0, 8, 0, 4); labels.Children.Add(title);
        labels.Children.Add(Text("Zuletzt geändert: " + a.Updated.ToString("dd.MM.yyyy"), 11, "#818A98")); heading.Children.Add(labels);
        var edit = Button("Bearbeiten", () => Edit(a));
        edit.SetResourceReference(StyleProperty, "GhostButton"); edit.Background = Brush("#1B2636"); edit.Foreground = Brush("#D7E6FB");
        edit.Margin = new Thickness(0, 16, 0, 0); edit.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(edit, 1); heading.Children.Add(edit); detail.Children.Add(heading);
        heading.Margin = new Thickness(8, 0, 0, 16);
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        void ActionButton(string label, Action action, bool destructive = false)
        {
            var b = Button(label, action); b.SetResourceReference(StyleProperty, "GhostButton");
            b.Foreground = Brush(destructive ? "#D99099" : "#8D96A3"); b.FontSize = 12; b.Margin = new Thickness(0, 0, 4, 4); b.Padding = new Thickness(8, 6, 8, 6);
            actions.Children.Add(b);
        }
        ActionButton(a.Favorite ? "Favorit entfernen" : "Als Favorit", () => Mutate(d => d.Accounts.First(item => item.Id == a.Id).Favorite = !a.Favorite));
        ActionButton("Löschen", () =>
        {
            if (Confirm(this, "In den Papierkorb?", "„" + a.Title + "“ wird 30 Tage verschlüsselt im Papierkorb aufbewahrt."))
                Mutate(d => d.Accounts.First(item => item.Id == a.Id).DeletedUtc = DateTime.UtcNow);
        }, true);
        actions.Children.Add(CreateAccountMenu(a));
        detail.Children.Add(actions);
        Credential("E-Mail", a.Email); Credential("Benutzername", a.Username); Credential("Passwort", a.Password, true);
        if (!string.IsNullOrWhiteSpace(a.Website))
        {
            var website = Button("Website öffnen ↗", () =>
            {
                if (!AccountQueries.ValidWebsite(a.Website)) { status.Text = "Nur gültige http- oder https-Adressen können geöffnet werden."; return; }
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(a.Website) { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { status.Text = "Website konnte nicht geöffnet werden."; }
            });
            website.Style = (Style)FindResource("GhostButton"); website.Foreground = Brush("#8D96A3");
            website.ToolTip = a.Website; website.HorizontalAlignment = HorizontalAlignment.Left; website.Margin = new Thickness(0, 8, 0, 0); detail.Children.Add(website);
        }
        foreach (var field in a.Fields) Credential(field.Label, field.Value, true);
        if (!string.IsNullOrWhiteSpace(a.Notes)) { var l = Text("Notiz", 12, "#8D96A3"); l.Margin = new Thickness(8, 20, 8, 8); detail.Children.Add(l); var notes = Text(a.Notes, 13, "#BDC2CC"); notes.Margin = new Thickness(8, 0, 8, 0); detail.Children.Add(notes); }
    }
    void Credential(string label, string value, bool secret = false)
    {
        var panel = new Grid(); panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = Text(string.IsNullOrEmpty(value) ? "Nicht hinterlegt" : secret ? "••••••••••••" : value, 14, string.IsNullOrEmpty(value) ? "#747C89" : "#E2E6EC");
        text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis; text.Margin = new Thickness(0, 0, 8, 0); panel.Children.Add(text);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        if (secret)
        {
            var show = IconButton(EyeGeometry, "Passwort anzeigen", () => { }); bool revealed = false;
            var concealTimer = new DispatcherTimer();
            void HideSecret() { concealTimer.Stop(); revealed = false; text.Text = value.Length == 0 ? "Nicht hinterlegt" : "••••••••••••"; SetButtonIcon(show, EyeGeometry, "Passwort anzeigen"); }
            concealTimer.Tick += (_, _) => HideSecret(); concealSecrets.Add(HideSecret);
            show.Click += (_, _) =>
            {
                revealed = !revealed; text.Text = revealed ? value : "••••••••••••";
                SetButtonIcon(show, revealed ? EyeOffGeometry : EyeGeometry, revealed ? "Passwort verbergen" : "Passwort anzeigen");
                concealTimer.Stop(); if (revealed) { concealTimer.Interval = TimeSpan.FromSeconds(prefs.RevealSeconds); concealTimer.Start(); }
            }; show.IsEnabled = value.Length > 0; buttons.Children.Add(show);
        }
        var copyFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        Button copy = null!;
        copy = IconButton(CopyGeometry, label + " kopieren", () =>
        {
            try { clipboard.ClearAfterSeconds = prefs.ClipboardSeconds; clipboard.Copy(value); SetButtonIcon(copy, "M2,7 L6,11 L14,3", label + " kopiert"); copyFeedbackTimer.Stop(); copyFeedbackTimer.Start(); status.Text = label + " kopiert · Wird nach " + prefs.ClipboardSeconds + " Sekunden automatisch entfernt."; }
            catch (System.Runtime.InteropServices.COMException) { status.Text = "Zwischenablage gerade belegt. Bitte erneut versuchen."; }
        }); copy.Margin = new Thickness(0); copy.IsEnabled = value.Length > 0;
        void ResetCopy() { copyFeedbackTimer.Stop(); SetButtonIcon(copy, CopyGeometry, label + " kopieren"); }
        copyFeedbackTimer.Tick += (_, _) => ResetCopy(); concealSecrets.Add(ResetCopy);
        System.Windows.Automation.AutomationProperties.SetName(copy, label + " kopieren"); buttons.Children.Add(copy); Grid.SetColumn(buttons, 1); panel.Children.Add(buttons);
        detail.Children.Add(new DetailRow(label, panel));
    }
    bool Mutate(Action<VaultData> mutation)
    {
        if (data == null) return false;
        try
        {
            var next = data.Clone(); mutation(next); next.PurgeExpired(DateTime.UtcNow); vault.Save(next); data = next;
            RefreshNavigation(); RefreshAccounts(); refreshQuickFill?.Invoke(); status.Text = "Gespeichert · Verschlüsselt"; _ = QueueAutomaticBackup(); return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
        { status.Text = "Nicht gespeichert: " + ex.Message; return false; }
    }
    void AddCategory() => EditCategory(null);
    void RenameCategory(string? old) => EditCategory(old);
    void DeleteCategory(string? old)
    {
        if (data == null || old == null || !data.Categories.Contains(old)) return;
        if (data.Categories.Count == 1) { status.Text = "Mindestens eine Kategorie muss erhalten bleiben."; return; }
        var form = new StackPanel();
        var number = data.Accounts.Count(a => a.Category == old);
        var note = Text(number == 0 ? "„" + old + "“ ist leer und wird gelöscht." : $"{number} Accounts aus „{old}“ bleiben erhalten. Wähle ihre neue Kategorie.", 14, "#A5ABB5");
        note.Margin = new Thickness(0, 18, 0, 12); form.Children.Add(note);
        var targets = data.Categories.Where(c => c != old).ToList();
        var destination = new ComboBox { ItemsSource = targets, SelectedIndex = 0 };
        if (number > 0) Field(form, "Accounts verschieben nach", destination);
        var w = Dialog(this, "Kategorie löschen", form);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) };
        actions.Children.Add(Button("Kategorie löschen", () =>
        {
            var target = (string)destination.SelectedItem;
            if (Mutate(d => { d.Categories.Remove(old); d.CategoryColors.Remove(old); foreach (var a in d.Accounts.Where(a => a.Category == old)) a.Category = target; }))
            { SelectCategory(target, false); w.Close(); }
        }, true));
        actions.Children.Add(Button("Abbrechen", () => w.Close())); form.Children.Add(actions); w.ShowDialog();
    }
    internal static string GeneratePassword() => new GeneratorOptions().Generate();
    void Settings() => CreateSettingsWindow().ShowDialog();
    void Backup(TextBlock? feedback = null)
    {
        var dialog = new SaveFileDialog { Filter = "vault-Tresor (*.nox)|*.nox", FileName = "vault-Sicherung-" + DateTime.Now.ToString("yyyy-MM-dd") + ".nox" };
        if (dialog.ShowDialog(this) != true) return;
        try { vault.Backup(dialog.FileName); status.Text = "Verschlüsselte Sicherung erstellt."; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { status.Text = "Sicherung fehlgeschlagen: " + ex.Message; }
        if (feedback != null) feedback.Text = status.Text;
    }
    string? ChooseBackup()
    {
        var dialog = new OpenFileDialog { Filter = "vault-Tresor (*.nox)|*.nox" };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }
    VaultData? UnlockBackup(string path)
    {
        try { return vault.ReadBackupForWindows(path); }
        catch (CryptographicException) { } // Older password-protected imports may use another key.
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { status.Text = "Sicherung nicht lesbar: " + ex.Message; return null; }
        var form = new StackPanel(); var pw = Secret("Passwort der Sicherung"); Field(form, "Passwort der Sicherung", pw);
        var error = Text("", 12, "#FF929A"); error.Margin = new Thickness(0, 12, 0, 12); form.Children.Add(error);
        var w = Dialog(this, "Sicherung öffnen", form); VaultData? backup = null;
        var check = Button("Sicherung prüfen", () => { }, true); form.Children.Add(check);
        w.Closing += (_, e) => { if (busy) e.Cancel = true; };
        check.Click += async (_, _) =>
        {
            if (busy) return;
            busy = true; check.IsEnabled = false; error.Text = "Sicherung wird geprüft …"; string secret = pw.Password;
            try { backup = await System.Threading.Tasks.Task.Run(() => Vault.ReadBackup(path, secret)); }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { error.Text = "Passwort falsch oder Sicherung nicht lesbar."; }
            finally { secret = ""; busy = false; check.IsEnabled = true; }
            if (lockPending) { backup = null; lockPending = false; Lock(); return; }
            if (backup != null) w.DialogResult = true;
        };
        w.ShowDialog(); pw.Clear(); return backup;
    }
    void Restore(TextBlock? feedback = null)
    {
        var path = ChooseBackup(); if (path == null || data == null) return;
        var backup = UnlockBackup(path); if (backup == null) return;
        if (!Confirm(this, "Tresor ersetzen?", $"Die Sicherung enthält {backup.Accounts.Count(a => a.DeletedUtc == null)} aktive Accounts und {backup.Accounts.Count(a => a.DeletedUtc != null)} Einträge im Papierkorb. Dateidatum: {File.GetLastWriteTime(path):dd.MM.yyyy HH:mm}. Die aktuellen Einträge werden ersetzt; zuvor wird eine verschlüsselte Sicherung im Tresorordner erstellt. Dein Windows-Zugriff bleibt erhalten.")) return;
        try
        {
            vault.Backup(Path.Combine(Preferences.Root, "vor-wiederherstellung-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".nox"));
            if (Mutate(d => { d.Categories = backup.Categories; d.CategoryColors = backup.CategoryColors; d.Accounts = backup.Accounts; })) SelectCategory(null, false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { status.Text = "Wiederherstellung abgebrochen: " + ex.Message; }
        if (feedback != null) feedback.Text = status.Text;
    }
}

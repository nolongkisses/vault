using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NoxVault;

// The vault window: lifecycle, locking and the hooks into Windows. Views are built in the other partial files.
internal sealed partial class MainWindow : Window
{
    readonly VaultSession session;
    readonly Preferences prefs;
    readonly Hotkeys hotkeys = new();
    readonly ClipboardGuard clipboard = new();
    readonly List<Action> concealSecrets = new();
    readonly DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly DispatcherTimer searchDelay = new() { Interval = TimeSpan.FromMilliseconds(150) };
    readonly Grid layers = new();
    readonly Grid root = new();
    readonly ContentControl captionContent;
    readonly List<InAppDialog> dialogs = new();
    readonly bool preview;
    DateTime lastActivity = DateTime.UtcNow;
    bool quitting;
    bool busy;
    bool lockPending;
    IntPtr handle;
    System.Windows.Forms.NotifyIcon? tray;
    string gateState = "Unknown";

    internal MainWindow(string? testPath = null, VaultData? previewData = null, bool designMode = false, bool startInTray = false)
    {
        preview = previewData != null || designMode;
        prefs = preview ? new Preferences { HotkeyModifiers = 3 } : Preferences.Load();
        session = new VaultSession(testPath ?? Path.Combine(Preferences.Root, "vault.nox"));
        Title = "vault";
        Width = 1180;
        Height = 780;
        MinWidth = 980;
        MinHeight = 660;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(Brand.IconUri);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Theme.Font;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        SetResourceReference(ForegroundProperty, Theme.Fg);
        layers.Children.Add(root);
        Content = layers;
        captionContent = WindowFrame.Attach(this, layers, root);
        SourceInitialized += (_, _) => Initialize();
        PreviewKeyDown += OnKey;
        PreviewMouseDown += (_, _) => lastActivity = DateTime.UtcNow;
        PreviewMouseMove += (_, _) => lastActivity = DateTime.UtcNow;
        idle.Tick += (_, _) => CheckIdle();
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); if (Data != null) RefreshAccounts(); };
        if (!preview)
        {
            idle.Start();
            SystemEvents.SessionSwitch += SessionChanged;
            SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        }
        Closing += OnClosing;
        if (previewData != null) { session.Data = previewData; BuildShell(); }
        else if (!preview && !startInTray && TryAutomaticLogin()) BuildShell();
        else BuildGate();
    }

    VaultData? Data => session.Data;

    void Initialize()
    {
        handle = new WindowInteropHelper(this).Handle;
        hotkeys.Attach(handle);
        Native.Round(this, Theme.Dark);
        HwndSource.FromHwnd(handle)?.AddHook(Hook);
        if (preview) return;
        hotkeys.OpenChanged += () => { if (tray != null) tray.Text = "vault · " + hotkeys.OpenLabel; };
        hotkeys.ReplaceOpen(prefs.HotkeyModifiers, prefs.EffectiveKey);
        hotkeys.RegisterFill(prefs.FillShortcutF);
        tray = CreateTray();
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        lastActivity = DateTime.UtcNow;
        if (Data == null || dialogs.Count != 0) return;
        if (e.Key == Key.Escape) { e.Handled = true; Lock(); }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; FocusSearch(); }
    }

    // Initializes the native handle, tray icon and shortcuts without showing or activating a window during sign-in.
    internal void StartInTray() => new WindowInteropHelper(this).EnsureHandle();

    void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) Dispatcher.BeginInvoke(Lock);
    }

    // Follows Windows' light/dark switch while the theme is set to "System".
    void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && prefs.Theme == "System") Dispatcher.BeginInvoke(() => Theme.Apply("System"));
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy || (quitting && automaticBackupRunning)) { e.Cancel = true; lockPending = true; quitting = false; return; }
        if (!quitting && !preview) { e.Cancel = true; LockCore(false); Hide(); return; }
        ConcealSecrets();
        idle.Stop();
        searchDelay.Stop();
        clipboard.Dispose();
        session.Dispose();
        tray?.Dispose();
        CancelFill();
        SystemEvents.SessionSwitch -= SessionChanged;
        SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        hotkeys.Release();
    }

    IntPtr Hook(IntPtr window, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        int id = message == Native.HotkeyMessage ? w.ToInt32() : 0;
        if (id == Hotkeys.EscapeId) { handled = true; fillCancellation?.Cancel(); }
        else if (id == Hotkeys.FillId) { handled = true; OpenQuickFill(); }
        else if (message == Native.PrepareUpdateMessage) { handled = true; QuitForUpdate(); }
        else if ((id != 0 && id == hotkeys.OpenId) || message == Native.ShowMessage) { handled = true; ShowVault(); }
        return IntPtr.Zero;
    }

    void QuitForUpdate()
    {
        if (busy || automaticBackupRunning || dialogs.Count != 0 || fillCancellation != null)
        {
            status.Text = "Update wartet: Bitte geöffnete Dialoge abschließen und das Update erneut starten.";
            return;
        }
        quitting = true;
        Close();
    }

    bool TryRememberedLogin()
    {
        lastActivity = DateTime.UtcNow;
        return session.TryRemembered();
    }

    bool TryAutomaticLogin()
    {
        lastActivity = DateTime.UtcNow;
        return session.TryAutomatic(mayCreate: !preview);
    }

    void ShowVault()
    {
        CheckIdle();
        if (Data == null && !busy && dialogs.Count == 0)
        {
            if (TryAutomaticLogin()) BuildShell();
            else BuildGate();
        }
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    internal string StartupDiagnostics() =>
        $"Visible={IsVisible}\nLocked={Data == null}\nTrayVisible={tray?.Visible == true}\nOpenShortcut={hotkeys.OpenLabel}\n"
        + $"OpenRegistered={hotkeys.OpenRegistered}\nFillShortcut={FillShortcutLabel}\nFillRegistered={hotkeys.FillRegistered}\n"
        + $"VaultPath={session.Vault.FilePath}\nGate={gateState}\n";

    // Only the design and test windows lock on inactivity; the installed app stays open until hidden (since 1.2.5).
    void CheckIdle()
    {
        if (preview && session.Expired(lastActivity, prefs.IdleMinutes)) Lock();
    }

    internal void PushDialog(InAppDialog dialog)
    {
        if (dialogs.Count > 0) dialogs[^1].IsEnabled = false;
        root.IsEnabled = false;
        dialogs.Add(dialog);
        Grid.SetRowSpan(dialog, 2);
        Panel.SetZIndex(dialog, 5);
        layers.Children.Add(dialog);
    }

    internal void PopDialog(InAppDialog dialog)
    {
        layers.Children.Remove(dialog);
        dialogs.Remove(dialog);
        if (dialogs.Count > 0) dialogs[^1].IsEnabled = true;
        else root.IsEnabled = true;
    }

    void Lock()
    {
        LockCore(true);
        if (!preview && !busy) Hide();
    }

    void LockCore(bool forget)
    {
        CancelFill();
        ConcealSecrets();
        searchDelay.Stop();
        string? notice = forget ? session.Forget() : null;
        if (busy) { lockPending = true; return; }
        foreach (var dialog in dialogs.ToArray().Reverse()) dialog.Close(true);
        foreach (Window owned in OwnedWindows.Cast<Window>().ToArray()) owned.Close();
        clipboard.Clear();
        session.Close();
        selected = null;
        query = "";
        category = null;
        favorites = false;
        accountList.Children.Clear();
        detail.Children.Clear();
        navigation.Rows.Clear();
        BuildGate(notice);
    }

    void ConcealSecrets()
    {
        foreach (var conceal in concealSecrets) conceal();
        concealSecrets.Clear();
    }

    // Saves first; views refresh only after the encrypted write succeeded.
    bool Mutate(Action<VaultData> mutation)
    {
        if (Data == null) return false;
        try { session.Commit(mutation); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or CryptographicException or InvalidOperationException)
        {
            status.Text = "Nicht gespeichert: " + ex.Message;
            return false;
        }
        RefreshNavigation();
        RefreshAccounts();
        refreshQuickFill?.Invoke();
        status.Text = "Gespeichert · Verschlüsselt";
        _ = QueueAutomaticBackup();
        return true;
    }

    void ClearRoot()
    {
        root.Children.Clear();
        root.RowDefinitions.Clear();
        root.ColumnDefinitions.Clear();
    }
}

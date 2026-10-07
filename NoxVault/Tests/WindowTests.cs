using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NoxVault;

// Window-level checks run by the self-test and the manual check modes (--hotkey-check, --frame-check). Synthetic data only.
internal sealed partial class MainWindow
{
    const string LifecyclePassword = "Synthetic lifecycle test!";

    internal void QuitForTest()
    {
        Native.PostMessage(new WindowInteropHelper(this).Handle, Native.PrepareUpdateMessage, IntPtr.Zero, IntPtr.Zero);
        Pump(DispatcherPriority.ApplicationIdle);
    }

    void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    static void Wait(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    internal void RefreshGateForTest() => ShowVault();

    VaultData OpenForTest()
    {
        session.Data = session.Vault.Open(LifecyclePassword);
        BuildShell();
        return session.Data;
    }

    internal void TestHotkeys()
    {
        new WindowInteropHelper(this).EnsureHandle();
        uint testKey = new uint[] { 0x78, 0x79, 0x7A }.FirstOrDefault(candidate => hotkeys.ReplaceOpen(7, candidate));
        if (testKey == 0) throw new Exception("No free shortcut for registration test");
        int savedId = hotkeys.OpenId;
        if (Native.RegisterHotKey(IntPtr.Zero, 1951, 7 | Native.NoRepeat, testKey))
        {
            Native.UnregisterHotKey(IntPtr.Zero, 1951);
            throw new Exception("Registered shortcut did not reserve combination");
        }
        // A conflicting replacement must leave the original registration active.
        uint blockedKey = testKey == 0x78 ? 0x79u : 0x78u;
        if (!Native.RegisterHotKey(IntPtr.Zero, 1952, 7 | Native.NoRepeat,
            blockedKey)) throw new Exception("Conflict-test key unavailable");
        try
        {
            if (hotkeys.ReplaceOpen(7, blockedKey)
            || hotkeys.OpenId != savedId) throw new Exception("Conflict replaced active shortcut");
        }
        finally { Native.UnregisterHotKey(IntPtr.Zero, 1952); }
        if (!hotkeys.ReplaceOpen(7, blockedKey)) throw new Exception("Shortcut replacement failed");
        hotkeys.ReleaseOpen();
    }

    internal void TestLifecycle()
    {
        session.Data = session.Vault.Create(LifecyclePassword);
        BuildShell();
        if (!Mutate(d => d.Accounts.Add(new Account
        {
            Title = "Lifecycle",
            Category = d.Categories[0]
        }))) throw new Exception("UI save failed");
        var lockOverlay = InAppDialog.Create(this, "Lock test", new StackPanel());
        Dispatcher.BeginInvoke(new Action(Lock));
        lockOverlay.ShowDialog();
        if (dialogs.Count != 0 || !root.IsEnabled) throw new Exception("Lock retained overlay state");
        if (Data != null || session.Vault.Unlocked || accountList.Children.Count != 0 || detail.Children.Count != 0)
            throw new Exception("Lock retained UI data");
        if (OpenForTest().Accounts.Count != 1) throw new Exception("Reopen failed");
        SelectCategory("Valorant", false);
        query = "NO-MATCH";
        RefreshAccounts();
        if (selected != null) throw new Exception("Search filter failed");
        lastActivity = DateTime.UtcNow.AddMinutes(-6);
        CheckIdle();
        if (Data != null || session.Vault.Unlocked) throw new Exception("Idle lock failed");
        RememberedLifecycle();
        SearchLifecycle();
    }

    void RememberedLifecycle()
    {
        OpenForTest();
        session.Remember();
        lastActivity = DateTime.UtcNow.AddMinutes(-6);
        CheckIdle();
        if (Data == null) throw new Exception("Remembered login should survive inactivity");
        LockCore(false);
        if (Data != null || session.Vault.Unlocked || !TryRememberedLogin()) throw new Exception("Remembered login failed after hiding");
        BuildShell();
        Lock();
        if (TryRememberedLogin()) throw new Exception("Explicit lock failed to revoke remembered login");
        OpenForTest();
        session.Remember();
        session.RememberedUntil = DateTime.Now.Date;
        CheckIdle();
        if (Data != null || session.Vault.Unlocked
            || TryRememberedLogin()) throw new Exception("Midnight failed to lock and revoke session");
        OpenForTest();
        session.Remember();
        using var heldSession = new FileStream(session.Vault.FilePath + ".session", FileMode.Open, FileAccess.Read, FileShare.Read);
        Lock();
        if (Data != null || session.Vault.Unlocked || TryRememberedLogin()) throw new Exception("Session file error prevented locking");
        using var reopened = new Vault(session.Vault.FilePath);
        if (new RememberedLogin(session.Vault.FilePath).Restore(reopened, DateTime.Now, out _) != null)
            throw new Exception("Revoked session restored in a new instance");
    }

    void SearchLifecycle()
    {
        session.Data = session.Vault.Open(LifecyclePassword);
        category = null;
        query = "";
        BuildShell();
        search.Text = "NO";
        search.Text = "NO-MATCH";
        if (!searchDelay.IsEnabled || accountList.Children.Count != 1) throw new Exception("Search was not deferred");
        FocusSearch();
        if (search.SelectionLength != search.Text.Length || FocusManager.GetFocusedElement(this) != search)
            throw new Exception("Search shortcut did not select input");
        Wait(TimeSpan.FromMilliseconds(250));
        if (searchDelay.IsEnabled || selected != null
            || count.Text != "0") throw new Exception("Deferred search returned incorrect results");
        search.Text = "Lifecycle";
        Lock();
        if (searchDelay.IsEnabled || accountList.Children.Count != 0) throw new Exception("Pending search survived lock");
        session.Vault.Dispose();
    }

    internal void TestSecurityOptions()
    {
        OpenForTest();
        prefs.IdleMinutes = 15;
        lastActivity = DateTime.UtcNow.AddMinutes(-6);
        CheckIdle();
        if (Data == null) throw new Exception("Configured idle duration ignored");
        prefs.IdleMinutes = 1;
        CheckIdle();
        if (Data != null || session.Vault.Unlocked) throw new Exception("Short idle duration did not lock");
        prefs.IdleMinutes = 5;
        session.Data = session.Vault.Open(LifecyclePassword);
        session.Data.Accounts[0].Password = "Synthetic reveal test";
        BuildShell();
        var row = detailRows[2];
        var show = (Button)row.Actions.Children[0];
        prefs.RevealSeconds = 0;
        Click(show);
        if (row.Value.Text != "Synthetic reveal test") throw new Exception("Reveal failed");
        Wait(TimeSpan.FromMilliseconds(30));
        if (row.Value.Text == "Synthetic reveal test") throw new Exception("Automatic concealment failed");
        prefs.RevealSeconds = 15;
        Click(show);
        RefreshAccounts();
        if (row.Value.Text == "Synthetic reveal test") throw new Exception("Account navigation retained revealed password");
        Lock();
    }

    internal void TestFeatureDialogs(Action<string> render)
    {
        void Run(Action open, string name)
        {
            Exception? failure = null;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { render(name); }
                catch (Exception ex) { failure = ex; } // Rethrown once the nested frame has ended.
                finally { foreach (var dialog in dialogs.ToArray().Reverse()) dialog.Close(true); }
            }));
            open();
            if (failure != null) throw failure;
        }
        Run(() => Edit(Data!.Accounts[0]), "account-editor");
        Run(() => Edit(null), "account-add");
        TestPasswordEditor();
        Run(() => ShowGenerator(new PasswordBox()), "generator");
        Run(ShowTrash, "trash");
        Run(ShowPasswordCheck, "password-check");
        Run(Settings, "settings-dialog");
    }

    void TestPasswordEditor()
    {
        var secret = new PasswordBox { Password = "Synthetic editor password", MaxLength = 2048 };
        var editor = EditorPassword(secret);
        Click(editor.Reveal);
        if (editor.Visible.Text != secret.Password
            || editor.Visible.Visibility != Visibility.Visible) throw new Exception("Editor reveal failed");
        editor.Visible.Text = "Edited synthetic password";
        if (secret.Password != editor.Visible.Text) throw new Exception("Visible password edit was lost");
        secret.Password = "Generated synthetic password";
        if (editor.Visible.Text != secret.Password) throw new Exception("Generator did not update visible editor");
        editor.Clear();
        if (editor.Visible.Text.Length != 0 || secret.Password.Length != 0 || editor.Visible.Visibility != Visibility.Collapsed)
            throw new Exception("Editor did not clear both password controls");
    }

    internal void TestPagedAccounts(int expected)
    {
        if (Data == null || Data.Accounts.Count != expected
            || accountList.Children.Count > PageSize + 1) throw new Exception("Unbounded account presentation");
        selected = Data.Accounts.Last().Id;
        RefreshAccounts();
        if (selected != Data.Accounts.Last().Id
            || accountPage != (expected - 1) / PageSize) throw new Exception("Last account unreachable");
        var first = (Button)accountList.Children[0];
        Click(first);
        if (!ReferenceEquals(first, accountList.Children[0])
            || selected != (Guid)first.Tag) throw new Exception("Selection rebuilt the list or selected the wrong entry");
    }

    internal string MeasureSelection(int size)
    {
        void Select(int i) => Click((Button)accountList.Children[i % 2]);
        for (int i = 0; i < 10; i++) Select(i);
        long start = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) Select(i);
        watch.Stop();
        double allocated = (GC.GetAllocatedBytesForCurrentThread() - start) / 1048576.0;
        return FormattableString.Invariant($"selection_100_{size},ms={watch.Elapsed.TotalMilliseconds:F2},allocated_MiB={allocated:F2}");
    }

    // --hotkey-check: Ctrl+Alt+F8 must reopen the hidden window, then restore it from minimized. Pressed by a human.
    internal static void StartHotkeyCheck(string report)
    {
        var window = new MainWindow(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nox"), designMode: true);
        var input = new Window
        {
            Title = "vault Shortcut-Test", Width = 460, Height = 180,
            Content = new TextBlock { Text = "Shortcut-Test: Strg + Alt + F8", Margin = new Thickness(Theme.S7) },
        };
        input.Show();
        window.BeginHotkeyCheck(report);
        input.Activate();
    }

    void BeginHotkeyCheck(string reportPath)
    {
        Show();
        bool registered = hotkeys.ReplaceOpen(3, 0x77); // Ctrl+Alt+F8; isolated test, no preference writes.
        File.WriteAllText(reportPath, registered ? "READY: Ctrl+Alt+F8 registered"
            : "FAIL: registration " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        Hide();
        int stage = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) =>
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (!IsActive) { File.WriteAllText(reportPath, "FAIL: reopened but not active"); timer.Stop(); return; }
            if (stage++ == 0)
            {
                File.WriteAllText(reportPath,
                    "PASS: hidden vault reopened and activated\nREADY: press Ctrl+Alt+F8 again to restore minimized vault");
                WindowState = WindowState.Minimized;
            }
            else { File.AppendAllText(reportPath, "\nPASS: minimized vault restored and activated"); timer.Stop(); }
        };
        timer.Start();
    }

    // --frame-check: the window over a white backdrop, to inspect the rounded native frame by eye.
    internal static void StartFrameCheck()
    {
        var backdrop = new Window
        {
            Title = "Nox frame check backdrop", Width = 1280, Height = 880, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = System.Windows.Media.Brushes.White,
        };
        backdrop.Show();
        var sample = new MainWindow(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nox"), designMode: true)
        {
            Title = "Nox frame check", Owner = backdrop,
        };
        sample.Closed += (_, _) => backdrop.Close();
        sample.Show();
    }
}

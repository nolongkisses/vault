using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace NoxVault;

public partial class App : Application
{
    Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        // Avoid GPU/driver antialiasing seams around transparent, clipped WPF
        // surfaces. These do not appear in RenderTargetBitmap previews.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
        if (e.Args.Contains("--autofill-ui-test"))
        {
            var test = new MainWindow(Path.Combine(Path.GetTempPath(), "NoxVault-FillTest-" + Guid.NewGuid() + ".nox"));
            MainWindow = test; test.Show(); test.PrepareFillIntegrationTest(e.Args.Last(), e.Args.Contains("--auto")); return;
        }
        if (e.Args.Contains("--prepare-update"))
        {
            Native.PostMessage(Native.FindWindow(null, "vault"), 0x8002, IntPtr.Zero, IntPtr.Zero);
            Shutdown(); return;
        }
        if (e.Args.Contains("--hotkey-check"))
        {
            var checkWindow = new MainWindow(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nox"), designMode: true);
            var inputWindow = new Window { Title = "vault Shortcut-Test", Width = 460, Height = 180,
                Content = new System.Windows.Controls.TextBlock { Text = "Shortcut-Test: Strg + Alt + F8", Margin = new Thickness(24) } };
            inputWindow.Show(); checkWindow.BeginHotkeyCheck(e.Args.Last()); inputWindow.Activate();
            return;
        }
        if (e.Args.Contains("--frame-check"))
        {
            // Isolated visual check against white, without opening a real vault.
            var backdrop = new Window { Title = "Nox frame check backdrop", Width = 1280, Height = 880,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = System.Windows.Media.Brushes.White };
            backdrop.Show();
            var sample = new MainWindow(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nox"), designMode: true)
                { Title = "Nox frame check", Owner = backdrop };
            sample.Closed += (_, _) => backdrop.Close();
            sample.Show();
            return;
        }
        if (e.Args.Contains("--self-test") || e.Args.Contains("--self-test-native"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { SelfTest.Run(e.Args.Last(), e.Args.Contains("--self-test-native")); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(e.Args.Last(), ex.ToString()); Shutdown(1); }
            return;
        }
        instance = new Mutex(true, "Local\\NoxVault.Desktop.v1", out bool first);
        if (!first)
        {
            if (!e.Args.Contains("--tray"))
                Native.PostMessage(Native.FindWindow(null, "vault"), 0x8001, IntPtr.Zero, IntPtr.Zero);
            Shutdown(); return;
        }
        bool startInTray = e.Args.Contains("--tray");
        try { StoragePaths.Initialize(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show("Der Tresor konnte nicht sicher geladen werden.\n\n" + ex.Message,
                "vault · Datenordner prüfen", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1); return;
        }
        StartupTrace.Write("CreatingWindow Tray=" + startInTray);
        var w = new MainWindow(startInTray: startInTray); MainWindow = w;
        if (startInTray) w.StartInTray(); else w.Show();
        StartupTrace.Write("Ready " + w.StartupDiagnostics().Replace('\n', ' '));
        try
        {
            Directory.CreateDirectory(Preferences.Root);
            File.WriteAllText(Path.Combine(Preferences.Root, "startup.log"),
                $"Started={DateTimeOffset.Now:O}\nTrayRequested={startInTray}\n" + w.StartupDiagnostics());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (e.Args.Contains("--startup-check")) File.WriteAllText(e.Args.Last(), w.StartupDiagnostics());
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}

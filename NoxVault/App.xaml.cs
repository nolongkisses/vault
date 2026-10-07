using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace NoxVault;

// Picks the launch mode: normal start (single instance), tray start, or one of the isolated check modes.
internal partial class App : Application
{
    Mutex? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Avoid GPU/driver antialiasing seams around transparent, clipped WPF
        // surfaces. These do not appear in RenderTargetBitmap previews.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
        if (!StartCheckMode(e.Args)) StartNormally(e.Args);
    }

    bool StartCheckMode(string[] args)
    {
        string report = args.LastOrDefault() ?? "";
        if (args.Contains("--autofill-ui-test"))
        {
            var test = new MainWindow(Path.Combine(Path.GetTempPath(), "NoxVault-FillTest-" + Guid.NewGuid() + ".nox"));
            MainWindow = test;
            test.Show();
            test.PrepareFillIntegrationTest(report, args.Contains("--auto"));
        }
        else if (args.Contains("--prepare-update"))
        {
            Native.PostMessage(Native.FindWindow(null, "vault"), Native.PrepareUpdateMessage, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
        }
        else if (args.Contains("--hotkey-check")) NoxVault.MainWindow.StartHotkeyCheck(report);
        else if (args.Contains("--frame-check")) NoxVault.MainWindow.StartFrameCheck();
        else if (args.Contains("--self-test")
            || args.Contains("--self-test-native")) RunSelfTest(report, args.Contains("--self-test-native"));
        else return false;
        return true;
    }

    void RunSelfTest(string report, bool includeNative)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            SelfTest.Run(report, includeNative);
            Shutdown(0);
        }
        catch (Exception ex) // The report carries the failure; the exit code tells publish.ps1 not to install.
        {
            File.WriteAllText(report, ex.ToString());
            Shutdown(1);
        }
    }

    void StartNormally(string[] args)
    {
        instance = new Mutex(true, "Local\\NoxVault.Desktop.v1", out bool first);
        bool startInTray = args.Contains("--tray");
        if (!first)
        {
            if (!startInTray) Native.PostMessage(Native.FindWindow(null, "vault"), Native.ShowMessage, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }
        try { StoragePaths.Initialize(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show("Der Tresor konnte nicht sicher geladen werden.\n\n" + ex.Message, "vault · Datenordner prüfen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }
        Theme.Apply(Preferences.Load().Theme);
        StartupTrace.Write("CreatingWindow Tray=" + startInTray);
        var window = new MainWindow(startInTray: startInTray);
        MainWindow = window;
        if (startInTray) window.StartInTray();
        else window.Show();
        StartupTrace.Write("Ready " + window.StartupDiagnostics().Replace('\n', ' '));
        WriteStartupLog(window, startInTray);
        if (args.Contains("--startup-check")) File.WriteAllText(args.Last(), window.StartupDiagnostics());
    }

    static void WriteStartupLog(MainWindow window, bool startInTray)
    {
        try
        {
            Directory.CreateDirectory(Preferences.Root);
            File.WriteAllText(Path.Combine(Preferences.Root, "startup.log"),
                $"Started={DateTimeOffset.Now:O}\nTrayRequested={startInTray}\n" + window.StartupDiagnostics());
        }
        catch (IOException) { } // Diagnostics only; a read-only profile must not stop the app.
        catch (UnauthorizedAccessException) { } // Same as above.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}

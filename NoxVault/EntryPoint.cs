using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NoxVault;

// Command-line modes that must not start WPF, then the app itself.
internal static class EntryPoint
{
    [STAThread]
    internal static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--enable-windows-access") { EnableWindowsAccess(args); return; }
        // UI Automation runs on an MTA worker without constructing Application,
        // loading themes, or starting a WPF dispatcher for each request.
        if (args.Contains("--autofill-worker")) { Task.Run(Autofill.Worker).GetAwaiter().GetResult(); return; }
        if (args.Contains("--install-shortcuts"))
        {
            try { StartupRegistration.Install(args.Contains("--enable-autostart")); }
            catch (Exception) { Environment.ExitCode = 1; } // publish.ps1 reads the exit code and restores the previous version.
            return;
        }
        bool normal = args.Length == 0 || args.Contains("--tray");
        if (normal) StartupTrace.Write("Entry Tray=" + args.Contains("--tray"));
        try { App.Main(); }
        catch (Exception ex)
        {
            if (normal) StartupTrace.Write("Failed Type=" + ex.GetType().Name + " HResult=" + ex.HResult);
            throw;
        }
    }

    // Moves a password vault to Windows access: the master password arrives on stdin, never on the command line.
    static void EnableWindowsAccess(string[] args)
    {
        try
        {
            if (args.Length == 1) StoragePaths.Initialize();
            var path = args.Length == 2 ? args[1] : Path.Combine(StoragePaths.Root, "vault.nox");
            using var input = new StreamReader(Console.OpenStandardInput());
            var secret = input.ReadLine() ?? throw new InvalidDataException();
            if (secret.Length > 10000) throw new InvalidDataException();
            using var vault = new Vault(path);
            vault.Open(secret);
            secret = "";
            var access = new RememberedLogin(path, permanent: true);
            access.Save(vault, DateTime.Now);
            if (access.Restore(vault, DateTime.Now, out _) == null) throw new CryptographicException();
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine("Windows-Zugriff konnte nicht eingerichtet werden: " + ex.GetType().Name);
            Environment.ExitCode = 1;
        }
    }
}

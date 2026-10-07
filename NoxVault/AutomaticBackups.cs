using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NoxVault;

internal sealed class AutomaticBackupService
{
    internal static string OwnedDirectory(string source, string destination) => Path.Combine(destination, "vault-auto-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToUpperInvariant())))[..16]);
    static readonly Regex Name = new(@"^auto-(\d{8})-\d{6}-[0-9a-f]{32}\.nox$", RegexOptions.CultureInvariant);
    internal static DateTime? LastSuccess(string source, string destination)
    {
        if (string.IsNullOrEmpty(destination)) return null;
        var owned = OwnedDirectory(source, destination);
        if (!Directory.Exists(owned)) return null;
        return Directory.EnumerateFiles(owned).Where(p => Name.IsMatch(Path.GetFileName(p)))
            .Select(p => DateTime.TryParseExact(Path.GetFileName(p).Substring(5, 15), "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out var when) ? (DateTime?)when : null)
            .Max();
    }
    internal static bool Run(string source, string destination, int retain, DateTime now)
    {
        if (retain is not (7 or 14 or 30)) throw new ArgumentException("Ungültige Aufbewahrung.");
        if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination)) throw new IOException("Der Sicherungsordner ist nicht erreichbar.");
        var owned = Path.GetFullPath(OwnedDirectory(source, destination));
        if (Directory.Exists(owned) && (File.GetAttributes(owned) & FileAttributes.ReparsePoint) != 0) throw new IOException("Der verwaltete Sicherungsordner darf keine Verknüpfung sein.");
        if (LastSuccess(source, destination)?.Date == now.Date) return false;
        byte[] snapshot;
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (input.Length < 68 || input.Length > 32 * 1024 * 1024) throw new InvalidDataException("Ungültige Tresorgröße.");
            snapshot = new byte[(int)input.Length]; input.ReadExactly(snapshot);
        }
        var target = Path.Combine(owned, "auto-" + now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".nox");
        Vault.AtomicWrite(target, snapshot);
        var candidates = Directory.GetFiles(owned).Where(p => Name.IsMatch(Path.GetFileName(p)))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(retain).ToArray();
        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (!string.Equals(Path.GetDirectoryName(full), owned, StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Sicherungsrotation abgebrochen: ungültiger Pfad.");
            File.Delete(full);
        }
        return true;
    }
}

public sealed partial class MainWindow
{
    bool automaticBackupRunning;
    string automaticBackupMessage = "";
    async Task QueueAutomaticBackup()
    {
        if (preview || !prefs.AutomaticBackups || automaticBackupRunning || !vault.Exists) return;
        automaticBackupRunning = true;
        string folder = prefs.BackupDirectory; int retain = prefs.BackupRetention;
        try
        {
            bool created = await Task.Run(() => AutomaticBackupService.Run(vault.FilePath, folder, retain, DateTime.Now));
            if (created) automaticBackupMessage = "Automatische Sicherung erstellt.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { automaticBackupMessage = "Tresor gespeichert, automatische Sicherung fehlgeschlagen: " + ex.Message; if (data != null) status.Text = automaticBackupMessage; }
        finally { automaticBackupRunning = false; if (lockPending && !busy) { lockPending = false; Lock(); } }
    }
}

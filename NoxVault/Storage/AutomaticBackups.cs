using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NoxVault;

// Daily encrypted copies into a folder of the user's choice. Only files this service named are ever rotated away.
internal static class AutomaticBackupService
{
    static readonly Regex Name = new(@"^auto-(\d{8})-\d{6}-[0-9a-f]{32}\.nox$", RegexOptions.CultureInvariant);

    internal static string OwnedDirectory(string source, string destination)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToUpperInvariant()));
        return Path.Combine(destination, "vault-auto-" + Convert.ToHexString(hash)[..16]);
    }

    internal static DateTime? LastSuccess(string source, string destination)
    {
        if (string.IsNullOrEmpty(destination)) return null;
        var owned = OwnedDirectory(source, destination);
        if (!Directory.Exists(owned)) return null;
        return Directory.EnumerateFiles(owned).Select(Path.GetFileName).OfType<string>().Where(name => Name.IsMatch(name))
            .Select(name => DateTime.TryParseExact(name.Substring(5, 15), "yyyyMMdd-HHmmss", null, DateTimeStyles.None, out var when)
                ? (DateTime?)when : null)
            .Max();
    }

    internal static bool Run(string source, string destination, int retain, DateTime now)
    {
        if (retain is not (7 or 14 or 30)) throw new ArgumentException("Ungültige Aufbewahrung.");
        if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            throw new IOException("Der Sicherungsordner ist nicht erreichbar.");
        var owned = Path.GetFullPath(OwnedDirectory(source, destination));
        if (Directory.Exists(owned) && (File.GetAttributes(owned) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Der verwaltete Sicherungsordner darf keine Verknüpfung sein.");
        if (LastSuccess(source, destination)?.Date == now.Date) return false;
        var target = Path.Combine(owned, "auto-" + now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".nox");
        Vault.AtomicWrite(target, Snapshot(source));
        Rotate(owned, retain);
        return true;
    }

    static byte[] Snapshot(string source)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (input.Length < 68 || input.Length > 32 * 1024 * 1024) throw new InvalidDataException("Ungültige Tresorgröße.");
        var snapshot = new byte[(int)input.Length];
        input.ReadExactly(snapshot);
        return snapshot;
    }

    static void Rotate(string owned, int retain)
    {
        var candidates = Directory.GetFiles(owned).Where(p => Name.IsMatch(Path.GetFileName(p)))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(retain).ToArray();
        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (!string.Equals(Path.GetDirectoryName(full), owned, StringComparison.OrdinalIgnoreCase)
                || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Sicherungsrotation abgebrochen: ungültiger Pfad.");
            File.Delete(full);
        }
    }
}

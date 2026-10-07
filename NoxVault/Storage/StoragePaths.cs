using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NoxVault;

internal static class StoragePaths
{
    // AppData writes from a packaged parent (including Codex) can be redirected
    // to its LocalCache. A user-profile directory is shared by all launch paths.
    internal static string Root { get; } = ProfileRoot(Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify));

    internal static string ProfileRoot(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile) || !Path.IsPathFullyQualified(profile))
            throw new IOException("Der Windows-Benutzerordner ist nicht verfügbar.");
        return Path.Combine(profile, ".noxvault");
    }

    internal static bool FilePresent(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
                throw new IOException("Am erwarteten Dateipfad liegt ein Ordner: " + path);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    internal static void Initialize()
    {
        if (FilePresent(Path.Combine(Root, "vault.nox")) || FilePresent(Path.Combine(Root, "vault.nox.initialized")))
        {
            Initialize(Root, Array.Empty<string>()); return;
        }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(local) || !Path.IsPathFullyQualified(local))
            throw new IOException("Der bisherige Windows-Datenordner ist nicht verfügbar.");
        var sources = new List<string> { Path.Combine(local, "NoxVault") };
        var packages = Path.Combine(local, "Packages");
        // Only inspect the known former host's vault directory, never other
        // applications' data. Originals and remembered sessions remain untouched.
        try
        {
            sources.AddRange(Directory.GetDirectories(packages, "OpenAI.Codex_*")
                .Select(p => Path.Combine(p, "LocalCache", "Local", "NoxVault")));
        }
        catch (DirectoryNotFoundException) { }
        Initialize(Root, sources);
    }

    internal static void Initialize(string target, IEnumerable<string> sources)
    {
        var destination = Path.Combine(target, "vault.nox");
        var marker = destination + ".initialized";
        if (FilePresent(destination))
        {
            if (!FilePresent(marker)) Vault.AtomicWrite(marker, Array.Empty<byte>(), overwrite: false);
            return;
        }
        if (FilePresent(marker))
            throw new IOException("Der bestehende Tresor ist momentan nicht erreichbar. "
                + "Es wird kein neuer Tresor angelegt und keine ältere Kopie automatisch geladen.");

        byte[]? encrypted = null;
        string selected = "";
        foreach (var source in sources.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(source, "vault.nox");
            if (!FilePresent(candidate)) continue;
            var bytes = Vault.ReadBounded(candidate);
            if (encrypted != null && !encrypted.AsSpan().SequenceEqual(bytes))
                throw new IOException("Mehrere unterschiedliche Tresore gefunden. "
                    + "Die Originale bleiben erhalten; bitte den richtigen Tresor vor der Übernahme auswählen.");
            encrypted = bytes; selected = source;
        }
        if (encrypted == null) return;
        Directory.CreateDirectory(target);
        // Copy settings first so an interrupted migration can retry. Never
        // carry DPAPI sessions across paths or overwrite an existing vault.
        var settings = Path.Combine(selected, "settings.json");
        var newSettings = Path.Combine(target, "settings.json");
        if (FilePresent(settings) && !FilePresent(newSettings)) File.Copy(settings, newSettings, false);
        Vault.AtomicWrite(destination, encrypted, overwrite: false);
        Vault.AtomicWrite(marker, Array.Empty<byte>(), overwrite: false);
    }
}

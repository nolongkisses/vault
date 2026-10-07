using System;
using System.IO;
using System.Security.Cryptography;

namespace NoxVault;

// Which vault data is open, and the Windows-protected keys that reopen it without a password.
// The daily session expires at midnight; the permanent Windows key (.quickfill) survives locking.
internal sealed class VaultSession : IDisposable
{
    readonly RememberedLogin daily;
    readonly RememberedLogin windows;

    internal VaultSession(string path)
    {
        Vault = new Vault(path);
        daily = new RememberedLogin(path);
        windows = new RememberedLogin(path, permanent: true);
    }

    internal Vault Vault { get; }
    internal VaultData? Data { get; set; }
    internal DateTime RememberedUntil { get; set; }
    internal bool RememberedBlocked { get; set; }
    internal string AccessError { get; set; } = "";
    internal string Warning { get; set; } = "";
    internal bool WindowsAccessStored => File.Exists(Vault.FilePath + ".quickfill");

    internal bool TryRemembered()
    {
        if (RememberedBlocked) return false;
        Data = daily.Restore(Vault, DateTime.Now, out var until);
        RememberedUntil = until;
        if (Data != null) SaveWindowsAccess();
        return Data != null;
    }

    void SaveWindowsAccess()
    {
        try { windows.Save(Vault, DateTime.Now); }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        { Warning = "Schnellzugriff konnte nicht gespeichert werden. Bitte Dateizugriff prüfen."; }
    }

    // Windows key first, then today's session; a first start without any vault creates one directly.
    internal bool TryAutomatic(bool mayCreate)
    {
        try
        {
            Data = windows.Restore(Vault, DateTime.Now, out _);
            if (Data != null || TryRemembered()) return true;
            if (mayCreate && !Vault.Exists) { Data = Vault.CreateForWindows(); return true; }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        { AccessError = ex.Message; }
        return false;
    }

    internal DateTime Remember()
    {
        RememberedUntil = daily.Save(Vault, DateTime.Now);
        RememberedBlocked = false;
        return RememberedUntil;
    }

    // The new data replaces the open data only after the encrypted write succeeded.
    internal void Commit(Action<VaultData> mutation)
    {
        var next = (Data ?? throw new InvalidOperationException("Tresor ist gesperrt.")).Clone();
        mutation(next);
        next.PurgeExpired(DateTime.UtcNow);
        Vault.Save(next);
        Data = next;
    }

    // Returns a notice when the daily session file could not be removed; the revocation marker still blocks it.
    internal string? Forget()
    {
        RememberedUntil = default;
        RememberedBlocked = true;
        try { daily.Clear(); return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "Tresor gesperrt. Die gespeicherte Anmeldung konnte nicht vollständig entfernt werden. Bitte Dateizugriff prüfen.";
        }
    }

    internal void Close()
    {
        Vault.Dispose();
        Data = null;
    }

    internal bool Expired(DateTime lastActivityUtc, int idleMinutes)
    {
        if (RememberedUntil != default)
            return DateTime.Now >= RememberedUntil || DateTime.Now.Date != RememberedUntil.AddDays(-1).Date;
        return Data != null && DateTime.UtcNow - lastActivityUtc > TimeSpan.FromMinutes(idleMinutes);
    }

    public void Dispose() => Vault.Dispose();
}

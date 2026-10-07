using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NoxVault;

internal static class StartupRegistration
{
    internal static string Executable => Path.Combine(AppContext.BaseDirectory, "NoxVault.exe");
    internal static string StartupLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "vault.lnk");
    const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    internal static bool Enabled
    {
        get
        {
            if (!Matches(StartupLink, "--tray")) return false;
            using var key = Registry.CurrentUser.OpenSubKey(ApprovalKey);
            // Windows uses 2/6 for enabled entries. An unrecognized state is
            // not reported as enabled; an absent entry is enabled by default.
            var state = key?.GetValue("vault.lnk") as byte[];
            return state == null || state.Length == 0 || state[0] is 2 or 6;
        }
        set
        {
            if (value)
            {
                WriteLink(StartupLink, "--tray");
                // This is an explicit enable action in settings or installation.
                using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey, true);
                approval?.DeleteValue("vault.lnk", false);
            }
            else if (File.Exists(StartupLink)) File.Delete(StartupLink);
            using var legacy = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            legacy?.DeleteValue("NoxVault", false);
        }
    }
    internal static void Install(bool enable)
    {
        WriteLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "vault.lnk"), "");
        if (enable) Enabled = true;
        else if (File.Exists(StartupLink)) WriteLink(StartupLink, "--tray");
    }
    internal static bool Matches(string path, string arguments)
    {
        if (!File.Exists(path) || !File.Exists(Executable)) return false;
        object? shell = null, link = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            link = ((dynamic)shell!).CreateShortcut(path);
            return string.Equals((string)((dynamic)link).TargetPath, Executable, StringComparison.OrdinalIgnoreCase)
                && (string)((dynamic)link).Arguments == arguments;
        }
        catch (COMException) { return false; }
        finally { if (link != null) Marshal.FinalReleaseComObject(link); if (shell != null) Marshal.FinalReleaseComObject(shell); }
    }
    internal static void WriteLink(string path, string arguments)
    {
        if (!File.Exists(Executable)) throw new FileNotFoundException("NoxVault.exe fehlt.", Executable);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        object? shell = null, link = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            link = ((dynamic)shell!).CreateShortcut(path);
            dynamic shortcut = link;
            shortcut.TargetPath = Executable; shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.IconLocation = Executable + ",0"; shortcut.Description = "vault – lokaler Passwortmanager";
            shortcut.Save();
        }
        finally { if (link != null) Marshal.FinalReleaseComObject(link); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        if (!Matches(path, arguments)) throw new IOException("vault-Verknüpfung konnte nicht geprüft werden.");
    }
}

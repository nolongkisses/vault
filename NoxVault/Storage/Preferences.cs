using System;
using System.IO;
using System.Text.Json;

namespace NoxVault;

// Local settings next to the vault; never contains credentials. Unknown values fall back to the defaults.
internal sealed class Preferences
{
    public int IdleMinutes { get; set; } = 5;
    public int ClipboardSeconds { get; set; } = 30;
    public int RevealSeconds { get; set; } = 15;
    public string BackupDirectory { get; set; } = "";
    public bool AutomaticBackups { get; set; }
    public int BackupRetention { get; set; } = 7;
    public string AccountSort { get; set; } = "Name";
    public int FillShortcutF { get; set; } = 8;
    public string Theme { get; set; } = "System";
    public int HotkeyF { get; set; } = 9;
    public uint HotkeyModifiers { get; set; } = 8; // Preserve legacy files that stored only HotkeyF.
    public uint HotkeyKey { get; set; }

    internal uint EffectiveKey => HotkeyKey == 0 ? (uint)(0x6F + Math.Clamp(HotkeyF, 8, 11)) : HotkeyKey;
    internal static string Root => StoragePaths.Root;
    internal static string PathName => Path.Combine(Root, "settings.json");

    internal void Normalize()
    {
        if (FillShortcutF is < 8 or > 12) FillShortcutF = 8;
        if (IdleMinutes is not (1 or 5 or 15)) IdleMinutes = 5;
        if (ClipboardSeconds is not (15 or 30 or 60)) ClipboardSeconds = 30;
        if (RevealSeconds is not (10 or 15 or 30)) RevealSeconds = 15;
        if (BackupRetention is not (7 or 14 or 30)) BackupRetention = 7;
        BackupDirectory ??= "";
        if (AccountSort is not ("Name" or "Zuletzt geändert")) AccountSort = "Name";
        if (Theme is not ("System" or "Dunkel" or "Hell")) Theme = "System";
    }

    internal static Preferences Load()
    {
        try
        {
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PathName)) ?? new();
            prefs.Normalize();
            return prefs;
        }
        catch { return new(); } // New installations and unreadable files start with the defaults (Win + F9).
    }

    internal void Save()
    {
        Normalize();
        Vault.AtomicWrite(PathName, JsonSerializer.SerializeToUtf8Bytes(this));
    }

    internal static bool AutoStart
    {
        get => StartupRegistration.Enabled;
        set => StartupRegistration.Enabled = value;
    }

    internal static string AutoStartCommand()
    {
        // ProcessPath can be dotnet.exe when launched via `dotnet NoxVault.dll`.
        // Register the apphost beside the application, never the runtime host.
        var exe = Path.Combine(AppContext.BaseDirectory, "NoxVault.exe");
        if (!File.Exists(exe))
            throw new FileNotFoundException("Die startfähige NoxVault.exe fehlt. Bitte die veröffentlichte Anwendung starten.", exe);
        return "\"" + exe + "\" --tray";
    }
}

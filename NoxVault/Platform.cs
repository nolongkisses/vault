using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NoxVault;

public static class Native
{
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    public static void Round(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero) return;
        int value = 2; DwmSetWindowAttribute(h, 33, ref value, 4);
        value = 1; DwmSetWindowAttribute(h, 20, ref value, 4);
        value = -2; DwmSetWindowAttribute(h, 34, ref value, 4);
    }
    public static Dictionary<string, string> ProbeHotkeys()
    {
        var results = new Dictionary<string, string>();
        foreach (var f in new[] { 8, 9, 10, 11 })
        {
            bool ok = RegisterHotKey(IntPtr.Zero, 1900 + f, 0x4008, (uint)(0x6F + f));
            results["Win + F" + f] = ok ? "Registrierung erfolgreich (zum Prüfzeitpunkt frei)" : "Nicht verfügbar; Win32-Fehler " + Marshal.GetLastWin32Error();
            if (ok) UnregisterHotKey(IntPtr.Zero, 1900 + f);
        }
        return results;
    }
}

public sealed class ClipboardGuard : IDisposable
{
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };
    readonly Action<string> write;
    readonly Func<uint> getSequence;
    readonly Action clear;
    uint sequence;
    bool owned;
    public int ClearAfterSeconds { get; set; } = 30;
    public ClipboardGuard() : this(Write, Native.GetClipboardSequenceNumber, System.Windows.Clipboard.Clear) { }
    internal ClipboardGuard(Action<string> write, Func<uint> getSequence, Action clear)
    { this.write = write; this.getSequence = getSequence; this.clear = clear; timer.Tick += (_, _) => Clear(); }
    static void Write(string value)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, value);
        // Windows clipboard history / cloud clipboard opt-out formats (DWORD zero).
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
        System.Windows.Clipboard.SetDataObject(data, true);
    }
    public void Copy(string value)
    {
        write(value);
        sequence = getSequence(); owned = true;
        timer.Stop(); timer.Interval = TimeSpan.FromSeconds(ClearAfterSeconds is 15 or 30 or 60 ? ClearAfterSeconds : 30); timer.Start();
    }
    public void Clear()
    {
        timer.Stop();
        if (!owned) return;
        try
        {
            if (sequence == getSequence()) clear();
            owned = false;
        }
        catch (COMException) { timer.Interval = TimeSpan.FromSeconds(2); timer.Start(); }
    }
    public void Dispose() { Clear(); timer.Stop(); }
}

public sealed class Preferences
{
    public int IdleMinutes { get; set; } = 5;
    public int ClipboardSeconds { get; set; } = 30;
    public int RevealSeconds { get; set; } = 15;
    public string BackupDirectory { get; set; } = "";
    public bool AutomaticBackups { get; set; }
    public int BackupRetention { get; set; } = 7;
    public string AccountSort { get; set; } = "Name";
    public int FillShortcutF { get; set; } = 8;
    public void Normalize()
    {
        if (FillShortcutF is < 8 or > 12) FillShortcutF = 8;
        if (IdleMinutes is not (1 or 5 or 15)) IdleMinutes = 5;
        if (ClipboardSeconds is not (15 or 30 or 60)) ClipboardSeconds = 30;
        if (RevealSeconds is not (10 or 15 or 30)) RevealSeconds = 15;
        if (BackupRetention is not (7 or 14 or 30)) BackupRetention = 7;
        BackupDirectory ??= "";
        if (AccountSort is not ("Name" or "Zuletzt geändert")) AccountSort = "Name";
    }
    public int HotkeyF { get; set; } = 9;
    public uint HotkeyModifiers { get; set; } = 8; // Preserve legacy files that stored only HotkeyF.
    public uint HotkeyKey { get; set; }
    public uint EffectiveKey => HotkeyKey == 0 ? (uint)(0x6F + Math.Clamp(HotkeyF, 8, 11)) : HotkeyKey;
    public static string Root => StoragePaths.Root;
    public static string PathName => Path.Combine(Root, "settings.json");
    public static Preferences Load()
    {
        try { var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PathName)) ?? new(); prefs.Normalize(); return prefs; }
        catch { return new(); } // New installations: Win + F9.
    }
    public void Save() { Normalize(); Vault.AtomicWrite(PathName, JsonSerializer.SerializeToUtf8Bytes(this)); }
    public static bool AutoStart
    {
        get => StartupRegistration.Enabled;
        set => StartupRegistration.Enabled = value;
    }
    internal static string AutoStartCommand()
    {
        // ProcessPath can be dotnet.exe when launched via `dotnet NoxVault.dll`.
        // Register the apphost beside the application, never the runtime host.
        var exe = Path.Combine(AppContext.BaseDirectory, "NoxVault.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("Die startfähige NoxVault.exe fehlt. Bitte die veröffentlichte Anwendung starten.", exe);
        return "\"" + exe + "\" --tray";
    }
}

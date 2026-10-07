using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NoxVault;

// Win32 calls shared by the window, shortcuts and clipboard.
internal static class Native
{
    internal const int HotkeyMessage = 0x312;
    internal const int ShowMessage = 0x8001;
    internal const int PrepareUpdateMessage = 0x8002;
    internal const uint NoRepeat = 0x4000;
    internal const uint WinModifier = 8;
    const int CornerPreference = 33, ImmersiveDarkMode = 20, BorderColor = 34;
    const int RoundCorners = 2, NoBorderColor = -2;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers,
        uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Windows 11 rounds the native frame; the immersive flag keeps the system resize border in the theme's brightness.
    internal static void Round(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int value = RoundCorners;
        DwmSetWindowAttribute(handle, CornerPreference, ref value, 4);
        value = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, ImmersiveDarkMode, ref value, 4);
        value = NoBorderColor;
        DwmSetWindowAttribute(handle, BorderColor, ref value, 4);
    }

    internal static Dictionary<string, string> ProbeHotkeys()
    {
        var results = new Dictionary<string, string>();
        foreach (var f in new[] { 8, 9, 10, 11 })
        {
            bool ok = RegisterHotKey(IntPtr.Zero, 1900 + f, NoRepeat | WinModifier, (uint)(0x6F + f));
            results["Win + F" + f] = ok
                ? "Registrierung erfolgreich (zum Prüfzeitpunkt frei)"
                : "Nicht verfügbar; Win32-Fehler " + Marshal.GetLastWin32Error();
            if (ok) UnregisterHotKey(IntPtr.Zero, 1900 + f);
        }
        return results;
    }
}

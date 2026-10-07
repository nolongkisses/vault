using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace NoxVault;

// Global shortcuts of the main window. The ids come back as the wParam of WM_HOTKEY.
internal sealed class Hotkeys
{
    internal const int FillId = 74;
    internal const int FillProbeId = 75;
    internal const int EscapeId = 76;
    const uint Escape = 0x1B;
    IntPtr window;

    internal int OpenId { get; private set; } = 42;
    internal bool OpenRegistered { get; private set; }
    internal bool FillRegistered { get; private set; }
    internal string OpenLabel { get; private set; } = "Nicht registriert";
    internal event Action? OpenChanged;

    internal void Attach(IntPtr handle) => window = handle;

    // The new combination is registered before the old one is released, so a conflict keeps the working shortcut.
    internal bool ReplaceOpen(uint modifiers, uint key, Action? persist = null)
    {
        int candidate = OpenId == 42 ? 43 : 42;
        if (!Native.RegisterHotKey(window, candidate, modifiers | Native.NoRepeat, key)) return false;
        if (persist != null)
        {
            try { persist(); }
            catch { Native.UnregisterHotKey(window, candidate); throw; }
        }
        if (OpenRegistered) Native.UnregisterHotKey(window, OpenId);
        OpenId = candidate;
        OpenRegistered = true;
        OpenLabel = Label(modifiers, key);
        OpenChanged?.Invoke();
        return true;
    }

    internal void ReleaseOpen()
    {
        if (OpenRegistered) Native.UnregisterHotKey(window, OpenId);
        OpenRegistered = false;
    }

    internal void RegisterFill(int f)
    {
        Native.UnregisterHotKey(window, FillId);
        FillRegistered = Native.RegisterHotKey(window, FillId, Native.NoRepeat | Native.WinModifier, FunctionKey(f));
    }

    // Reserves Win+F<f> under a separate id until the choice is saved or rejected.
    internal bool ProbeFill(int f) => Native.RegisterHotKey(window, FillProbeId, Native.NoRepeat | Native.WinModifier, FunctionKey(f));
    internal void ReleaseFillProbe() => Native.UnregisterHotKey(window, FillProbeId);
    internal bool RegisterEscape() => Native.RegisterHotKey(window, EscapeId, Native.NoRepeat, Escape);
    internal void ReleaseEscape() => Native.UnregisterHotKey(window, EscapeId);

    internal void Release()
    {
        Native.UnregisterHotKey(window, FillId);
        ReleaseOpen();
    }

    internal static string FillLabel(int f) => "Win + F" + f;

    internal static string Label(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Strg");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)key).ToString());
        return string.Join(" + ", parts);
    }

    static uint FunctionKey(int f) => (uint)(0x6F + f);
}

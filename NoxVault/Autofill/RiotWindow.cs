using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NoxVault;

// Identifies the one Riot Client window and proves it is still the signed process that was verified.
internal static partial class Autofill
{
    internal static async Task<bool> ActivateTarget(FillTarget target)
    {
        // Do not carry a held Enter/shortcut modifier from the picker into Riot.
        var keys = new[] { 0x0D, 0x10, 0x11, 0x12, 0x5B, 0x5C };
        for (int attempt = 0; keys.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); attempt++)
        {
            if (attempt >= 60) return false;
            await Task.Delay(25);
        }
        var window = (IntPtr)target.Window;
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out uint foregroundPid);
        // Only hand over focus from vault or keep an already active Riot target.
        if (foreground != window && foregroundPid != Environment.ProcessId) return false;
        GetWindowThreadProcessId(window, out uint targetPid);
        if (targetPid != target.Pid) return false;
        if (IsIconic(window) && !await Restore(window)) return false;
        if (foreground == window) return true;
        SetForegroundWindow(window);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (GetForegroundWindow() == window) return true;
            await Task.Delay(25);
        }
        return false;
    }

    static async Task<bool> Restore(IntPtr window)
    {
        ShowWindowAsync(window, 9);
        for (int attempt = 0; IsIconic(window); attempt++)
        {
            if (attempt >= 10) return false;
            await Task.Delay(25);
        }
        return true;
    }

    static FillTarget Discover()
    {
        var targets = Process.GetProcessesByName("Riot Client");
        try
        {
            var visible = targets.Where(p => p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle)).ToArray();
            if (visible.Length != 1) throw new InvalidOperationException("Ziel nicht eindeutig");
            var p = visible[0];
            var path = p.MainModule?.FileName ?? throw new InvalidOperationException("Riot-Pfad unbekannt");
            return new(p.MainWindowHandle.ToInt64(), p.Id, p.StartTime.ToUniversalTime().Ticks, path, "");
        }
        finally { foreach (var p in targets) p.Dispose(); }
    }

    static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    static Process Validate(FillTarget target, bool discovering, out string verifiedHash)
    {
        var p = Process.GetProcessById(target.Pid);
        try
        {
            GetWindowThreadProcessId((IntPtr)target.Window, out uint pid);
            if (pid != target.Pid || p.ProcessName != "Riot Client" || p.StartTime.ToUniversalTime().Ticks != target.Started
                || !string.Equals(p.MainModule?.FileName, target.Path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException();
            verifiedHash = FileHash(target.Path);
            if ((!discovering && verifiedHash != target.Hash) || !TrustedRiotFile(target.Path)) throw new InvalidOperationException();
            return p;
        }
        catch { p.Dispose(); throw; }
    }

    static void EnsureCurrent(FillTarget target, bool requireForeground = true)
    {
        GetWindowThreadProcessId((IntPtr)target.Window, out uint pid);
        using var p = Process.GetProcessById(target.Pid);
        if ((requireForeground && GetForegroundWindow() != (IntPtr)target.Window) || pid != target.Pid
            || p.StartTime.ToUniversalTime().Ticks != target.Started || !IsWindowVisible((IntPtr)target.Window))
            throw new InvalidOperationException();
    }

    // WinVerifyTrust validates Authenticode, then require Riot's publisher certificate.
    static bool TrustedRiotFile(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, File = pointer, StateAction = 1,
                ProviderFlags = 0x1000,
            };
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try
            {
                if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
#pragma warning disable SYSLIB0057
                using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                return cert.GetNameInfo(X509NameType.SimpleName, false) == "Riot Games, Inc.";
            }
            finally { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data); }
        }
        finally { Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct TrustFile
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr File, Subject;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TrustData
    {
        public uint Size;
        public IntPtr Callback, Client;
        public uint UiChoice, Revocation, UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr State, Url;
        public uint ProviderFlags, Context;
        public IntPtr Signature;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)] static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
}

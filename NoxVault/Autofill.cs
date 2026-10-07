using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UIA = Interop.UIAutomationClient;

namespace NoxVault;

// No keyboard/clipboard fallback. The worker receives secrets over redirected stdin,
// never command-line arguments, and can be killed if a foreign UIA provider hangs.
internal sealed record FillTarget(long Window, int Pid, long Started, string Path, string Hash);
internal sealed record FillRequest(string Mode, FillTarget? Target = null, string Username = "", string Password = "");
internal sealed record FillResult(bool Ok, string Message, FillTarget? Target = null, string? Diagnostic = null);

internal static class Autofill
{
    internal static async Task<FillResult> RunAsync(FillRequest request, CancellationToken cancellation = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        var start = new ProcessStartInfo(Environment.ProcessPath!) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--autofill-worker");
        Process? worker = null;
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (request.Mode == "probe")
            {
                var target = await Task.Run(() =>
                {
                    var candidate = request.Target ?? Discover();
                    using var verified = Validate(candidate, request.Target == null, out var hash);
                    return candidate with { Hash = hash };
                }, timeout.Token).WaitAsync(timeout.Token);
                if (!await ActivateTarget(target)) return new(false, "Riot konnte nicht aktiviert werden. Login öffnen und erneut ausfüllen.");
                request = request with { Target = target };
            }
            worker = Process.Start(start) ?? throw new IOException();
            AllowSetForegroundWindow(worker.Id);
            using var stop = timeout.Token.Register(() => { try { if (!worker.HasExited) worker.Kill(); } catch (InvalidOperationException) { } });
            await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token);
            worker.StandardInput.Close();
            var line = await worker.StandardOutput.ReadLineAsync(timeout.Token);
            await worker.WaitForExitAsync(timeout.Token);
            return line == null ? new(false, "Die Ausfüllprüfung wurde beendet.") :
                JsonSerializer.Deserialize<FillResult>(line) ?? new(false, "Keine gültige Antwort vom Ausfüllprozess.");
        }
        catch (OperationCanceledException) { return new(false, "Ausfüllen abgebrochen oder Zeitlimit erreicht. Bereits ausgefüllte Felder bitte prüfen."); }
        catch (Exception) { return new(false, "Ausfüllen konnte nicht abgeschlossen werden. Bitte Loginfelder prüfen."); }
        finally { if (worker != null) { try { if (!worker.HasExited) worker.Kill(); } catch (InvalidOperationException) { } worker.Dispose(); } }
    }

    internal static void Worker()
    {
        FillResult result;
        try
        {
            var request = JsonSerializer.Deserialize<FillRequest>(Console.ReadLine() ?? "") ?? throw new InvalidOperationException();
            result = Execute(request);
        }
        catch { result = new(false, "Ausfüllen hat nicht geklappt. Öffne den Riot-Login und wähle deinen Account erneut."); }
        Console.WriteLine(JsonSerializer.Serialize(result));
    }

    internal static FillResult Execute(FillRequest request)
    {
        if (request.Mode is not ("probe" or "fill" or "clear-test") || request.Mode != "probe" && request.Target == null)
            return new(false, "Kein geprüfter Ausfüllauftrag.");
        if (request.Mode == "fill" && (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password) ||
            request.Username.Length > 10000 || request.Password.Length > 10000))
            return new(false, "Benutzername oder Passwort fehlt oder überschreitet die unterstützte Länge.");
        FillTarget target;
        try { target = request.Target ?? Discover(); }
        catch { return new(false, "Öffne den Riot-Anmeldebildschirm und wähle deinen Account hier erneut."); }
        using var process = Validate(target, request.Target == null, out var verifiedHash);
        target = target with { Hash = verifiedHash };
        UIA.IUIAutomationElement user, password;
        try { (user, password) = ReadReadyFields(() => Fields(target), () => EnsureCurrent(target, request.Mode == "fill"), () => Thread.Sleep(50)); }
        catch (Exception ex) { return new(false, "Die Riot-Loginfelder sind noch nicht bereit. Schließe offene Riot-Dialoge und wähle deinen Account erneut.", Diagnostic: ex is InvalidOperationException ? ex.Message : ex.GetType().Name); }
        if (request.Mode == "probe") return new(true, "Riot-Login erkannt · direkter Feldzugriff verfügbar", target);
        if (GetForegroundWindow() != (IntPtr)target.Window)
            return new(false, "Riot konnte nicht aktiviert werden. Login öffnen und erneut ausfüllen.");
        if (request.Mode == "clear-test")
        {
            EnsureCurrent(target);
            var userPattern = (UIA.IUIAutomationValuePattern)user.GetCurrentPattern(10002);
            if (userPattern.CurrentValue != "vault-autofill-test") return new(false, "Testfelder wurden verändert und bleiben erhalten.");
            ((UIA.IUIAutomationValuePattern)password.GetCurrentPattern(10002)).SetValue("");
            EnsureCurrent(target); userPattern.SetValue("");
            return new(true, "Synthetische Testfelder geleert.");
        }
        bool written = GuardedFill(() => EnsureCurrent(target),
            () => ((UIA.IUIAutomationValuePattern)user.GetCurrentPattern(10002)).SetValue(request.Username),
            () =>
            {
                // Re-resolve after the first mutation; never read back the password.
                var next = ReadReadyFields(() => Fields(target), () => EnsureCurrent(target), () => Thread.Sleep(50));
                return user.GetRuntimeId().SequenceEqual(next.user.GetRuntimeId()) && password.GetRuntimeId().SequenceEqual(next.password.GetRuntimeId()) &&
                    ((UIA.IUIAutomationValuePattern)next.user.GetCurrentPattern(10002)).CurrentValue == request.Username;
            }, () => ((UIA.IUIAutomationValuePattern)password.GetCurrentPattern(10002)).SetValue(request.Password));
        if (!written) return new(false, "Loginfelder oder Benutzername wurden verändert. Passwort wurde nicht eingefügt.");
        return new(true, "Eingabe ausgeführt. Bitte im Riot Client prüfen und selbst anmelden.", target);
    }

    internal static bool GuardedFill(Action ensureTarget, Action writeUser, Func<bool> confirmFields, Action writePassword)
    {
        ensureTarget(); writeUser(); ensureTarget();
        if (!confirmFields()) return false;
        ensureTarget(); writePassword(); return true;
    }
    internal sealed class FieldsNotReadyException(string message) : InvalidOperationException(message);
    internal static T ReadReadyFields<T>(Func<T> read, Action validate, Action wait)
    {
        for (int attempt = 0; ; attempt++)
        {
            validate();
            try { return read(); }
            catch (FieldsNotReadyException) when (attempt < 4) { wait(); }
        }
    }

    internal static async Task<bool> ActivateTarget(FillTarget target)
    {
        // Do not carry a held Enter/shortcut modifier from the picker into Riot.
        var keys = new[] { 0x0D, 0x10, 0x11, 0x12, 0x5B, 0x5C };
        for (int attempt = 0; keys.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); attempt++)
        {
            if (attempt >= 60) return false;
            await Task.Delay(25);
        }
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out uint foregroundPid);
        // Only hand over focus from vault or keep an already active Riot target.
        if (foreground != (IntPtr)target.Window && foregroundPid != Environment.ProcessId) return false;
        GetWindowThreadProcessId((IntPtr)target.Window, out uint targetPid);
        if (targetPid != target.Pid) return false;
        if (IsIconic((IntPtr)target.Window))
        {
            ShowWindowAsync((IntPtr)target.Window, 9);
            for (int attempt = 0; IsIconic((IntPtr)target.Window); attempt++)
            {
                if (attempt >= 10) return false;
                await Task.Delay(25);
            }
        }
        if (foreground == (IntPtr)target.Window) return true;
        SetForegroundWindow((IntPtr)target.Window);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (GetForegroundWindow() == (IntPtr)target.Window) return true;
            await Task.Delay(25);
        }
        return false;
    }

    static FillTarget Discover()
    {
        var targets = Process.GetProcessesByName("Riot Client");
        try
        {
            var visible = targets.Where(p => p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle)).ToArray();
            if (visible.Length != 1) throw new InvalidOperationException("Ziel nicht eindeutig");
            var p = visible[0]; var path = p.MainModule!.FileName;
            return new(p.MainWindowHandle.ToInt64(), p.Id, p.StartTime.ToUniversalTime().Ticks, path, "");
        }
        finally { foreach (var p in targets) p.Dispose(); }
    }
    static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    static Process Validate(FillTarget target, bool discovering, out string verifiedHash)
    {
        var p = Process.GetProcessById(target.Pid);
        try
        {
            GetWindowThreadProcessId((IntPtr)target.Window, out uint pid);
            if (pid != target.Pid || p.ProcessName != "Riot Client" || p.StartTime.ToUniversalTime().Ticks != target.Started ||
                !string.Equals(p.MainModule!.FileName, target.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
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
        if ((requireForeground && GetForegroundWindow() != (IntPtr)target.Window) || pid != target.Pid ||
            p.StartTime.ToUniversalTime().Ticks != target.Started || !IsWindowVisible((IntPtr)target.Window))
            throw new InvalidOperationException();
    }
    static (UIA.IUIAutomationElement user, UIA.IUIAutomationElement password) Fields(FillTarget target)
    {
        var automation = new UIA.CUIAutomation8();
        var root = automation.ElementFromHandle((IntPtr)target.Window);
        var dialogCondition = automation.CreateOrCondition(
            automation.CreatePropertyCondition(30003, 50032), automation.CreateOrCondition(
                automation.CreatePropertyCondition(30101, "dialog"), automation.CreatePropertyCondition(30101, "alertdialog")));
        var editCondition = automation.CreateAndCondition(automation.CreatePropertyCondition(30003, 50004),
            automation.CreateOrCondition(automation.CreatePropertyCondition(30011, "username"), automation.CreatePropertyCondition(30011, "password")));
        // One provider traversal for both login fields and blocking dialogs.
        var results = root.FindAll(UIA.TreeScope.TreeScope_Descendants, automation.CreateOrCondition(dialogCondition, editCondition));
        var elements = Enumerable.Range(0, results.Length).Select(results.GetElement).ToArray();
        foreach (var element in elements)
            if ((element.CurrentControlType == 50032 || element.CurrentAriaRole is "dialog" or "alertdialog") && element.CurrentIsOffscreen == 0)
                throw new InvalidOperationException("VisibleDialog");
        UIA.IUIAutomationElement Find(string id, bool secret)
        {
            var matches = elements.Where(e => e.CurrentControlType == 50004 && e.CurrentAutomationId == id).ToArray();
            if (matches.Length == 0) throw new FieldsNotReadyException($"Field={id};Matches=0");
            if (matches.Length != 1) throw new InvalidOperationException($"Field={id};Matches={matches.Length}");
            var element = matches[0];
            if (element.CurrentProcessId != target.Pid || (element.CurrentIsPassword != 0) != secret)
                throw new InvalidOperationException($"Field={id};Pid={element.CurrentProcessId};ExpectedPid={target.Pid};IsPassword={element.CurrentIsPassword};Enabled={element.CurrentIsEnabled};Offscreen={element.CurrentIsOffscreen}");
            if (element.CurrentIsEnabled == 0 || element.CurrentIsOffscreen != 0)
                throw new FieldsNotReadyException($"Field={id};Enabled={element.CurrentIsEnabled};Offscreen={element.CurrentIsOffscreen}");
            if (element.GetCurrentPattern(10002) is not UIA.IUIAutomationValuePattern pattern || pattern.CurrentIsReadOnly != 0)
                throw new InvalidOperationException($"Field={id};NotWritable");
            return element;
        }
        return (Find("username", false), Find("password", true));
    }
    // WinVerifyTrust validates Authenticode, then require Riot's publisher certificate.
    static bool TrustedRiotFile(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1,
                File = pointer, StateAction = 1, ProviderFlags = 0x1000 };
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try
            {
                if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
#pragma warning disable SYSLIB0057
                using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                return cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false) == "Riot Games, Inc.";
            }
            finally { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data); }
        }
        finally { Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr File, Subject; }
    [StructLayout(LayoutKind.Sequential)] struct TrustData { public uint Size; public IntPtr Callback, Client; public uint UiChoice, Revocation, UnionChoice; public IntPtr File; public uint StateAction; public IntPtr State, Url; public uint ProviderFlags, Context; public IntPtr Signature; }
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

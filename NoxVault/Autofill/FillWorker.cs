using System;
using System.Linq;
using System.Threading;
using UIA = Interop.UIAutomationClient;

namespace NoxVault;

// Runs inside the --autofill-worker process: finds the Riot login fields over UI Automation and writes them directly.
internal static partial class Autofill
{
    const int ValuePattern = 10002;

    internal sealed class FieldsNotReadyException(string message) : InvalidOperationException(message);

    internal static FillResult Execute(FillRequest request)
    {
        if (request.Mode is not ("probe" or "fill" or "clear-test") || request.Mode != "probe" && request.Target == null)
            return new(false, "Kein geprüfter Ausfüllauftrag.");
        if (request.Mode == "fill" && (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password)
            || request.Username.Length > 10000 || request.Password.Length > 10000))
            return new(false, "Benutzername oder Passwort fehlt oder überschreitet die unterstützte Länge.");
        FillTarget target;
        try { target = request.Target ?? Discover(); }
        catch (Exception) // No unique Riot window: ask the user to open the login instead of guessing.
        {
            return new(false, "Öffne den Riot-Anmeldebildschirm und wähle deinen Account hier erneut.");
        }
        using var process = Validate(target, request.Target == null, out var verifiedHash);
        target = target with { Hash = verifiedHash };
        UIA.IUIAutomationElement user, password;
        try { (user, password) = ReadReadyFields(() => Fields(target), () => EnsureCurrent(target, request.Mode == "fill"), Pause); }
        catch (Exception ex) // Reported with a diagnostic so a changed Riot client can be told apart from a blocked login.
        {
            return new(false, "Die Riot-Loginfelder sind noch nicht bereit. Schließe offene Riot-Dialoge und wähle deinen Account erneut.",
                Diagnostic: ex is InvalidOperationException ? ex.Message : ex.GetType().Name);
        }
        if (request.Mode == "probe") return new(true, "Riot-Login erkannt · direkter Feldzugriff verfügbar", target);
        if (GetForegroundWindow() != (IntPtr)target.Window)
            return new(false, "Riot konnte nicht aktiviert werden. Login öffnen und erneut ausfüllen.");
        return request.Mode == "clear-test" ? ClearTestFields(target, user, password) : Write(request, target, user, password);
    }

    static FillResult ClearTestFields(FillTarget target, UIA.IUIAutomationElement user, UIA.IUIAutomationElement password)
    {
        EnsureCurrent(target);
        var userPattern = (UIA.IUIAutomationValuePattern)user.GetCurrentPattern(ValuePattern);
        if (userPattern.CurrentValue != "vault-autofill-test") return new(false, "Testfelder wurden verändert und bleiben erhalten.");
        ((UIA.IUIAutomationValuePattern)password.GetCurrentPattern(ValuePattern)).SetValue("");
        EnsureCurrent(target);
        userPattern.SetValue("");
        return new(true, "Synthetische Testfelder geleert.");
    }

    static FillResult Write(FillRequest request, FillTarget target, UIA.IUIAutomationElement user, UIA.IUIAutomationElement password)
    {
        bool written = GuardedFill(() => EnsureCurrent(target),
            () => ((UIA.IUIAutomationValuePattern)user.GetCurrentPattern(ValuePattern)).SetValue(request.Username),
            () =>
            {
                // Re-resolve after the first mutation; never read back the password.
                var next = ReadReadyFields(() => Fields(target), () => EnsureCurrent(target), Pause);
                return user.GetRuntimeId().SequenceEqual(next.user.GetRuntimeId())
                    && password.GetRuntimeId().SequenceEqual(next.password.GetRuntimeId())
                    && ((UIA.IUIAutomationValuePattern)next.user.GetCurrentPattern(ValuePattern)).CurrentValue == request.Username;
            },
            () => ((UIA.IUIAutomationValuePattern)password.GetCurrentPattern(ValuePattern)).SetValue(request.Password));
        if (!written) return new(false, "Loginfelder oder Benutzername wurden verändert. Passwort wurde nicht eingefügt.");
        return new(true, "Eingabe ausgeführt. Bitte im Riot Client prüfen und selbst anmelden.", target);
    }

    static void Pause() => Thread.Sleep(50);

    internal static bool GuardedFill(Action ensureTarget, Action writeUser, Func<bool> confirmFields, Action writePassword)
    {
        ensureTarget();
        writeUser();
        ensureTarget();
        if (!confirmFields()) return false;
        ensureTarget();
        writePassword();
        return true;
    }

    internal static T ReadReadyFields<T>(Func<T> read, Action validate, Action wait)
    {
        for (int attempt = 0; ; attempt++)
        {
            validate();
            try { return read(); }
            catch (FieldsNotReadyException) when (attempt < 4) { wait(); }
        }
    }

    static (UIA.IUIAutomationElement user, UIA.IUIAutomationElement password) Fields(FillTarget target)
    {
        var automation = new UIA.CUIAutomation8();
        var root = automation.ElementFromHandle((IntPtr)target.Window);
        var dialogCondition = automation.CreateOrCondition(
            automation.CreatePropertyCondition(30003, 50032), automation.CreateOrCondition(
                automation.CreatePropertyCondition(30101, "dialog"), automation.CreatePropertyCondition(30101, "alertdialog")));
        var editCondition = automation.CreateAndCondition(automation.CreatePropertyCondition(30003, 50004),
            automation.CreateOrCondition(automation.CreatePropertyCondition(30011, "username"),
                automation.CreatePropertyCondition(30011, "password")));
        // One provider traversal for both login fields and blocking dialogs.
        var results = root.FindAll(UIA.TreeScope.TreeScope_Descendants, automation.CreateOrCondition(dialogCondition, editCondition));
        var elements = Enumerable.Range(0, results.Length).Select(results.GetElement).ToArray();
        foreach (var element in elements)
            if ((element.CurrentControlType == 50032 || element.CurrentAriaRole is "dialog" or "alertdialog")
                && element.CurrentIsOffscreen == 0)
                throw new InvalidOperationException("VisibleDialog");
        return (Field(elements, target, "username", secret: false), Field(elements, target, "password", secret: true));
    }

    static UIA.IUIAutomationElement Field(UIA.IUIAutomationElement[] elements, FillTarget target, string id, bool secret)
    {
        var matches = elements.Where(e => e.CurrentControlType == 50004 && e.CurrentAutomationId == id).ToArray();
        if (matches.Length == 0) throw new FieldsNotReadyException($"Field={id};Matches=0");
        if (matches.Length != 1) throw new InvalidOperationException($"Field={id};Matches={matches.Length}");
        var element = matches[0];
        if (element.CurrentProcessId != target.Pid || (element.CurrentIsPassword != 0) != secret)
            throw new InvalidOperationException($"Field={id};Pid={element.CurrentProcessId};ExpectedPid={target.Pid};"
                + $"IsPassword={element.CurrentIsPassword};Enabled={element.CurrentIsEnabled};Offscreen={element.CurrentIsOffscreen}");
        if (element.CurrentIsEnabled == 0 || element.CurrentIsOffscreen != 0)
            throw new FieldsNotReadyException($"Field={id};Enabled={element.CurrentIsEnabled};Offscreen={element.CurrentIsOffscreen}");
        if (element.GetCurrentPattern(ValuePattern) is not UIA.IUIAutomationValuePattern pattern || pattern.CurrentIsReadOnly != 0)
            throw new InvalidOperationException($"Field={id};NotWritable");
        return element;
    }
}

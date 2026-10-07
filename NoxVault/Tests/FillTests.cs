using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NoxVault;

// Quick-fill window checks with synthetic accounts, and the --autofill-ui-test harness against a real Riot Client.
internal sealed partial class MainWindow
{
    static readonly FillTarget SyntheticTarget = new(1, 1, 1, @"C:\Synthetic\Riot Client.exe", new string('A', 64));

    internal Window CreateFillPreview()
    {
        if (!preview || Data == null) throw new InvalidOperationException();
        Data.Accounts[0].Autofill = new(SyntheticTarget.Path, SyntheticTarget.Hash, false);
        ShowQuickFill(SyntheticTarget, false);
        return quickFillWindow ?? throw new InvalidOperationException("Picker did not open.");
    }

    internal void TestQuickFillIsolation()
    {
        Hide();
        ShowQuickFillNotice("Synthetischer Test: Riot-Login aktivieren.");
        if (IsVisible || quickFillWindow?.IsVisible != true) throw new InvalidOperationException("Riot notice opened the main window.");
        quickFillWindow.Close();
        ShowQuickFillNotice("Synthetischer Test: Windows-Zugriff einrichten.");
        if (IsVisible || quickFillWindow?.IsVisible != true) throw new InvalidOperationException("Riot unlock opened the main window.");
        if (((StackPanel)quickFillWindow.Content).Children.OfType<Button>().Any(b => Equals(b.Content, "Entsperren")))
            throw new InvalidOperationException("Quick fill still offers password entry.");
        quickFillWindow.Close();
        ShowQuickFill(null);
        if (IsVisible || quickFillWindow?.IsVisible != true) throw new InvalidOperationException("Riot picker opened the main window.");
        var form = (StackPanel)((Border)quickFillWindow.Content).Child;
        var list = (StackPanel)form.Children.OfType<ScrollViewer>().Single().Content;
        var added = new Account
        {
            Title = "Synthetic new Riot entry", Category = "Valorant", Username = "Synthetic",
            Password = "Synthetic"
        };
        Data!.Accounts.Add(added);
        refreshQuickFill!();
        if (!list.Children.OfType<Button>().Any(b => Equals(b.Tag,
            added.Id))) throw new InvalidOperationException("New unbound Riot account missing from picker.");
        added.Category = "Unrelated";
        refreshQuickFill();
        if (list.Children.OfType<Button>().Any(b => Equals(b.Tag, added.Id)))
            throw new InvalidOperationException("Picker retained account moved out of Riot categories.");
        Data.Accounts.Remove(added);
        quickFillWindow.Close();
        if (refreshQuickFill != null) throw new InvalidOperationException("Closed picker retained refresh callback.");
    }

    internal void TestDirectQuickFill()
    {
        try
        {
            OpenQuickFill();
            if (IsVisible || Data == null || quickFillWindow?.Content is not Border)
                throw new InvalidOperationException("Quick fill did not restore the account picker directly.");
            quickFillWindow.Close();
            quickFillAfterUnlock = true;
            BuildShell();
            Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            if (quickFillAfterUnlock || quickFillWindow?.Content is not Border)
                throw new InvalidOperationException("Quick fill did not resume automatically after initial setup.");
            Lock();
            if (Data != null || session.Vault.Unlocked || !File.Exists(session.Vault.FilePath + ".quickfill"))
                throw new InvalidOperationException("Lock failed to release data or removed direct quick-fill access.");
            if (gateState != "WindowsAccess") throw new InvalidOperationException("Main window still offers a password gate.");
            if (!TryAutomaticLogin()
                || !session.Vault.Unlocked) throw new InvalidOperationException("Main window did not restore Windows access.");
            BuildShell();
            LockCore(false);
            OpenQuickFill();
            if (Data == null || quickFillWindow?.Content is not Border)
                throw new InvalidOperationException("Quick fill required a password after locking.");
        }
        finally { quitting = true; Close(); }
    }

    internal async void PrepareFillIntegrationTest(string report, bool automatic = false)
    {
        quitting = true; // Closing the isolated test window exits instead of hiding.
        session.Data ??= session.Vault.CreateForWindows();
        var data = session.Data;
        var probe = await FillOperation(new("probe"));
        if (!probe.Ok || probe.Target is not { } target)
        {
            File.WriteAllText(report, "FAIL: " + probe.Message + ";diagnostic=" + probe.Diagnostic);
            Close();
            return;
        }
        data.Accounts.Add(new Account
        {
            Title = "Autofill-Testaccount", Category = data.Categories[0], Username = "vault-autofill-test",
            Password = "Synthetic-Only-42!",
            Autofill = new(target.Path, target.Hash, false),
        });
        session.Vault.Save(data);
        BuildShell();
        Title = "vault · Ausfülltest";
        FillTestObserver = result => File.AppendAllText(report, (result.Ok ? "PASS: " : "FAIL: ") + result.Message + Environment.NewLine);
        FillTestTrace = message => File.AppendAllText(report, "TRACE: " + message + Environment.NewLine);
        File.WriteAllText(report, hotkeys.FillRegistered ? "READY: synthetic account; " + FillShortcutLabel + Environment.NewLine
            : "FAIL: shortcut registration" + Environment.NewLine);
        if (!automatic) return;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await FillAccount(data.Accounts.Single().Id);
        File.AppendAllText(report,
            "STATUS: " + status.Text + Environment.NewLine + "elapsed_ms=" + elapsed.ElapsedMilliseconds + Environment.NewLine);
        var cleanup = await FillOperation(new("clear-test", target));
        File.AppendAllText(report, "cleanup=" + cleanup.Ok + ";" + cleanup.Message + Environment.NewLine);
        Close();
    }
}

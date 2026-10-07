using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// Binding an account to the Riot Client and filling its login. Every fill runs a fresh probe first.
internal sealed partial class MainWindow
{
    CancellationTokenSource? fillCancellation;
    System.Windows.Threading.DispatcherTimer? fillStatusTimer;
    internal Action<FillResult>? FillTestObserver;
    internal Action<string>? FillTestTrace;

    string FillShortcutLabel => Hotkeys.FillLabel(prefs.FillShortcutF);

    void CancelFill()
    {
        fillStatusTimer?.Stop();
        fillStatusTimer = null;
        pendingFillTarget = null;
        quickFillAfterUnlock = false;
        fillCancellation?.Cancel();
        quickFillWindow?.Close();
        quickFillWindow = null;
    }

    // Esc is registered globally while a fill runs, so the user can stop it from inside Riot.
    async Task<FillResult> FillOperation(FillRequest request)
    {
        if (fillCancellation != null) return new(false, "Eine Ausfüllprüfung läuft bereits.");
        using var cancellation = new CancellationTokenSource();
        fillCancellation = cancellation;
        bool escape = request.Mode == "fill" && hotkeys.RegisterEscape();
        try
        {
            if (request.Mode == "fill" && !escape) return new(false, "Abbruch mit Esc ist gerade nicht verfügbar. Bitte erneut versuchen.");
            return await Autofill.RunAsync(request, cancellation.Token);
        }
        finally
        {
            if (escape) hotkeys.ReleaseEscape();
            fillCancellation = null;
        }
    }

    void ConfigureFill(Guid accountId)
    {
        if (Data == null || preview) return;
        var account = Data.Accounts.FirstOrDefault(a => a.Id == accountId && a.DeletedUtc == null);
        if (account == null || !AccountQueries.SupportsRiotFill(account)) return;
        var form = new StackPanel();
        form.Children.Add(Text("Ein Klick für deinen Riot-Login", TextRole.Title, bold: true));
        var introduction = Text("Öffne den Anmeldebildschirm im Riot Client und wähle, welche Zugangsdaten vault einfügen soll.",
            TextRole.Body, Theme.Sub);
        introduction.Margin = new Thickness(0, Theme.S4, 0, 0);
        form.Children.Add(introduction);
        var source = new ComboBox
        {
            ItemsSource = new[] { "Benutzername", "E-Mail" },
            SelectedIndex = account.Autofill?.UseEmail == true ? 1 : 0
        };
        Field(form, "Für das Login verwenden", source);
        var hint = Text("Verwende deinen Riot-Loginnamen. Dein Spielername mit #Tag kann davon abweichen. "
            + "Nach einem Clientupdate kann eine "
            + "erneute Zuordnung nötig sein.", TextRole.Small, Theme.Sub);
        hint.Margin = new Thickness(0, Theme.S5, 0, 0);
        form.Children.Add(hint);
        var feedback = Text("", TextRole.Small, Theme.Fg, bold: true);
        feedback.Margin = new Thickness(0, Theme.S5, 0, 0);
        form.Children.Add(feedback);
        var dialog = new InAppDialog(this, "Riot verbinden", form, 510, false);
        var check = Primary("Login prüfen & verbinden", () => { });
        check.Click += async (_, _) => await BindRiot(accountId, source, check, (dialog, feedback));
        var actions = account.Autofill != null
            ? InAppDialog.Actions(Ghost("Zuordnung entfernen",
                () => { if (Mutate(d => d.Accounts.First(a => a.Id == accountId).Autofill = null)) dialog.Close(); }), check)
            : InAppDialog.Actions(check);
        form.Children.Add(actions);
        dialog.Closed += (_, _) => fillCancellation?.Cancel();
        dialog.ShowDialog();
    }

    async Task BindRiot(Guid accountId, ComboBox source, Button check, (InAppDialog Dialog, TextBlock Feedback) view)
    {
        bool useEmail = source.SelectedIndex == 1;
        var current = Data?.Accounts.FirstOrDefault(a => a.Id == accountId && a.DeletedUtc == null);
        if (current == null || !AccountQueries.SupportsRiotFill(current)) return;
        if (string.IsNullOrWhiteSpace(useEmail ? current.Email : current.Username) || string.IsNullOrEmpty(current.Password))
        {
            view.Feedback.Text = "Bitte zuerst Loginname und Passwort im Account hinterlegen.";
            return;
        }
        check.IsEnabled = source.IsEnabled = false;
        view.Feedback.Text = "Riot-Login wird geprüft …";
        var result = await FillOperation(new("probe"));
        check.IsEnabled = source.IsEnabled = true;
        if (Data == null || !view.Dialog.IsVisible) return;
        view.Feedback.Text = result.Message;
        if (result.Ok && result.Target is { } target
            && Mutate(d => d.Accounts.First(a => a.Id == accountId).Autofill = new(target.Path, target.Hash, useEmail)))
        {
            view.Dialog.Close();
            status.Text = "Riot zugeordnet · Schnellauswahl: "
                + (hotkeys.FillRegistered ? FillShortcutLabel : "Shortcut belegt – Einstellungen öffnen");
        }
    }

    async Task FillAccount(Guid id, FillTarget? target = null, TextBlock? feedback = null)
    {
        if (fillCancellation != null || Data == null || preview) return;
        var account = Data.Accounts.FirstOrDefault(a => a.Id == id && a.DeletedUtc == null);
        if (account == null || !AccountQueries.SupportsRiotFill(account)) return;
        var savedProfile = account.Autofill;
        var message = feedback ?? status;
        lastActivity = DateTime.UtcNow;
        message.Text = "Riot-Login wird geprüft …";
        // Always obtain a fresh worker verification before renewing a binding.
        // A captured target must pass the same signature, identity and field checks.
        var probe = await FillOperation(new("probe", target));
        FillTestTrace?.Invoke("probe=" + probe.Ok + ";diagnostic=" + probe.Diagnostic);
        if (Data == null) return;
        account = Data.Accounts.FirstOrDefault(a => a.Id == id && a.DeletedUtc == null);
        if (account == null || account.Autofill != savedProfile || !AccountQueries.SupportsRiotFill(account)) return;
        if (!probe.Ok || probe.Target is not { } found) { message.Text = probe.Message; return; }
        var profile = savedProfile ?? new RiotFillProfile(found.Path, found.Hash, string.IsNullOrWhiteSpace(account.Username));
        if (string.IsNullOrWhiteSpace(profile.UseEmail ? account.Email : account.Username) || string.IsNullOrEmpty(account.Password))
        { message.Text = "Bitte zuerst Benutzername oder E-Mail und Passwort ergänzen."; return; }
        var refreshed = Autofill.ProfileForVerifiedTarget(profile, probe);
        if (refreshed == null)
        {
            message.Text = "Riot wurde an einen anderen Ort verschoben. Bitte die Riot-Zuordnung erneut einrichten.";
            return;
        }
        if (savedProfile != null && refreshed != savedProfile)
        {
            if (!Mutate(d => d.Accounts.First(a => a.Id == id).Autofill = refreshed))
            { message.Text = "Die aktualisierte Riot-Zuordnung konnte nicht gespeichert werden. Bitte erneut versuchen."; return; }
            savedProfile = refreshed;
        }
        if (!await Autofill.ActivateTarget(found))
        {
            message.Text = "Riot konnte nicht aktiviert werden. Bitte die Ausfüllaktion erneut wählen.";
            return;
        }
        await FinishFill(id, savedProfile, refreshed, (found, message));
    }

    async Task FinishFill(Guid id, RiotFillProfile? savedProfile, RiotFillProfile profile, (FillTarget Target, TextBlock Message) fill)
    {
        var latest = Data?.Accounts.FirstOrDefault(a => a.Id == id && a.DeletedUtc == null);
        if (latest == null || latest.Autofill != savedProfile || !AccountQueries.SupportsRiotFill(latest)) return;
        var result = await FillOperation(new("fill", fill.Target, profile.UseEmail ? latest.Email : latest.Username, latest.Password));
        FillTestObserver?.Invoke(result);
        if (Data == null) return;
        fill.Message.Text = result.Message;
        if (!result.Ok) return;
        quickFillWindow?.Close();
        fillStatusTimer?.Stop();
        status.Text = result.Message;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        fillStatusTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!ReferenceEquals(fillStatusTimer, timer)) return;
            fillStatusTimer = null;
            if (status.Text == result.Message) status.Text = "";
        };
        timer.Start();
    }
}

using System.Windows;
using static NoxVault.Elements;

namespace NoxVault;

// Windows access, clipboard and reveal timing, and the local password check.
internal sealed partial class MainWindow
{
    void SecurityPage(SettingsView view)
    {
        var security = view.AddPage("Sicherheit", "Windows-Zugriff und Schutz deiner Zugangsdaten.");
        SettingsView.Section(security, "Direkt öffnen", "vault startet ohne Master-Passwort. Dein Windows-Benutzerkonto öffnet die "
            + "Zugangsdaten automatisch, auch nach einem Neustart. Bei Inaktivität bleibt vault geöffnet.");
        var clipboardSection = SettingsView.Section(security, "Kopierte Daten entfernen",
            "Leert nur von vault kopierte Inhalte. Gilt ab dem nächsten Kopieren.");
        AddTimeSetting(clipboardSection, "Zwischenablage leeren nach", new[] { 15, 30, 60 },
            (() => prefs.ClipboardSeconds, v => prefs.ClipboardSeconds = v), "Sekunden");
        var revealSection = SettingsView.Section(security, "Passwörter wieder verbergen",
            "Blendet sichtbare Passwörter nach dieser Zeit wieder aus.");
        AddTimeSetting(revealSection, "Passwort verbergen nach", new[] { 10, 15, 30 },
            (() => prefs.RevealSeconds, v => prefs.RevealSeconds = v), "Sekunden");
        var audit = SettingsView.Section(security, "Lokaler Passwortcheck", "Leere und mehrfach verwendete Passwörter finden.");
        var check = Button("Passwörter prüfen …", ShowPasswordCheck);
        check.HorizontalAlignment = HorizontalAlignment.Left;
        audit.Children.Add(check);
    }
}

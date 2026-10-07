# Aufbau

vault ist eine einzelne WPF-Anwendung (`NoxVault/`, .NET 10, nur Windows). Ein Prozess, eine Datei pro Tresor, kein
Netzwerk. Dieselbe EXE läuft in mehreren Rollen, die `EntryPoint` und `App` anhand der Argumente wählen.

```text
EntryPoint ── --enable-windows-access ──> Vault + RememberedLogin   (ohne WPF)
           ── --autofill-worker ────────> Autofill.Worker           (eigener Prozess, MTA, ohne WPF)
           ── sonst ────────────────────> App ── --self-test … ──> Tests/SelfTest
                                              └─ normal ─────────> MainWindow
```

## Module

| Ordner | Inhalt | Hängt ab von |
| --- | --- | --- |
| `Storage/` | Tresordatei (`Vault`: Format, AES-GCM, Argon2id, atomares Schreiben), Datenmodell (`VaultData`, `Account`), Datenordner und Migration (`StoragePaths`), Windows-geschützte Schlüssel (`RememberedLogin`), automatische Sicherungen, Einstellungen | – |
| `Accounts/` | Offener Tresorzustand (`VaultSession`: entsperren, speichern, sperren, vergessen), Suche und Passwortcheck (`AccountQueries`), Generator | `Storage` |
| `Platform/` | Win32: globale Shortcuts (`Hotkeys`), Zwischenablage mit Besitzprüfung, Autostart-Verknüpfung, Startprotokoll, Hell/Dunkel von Windows | – |
| `Autofill/` | Riot-Login ausfüllen: Anfrage an den Worker-Prozess, Feldzugriff über UI Automation, Prüfung von Fenster, Prozess und Signatur | `Storage` (Profil) |
| `Interface/Theme/` | Tokens (`Dark.xaml`, `Light.xaml`, `Theme`), Control-Styles, Icons, Federkurven, Marke | `Platform` (Theme) |
| `Interface/Elements/` | Bausteine: Text, Buttons, Felder, Navigationsliste mit Pille, Segmented Control, Schalter, In-App-Dialog | `Theme` |
| `Interface/Window/` | `MainWindow`: Lebenszyklus, Rahmen, Sperrbildschirm, Shell, Sidebar, Liste, Detail, Tray | alles oben |
| `Interface/Dialogs/`, `Settings/`, `Fill/`, `Drag/` | Editor, Generator, Kategorien, Papierkorb, Passwortcheck, Sicherungen; Einstellungsseiten; Riot-Schnellauswahl; Drag & Drop | `Window` |
| `Tests/` | Selbsttest (`--self-test`), Renders beider Themes, Prüfmodi | alles |

`MainWindow` ist eine `partial class` über `Interface/*`: jede Datei baut eine Ansicht. Zustand und Regeln liegen nicht
dort, sondern in `VaultSession`, `Hotkeys`, `AccountQueries` und `Autofill`.

## Datenfluss beim Speichern

1. Eine Ansicht ruft `Mutate(d => …)` auf.
2. `VaultSession.Commit` klont die offenen Daten, wendet die Änderung an, entfernt abgelaufene Papierkorb-Einträge und
   lässt `Vault.Save` validieren, verschlüsseln und atomar schreiben.
3. Erst nach erfolgreichem Schreiben ersetzt die Session ihre Daten; danach werden Navigation, Liste und Detail neu
   aufgebaut und eine automatische Sicherung angestoßen.

Schlägt Schreiben fehl, bleiben Datei und Anzeige unverändert und die Statuszeile nennt den Grund.

## Entsperren und Sperren

- Start: `VaultSession.TryAutomatic` versucht den dauerhaften Windows-Schlüssel (`vault.nox.quickfill`, DPAPI), dann die
  Tagessitzung (`vault.nox.session`), und legt beim allerersten Start einen neuen Tresor an.
- Sperren (Esc, Tray, Windows-Sitzungssperre): Geheimnisse verbergen, Zwischenablage leeren, Schlüssel nullen,
  Ansichten leeren, Sperrbildschirm. Die Tagessitzung wird widerrufen, der Windows-Schlüssel bleibt.

## Ausfüllen

Die Schnellauswahl (Win+F8) und das Mehr-Menü starten pro Vorgang einen frischen `--autofill-worker`. Der Worker
bekommt die Anfrage über stdin, prüft Fenster, Prozess, Startzeit, Dateihash und Riot-Signatur, schreibt Benutzername,
prüft die Felder erneut und schreibt erst dann das Passwort. Es gibt keinen Rückfall auf Zwischenablage oder Tastatur.

# vault

Lokaler Account-Tresor für Windows 10/11 (64 Bit): Zugangsdaten verschlüsselt auf dem eigenen Gerät, geöffnet über das
Windows-Benutzerkonto, mit Ausfüllen im Riot-Login.

Aktueller Stand: **1.2.6**. Direkter Zugriff über das Windows-Benutzerkonto ohne Master-Anmeldung; Riot-Fenster wird vor der Felderkennung aktiviert. Der Live-Test im Riot-Client v140.0.7 verwendete ausschließlich synthetische Zugangsdaten und sendete keine Anmeldung ab.

Dieses Repository enthält Quellcode, Icons, Buildskript und technische Dokumentation. Accountdateien, Schlüssel, Sitzungen, lokale Einstellungen, Laufzeitprotokolle, Build-Ausgaben und Screenshots gehören nicht zum Repository. Test-Zugangsdaten im Quellcode sind ausschließlich synthetische Beispiele. Die älteren Projektpläne und Prüfberichte unter `docs/plans` und `docs/verification` dokumentieren historische Entwicklungsstände.

## Funktionen

- Accounts mit E-Mail, Benutzername, Passwort, Login-Seite, Notiz und eigenen geschützten Feldern
- Kategorien mit Farbe, Favoriten, Suche, Sortierung, Papierkorb für 30 Tage
- Kopieren mit automatischem Leeren der Zwischenablage, Passwortgenerator, lokaler Passwortcheck
- Öffnen per Shortcut (Standard Win+F9) und aus dem Infobereich, Autostart optional
- Riot-Login ausfüllen per Win+F8, ohne Zwischenablage und ohne automatisches Absenden
- Verschlüsselte Sicherungen von Hand und täglich automatisch
- Helles und dunkles Theme, folgt auf Wunsch Windows

## Starten

`dist/NoxVault/NoxVault.exe` öffnen oder die Desktop-Verknüpfung **vault** verwenden. Das veröffentlichte Paket bringt
seine .NET-Laufzeit mit. Bedienung im Detail: [docs/usage.md](docs/usage.md). Speicherung und Schutz:
[docs/security.md](docs/security.md).

## Prüfen

```powershell
dotnet run --project tools/Check
```

Format, Build ohne Warnungen, Struktur- und Code-Regeln und der Selbsttest (230 Prüfungen mit synthetischen Daten,
Renders aller Ansichten in beiden Themes). Details und Veröffentlichung: [docs/development.md](docs/development.md).

## Dokumentation

Einstieg: [docs/README.md](docs/README.md). Regeln für Änderungen: [docs/conventions.md](docs/conventions.md).

Icons: [Material Design Icons](https://pictogrammers.com/library/mdi/) 7.4.47, Apache-2.0.

# Autostartprüfung – 13.09.2026

## Befund vor dem Update

- Der manuelle App-Start wurde vom Windows-Anmeldestart unterschieden.
- Startmenü zeigte auf `dist/NoxVault/NoxVault.exe`; deren SHA-256 entsprach dem letzten Build vom 12.09. Eine zweite alte Installation wurde nicht gefunden.
- Der HKCU-Run-Eintrag NoxVault war vorhanden und korrekt zitiert, erschien aber weder in der abgefragten Windows-StartupCommand-Liste noch in der gezeigten Windows-Autostartansicht.
- Das alte Abschlussprotokoll enthielt nur den Start vom Vortag. Damit war der bisherige Test eines manuellen Tray-Starts kein Nachweis für einen funktionierenden Windows-Anmeldestart. Der genaue Grund für das Übergehen des alten Run-Eintrags ist nicht abschließend nachgewiesen.

## Änderung in 1.2.1

- Reguläre `vault.lnk` im persönlichen Windows-Autostartordner mit `--tray`, festem Arbeitsordner und geprüftem Ziel.
- Startmenü-Link wird bei Veröffentlichung ebenfalls auf dieselbe geprüfte EXE geschrieben.
- Alten Run-Eintrag entfernt; Autostart ausdrücklich aktiviert. Normale spätere Updates setzen eine Windows-Deaktivierung nicht zurück.
- Frühes, begrenztes Startverlaufsprotokoll mit Version, Prozess-ID und Initialisierungsphase; ohne Zugangsdaten oder Kommandozeileninhalte.

## Nachweise

- 176 Prüfungen bestanden, darunter Schreiben/Lesen der Verknüpfung und Prüfung der Tray-Argumente.
- Windows `Win32_StartupCommand` meldet `vault`, Quelle `Startup`.
- Windows Einstellungen nach erneutem Öffnen: **NoxVault – Ein** visuell bestätigt.
- Start über die tatsächliche Autostart-Verknüpfung: Version 1.2.1.0, `Visible=False`, `Locked=True`, `TrayVisible=True`, beide Shortcuts registriert.
- Win+F9 per Tastatureingabe: verstecktes Hauptfenster geöffnet.
- Win+F8 per Tastatureingabe: Riot-Entsperrdialog mit Passwortfeld sichtbar. Kein Passwort eingegeben und keine Zugangsdaten ausgefüllt.
- Start über die tatsächliche Startmenü-Verknüpfung separat geprüft.

## Verbleibende Grenze

Es wurde kein erneuter Windows-Neustart und keine Abmeldung ausgelöst. Die komplette Anmeldung nach dem nächsten Boot bleibt separat zu bestätigen. Das neue Verlaufsprotokoll ermöglicht dafür eine genaue Prüfung.

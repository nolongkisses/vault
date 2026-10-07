# vault

Aktueller Stand: **1.2.6**. Direkter Zugriff über das Windows-Benutzerkonto ohne Master-Anmeldung; Riot-Fenster wird vor der Felderkennung aktiviert. 223 Standardprüfungen bestanden. Der zusätzliche Live-Test im Riot-Client v140.0.7 verwendete ausschließlich synthetische Zugangsdaten und sendete keine Anmeldung ab.

Dieses Repository enthält Quellcode, Icons, Buildskript und technische Dokumentation. Accountdateien, Schlüssel, Sitzungen, lokale Einstellungen, Laufzeitprotokolle, Build-Ausgaben und Screenshots gehören nicht zum Repository. Test-Zugangsdaten im Quellcode sind ausschließlich synthetische Beispiele. Die älteren Projektpläne und Prüfberichte dokumentieren historische Entwicklungsstände.

Lokaler Account-Tresor für Windows 10/11 (64 Bit). Dunkle WPF-Oberfläche mit abgerundeten Karten und nativen runden Fensterecken unter Windows 11.

## Starten

`dist/NoxVault/NoxVault.exe` öffnen oder die Desktop-Verknüpfung **vault** verwenden. Das veröffentlichte Paket bringt seine .NET-Laufzeit mit.

Ab 1.2.5 startet vault direkt mit deinen Zugangsdaten. Es gibt keine Master-Passwort-Anmeldung und keinen Schalter „Anmeldung merken“ mehr. Windows-DPAPI schützt den lokalen Schlüssel für dein Windows-Benutzerkonto. Neue Tresore werden automatisch ohne Passwort eingerichtet; vorhandene verschlüsselte Tresore müssen einmal in Windows-Zugriff überführt werden.

## Bedienung


- **Account hinzufügen:** Bezeichnung, Kategorie, E-Mail, Benutzername, Passwort und optional eine Notiz eingeben. Nur die Bezeichnung ist verpflichtend.
- **Kopieren:** E-Mail, Benutzername und Passwort haben jeweils einen eigenen Kopierbutton. Passwörter sind standardmäßig verdeckt.
- **Kategorien:** Eigene Kategorien hinzufügen und die ausgewählte Kategorie umbenennen oder löschen. Beim Löschen werden ihre Accounts in die angezeigte verbleibende Kategorie verschoben.
- **Suche:** Durchsucht Bezeichnung, E-Mail, Benutzername und Kategorie. Favoriten sind separat erreichbar.
- **Ausblenden:** Esc, der Seitenleistenbutton und die Windows-Sitzungssperre blenden vault aus und geben die geöffneten Daten frei. Beim nächsten Öffnen wird automatisch über Windows entsperrt. Die Inaktivitätssperre entfällt.
- **Schließen:** Das Kreuz blendet vault aus und lässt die App im Infobereich weiterlaufen. Vollständig beenden: Rechtsklick auf das Tray-Symbol → Beenden.
- **Shortcut:** Standardmäßig Win + F9. Funktioniert bei laufender App, auch im Tray. Unter Einstellungen → Allgemein das Shortcut-Feld anklicken, eine eigene Kombination aus Strg, Alt, Shift oder Win und einer weiteren Taste drücken und übernehmen. Belegte Kombinationen werden nicht übernommen.
- **Autostart:** Unter Einstellungen optional aktivieren. Startet gesperrt im Tray; standardmäßig ausgeschaltet.
  Ab 1.2.1 erfolgt die Anmeldung über `vault.lnk` im persönlichen Windows-Autostartordner. Der bisherige NoxVault-Run-Eintrag wird beim Aktivieren entfernt. `./publish.ps1 -EnableAutoStart` aktiviert den Autostart ausdrücklich; normale Updates erneuern die Startmenü-Verknüpfung und erhalten eine vorhandene Autostart-Verknüpfung einschließlich ihrer Windows-Deaktivierung. Windows zeigt den Eintrag gegebenenfalls als **NoxVault**; eine bereits offene Einstellungsseite neu öffnen. Dieser Weg entspricht der [Microsoft-Anleitung für nicht aufgelistete Autostart-Apps](https://support.microsoft.com/de-de/windows/experience/startup-boot/configure-startup-applications-in-windows).
  `startup-history.log` im lokalen NoxVault-Datenordner protokolliert zusätzlich Eintritt, Version und Startabschluss. So lassen sich ein ausgebliebener Prozessstart und ein Fehler während der Initialisierung unterscheiden.
  Der Hintergrundstart erzeugt kein sichtbares Fenster und öffnet auch keine gemerkte Sitzung. Ein weiterer Autostart-Aufruf lässt eine bereits laufende App im Hintergrund. `%LOCALAPPDATA%\NoxVault\startup.log` zeigt Zeitpunkt, Tray-Status, Sperrstatus und Shortcut-Registrierung des letzten Starts (ohne Tresorinhalte). Bei ausbleibendem Start auch Windows → Apps → Autostart prüfen; ein vorhandener Registry-Eintrag allein belegt keinen erfolgten Start.
- **Sicherungen:** Unter Einstellungen verschlüsselt exportieren oder wiederherstellen. Sicherungen des aktuellen Tresors öffnen mit dessen Windows-Schlüssel automatisch. Vor dem Ersetzen wird eine verschlüsselte Kopie im Tresorordner abgelegt. Alte Sicherungen mit einem anderen Schlüssel können mit ihrem damaligen Passwort importiert werden. Neue Windows-Tresore und ihre Sicherungen benötigen den zugehörigen Windows-Schlüssel; sie sind nicht allein über ein Master-Passwort auf einem anderen PC wiederherstellbar.

## Speicherung und Schutz

Tresordatei ab 1.2.4: `%USERPROFILE%\.noxvault\vault.nox`. Dieser Ordner wird unabhängig davon verwendet, ob vault aus Codex, über eine Verknüpfung oder beim Windows-Autostart geöffnet wird. Die lokale Einstellungsdatei enthält Shortcut, Zeitoptionen, Sortierung und Sicherungsordner; keine Zugangsdaten. Keine Cloud, keine Telemetrie und keine Netzwerkfunktionen in der App.

Beim ersten Start mit 1.2.4 wird der vorhandene verschlüsselte Tresor aus dem bisherigen `%LOCALAPPDATA%\NoxVault` oder dem umgeleiteten Codex-Verzeichnis übernommen. Die Originaldateien bleiben unverändert. Unterschiedliche vorhandene Tresore führen zum Abbruch statt zu einer geratenen Auswahl. Gemerkte Sitzungen werden nicht übertragen; einmal mit dem bisherigen Master-Passwort entsperren. Ein bekannter, vorübergehend fehlender oder nicht zugänglicher Tresor darf nicht als neue Installation behandelt werden. Startprotokolle liegen ebenfalls in `%USERPROFILE%\.noxvault`; sie enthalten nun Datenpfad und Zustand der Anmeldeseite.

Die folgende Passwortableitung beschreibt bestehende und importierte Passwort-Tresore. Neue Tresore ab 1.2.5 verwenden einen zufälligen 256-Bit-Schlüssel, der vor dem Speichern des Tresors mit Windows-DPAPI in `vault.nox.quickfill` gesichert wird. Das bestehende Format v4 und AES-GCM bleiben erhalten. Die bisherige Formatbeschreibung: AES-256-GCM, pro Speicherung ein zufälliger 96-Bit-Nonce, 128-Bit-Authentifizierungstag. Argon2id v1.3 leitet den 256-Bit-Schlüssel aus dem Master-Passwort ab: 64 MiB Speicher, 3 Durchläufe, Parallelität 4, zufälliger 256-Bit-Salt. Die Parameter sind durch die Formatversion festgelegt. Umsetzung mit BouncyCastle.Cryptography 2.7.0 für Argon2id und den .NET-Kryptografie-APIs für AES-GCM; keine RSA-Verschlüsselung. Magic und Salt sind als zusätzliche Daten authentifiziert, Nonce und Nutzdaten durch GCM geschützt. Speicherung über eine temporäre verschlüsselte Datei mit anschließendem atomarem Austausch; fehlgeschlagene Speicherung übernimmt keine Änderungen in die Oberfläche. Schlüssel und temporäre Klartext-Bytearrays werden beim Freigeben überschrieben.

Bestehende v1-Tresore (PBKDF2-HMAC-SHA256 mit 600.000 Iterationen) werden nach erfolgreicher Eingabe des Master-Passworts automatisch mit frischem Salt auf v3 umgestellt. Bei falschem Passwort oder fehlgeschlagenem Schreiben bleibt die ursprüngliche Datei erhalten. Eine alte gemerkte Anmeldung wird verworfen, damit diese Umstellung mit dem Master-Passwort erfolgen kann. Alte Sicherungen bleiben lesbar und werden beim Lesen nicht geändert; sie behalten ihren bisherigen Schutz. Nach der Umstellung können frühere App-Versionen den aktiven v3-Tresor nicht öffnen.

Grundlagen: [OWASP zur Verschlüsselung](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html), [Argon2-Empfehlungen in RFC 9106](https://www.rfc-editor.org/rfc/rfc9106.html#section-4). Mitgelieferte Laufzeit: .NET 10.0.12 LTS (Stand 11.09.2026).

Die Zwischenablage wird nach der eingestellten Zeit (standardmäßig 30 Sekunden) oder beim Sperren geleert, wenn sie noch den von Nox gesetzten Inhalt enthält. Neuer Zwischenablageinhalt bleibt erhalten. Windows-Verlauf und Cloud-Zwischenablage werden für kopierte Inhalte über die vorgesehenen Ausschlussformate deaktiviert. Andere Clipboard-Tools können Inhalte trotzdem erfassen. Während der Tresor entsperrt ist, liegen Zugangsdaten im Prozessspeicher; verwaltete .NET-Zeichenfolgen können nicht zuverlässig überschrieben werden. Dies ist ein individuell entwickeltes Programm, kein unabhängig sicherheitsgeprüfter Passwortmanager.

## Shortcut-Prüfung

Die lokale Prüfung ergab am 11.09.2026: Win + F8, F9 und F10 ließen sich registrieren; Win + F11 war bereits belegt (Fehler 1409). Das ist eine Momentaufnahme. Nox prüft die Registrierung erneut bei jedem Start und zeigt Konflikte in den Einstellungen.

Microsoft reserviert Kombinationen mit der Windows-Taste grundsätzlich für das Betriebssystem: [RegisterHotKey-Dokumentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey). Die Prüfung belegt erfolgreiche Registrierung, nicht die dauerhafte Verfügbarkeit über Systemupdates oder alle Programme hinweg. Strg + Alt + F8 wurde zusätzlich durch automatisierte Tastatureingabe geprüft: Das versteckte und das minimierte Fenster wurden geöffnet und aktiviert.

## Entwicklung und Prüfungen

Geprüftes lokales Update: `./publish.ps1`. Das Skript baut in ein separates Verzeichnis, testet die fertige Anwendung mit synthetischen Daten und ersetzt die installierte Datei erst nach bestandenen Tests. Die vorherige Version bleibt als `dist/NoxVault/NoxVault.previous.exe` erhalten. `./publish.ps1 -CheckOnly` baut und testet ohne Installation. Vor dem Update laufende Eingaben abschließen; die installierte App wird für den Austausch beendet. Ab App-Version 1.1 beendet sie sich dafür nur ohne offene Dialoge oder laufende Vorgänge; andernfalls bricht das Update ab. Beim einmaligen Wechsel von einer älteren Version gilt noch deren bisheriger Beendigungsablauf.

Projektplan und nächste Ausbaustufen: [PROJEKTPLAN.md](PROJEKTPLAN.md).

Strg+F fokussiert und markiert die Accountsuche. Schnelle Sucheingaben werden über 150 ms gebündelt. Beim Sperren wird eine geplante Suche verworfen. Kann eine gemerkte Sitzung wegen einer Dateisperre nicht gelöscht werden, verhindert eine separate Widerrufsdatei ihre Wiederverwendung. Bei umfassend fehlenden Schreibrechten wird die Oberfläche dennoch gesperrt und ein Hinweis angezeigt; die Wiederanmeldung ist in der laufenden App blockiert.

Voraussetzung für den Quellcode: .NET 10 SDK unter Windows. `publish.ps1` verwendet das lokale SDK 10.0.401 unter `artifacts/dotnet-10.0.401`, falls vorhanden, sonst das installierte `dotnet`.

```powershell
dotnet build NoxVault/NoxVault.csproj -c Release
dotnet publish NoxVault/NoxVault.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist/NoxVault
```

Tests mit rein synthetischen Daten; kein Zugriff auf den produktiven Tresor und keine Änderung der realen Zwischenablage:

```powershell
New-Item -ItemType Directory artifacts -Force
$report = Join-Path (Get-Location) 'artifacts\test-results.txt'
Start-Process '.\NoxVault\bin\Release\net10.0-windows\NoxVault.exe' -ArgumentList '--self-test', ('"' + $report + '"') -Wait
Get-Content $report
```

Die Tests prüfen Verschlüsselung, falsche Passwörter, manipulierte Dateien, atomare Validierung, Änderungen und Löschungen, Backup/Restore, Zwischenablage-Besitzprüfung mit einem Testadapter, Passwortgenerator sowie Speichern/Sperren/Entsperren und Suche in der WPF-Oberfläche. Zusätzlich werden PNGs der Oberfläche mit synthetischen Daten bei 1180×780 und 980×660 gerendert. Automatische Sperre per Windows-Sitzungsereignis, echter Clipboard-Verlauf und physische globale Tastatureingabe benötigen ergänzend einen manuellen Test.

Die Einstellungen sind in Allgemein, Sicherheit, Sicherungen, Ausfüllen und Über vault gegliedert. Bei wenig Platz erscheint eine kompakte Bereichsauswahl. Änderungen am Shortcut zum Öffnen von vault werden erst mit „Übernehmen“ gespeichert und können vorher verworfen werden.

## Neu in 1.2: Riot-Login ausfüllen

1. Riot-Login öffnen und in vault einen Account auswählen.
2. Unter „Ausfüllen einrichten“ den tatsächlichen Login-Benutzernamen oder die E-Mail als Quelle wählen und den Login prüfen lassen.
3. „Im Riot Client ausfüllen“ füllt beide Felder. Alternativ im aktiven Riot-Login **Win+F8** drücken und den Account in der kleinen Auswahl anklicken. Unter Einstellungen → Ausfüllen sind Win+F8–F12 wählbar; belegte Kombinationen werden gemeldet.

Die Anmeldung bestätigst du selbst. Bei gesperrtem Tresor ist zunächst Entsperren erforderlich. Ab Version 1.2.3 erneuert vault die gespeicherte Dateiprüfsumme nach einem Riot-Update automatisch, sofern der Installationspfad unverändert ist und die erneute Prüfung von Riot-Signatur, Prozess und Loginfeldern erfolgreich war. Die gewählte Loginquelle bleibt erhalten. Ein anderer Installationspfad erfordert eine neue Zuordnung. Vor dem Schreiben prüft der Ausfüllprozess die aktuelle Datei erneut; andere Apps und Browser sind nicht freigegeben.

Das Hauptfenster und Win+F8 öffnen die Zugangsdaten direkt über das aktuelle Windows-Benutzerkonto. Nach dem Update einmal mit dem bisherigen Master-Passwort entsperren, sofern noch kein Windows-Schlüssel gespeichert wurde. Windows-DPAPI schützt den Sitzungsschlüssel in `vault.nox.quickfill`; das Master-Passwort wird nicht gespeichert. Der Zugriff gilt auch nach Mitternacht, App-Neustart und „Tresor sperren“; die Sperre entfernt weiterhin Daten aus dem Prozessspeicher, beim erneuten Öffnen wird automatisch entsperrt. Das kleine Fenster enthält keine Passworteingabe mehr. Ein Passwortwechsel erneuert den Windows-Schlüssel; bei fehlendem oder beschädigtem Schlüssel zeigt das Hauptfenster einen Zugriffsfehler und erhält den vorhandenen Tresor. Win+F8 funktioniert auch, wenn Riot noch nicht bereit oder im Vordergrund ist. Erst beim Auswählen eines Accounts werden die aktuellen Loginfelder geprüft. Ist Riot noch nicht bereit, bleibt die Auswahl offen; nach dem Öffnen des Riot-Logins denselben Account erneut wählen.

Der separate Ausfüllprozess prüft Riot-Signatur, Dateiidentität, Prozess, Fenster und Eingabefelder. Sichtbare Dialoge, veränderte Felder oder verlorener Fokus führen zum Abbruch. Es gibt kein stilles Ausweichen auf Zwischenablage oder blindes Tippen. Bereits geschriebene Felder werden bei einem Abbruch nicht automatisch geleert.

**Dateikompatibilität ab 1.2:** Format v4 ergänzt verschlüsselte App-Zuordnungen. AES-256-GCM und Argon2id bleiben unverändert. v1–v3 werden nach erfolgreicher Passworteingabe migriert; ältere Apps können v4 nicht öffnen. Vor dem Update eine verschlüsselte Sicherung behalten. Die aktuelle Standardprüfung umfasst 167 Prüfungen; die vollständige Riot-Kompatibilitätsmatrix bleibt offen.


## Neu in 1.1

- **Sicherheit:** Sperre nach 1/5/15 Minuten, Zwischenablage nach 15/30/60 Sekunden; eingeblendete Passwörter nach 10/15/30 Sekunden wieder verdecken. Die Ausnahme bei „Anmeldung merken“ bleibt bestehen. In den Einstellungen lässt sich die gemerkte Anmeldung beenden.
- **Automatische Sicherungen:** Unter Einstellungen → Sicherungen einen Zielordner wählen. Das aktiviert tägliche verschlüsselte Sicherungen nach Änderungen während die App läuft. Aufbewahrung: 7/14/30 automatische Versionen. Ohne Zielordner bleibt die Funktion aus. Sie erstellt keinen Dienst für die Zeit, in der vault geschlossen ist. Fehler verhindern nicht das Speichern im aktiven Tresor.
- **Generator:** Länge und Zeichengruppen auswählen, ähnliche Zeichen vermeiden; jede ausgewählte Gruppe ist enthalten. Erzeugte Passwörter sind in der Vorschau verdeckt und werden erst mit „Übernehmen“ in den Editor übertragen. Der Account wird erst mit „Account speichern“ geändert.
- **Accounts:** Website-Adressen öffnen nur nach Klick im Standardbrowser. Zugelassen sind HTTP(S)-Adressen ohne eingebettete Zugangsdaten. Eigene geschützte Felder sind verschlüsselt und standardmäßig verdeckt. Duplizieren öffnet eine bearbeitbare Kopie, die erst beim Speichern angelegt wird.
- **Papierkorb:** Löschen verschiebt Accounts für 30 Tage in den verschlüsselten Papierkorb. Unter Einstellungen → Sicherungen können sie wiederhergestellt oder endgültig gelöscht werden. Abgelaufene Einträge werden beim nächsten Entsperren oder Speichern bereinigt. Frühere Sicherungen bleiben unverändert und können ältere Inhalte enthalten.
- **Passwortcheck:** Unter Einstellungen → Sicherheit werden leere und mehrfach verwendete Passwörter lokal erkannt. Keine Netzwerkabfrage, keine Ausgabe der Passwörter in der Ergebnisliste, keine vollständige Bewertung der Passwortstärke.
- **Große Tresore:** Nach Name oder letzter Änderung sortieren. Höchstens 100 Accounts pro Seite, Such- und Filterergebnisse werden über den gesamten Tresor berechnet.

**Dateikompatibilität:** Die Formatkennung v3 schützt die neuen Felder und den Papierkorb davor, von älteren Apps unbeabsichtigt überschrieben zu werden. Kryptografie und Argon2id-Parameter bleiben gegenüber v2 unverändert. v1- und v2-Dateien werden nach erfolgreicher Passworteingabe automatisch umgestellt. Eine bisher gemerkte Anmeldung muss dabei erneuert werden. Ältere App-Versionen können v3 nicht öffnen; alte Sicherungen bleiben in der neuen App lesbar.

Die Standardprüfung verwendet keine Registrierung globaler Hotkeys mehr. Die optionalen, betriebssystemabhängigen Tests lassen sich getrennt mit `--self-test-native <Berichtspfad>` ausführen. Der Standardlauf umfasst derzeit 135 Prüfungen. Layout-Prüfungen mit simulierten 150 %/200 % ersetzen keinen manuellen Windows-DPI- oder Bildschirmlesertest. `performance.txt` im jeweiligen Release-Testordner enthält Messungen mit synthetischen Accounts.

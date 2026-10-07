# Bedienung

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

## Riot-Login ausfüllen (seit 1.2)

1. Riot-Login öffnen und in vault einen Account auswählen.
2. Unter „Ausfüllen einrichten“ den tatsächlichen Login-Benutzernamen oder die E-Mail als Quelle wählen und den Login prüfen lassen.
3. „Im Riot Client ausfüllen“ füllt beide Felder. Alternativ im aktiven Riot-Login **Win+F8** drücken und den Account in der kleinen Auswahl anklicken. Unter Einstellungen → Ausfüllen sind Win+F8–F12 wählbar; belegte Kombinationen werden gemeldet.

Die Anmeldung bestätigst du selbst. Bei gesperrtem Tresor ist zunächst Entsperren erforderlich. Ab Version 1.2.3 erneuert vault die gespeicherte Dateiprüfsumme nach einem Riot-Update automatisch, sofern der Installationspfad unverändert ist und die erneute Prüfung von Riot-Signatur, Prozess und Loginfeldern erfolgreich war. Die gewählte Loginquelle bleibt erhalten. Ein anderer Installationspfad erfordert eine neue Zuordnung. Vor dem Schreiben prüft der Ausfüllprozess die aktuelle Datei erneut; andere Apps und Browser sind nicht freigegeben.

Das Hauptfenster und Win+F8 öffnen die Zugangsdaten direkt über das aktuelle Windows-Benutzerkonto. Nach dem Update einmal mit dem bisherigen Master-Passwort entsperren, sofern noch kein Windows-Schlüssel gespeichert wurde. Windows-DPAPI schützt den Sitzungsschlüssel in `vault.nox.quickfill`; das Master-Passwort wird nicht gespeichert. Der Zugriff gilt auch nach Mitternacht, App-Neustart und „Tresor sperren“; die Sperre entfernt weiterhin Daten aus dem Prozessspeicher, beim erneuten Öffnen wird automatisch entsperrt. Das kleine Fenster enthält keine Passworteingabe mehr. Ein Passwortwechsel erneuert den Windows-Schlüssel; bei fehlendem oder beschädigtem Schlüssel zeigt das Hauptfenster einen Zugriffsfehler und erhält den vorhandenen Tresor. Win+F8 funktioniert auch, wenn Riot noch nicht bereit oder im Vordergrund ist. Erst beim Auswählen eines Accounts werden die aktuellen Loginfelder geprüft. Ist Riot noch nicht bereit, bleibt die Auswahl offen; nach dem Öffnen des Riot-Logins denselben Account erneut wählen.

Der separate Ausfüllprozess prüft Riot-Signatur, Dateiidentität, Prozess, Fenster und Eingabefelder. Sichtbare Dialoge, veränderte Felder oder verlorener Fokus führen zum Abbruch. Es gibt kein stilles Ausweichen auf Zwischenablage oder blindes Tippen. Bereits geschriebene Felder werden bei einem Abbruch nicht automatisch geleert.

**Dateikompatibilität ab 1.2:** Format v4 ergänzt verschlüsselte App-Zuordnungen. AES-256-GCM und Argon2id bleiben unverändert. v1–v3 werden nach erfolgreicher Passworteingabe migriert; ältere Apps können v4 nicht öffnen. Vor dem Update eine verschlüsselte Sicherung behalten. Die aktuelle Standardprüfung umfasst 167 Prüfungen; die vollständige Riot-Kompatibilitätsmatrix bleibt offen.

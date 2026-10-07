# Änderungen

Die aktuellen Neuerungen stehen in den Abschnitten von [usage.md](usage.md) und [security.md](security.md); hier die Liste der Version 1.1.

## 1.1

- **Sicherheit:** Sperre nach 1/5/15 Minuten, Zwischenablage nach 15/30/60 Sekunden; eingeblendete Passwörter nach 10/15/30 Sekunden wieder verdecken. Die Ausnahme bei „Anmeldung merken“ bleibt bestehen. In den Einstellungen lässt sich die gemerkte Anmeldung beenden.
- **Automatische Sicherungen:** Unter Einstellungen → Sicherungen einen Zielordner wählen. Das aktiviert tägliche verschlüsselte Sicherungen nach Änderungen während die App läuft. Aufbewahrung: 7/14/30 automatische Versionen. Ohne Zielordner bleibt die Funktion aus. Sie erstellt keinen Dienst für die Zeit, in der vault geschlossen ist. Fehler verhindern nicht das Speichern im aktiven Tresor.
- **Generator:** Länge und Zeichengruppen auswählen, ähnliche Zeichen vermeiden; jede ausgewählte Gruppe ist enthalten. Erzeugte Passwörter sind in der Vorschau verdeckt und werden erst mit „Übernehmen“ in den Editor übertragen. Der Account wird erst mit „Account speichern“ geändert.
- **Accounts:** Website-Adressen öffnen nur nach Klick im Standardbrowser. Zugelassen sind HTTP(S)-Adressen ohne eingebettete Zugangsdaten. Eigene geschützte Felder sind verschlüsselt und standardmäßig verdeckt. Duplizieren öffnet eine bearbeitbare Kopie, die erst beim Speichern angelegt wird.
- **Papierkorb:** Löschen verschiebt Accounts für 30 Tage in den verschlüsselten Papierkorb. Unter Einstellungen → Sicherungen können sie wiederhergestellt oder endgültig gelöscht werden. Abgelaufene Einträge werden beim nächsten Entsperren oder Speichern bereinigt. Frühere Sicherungen bleiben unverändert und können ältere Inhalte enthalten.
- **Passwortcheck:** Unter Einstellungen → Sicherheit werden leere und mehrfach verwendete Passwörter lokal erkannt. Keine Netzwerkabfrage, keine Ausgabe der Passwörter in der Ergebnisliste, keine vollständige Bewertung der Passwortstärke.
- **Große Tresore:** Nach Name oder letzter Änderung sortieren. Höchstens 100 Accounts pro Seite, Such- und Filterergebnisse werden über den gesamten Tresor berechnet.

**Dateikompatibilität:** Die Formatkennung v3 schützt die neuen Felder und den Papierkorb davor, von älteren Apps unbeabsichtigt überschrieben zu werden. Kryptografie und Argon2id-Parameter bleiben gegenüber v2 unverändert. v1- und v2-Dateien werden nach erfolgreicher Passworteingabe automatisch umgestellt. Eine bisher gemerkte Anmeldung muss dabei erneuert werden. Ältere App-Versionen können v3 nicht öffnen; alte Sicherungen bleiben in der neuen App lesbar.

Die Standardprüfung verwendet keine Registrierung globaler Hotkeys mehr. Die optionalen, betriebssystemabhängigen Tests lassen sich getrennt mit `--self-test-native <Berichtspfad>` ausführen. Der Standardlauf umfasst derzeit 135 Prüfungen. Layout-Prüfungen mit simulierten 150 %/200 % ersetzen keinen manuellen Windows-DPI- oder Bildschirmlesertest. `performance.txt` im jeweiligen Release-Testordner enthält Messungen mit synthetischen Accounts.

# Verifikation

Eine Aussage über vault gilt erst, wenn ein Nachweis sie trägt. Ein übersprungener oder nicht gelaufener Test ist kein
bestandener. Prüfberichte sind historisch und werden nicht nachträglich umgeschrieben.

## Was der Selbsttest belegt

| Aussage | Nachweis | Stärke |
| --- | --- | --- |
| Argon2id läuft mit den Formatparametern (64 MiB, t=3, p=4) | Vergleich mit einem Referenzvektor aus libargon2 (argon2-cffi), `CryptoMigrationTests` | unabhängiges Orakel |
| Manipulation an Header, Salt, Nonce, Tag oder Daten wird erkannt | Bitkipper an fünf Stellen, `SelfTest.TamperTests` | Beispiele, AES-GCM als Orakel |
| v1–v3 werden nach korrekter Anmeldung zu v4, Fehler lassen das Original unverändert | unabhängig erzeugte v1-, v2- und v3-Dateien, gesperrte Zieldatei | Beispiele |
| Fehlgeschlagenes Speichern ändert weder Datei noch geöffnete Daten | gesperrte Datei, ungültige Daten, `VaultSession.Commit` | Beispiele |
| Windows-Schlüssel und Tagessitzung sind an Benutzer und Pfad gebunden, Manipulation und Uhr-Rücksprung werden abgelehnt | DPAPI als Orakel, gekippte Bits, Zeitpunkte vor/nach Mitternacht | Beispiele |
| Ausfüllen schreibt nie das Passwort, wenn sich Ziel oder Felder ändern | `GuardedFill` an jedem Prüfpunkt abgebrochen | erschöpfend über die drei Prüfpunkte |
| Zwischenablage wird nur geleert, solange sie noch vaults Inhalt hält | Testadapter mit Sequenznummer | Beispiele |
| Federn starten exakt, schwingen innerhalb der Genauigkeit ein, Retarget setzt die Bahn fort | analytische Lösung gegen sich selbst zu zwei Zeitpunkten | Eigenschaft |
| Jede Ansicht rendert in beiden Themes und bei 150 %/200 % | PNGs neben dem Bericht, angesehen vor jeder Freigabe | Augenschein |

## Was nicht belegt ist

- Kein unabhängiges Sicherheitsaudit.
- Echte Windows-Sitzungssperre, Clipboard-Verlauf, physische globale Tastatureingabe und echte DPI-Wechsel nur manuell.
- Riot-Kompatibilität nur für die in [verification/autofill-bedienpruefung.md](verification/autofill-bedienpruefung.md)
  genannten Clientversionen; die vollständige Matrix aus [plans/autofill.md](plans/autofill.md) ist offen.

## Berichte

| Bericht | Stand |
| --- | --- |
| [verification/autofill-bedienpruefung.md](verification/autofill-bedienpruefung.md) | 11.09.2026, vault 1.2.0 |
| [verification/autostart-2026-09-13.md](verification/autostart-2026-09-13.md) | 13.09.2026, vault 1.2.1 |

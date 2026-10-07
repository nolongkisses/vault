# Bedienprüfung: Riot-Ausfüllen

Stand: 11.09.2026 · vault 1.2.0 · Riot-Login v138.0.1

Die Prüfung verwendete einen isolierten Testtresor und ausschließlich synthetische Zugangsdaten. Keine Anmeldung wurde abgesendet und der produktive Tresor wurde nicht für die Tests entschlüsselt.

| Bedienweg | Ergebnis |
| --- | --- |
| Account → „Im Riot Client ausfüllen“ | Beide Felder übernommen; Passwort verdeckt; Anmeldeknopf aktiv. Fokusübergabe korrigiert und erneut erfolgreich geprüft. |
| „Zuordnung …“ → „Login prüfen und zuordnen“ | Dialog schließt, bestätigte Zuordnung wird gespeichert; anschließendes Ausfüllen im finalen Paket erfolgreich. |
| Strg+Alt+F10 im Riot-Login | Passende Account-Auswahl erscheint und das Suchfeld erhält Fokus. |
| Account anklicken | Beide Felder ausgefüllt; Auswahl schließt und Riot bleibt das Ziel. |
| Suche ohne Treffer / mit Treffer | Ergebnisliste reagiert korrekt. |
| Enter in der Account-Suche | Gefundenen Account ausgefüllt; keine Anmeldung abgesendet. Vor Fokuswechsel werden gehaltene Enter-/Modifikatortasten abgewartet. |
| Esc in der Auswahl | Fenster schließt ohne weiteren Ausfüllauftrag. |
| Riot-Inaktivitätsdialog | Direkte Prüfung verweigert den Zugriff auf verdeckte Loginfelder. |

Der externe UI-Test kann nur Fenster mit Taskleisten-Eintrag gezielt ansprechen. Deshalb macht ausschließlich der isolierte Testmodus sein Auswahlfenster dort sichtbar; der normale Modus bleibt ohne zusätzlichen Taskleisten-Eintrag. Die normale Auswahl wurde ebenfalls sichtbar über dem Riot-Login beobachtet.

167 automatisierte Standardprüfungen bestanden im finalen selbstständigen Paket `release-cfafdc79c15043308d5c827a320d8039`. Die vollständige 100er-Matrix, weitere Clientversionen/Sprachen, physische DPI-Wechsel und Windows-Sitzungssperre während einer laufenden Riot-Eingabe sind damit nicht als geprüft ausgewiesen.

Die lokale App wurde mit dem geprüften Paket auf 1.2.0 aktualisiert; der Dateihash wurde verglichen. Vor dem Austausch wurde die vorhandene Tresordatei unverändert verschlüsselt als `vault.nox.before-v4-20260911-193114.nox` im lokalen NoxVault-Datenordner gesichert. Das erste Entsperren mit dem Masterpasswort migriert den Tresor auf v4. Kein GitHub-Push.

## Nachprüfung vom 07.10.2026 · vault 1.2.6 · Riot v140.0.7

Der Bereitschaftscheck lief vor der Aktivierung des Riot-Fensters. vault aktiviert und restauriert das verifizierte Ziel jetzt vor dem Zugriff auf die Loginfelder. Vorübergehend fehlende oder unsichtbare Felder werden bis zu viermal im Abstand von 50 ms neu gelesen; die Fenster- und Prozessidentität wird jedes Mal geprüft. Sichtbare Dialoge, falsche Prozesse, mehrdeutige Felder und Schreibschutz werden weiterhin sofort abgewiesen.

223 Standardprüfungen bestanden. Das veröffentlichte Paket wurde zusätzlich mit synthetischen Zugangsdaten im echten Riot-Client geprüft: beide Felder in 1.583 ms ausgefüllt, keine Anmeldung abgesendet, Testfelder anschließend wieder geleert. Der gesonderte Testmodus konnte Win+F8 wegen der parallel laufenden produktiven App nicht zusätzlich registrieren; der Ausfülltest lief automatisch und prüfte keinen physischen Hotkey. Bericht: `artifacts/release-riot-1.2.6/riot-live-test.txt`.

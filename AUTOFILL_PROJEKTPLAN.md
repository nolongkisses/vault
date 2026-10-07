# Projektplan: Zugangsdaten mit einem Klick ausfüllen

Stand: 11.09.2026 · Status: Erste Riot-Version implementiert; Bedienprüfung mit synthetischen Daten durchgeführt.

## Umsetzungsstand

- Direkter Zugriff über die native Windows-UI-Automation auf `username` und `password` im Riot Client v138.0.1 geprüft. Signaturprüfung auf Riot, Prozess-/Fensterbindung und erneute Prüfung vor der Passwortübergabe umgesetzt.
- Ausfüllbutton, verschlüsselte Account-Zuordnung, Benutzername/E-Mail-Auswahl und Schnellauswahl mit Strg+Alt+F10 (F9–F12 konfigurierbar) implementiert. Kein automatisches Absenden, kein Clipboard- oder Tastatur-Fallback.
- Bedienprüfung: Ausfüllbutton, globaler Shortcut, Account anklicken, Suche mit/ohne Treffer, Enter-Auswahl und Esc-Schließen. Ein dabei gefundener Fehler bei der Fokusübergabe wurde korrigiert. Sichtbare Riot-Dialoge blockieren den Zugriff auf dahinterliegende Loginfelder.
- 167 Standardprüfungen bestanden. Zusätzlich 34 aufeinanderfolgende synthetische Feldübergaben mit Sonderzeichen und längeren Passwörtern; danach Abbruch bei Fokuswechsel. Keine echte Anmeldung abgesendet.
- Noch offen: vollständige 100er-Testmatrix, weitere Clientversionen/Sprachen, echte DPI-Wechsel und Windows-Hello. Das ist eine erste lokale Version für den geprüften Riot-Client, keine allgemeine Freigabe für beliebige Apps.
- Dateiformat v4 speichert die Zuordnung innerhalb der bestehenden AES-GCM-Verschlüsselung. v1–v3 werden nach erfolgreichem Masterpasswort migriert; die gemerkte Anmeldung muss dann erneuert werden.

Die folgenden Abschnitte enthalten den ursprünglichen Entwurf und die weitergehenden Abnahmekriterien.

## 1. Ziel und Empfehlung

Du öffnest den Riot-Client für VALORANT, wählst deinen gespeicherten Account und füllst Benutzername und Passwort mit einer Aktion aus. vault soll dabei als alltäglicher Passwortmanager bequem genug sein, dass getrenntes Kopieren und Einfügen zur Ausnahme wird.

**Empfehlung: appgebundenes Ausfüllen mit geprüften Ziel-Feldern und einer kleinen Account-Auswahl.** Zuerst ein gezielt getestetes Riot-Profil, danach weitere Programme. Keine pauschale Zusage „funktioniert in jeder App“.

Die erste Aufgabe ist ein Machbarkeitsnachweis am tatsächlich installierten Riot-Client. Im Rahmen dieser Planung wurden weder dessen Oberfläche untersucht noch Zugangsdaten eingefügt. Ob der aktuelle Client geeignete Windows-Automationselemente bereitstellt, ist deshalb ausdrücklich offen.

Der Hauptanwendungsfall ist das **Loginfenster des Riot-Clients**. Ein gestartetes Spiel, dessen Chat oder ein Launcher mit bereits aktiver Anmeldung ist kein Loginziel.

## 2. So soll sich die Bedienung anfühlen

### A. Aus dem bereits geöffneten Tresor – der direkte Ein-Klick-Weg

1. Du öffnest den Riot-Login und wählst in vault deinen Account.
2. Im Detailbereich steht eine klare Aktion **„Im Riot Client ausfüllen“**, ergänzt um den sichtbaren Zielnamen.
3. Ein Klick bringt genau das zugeordnete Loginfenster nach vorne und befüllt die geprüften Felder.
4. Der Fokus bleibt im Riot-Client. vault meldet knapp „Eingabe ausgeführt“ oder benennt den konkreten Fehler.

Der Button ist bei fehlendem Ziel nicht einfach wirkungslos: Er zeigt beispielsweise „Riot-Login öffnen“ als Erklärung. Das eigenständige Starten des Clients ist eine spätere Komfortfunktion.

**Voraussetzungen für einen Klick:** Tresor entsperrt, Account ausgewählt, App einmal zugeordnet, unterstütztes Loginfenster vorhanden. Eine notwendige Tresorentsperrung oder eine Auswahl zwischen mehreren Fenstern darf nicht als „ein Klick“ verkauft werden.

### B. Direkt aus dem Riot-Client – ohne das große vault-Fenster

- Ein separat konfigurierbarer globaler Shortcut öffnet eine kompakte Auswahl der zu Riot passenden Accounts.
- Ein Klick auf „Main Account“ füllt diesen Account aus. Suche und Tastaturauswahl funktionieren ebenso.
- Die Auswahl zeigt Accounttitel, optional Loginname und Favoritenstatus; niemals das Passwort.
- Optional später: „Standardaccount für diese App“ festlegen. Ein eigener Direkt-Ausfüllen-Shortcut kann diesen Account ohne Auswahl nutzen.

Den bestehenden Shortcut zum Öffnen von vault beibehalten. Eine neue Kombination erst nach Prüfung auf Konflikte vergeben; hier noch keine feste Tastenkombination versprechen.

Wenn der Tresor gesperrt ist, erscheint zuerst die Entsperrung. Anschließend wird dasselbe Ziel erneut geprüft. Ist es verschwunden oder hat sich der Loginzustand geändert, wird der Auftrag verworfen und nicht auf ein anderes Fenster umgeleitet.

### C. Einmalige Einrichtung

Im Account-Editor kommt ein Bereich **„Automatisch ausfüllen“** hinzu:

| Einstellung | Vorgeschlagener Standard |
| --- | --- |
| Ziel-App | „Riot Client“ über Einrichtung am geöffneten Loginfenster |
| Login-Feld | Gespeicherter Benutzername; E-Mail nur bei bewusst gewählter Zuordnung |
| Passwort-Feld | Gespeichertes Passwort |
| Standardaccount dieser App | Aus, optional explizit wählen |
| Nach dem Ausfüllen anmelden | Aus; nicht Bestandteil der ersten Version |
| Kompatibilität | „Geprüft“, „Neu prüfen“ oder „Nicht unterstützt“, mit letzter geprüfter Clientversion |

Bei Riot muss der tatsächliche Loginname zugeordnet werden. Ein angezeigter Spielername oder ein Wert wie `Name#Tag` darf nicht allein wegen der Kategorie „Valorant“ automatisch als Loginname angenommen werden. Der Einrichtungsablauf erklärt diese Unterscheidung und lässt die Quelle bestätigen.

Die erste Einrichtung ist bewusst gründlicher; im Alltag sollen diese Fragen nicht erneut auftauchen, solange Profil und Ziel eindeutig bleiben.

## 3. Technische Varianten und Entscheidung

| Ansatz | Vorteil | Grenze | Entscheidung |
| --- | --- | --- | --- |
| Windows UI Automation: Felder gezielt ansprechen | Zielbezogene Bedienung, weniger abhängig von Fensterposition und Tab-Reihenfolge | Nur nutzbar, wenn der Client geeignete Elemente und Bedienmuster bereitstellt | Bevorzugter Weg; zuerst prüfen |
| UI Automation zur Feldwahl, danach Windows-Tastatureingabe | Kann Felder bedienen, die keinen direkten Schreibzugriff anbieten | Abhängig von Fokus, Eingabeunterstützung und Berechtigungen | Nur für ausdrücklich getestete Profile |
| Blind Benutzername → Tab → Passwort tippen | Einfacher Prototyp | Ein Dialog oder geänderte Reihenfolge kann das Passwort ins falsche Feld lenken | Kein Standard und keine unbeaufsichtigte Ausweichlösung |
| Bildschirmkoordinaten oder Bilderkennung | Kann auch ohne Automationselemente klicken | Skalierung, Sprache und Layoutwechsel machen es fragil | Nicht für die erste verlässliche Version |
| Benutzername und Passwort per Zwischenablage | Manche Anwendungen unterstützen Einfügen gut | Zusätzliche Weitergabe an Zwischenablage und deren Beobachter | Bestehende manuelle Kopierfunktion als Rückfall; kein stiller Wechsel |
| Browser-Erweiterung | Kann Web-Logins an echte Herkunft und Formularfelder binden | Hilft nicht automatisch beim nativen Riot-Client; eigenes Projekt | Später für Browser, getrennt planen |

Windows beschreibt UI-Automationseigenschaften und Bedienmuster für Eingabefelder. Die tatsächliche Unterstützung ist vom Anbieter des Controls abhängig; insbesondere darf der Inhalt eines geschützten Passwortfeldes nicht einfach als Kontrollprüfung ausgelesen werden. [Microsoft: Edit-Control und UI Automation](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-support-for-the-edit-control-type)

`SendInput` fügt Ereignisse in den Windows-Eingabestrom ein. Es besitzt keinen Parameter für ein bestimmtes Zielfenster und unterliegt den Integritätsgrenzen von Windows. Bereits gedrückte Modifikatortasten können die Eingabe beeinflussen. Diese Eigenschaften sind der Grund für die eingeschränkte, geprüfte Verwendung. [Microsoft: SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

Auto-Type ist ein etablierter Ansatz, etwa in KeePass. Dessen Existenz belegt das Grundprinzip, aber nicht die Kompatibilität dieses Projekts mit der aktuellen Riot-Version. [KeePass: Auto-Type](https://keepass.info/help/base/autotype.html)

## 4. Der entscheidende erste Schritt: Riot-Kompatibilität prüfen

Die Entwicklung beginnt mit einer Diagnose ohne echte Geheimnisse und ohne Loginversuch:

1. Prozess, ausführbare Datei, Herausgeber-Signatur und zugehöriges Loginfenster erfassen. Die tatsächlichen Prozessnamen werden ermittelt, nicht geraten.
2. Prüfen, ob Benutzername und Passwort über UI Automation eindeutig identifizierbar sind: Rolle, AutomationId, Struktur, Passwortkennzeichnung, Sichtbarkeit und Bearbeitbarkeit.
3. Prüfen, ob direktes Setzen möglich ist und ob der Client Änderungen tatsächlich in sein Formularmodell übernimmt. Ein erfolgreicher API-Aufruf allein reicht nicht.
4. Wenn direktes Schreiben fehlt: Feldfokus und zulässige Tastatureingabe mit synthetischen Testwerten untersuchen. Keine Anmeldung absenden.
5. Deutsch/Englisch, Fenstergrößen, Skalierung, Startzustände und ein erneut geöffnetes Login prüfen.
6. Ergebnis als Profilstufe festhalten.

| Ergebnis | Konsequenz |
| --- | --- |
| Beide Felder eindeutig und direkt beschreibbar | Ein-Klick-Profil auf dieser Grundlage umsetzen |
| Felder eindeutig fokussierbar, Tastatureingabe zuverlässig | Bedingtes Profil mit enger Fokusprüfung und dokumentierter Grenze |
| Passwortfeld oder Loginzustand nicht verlässlich identifizierbar | Kein Ein-Klick-Passwortausfüllen freigeben; manuelle Kopierfunktionen bleiben verfügbar |

Eine fehlende Fähigkeit darf nicht durch immer mehr blinde Tab-Schritte, willkürliche Wartezeiten oder Abschalten von Schutzsoftware verdeckt werden.

## 5. Wie das richtige Ziel feststeht

### Dauerhaft gespeicherte Zuordnung

Ein Profil verbindet den Account mit einer App-Identität und einem bekannten Loginformular. Die Kategorie „Valorant“ oder ein Fenstertitel allein genügt nicht. Installationspfad, Signatur/Herausgeber und passende Formularmerkmale werden gemeinsam geprüft.

Ein Dateihash eignet sich als Versionshinweis, aber nicht als alleinige dauerhafte Identität: Ein reguläres Update ändert ihn. Bei geänderter Formularstruktur wird das Profil auf „Neu prüfen“ gesetzt. Eine digitale Signatur ist ein zusätzliches Identitätsmerkmal, kein Beweis, dass auf dem Bildschirm gerade das richtige Formular steht.

### Ein einzelner Ausfüllauftrag

- Vor dem Öffnen der Account-Auswahl das aktuelle externe Ziel als Momentaufnahme erfassen: Fensterhandle, Prozess-ID und Prozessstartzeit.
- Beim Klick aus vault ein passendes bereits zugeordnetes Fenster suchen. Bei mehreren passenden Fenstern eine Auswahl verlangen; nicht das erste Ergebnis verwenden.
- Nie nachträglich auf „irgendein aktuell aktives Fenster“ ausweichen. Schließen, Prozessneustart oder wiederverwendete Fensterhandles machen den Auftrag ungültig.
- Vor jeder Feldoperation Zielidentität, Formularzustand und gegebenenfalls Fokus erneut prüfen.
- Nicht in einen Browser allein aufgrund eines passenden Seitentitels einfüllen; dafür wäre eine echte Herkunftsprüfung nötig.

Windows begrenzt, welche Prozesse andere Fenster in den Vordergrund holen dürfen. Eine fehlgeschlagene Aktivierung führt deshalb zu „Bitte Riot-Login aktivieren“ statt zu einer Eingabe ins bisherige Vordergrundfenster. [Microsoft: SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)

## 6. Ablauf eines Ausfüllauftrags

`Account wählen → Ziel prüfen → bei Bedarf Tresor entsperren → Ziel erneut prüfen → Loginfelder erkennen → Benutzername setzen → Passwortfeld prüfen → Passwort setzen → Ergebnis melden`

Konkrete Regeln:

- Nur eine ausdrücklich ausgelöste Aktion startet die Eingabe. Das Öffnen von VALORANT allein löst keine Passwortweitergabe aus.
- Die Account-ID wird für den Auftrag festgehalten. Ein nachträglicher Auswahlwechsel darf nicht Benutzername von Account A mit Passwort von Account B kombinieren.
- Geheimnisse erst direkt vor der Feldoperation aus dem entsperrten Tresor übernehmen; keine dauerhafte Klartextkopie im Profil oder in einer Warteschlange.
- Schon vorhandene Feldinhalte gezielt ersetzen; niemals einfach anhängen. Feldleeren und Eingabe müssen auf dasselbe geprüfte Control zielen.
- Bei Tastatureingabe zunächst das Loslassen des auslösenden Shortcuts abwarten. Zeichen wie `+`, `%`, `{` oder `}` sind Literale, keine Makrobefehle. Unicode und Tastaturlayouts explizit testen.
- Nach einem Timeout, Fokuswechsel, Clientneustart, Esc oder Tresorsperre keine weiteren Zeichen senden.
- Keine automatische Wiederholung nach einer teilweise ausgeführten Eingabe. Neu versuchen ist eine bewusste Benutzeraktion.
- Niemals durch globale Tastenkombinationen vermeintlich falsch eingefügte Daten aus einem inzwischen fremden Fenster entfernen.
- Nur dann „Felder ausgefüllt“ anzeigen, wenn der unterstützte Adapter das zuverlässig feststellen kann. Sonst „Eingabe ausgeführt – bitte im Client prüfen“. Kein „Angemeldet“, solange keine verlässliche Bestätigung vorliegt.

**Wichtige technische Grenze:** Bei globaler Tastatureingabe bleibt zwischen Fokusprüfung und Verarbeitung ein Zeitfenster. Bereits zugestellte Ereignisse lassen sich nicht zurückholen. Häufige Prüfungen und kleine Eingabeschritte reduzieren Risiken, liefern aber keine absolute Garantie. Kann diese Restunsicherheit für ein Profil nicht vertretbar begrenzt werden, erhält es keinen automatischen Passwortmodus.

## 7. Was bei typischen Alltagssituationen passiert

| Situation | Verhalten |
| --- | --- |
| Riot-Login offen, Account gewählt, vault entsperrt | Ein Klick füllt die erkannten Felder |
| vault im Infobereich | Shortcut öffnet die kleine passende Account-Auswahl |
| vault gesperrt | Entsperren, Ziel erneut prüfen, dann fortsetzen; keine Passwortspeicherung außerhalb des Tresors |
| Mehrere Riot-Accounts | Ausgewählten Account verwenden; in der Schnellauswahl nur zu Riot zugeordnete Accounts zeigen |
| Mehrere passende Fenster | Zielauswahl statt automatischem Raten |
| Benutzername fehlt | Auftrag vor der Eingabe abbrechen und Feldzuordnung erklären |
| Benutzername bereits im Loginfeld | Ersetzen, nicht anhängen |
| Client lädt oder zeigt einen Fehlerdialog | Kurz auf einen bekannten Bereitschaftszustand warten, dann klarer Timeout |
| Client ist bereits angemeldet | Nichts einfüllen; kein automatisches Abmelden oder Accountwechseln |
| Spiel oder Chat ist im Vordergrund | Keine Ausführung für das Riot-Loginprofil |
| Fokus wechselt während der Eingabe | Weitere Eingaben stoppen; keine automatische Wiederholung |
| Ziel läuft mit höheren Rechten | Verständliche Erklärung; vault nicht automatisch als Administrator starten |
| Zwei Klicks kurz hintereinander | Ein Auftrag; Doppelauslösung wird verworfen |
| MFA, Captcha, Social Login oder QR-Anmeldung | Beim Nutzer belassen; kein Versuch, diese Abläufe als Passwortformular zu behandeln |
| Clientupdate verändert den Login | Profil deaktivieren bzw. neu prüfen, statt blind weitertippen |

## 8. Gestaltung in vault

### Account-Detail

Eine verständliche Textaktion **„Im Riot Client ausfüllen“** im Kopfbereich oder direkt über den Zugangsdaten. Diese zentrale Aktion erhält bewusst mehr Gewicht als Bearbeiten, Duplizieren und Favorit. Der bestehende minimalistische Stil bleibt erhalten.

Darunter höchstens eine kurze Zustandszeile:

- „Riot-Login bereit“
- „Kein passendes Loginfenster geöffnet“
- „Einrichtung erforderlich“
- „Profil nach Clientupdate prüfen“

Keine Prozessnamen, Handles oder UI-Automationbegriffe im normalen Bedienablauf.

### Kompakte Auswahl

Kleines separates vault-Fenster, keine Einblendung in das Spiel. Ziel-App oben, Suche darunter, anschließend passende Accounts mit klarer Klickfläche. Esc schließt die Auswahl. Ein leeres Ergebnis bietet „Account zuordnen“. Der ursprüngliche Zielbezug bleibt bestehen, auch wenn die Auswahl selbst den Fokus übernimmt.

### Einstellungen

Ein neuer Bereich „Ausfüllen“ mit Shortcut, verbundenen Apps und einem manuell ausgelösten Kompatibilitätstest. Technische Diagnosen liegen hinter „Details“ und enthalten keine Feldwerte oder Passwörter.

## 9. Umsetzung im bestehenden Projekt

Die bestehende WPF-App besitzt bereits Traybetrieb, globale Shortcuts, Sperrlogik und Account-IDs. Diese Bausteine lassen sich weiterverwenden. Neue Verantwortlichkeiten werden getrennt ergänzt:

| Komponente | Aufgabe |
| --- | --- |
| `FillProfile` | App-Zuordnung, Login-Feldquelle, erlaubter Adapter, Profilversion |
| `TargetResolver` | Passende Prozess-/Fensterinstanz erfassen und erneut verifizieren |
| `LoginAdapter` | Unterstütztes Formular erkennen und gezielt bedienen |
| `FillCoordinator` | Auftrag, Entsperrung, Abbruch, Zeitlimits und Doppelklickschutz |
| `QuickFillWindow` | Kleine Account-Auswahl mit ursprünglichem Zielbezug |
| `FillDiagnostics` | Fehlercodes, Dauer und Kompatibilität ohne Geheimnisse |

Profile und die Zuordnung zu Accounts werden verschlüsselt im Tresor abgelegt. Dort werden keine Passwortduplikate gespeichert, sondern vorhandene Accountfelder referenziert. Der globale Shortcut kann bei den bisherigen nicht geheimen Einstellungen liegen. Änderungen am Datenschema benötigen Migrationstests und Schutz vor Datenverlust beim Öffnen mit älteren App-Versionen.

UI-Automationaufrufe laufen nicht auf dem WPF-Oberflächenthread. Ein abbrechbarer Task allein beendet keinen bereits blockierenden nativen Aufruf. Deshalb im Machbarkeitsnachweis auch Hänger prüfen; bei Bedarf einen begrenzten Hilfsprozess einsetzen. Kommunikation dann nur lokal und zugriffsbeschränkt, ohne Geheimnisse in Kommandozeilen oder Dateien. Ein Abbruch beendet neue Arbeit, macht bereits erfolgte Eingabe aber nicht rückgängig.

Beim Sperren werden Auswahlfenster geschlossen, ausstehende Aufträge verworfen und temporäre Schlüssel-/Zeichenpuffer soweit möglich geleert. Die schon bestehenden Grenzen verwalteter Zeichenfolgen bleiben bestehen.

## 10. Riot-spezifische Einordnung

Das Vorhaben beschränkt sich auf einen vom Nutzer ausgelösten Loginablauf. Keine Eingriffe in Spielspeicher, kein Code im Spielprozess, keine versteckten Authentifizierungsendpunkte und keine Umgehung von Vanguard. Falls der Client den vorgesehenen Windows-Weg blockiert, wird diese Grenze akzeptiert.

Aus den recherchierten Quellen lässt sich **keine ausdrückliche Riot-Freigabe für diese konkrete Funktion** ableiten. Vor einer produktiven Riot-Freigabe aktuelle Kompatibilität und gegebenenfalls eine Auskunft von Riot prüfen. Riots Informationen zu Vanguard zeigen, dass Drittanbieter-Kompatibilität gesondert behandelt wird; daraus folgt keine pauschale Zusicherung für Auto-Type. [Riot: Vanguard Updates](https://www.riotgames.com/en/DevRel/vanguard)

Riot Sign On ist ein dokumentierter Weg, über den Drittanbieter nach Nutzerzustimmung Zugriff auf Account-/Spieldaten erhalten. Daraus ergibt sich kein dokumentierter Weg, unseren Passwortmanager den Desktop-Launcher anmelden zu lassen. RSO ist deshalb für dieses Ziel keine passende Abkürzung. [Riot Developer Portal: FAQ zu RSO](https://developer.riotgames.com/docs/faqs)

## 11. Arbeitspakete und Freigabepunkte

| Phase | Ergebnis | Abnahme |
| --- | --- | --- |
| A · Machbarkeit | Riot-Login untersucht; direkter Zugriff und Eingabeverhalten mit Testdaten geklärt | Unterstützter Modus oder ehrliche Nichtunterstützung dokumentiert; kein echter Login abgesendet |
| B · Sichere Grundlage | Zielbindung, Abbruchlogik und Adapter gegen eigene Test-Login-App | Kein Versand an falsches Fenster in definierten Störtests; Sperren beendet ausstehende Aufträge |
| C · Erster nutzbarer Ablauf | Button im Account für das geprüfte Riot-Profil | Ein Klick im eingerichteten, entsperrten Zustand; keine manuelle Tab-Reihenfolge im Alltag |
| D · Schnellauswahl | Globaler Shortcut, passende Accounts, Entsperren und Rückkehr zum Ziel | Bedienbar aus dem Client und aus dem Tray; Mehrdeutigkeiten sauber aufgelöst |
| E · Alltagshärtung | DPI, Sprache, Clientstarts, Updates, Berechtigungen, Sonderzeichen | Testmatrix erfüllt; Fehlertexte verständlich; keine stillen Wiederholungen |
| F · Weitere Apps | Wiederverwendbare Profile für ausdrücklich getestete Programme | Jedes Profil erhält eigene Kompatibilitätsnachweise |

„Client starten und anschließend ausfüllen“, automatisches Absenden und Browserintegration werden erst danach separat bewertet. Die erste Version füllt ausschließlich Benutzername und Passwort aus, weil genau das der gewünschte Kernnutzen ist.

## 12. Wann die Funktion als alltagstauglich gilt

- Im bereits geöffneten, unterstützten Login mit entsperrtem Tresor genau eine Ausfüllaktion.
- Zielwert für die Eingabe bei bereitem Client: in der Regel unter zwei Sekunden. Ladezeit und Tresorentsperrung getrennt messen; kein festes Versprechen vor Messung.
- 100 aufeinanderfolgende Testdurchläufe pro freizugebender Konfiguration ohne vertauschte Felder, abgeschnittene Zeichen oder unbeabsichtigtes Absenden.
- Störtests mit fremdem Fenster, gleichnamigem Fenster, Fokuswechsel, umsortierten Controls, Clientneustart, Doppelklick und Sperre. Jeder falsche Empfänger blockiert die Freigabe.
- Deutsche und englische Oberfläche; 100/150/200 % Skalierung; DE/US-Tastaturlayout; längere Passwörter und Sonderzeichen; volle und leere Loginfelder.
- Normale und höhere Rechte des Zielprozesses, veraltetes Profil, mehrere Accounts und mehrere Zielinstanzen.
- Keine Geheimnisse in Logs, Dateinamen, Kommandozeilen oder automatisch erzeugten Screenshots. Diagnosen protokollieren Zustände und Fehlercodes.
- Zuerst eine kontrollierte Test-Login-App. Im Riot-Client zunächst nur synthetische Eingaben ohne Absenden. Einen echten Login testet der Nutzer anschließend bewusst selbst.
- Resultate als konkrete geprüfte Versionen dokumentieren. Eine erfolgreiche Testserie ist keine Garantie gegen beliebige Fokusrennen, Schadsoftware oder zukünftige Clientänderungen.

## 13. Was wir von Apple Passwords übernehmen

Apples dokumentierter Ansatz trennt Passwortverwaltung und systemintegriertes Ausfüllen. Auf dem iPhone erscheint am Login eine passende Account-Auswahl; nach Auswahl und Authentifizierung werden Zugangsdaten ausgefüllt. Die Passwort-App muss dafür nicht manuell geöffnet und durchsucht werden. [Apple: Passwörter automatisch ausfüllen](https://support.apple.com/guide/iphone/automatically-fill-in-strong-passwords-iphf9219d8c9/ios)

Für passende Vorschläge verknüpft Apple Apps mit Websites über Associated Domains und die Datei apple-app-site-association. Die Beziehung wird von App und Website erklärt. Feldtypen und zusätzlich Heuristiken helfen bei der Login-Erkennung; Drittanbieter-Passwortmanager können Credential Provider Extensions anbieten. Das ist eine Integration in Apples Autofill-System. [Apple: AutoFill-Workflow](https://developer.apple.com/documentation/security/about-the-password-autofill-workflow), [Apple: Password AutoFill](https://developer.apple.com/documentation/security/password-autofill)

Unter Windows dokumentiert Apple iCloud Passwords für Websites über Erweiterungen in Chrome, Edge und Firefox. Diese Dokumentation bietet keinen Nachweis für automatisches Ausfüllen im nativen Riot Client. Apples Plattformintegration lässt sich deshalb nicht einfach auf unser WPF-Programm übertragen. [Apple: iCloud Passwords unter Windows](https://support.apple.com/en-gb/guide/icloud-windows/icw76039ec0f/icloud)

### Konsequenzen für vault – unser Entwurf

| Apple-Prinzip | Geplante Umsetzung in vault |
| --- | --- |
| Passender Account direkt beim Login | Shortcut öffnet eine kleine Auswahl mit den zum geprüften Ziel passenden Accounts; die Hauptansicht bleibt im Hintergrund. |
| Eindeutiger Bezug zur App/Website | Explizites Riot-Profil mit geprüfter Prozessidentität und Loginfeldern. Diese lokale Prüfung ersetzt keine von Riot bestätigte Domain-Verknüpfung. |
| Auswahl ohne offengelegtes Passwort | Liste zeigt Accountname und Benutzername, Ziel „Riot Client“ und eine klare Ausfüllaktion; kein Passwort in der Vorschau. |
| Freigabe vor Geheimniszugriff | Gesperrten Tresor zuerst entsperren; Ziel nach der Entsperrung erneut prüfen. Windows Hello später separat mit geschützter Schlüsselverwendung bewerten, nicht als rein optische Freigabe. |
| Ausfüllen ohne manuelles Kopieren | Direkte Feldübergabe bevorzugen; eine Windows-Eingabesimulation bleibt ein eingeschränkter, separat geprüfter Ersatz. |

Der empfohlene Alltag: Riot-Login öffnen → vault-Shortcut → passenden Account anklicken → beide Felder werden ausgefüllt. Bei einem bereits ausgewählten Account bleibt der Button in vault der direkte Ein-Klick-Weg. Eine optionale Standardaccount-Zuordnung kann später den Shortcut verkürzen. Im gesperrten Zustand kommt bewusst die Entsperrung hinzu.

Eine automatisch eingeblendete Schaltfläche direkt im fremden Loginfeld ist vorerst kein Kernziel: Positionierung, Fokus und Änderungen des Clients müssten zusätzlich zuverlässig beherrscht werden. Zunächst verwenden wir ein eigenes, kompaktes Auswahlfenster. Eine Browser-Erweiterung wäre später der passende Weg für Websites mit einer Prüfung der tatsächlichen Origin.

**Priorität nach dem Apple-Vergleich:** kontextbezogene Auswahl und zuverlässige Zielbindung zuerst. Der Machbarkeitsnachweis am Riot-Client bleibt Voraussetzung; aus Apples gutem Bedienkonzept folgt keine bestätigte Riot-Kompatibilität.

## 14. Meine Entscheidung

**Ich würde dieses Vorhaben als nächsten großen Schwerpunkt umsetzen – beginnend mit Phase A.** Es trifft den eigentlichen Alltagsnutzen des Passwortmanagers deutlich stärker als weitere rein optische Einstellungen.

Die beste Lösung ist keine allgemeine Tipp-Makrofunktion. Es ist ein kleiner, klar geführter Ablauf für bekannte Loginfenster: Account wählen, Ziel prüfen, gezielt ausfüllen. Wenn der Riot-Client die nötigen Voraussetzungen erfüllt, kann daraus der gewünschte bequeme Ein-Klick-Weg werden. Wenn nicht, muss das früh sichtbar werden, bevor eine scheinbar funktionierende, aber unzuverlässige Lösung entsteht.

Dieser Arbeitsschritt hat ausschließlich die Planung ergänzt. Keine Codeänderung, kein Clientstart, kein Loginversuch und kein GitHub-Push.

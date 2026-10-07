# vault – Projektplan

Stand: 11.09.2026 · Status: Phasen 1–6 im beschriebenen Kernumfang umgesetzt; optionale spätere Ideen bleiben im Backlog.

## Ziel und Rahmen

vault soll ein übersichtlicher, schneller, lokaler Passwortmanager bleiben. Der nächste Schwerpunkt sind schönere und verständlichere Einstellungen. Danach folgen Funktionen, die im Alltag Zeit sparen und versehentlichen Datenverlust vermeiden.

Dieser Plan basiert auf dem aktuellen Quellcode und der gerenderten Einstellungsansicht. Er enthält eigene Produkt- und Designvorschläge. Nach Freigabe von Phase 1 wurde auch das Durcharbeiten der weiteren Phasen beauftragt. Die Ergebnisse stehen am Ende des Dokuments; später vorgemerkte Ideen sind weiterhin Vorschläge. Die Änderungen werden lokal installiert und vor einer GitHub-Veröffentlichung gesondert vorgestellt.

Gestalterische Grundlage bleiben der dunkle, monochrome Stil, dezente Konturen und kleine Linien-Icons. AES-256-GCM und Argon2id bleiben die kryptografische Grundlage. Für diese Ausbaustufen ist kein Wechsel des Verschlüsselungsverfahrens vorgesehen.

## Aktueller Stand

Bereits vorhanden:

- Lokaler verschlüsselter Tresor; automatische Umstellung alter v1-Tresore auf v2 nach erfolgreicher Passworteingabe.
- Accounts, Kategorien, Favoriten, Suche, Notizen und ein Generator für 24 Zeichen.
- Kleine Kopier- und Augen-Icons mit Hover-Feedback; Löschen rechts im Detailbereich.
- Autostart, globaler Shortcut, Inaktivitätssperre, Windows-Sitzungssperre und optionale Anmeldung bis Mitternacht.
- Passwortwechsel, verschlüsselter Export und Wiederherstellung mit vorheriger Sicherung.
- Geprüfter lokaler Veröffentlichungsablauf und privates GitHub-Repository.
- Zuletzt 70 bestandene Prüfungen, unter anderem zu Kryptografie, Migration und Schreibfehlern. Das ersetzt keine unabhängige Sicherheitsprüfung.

Die bisherigen Arbeiten zu verzögerter Suche, Strg+F, Widerruf gemerkter Sitzungen und sichererem lokalen Update sind abgeschlossen. Die früheren 54 Tests wurden im Zuge der Verschlüsselungsaktualisierung auf 70 erweitert.

## 1. Einstellungen neu gestalten – zuerst

### Was derzeit stört

- Vier große Karten mit eigenen Rahmen erzeugen viel optisches Gewicht und benötigen bereits bei kleiner Fenstergröße einen Scrollbereich.
- Beschreibungen, Eingaben und Aktionen haben keine durchgehend einheitliche Anordnung.
- Autostart wird sofort gespeichert, der Shortcut erst nach „Übernehmen“. Das Verhalten ist funktional, aber nicht ausreichend erkennbar.
- Rückmeldungen landen gesammelt am unteren Ende. Die betroffene Einstellung kann dadurch weit von ihrer Fehlermeldung entfernt sein.
- „Automatische Sperre nach 5 Minuten“ berücksichtigt die aktuelle Ausnahme bei aktivierter gemerkter Anmeldung nicht.
- Backup und Wiederherstellung sind als gleichartige Aktionen nebeneinander angeordnet, obwohl Wiederherstellen bestehende Inhalte ersetzt.

### Empfohlene Gestaltung

Eine breitere Einstellungsansicht innerhalb der App, mit schmaler Bereichsnavigation links und einer ruhigen Inhaltsfläche rechts. Als Ausgangspunkt etwa 720–780 px Breite, immer an das verfügbare Fenster angepasst. Bei wenig Platz wird die Navigation zur kompakten Bereichsauswahl oberhalb des Inhalts. Es gibt nur einen scrollenden Inhaltsbereich; Titel und Schließen bleiben erreichbar.

| Bereich | Inhalt |
| --- | --- |
| Allgemein | Mit Windows starten, Tastenkombination, Verhalten beim Schließen |
| Sicherheit | Inaktivitätssperre, gemerkte Anmeldung, Zwischenablage, Tresorpasswort |
| Sicherungen | Sicherung erstellen, letzte erfolgreiche Sicherung, Zielordner, Wiederherstellung |
| Darstellung | Abstände, Textgröße, reduzierte Bewegung; wird erst mit passenden Funktionen ergänzt |
| Über vault | App-Version, Laufzeitversion, lokale Speicherung, technische Details aufklappbar |

Keine leeren Platzhalterseiten im ersten Umsetzungsschritt. „Darstellung“ erscheint erst, wenn dort nutzbare Optionen vorhanden sind. Eine Einstellungssuche ist bei diesem Umfang noch nicht erforderlich.

**Visuelle Richtung:**

- Eine gemeinsame Oberfläche statt einer Karte um jede einzelne Einstellung; Gruppen durch Überschrift, Abstand und feine Trennlinien gliedern.
- Links Titel und höchstens eine kurze Erklärung, rechts Schalter, Auswahl oder Aktion. Längere Erklärungen bei Bedarf aufklappen.
- Kleine graue Linien-Icons ohne dauerhaften Button-Hintergrund; beim Hover dezent heller. Die Klickfläche bleibt größer als das sichtbare Icon.
- Bestehende 12-px-Aktionsicons als Ausgangspunkt beibehalten; Navigation kann etwas größere Icons erhalten. Größe und Linienstärke über gemeinsame Werte pflegen.
- Gleichmäßiges Abstandsraster, beispielsweise 8/16/24 px; abgestimmte Radien statt unterschiedlich verschachtelter Rundungen.
- Weiß für wichtige Texte, zurückhaltendes Grau für Erklärungen, Rot gezielt für Fehler und destruktive Aktionen. Keine zusätzlichen bunten Akzente.
- Ein sichtbarer Tastaturfokus gehört zum Design und darf nicht ausschließlich durch Hover ersetzt werden.

### Konkretes Beispiel: Sicherheit

| Einstellung | Bedienelement / Rückmeldung |
| --- | --- |
| Automatisch sperren | Auswahl „5 Minuten“; kurze Erklärung des tatsächlichen Verhaltens |
| Anmeldung merken | Aktueller Zustand, gegebenenfalls „Aktiv bis heute, 00:00 Uhr des Folgetags“ und Aktion „Beenden“ |
| Zwischenablage leeren | Auswahl „30 Sekunden“ mit kurzem Hinweis auf neu kopierte Inhalte |
| Passwort wieder verbergen | Auswahl für automatisch verdeckte, zuvor eingeblendete Passwörter |
| Tresorpasswort | Aktion „Ändern …“, getrennt von alltäglichen Schaltern |

Die Tabelle ist ein Konzept. Neue Optionen entstehen erst in Phase 2. In Phase 1 wird die tatsächlich geltende Sitzungsregel korrekt angezeigt; ihre Funktionsweise wird nicht stillschweigend geändert.

### Verhalten beim Speichern

- Einfache Schalter und Auswahlwerte speichern sofort. Kurzes „Gespeichert“ erscheint direkt an der Zeile.
- Bei einem Fehler bleibt der bisherige Wert wirksam; die Fehlermeldung steht an der betroffenen Einstellung.
- Die Shortcut-Erfassung bleibt ein bewusster Vorgang mit „Übernehmen“ und „Verwerfen“; belegte Kombinationen erklären, ohne den bisherigen Shortcut zu verlieren.
- Passwortwechsel und Wiederherstellen bleiben eigene Abläufe. Wiederherstellen erhält eine klare Vorschau auf die Auswirkungen.
- Ein allgemeiner „Fertig“-Button ist nicht nötig, wenn alles gespeichert ist; ein erreichbares Schließen genügt. Unbestätigte Shortcut-Eingaben dürfen nicht irrtümlich als gespeichert erscheinen.

### Abnahme

Alle bisherigen Einstellungen bleiben erreichbar. Bei 980×660 und 1180×780 sowie 100 %, 150 % und 200 % Windows-Skalierung sind Inhalte und Aktionen ohne Abschneiden erreichbar. Tastaturbedienung, Hover, Fokus und deaktivierte Zustände sind geprüft. Fehler erscheinen dort, wo sie verursacht werden. Die Hinweise zu Inaktivität und gemerkter Anmeldung stimmen mit dem Verhalten überein.

## 2. Sinnvolle neue Funktionen

| Priorität | Vorschlag | Nutzen und erste sinnvolle Ausbaustufe | Aufwand |
| --- | --- | --- | --- |
| Hoch | Einstellbare Sperr- und Zwischenablagezeiten | Sperre beispielsweise nach 1/5/15 Minuten; Zwischenablage nach 15/30/60 Sekunden. Bisherige Werte bleiben Standard. | Mittel |
| Hoch | Gemerkte Anmeldung verständlicher machen | Ablauf und Auswirkungen sichtbar machen; direkt beenden können. Separat entscheiden, ob Inaktivität künftig auch bei gemerkter Anmeldung sperrt. | Mittel |
| Hoch | Sichtbare Passwörter automatisch verbergen | Nach kurzer Zeit und beim Wechsel des Accounts wieder verdecken. Sperren verwirft alle sichtbaren Geheimnisse. | Klein |
| Hoch | Automatische verschlüsselte Sicherungen | Nach einer Änderung höchstens einmal täglich eine Sicherung in einen gewählten Ordner schreiben; letzte erfolgreiche Sicherung anzeigen. | Mittel |
| Hoch | Flexibler Passwortgenerator | Länge und Zeichengruppen wählen, ähnliche Zeichen optional vermeiden, Vorschau und bewusste Übernahme. Keine Änderung am Account vor dem Speichern. | Mittel |
| Mittel | Website-Feld pro Account | Adresse speichern und bewusst im Standardbrowser öffnen. Nur erlaubte Web-Adressen; kein automatisches Abrufen von Favicons. | Klein–Mittel |
| Mittel | Papierkorb | Gelöschte Accounts zunächst verschlüsselt aufbewahren, wiederherstellen oder bewusst endgültig entfernen. Vorgeschlagene Frist: 30 Tage. | Mittel–Groß |
| Mittel | Lokaler Passwortcheck | Wiederverwendete und leere Passwörter auffinden; betroffene Accounts öffnen, ohne Passwörter in der Liste anzuzeigen. | Mittel |
| Mittel | Eintragsverwaltung verbessern | Sortierung, „Account duplizieren“ und Bearbeitungsdatum; Duplikate erst nach bewusstem Speichern anlegen. | Klein–Mittel |
| Mittel | Eigene geschützte Felder | Zusätzliche Login-IDs oder Wiederherstellungscodes verschlüsselt und standardmäßig verdeckt speichern. | Mittel–Groß |
| Später | Passwortverlauf | Begrenzte frühere Passwörter pro Account verschlüsselt behalten; bewusst anzeigen und löschen. Erst nach Regeln zur Aufbewahrung. | Groß |
| Später | Import-Assistent | Ausgewähltes Format mit Feldzuordnung und Vorschau; Duplikate erklären, Import atomar abschließen. | Groß |

Aufwand ist relativ zum bestehenden Projekt: Klein = lokaler Eingriff, Mittel = mehrere Komponenten und neue Prüfungen, Groß = Datenmodell oder neuer umfassender Ablauf. Keine verbindlichen Zeitangaben.

### Sicherungen: wichtige Produktentscheidungen

- Standardvorschlag: die letzten sieben automatisch erstellten Sicherungen behalten; manuelle Sicherungen niemals automatisch löschen.
- Nur eindeutig von vault verwaltete Sicherungen im ausgewählten Zielordner rotieren. Keine fremden Dateien anfassen.
- Fehlender Datenträger oder Schreibfehler darf das Speichern im aktiven Tresor nicht verhindern. „Letzte erfolgreiche Sicherung“ erst nach erfolgreichem Abschluss aktualisieren.
- Den Auslöser klar benennen: zunächst bei Änderungen während laufender App, kein Versprechen eines Hintergrunddienstes bei geschlossener App.
- Bei Passwortwechsel erklären, dass frühere Sicherungen ihr bisheriges Passwort behalten. Passwortänderung und Backup-Aufbewahrung gemeinsam testen.
- Wiederherstellen zeigt Datum und Anzahl der Accounts nach erfolgreicher Prüfung; Sicherheitskopie des aktuellen Stands vor dem Ersetzen beibehalten.
- „Sicherung erstellt“ und „Wiederherstellung geprüft“ sind unterschiedliche Zustände; keine unbelegte Erfolgsaussage.

### Passwortgenerator: empfohlene Bedienung

Kleines eingebettetes Panel beim Passwortfeld: Länge, Zeichengruppen, „Neu erzeugen“ und „Übernehmen“. Die aktuellen 24 Zeichen bleiben Standard. Ungültige Kombinationen, etwa keine gewählte Zeichengruppe, lassen sich nicht übernehmen. Falls ausgewählte Gruppen garantiert enthalten sein sollen, muss die Erzeugung dies ohne vorhersehbare Positionen gewährleisten. Eine Schätzung zur Stärke darf nicht als Sicherheitsgarantie erscheinen.

### Papierkorb und Verlauf: erst die Regeln klären

Beides hält alte Geheimnisse länger vor. Deshalb verschlüsselt im Tresor speichern, Aufbewahrung sichtbar machen und eine bewusste endgültige Löschung ermöglichen. Bei geschlossenem Tresor erfolgt die Bereinigung erst beim nächsten Entsperren; das muss zur angezeigten Frist passen. Sicherungen können weiterhin ältere Inhalte enthalten. Neue Datenstrukturen müssen von Migration und Wiederherstellung abgedeckt sein.

## 3. Weitere Designverbesserungen

1. **Account bearbeiten:** klare Reihenfolge aus Name, Kategorie, Zugangsdaten und Notiz; Speicher- und Abbrechen-Aktionen immer gut erreichbar. Ungespeicherte Änderungen beim Wechsel berücksichtigen.
2. **Kopierfeedback direkt am Icon:** kurz ein Häkchen zeigen; bei Fehlern verständliches Feedback am Feld. Keine zusätzlichen dauerhaften Textleisten.
3. **Leere Ansichten:** kurze, konkrete Hinweise und jeweils eine passende Aktion, etwa „Ersten Account hinzufügen“ oder „Suche zurücksetzen“.
4. **Listen und Detailansicht:** einheitliche Textgrößen und Abstände, gut erkennbare Auswahl, längere Accountnamen zugänglich machen.
5. **Darstellungsoptionen:** zuerst „Kompakt / Normal“ und eine gut getestete Textskalierung. Freie Farben oder viele Themes später nur bei echtem Bedarf.
6. **Bewegung:** kurze, dezente Übergänge für Hover und Bereichswechsel; reduzierte Bewegung respektieren. Kein Animationseffekt darf Sperren oder Entsperren verzögern.

## 4. Empfohlene Reihenfolge

| Phase | Umfang | Voraussetzung | Fertig, wenn … |
| --- | --- | --- | --- |
| 1 · Erledigt | Einstellungen gestalten und vorhandene Funktionen neu anordnen | Designrichtung festlegen | Layout und vorhandene Abläufe erfüllen die Abnahme aus Abschnitt 1. |
| 2 · Erledigt | Sperrzeiten, Zwischenablagezeiten, automatische Passwortverdeckung, klarer Sitzungsstatus | Phase 1; Entscheidung zur Inaktivität bei gemerkter Anmeldung | Werte bleiben nach Neustart erhalten; Sperrereignisse, Zeitwechsel und Clipboard-Besitz werden korrekt behandelt. |
| 3 · Erledigt | Automatische Sicherungen und bessere Wiederherstellung | Phase 1; Zielordner und Aufbewahrungsregel | Fehlende Laufwerke, Rotation und Passwortwechsel sind geprüft; synthetischer Tresor lässt sich vollständig wiederherstellen. |
| 4 · Erledigt | Passwortgenerator und Website-Feld | Vorherige Grundfunktionen stabil | Einstellungen des Generators werden eingehalten; vorhandene Accounts bleiben lesbar; URLs öffnen nur durch Benutzeraktion. |
| 5 · Erledigt | Papierkorb, lokale Passwortprüfung, optional eigene Felder | Datenmodell und Aufbewahrung gesondert planen | Migration erhält bestehende Daten; Wiederherstellen und endgültiges Löschen sind eindeutig und getestet. |
| 6 · Erledigt | Große Tresore, Wartbarkeit und Veröffentlichungsqualität | Messbare Engpässe bzw. nächster Release | Messungen mit 100/1.000/10.000 Testaccounts liegen vor; Verbesserungen sind belegt. |

Die Phasen 1–6 wurden anschließend als zusammenhängender Ausbau beauftragt. Passwortverlauf, Import-Assistent und Online-Integrationen bleiben optionale Folgeprojekte.

## 5. Technische Arbeiten passend einplanen

- Beim Umbau die Einstellungen aus der großen MainWindow-Klasse in eine eigene Komponente lösen; keine komplette Architektur-Neuschreibung voraussetzen.
- Gemeinsame Werte für Abstände, Textgrößen, Icons und Zeilen verwenden, damit spätere Seiten zusammenpassen.
- Neue Einstellungen validieren und mit bestehenden Konfigurationsdateien kompatibel laden. Alte Installationen erhalten die bisherigen Standardwerte.
- Längere Sicherungs- und Prüfoperationen dürfen die Oberfläche nicht blockieren. Sperren während einer laufenden Operation muss definiert bleiben.
- Vor weiterer Update-Automatisierung das Beenden der App mit laufenden Schreibvorgängen koordinieren. Bisher kann das lokale Veröffentlichungsskript die App beenden.
- Bei eingeschränkten Dateirechten die bestehende Grenze des Sitzungswiderrufs berücksichtigen; keine Garantie für dauerhaften Widerruf ohne Schreibzugriff behaupten.
- Für große Listen erst messen, dann bei Bedarf virtualisieren. Scrollposition und Auswahl bei Änderungen erhalten.
- Betriebssystemabhängige Hotkeytests von deterministischen Datenprüfungen trennen. Automatisierte Builds später nur mit synthetischen Tresoren ausführen.
- Abhängigkeiten und mitgelieferte Laufzeit bei Releases auf unterstützte Updates und bekannte Schwachstellen prüfen.

## 6. Bewusst nicht im nächsten Ausbau

Cloud-Synchronisierung, Browser-Autofill, Browser-Erweiterung, Teamfreigaben, TOTP, Windows-Hello-Entsperrung und Online-Leak-Abfragen sind eigene Projekte mit zusätzlichem Integrations- und Sicherheitsbedarf. Sie bleiben Ideen für später. Kein Netzwerkdienst oder externer Abruf wird allein für eine schönere Oberfläche eingeführt.

Ebenso sind eine neue Verschlüsselungsart, ein frei wählbarer Algorithmus und eine versprochene Wiederherstellung ohne Master-Passwort nicht Teil dieses Plans.

## 7. Entscheidungen vor den jeweiligen Phasen

| Entscheidung | Vorgeschlagener Startpunkt | Wann nötig |
| --- | --- | --- |
| Einstellungen als breite Ansicht mit Bereichsnavigation? | Ja, im bestehenden App-Overlay | Vor Phase 1 |
| Wie wirkt „Anmeldung merken“ auf Inaktivität? | Sichtbar machen; eine Änderung der heutigen Ausnahme separat bestätigen | Vor Phase 2 |
| Welche Zeitoptionen? | Sperre 1/5/15 Minuten, Clipboard 15/30/60 Sekunden; bisherige Standards behalten | Vor Phase 2 |
| Wohin und wie viele automatische Sicherungen? | Benutzer wählt Ordner; sieben automatische Versionen | Vor Phase 3 |
| Papierkorb gewünscht und welche Frist? | 30 Tage, verschlüsselt, sichtbar endgültig löschbar | Vor Phase 5 |

## 8. Arbeitsweise und Status

- Phasen 1–6 sind im unten dokumentierten Umfang umgesetzt. Später vorgemerkte Ideen sind nicht begonnen.
- Zuerst die jeweilige Phase freigeben; dieser Plan ist keine pauschale Implementierungsfreigabe.
- Prüfungen verwenden synthetische Daten und öffnen keine echten Passwörter.
- Pro Phase relevante Funktionen und Fehlerfälle testen; Designänderungen zusätzlich in kleinen Fenstern und bei höherer Skalierung ansehen.
- Abschließend dokumentieren, was umgesetzt und geprüft wurde und welche Grenzen bleiben.
- GitHub-Veröffentlichungen folgen der vereinbarten Vorschau und Freigabe. Dieser Plan wurde noch nicht gepusht.


## Ergebnis Phase 1

- Einstellungen in eine eigene Komponente ausgelagert: Allgemein, Sicherheit, Sicherungen und Über vault.
- Ruhige gemeinsame Fläche mit Trennlinien, stabiler Höhe beim Bereichswechsel und kompaktem Bereichswähler bei geringer Breite.
- Ein scrollender Inhaltsbereich; Titel und Schließen bleiben außerhalb des Scrollbereichs.
- Autostart- und Shortcut-Rückmeldungen direkt an der Einstellung; ungespeicherte Shortcut-Änderungen lassen sich verwerfen und werden beim gewöhnlichen Schließen abgefragt.
- Erzwungenes Schließen beim Sperren umgeht Rückfragen und blockiert die Sperre nicht.
- Aktuelle Sitzungsregeln sichtbar: gemerkte Anmeldung setzt die Inaktivitätssperre aus; Darstellung aktualisiert sich nach Passwortwechsel.
- Sicherung und Wiederherstellung getrennt beschrieben; Ergebnisse direkt an der Aktion.
- Eigene Fokusdarstellung für Einstellungsbuttons und Schließen; technische Informationen aufklappbar.
- 88 Prüfungen bestanden. Alle vier Seiten gerendert; Layout bei 100 %, simulierter 150-%- und 200-%-Skalierung geprüft. Erreichbarkeit des Inhaltsendes bei 200 % automatisch geprüft.
- Einschränkung: Rendering mit Layout-Skalierung ersetzt keinen vollständigen manuellen Windows-DPI-, Bildschirmleser- oder physischen Tastaturtest.
- Keine neuen Sperrzeiten, Backup-Automatik oder Generatorfunktionen in dieser Phase. Kein Push auf GitHub.


## Ergebnis Phasen 2–6 · Version 1.1

### Phase 2 – Sicherheit im Alltag

Zeitoptionen für Inaktivität, Zwischenablage und automatische Passwortverdeckung sind gespeichert und validiert. Alte Einstellungen erhalten die bisherigen Standardwerte. Gemerkte Anmeldungen lassen sich beenden; ihre Ausnahme von der Inaktivitätssperre wurde bewusst beibehalten. Accountwechsel und Sperren verdecken angezeigte Geheimnisse und stoppen die zugehörigen Timer.

### Phase 3 – Sicherungen

Automatische verschlüsselte Sicherungen nach Änderungen, höchstens einmal pro lokalem Tag während laufender App; Aktivierung durch die bewusste Wahl eines Zielordners. Aufbewahrung von 7/14/30 eigenen automatischen Dateien in einem eigenen Unterordner, keine Rotation manueller Sicherungen. Der letzte erfolgreiche Sicherungszeitpunkt wird angezeigt. Fehlende Ziele und Schreibfehler lassen das aktive Speichern unberührt. Wiederherstellung zeigt nach der Passwortprüfung die Anzahl aktiver Accounts, Papierkorbeinträge und das Dateidatum. Das Dateidatum ist keine kryptografisch bestätigte Erstellungszeit. Die Passwortprüfung läuft im Hintergrund; eine angeforderte Sperre wird berücksichtigt.

### Phase 4 – Generator und Accounts

Generator für 8–128 Zeichen mit frei wählbaren Zeichengruppen und optionalem Ausschluss ähnlicher Zeichen. Alle ausgewählten Gruppen sind garantiert enthalten; Erzeugung mit kryptografischer Zufallsquelle. Verdeckte Vorschau und ausdrückliches Übernehmen. Website-Feld mit Prüfung auf HTTP(S); kein automatischer Abruf. Account-Editor mit erreichbaren Speicheraktionen und Rückfrage bei ungespeicherten Änderungen. Duplizieren und Sortierung ergänzt, Kopierfeedback direkt am Icon.

### Phase 5 – Papierkorb und geschützte Felder

Papierkorb mit 30 Tagen Aufbewahrung, Wiederherstellen und bewusstem endgültigem Löschen. Bereinigung beim Entsperren und Speichern, keine Bereinigung durch einen Dienst bei geschlossener App. Lokaler Check auf leere und doppelte Passwörter; Geheimnisse werden nicht als Suchergebnis ausgegeben. Bis zu 30 eigene geschützte Felder pro Account.

Die neue Formatkennung v3 verwendet weiterhin AES-256-GCM und dieselben Argon2id-Parameter. Sie verhindert, dass ältere Apps beim Speichern neue Datenfelder verwerfen. v1/v2 werden nach erfolgreicher Passworteingabe migriert; bestehende Sicherungen bleiben lesbar. Eine Umstellung ist erst beim nächsten tatsächlichen Entsperren des Nutzertresors erfolgt, nicht bereits durch Installation der App.

### Phase 6 – Umfang, Messung und Veröffentlichung

Suchlogik, Generator, Editor, Tresorwerkzeuge und automatische Sicherungen sind eigene Komponenten. Die Accountliste stellt höchstens 100 Einträge pro Seite dar; Papierkorb und Prüfberichte jeweils höchstens 50. Diese begrenzte Darstellung ersetzt für diesen Ausbau eine vollständige Virtualisierung. Alle Accounts bleiben über Suche und Seitenwechsel erreichbar.

Messung mit synthetischen Daten auf diesem Gerät, ohne Bildschirmrenderzeit: 10.000 Accounts filtern rund 5 ms; Fensteraufbau einschließlich Wechsel zur letzten Seite rund 33 ms. Der erste 100-Account-Lauf enthält Initialisierungskosten und ist nicht direkt mit aufgewärmten Läufen vergleichbar. Konkrete Messwerte liegen in `performance.txt`; keine allgemeine Leistungsgarantie.

Standardprüfungen und native Hotkeytests getrennt. Künftige lokale Updates ab Version 1.1 verwenden eine Beendigungsanfrage und brechen bei offenen Dialogen oder laufenden Vorgängen ab. Das einmalige Update älterer Versionen verwendet deren bisherigen Beendigungsablauf.

### Prüfung und Grenzen

- 135 Prüfungen bestanden, darunter unabhängiger Argon2id-Vektor, v1/v2-Migration, verschlüsselte Feld- und Papierkorb-Rundläufe, fehlgeschlagene Bereinigung bei Dateisperre, Backup-Rotation, Wiederherstellung, Generatorgruppen und UI-Verdeckung.
- Hauptansicht, Einstellungen, Account-Editor, Generator, Papierkorb und Passwortcheck mit synthetischen Daten gerendert. Einstellungen zusätzlich mit simulierter 150-%-/200-%-Skalierung.
- Paketprüfung meldet gemäß den abgefragten NuGet-Quellen keine bekannten anfälligen Abhängigkeiten (11.09.2026).
- Automatische Sicherungen sind ohne gewählten Ordner aus. Es wurden keine produktiven Passwörter für Tests geöffnet.
- Native Hotkeytests, echte Monitor-DPI-Wechsel, Bildschirmleser und Wechseldatenträger benötigen ergänzende manuelle Prüfungen.
- Passwortverlauf, Import-Assistent, freie Themes, Cloud, Autofill und Online-Prüfungen bleiben spätere Ideen. Keine Implementierung dieser optionalen Projekte behauptet.
- Änderungen bleiben lokal; kein weiterer GitHub-Push ohne Vorschau und Okay.

## Nächster Schwerpunkt: Zugangsdaten ausfüllen

Der neue [Projektplan für Ein-Klick-Ausfüllen](AUTOFILL_PROJEKTPLAN.md) beschreibt den gewünschten Riot-/VALORANT-Ablauf, App-Zuordnung, Schnellauswahl und technische Freigabekriterien. Status: erste Riot-Version in vault 1.2.0 implementiert und lokal installiert. Ausfüllbutton, Schnellauswahl und Zuordnung mit synthetischen Daten geprüft; Details und verbleibende Testgrenzen stehen in [AUTOFILL_BEDIENPRUEFUNG.md](AUTOFILL_BEDIENPRUEFUNG.md). Keine Anmeldung abgesendet.

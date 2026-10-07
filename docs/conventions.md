# Konventionen

Verbindlich für jede Änderung an vault. Abgeleitet aus clean-project; Abweichungen stehen als ADR in
[decisions/](decisions/). Was eine Maschine prüfen kann, prüft `dotnet run --project tools/Check`.

## Sprache

Bedienoberfläche und Doku auf Deutsch, Bezeichner, Code-Kommentare und Commit-Nachrichten auf Englisch
([ADR 0002](decisions/0002-sprache.md)).

## Struktur (geprüft)

| Regel | Wert |
| --- | --- |
| Zeilen pro Datei, auch Tests und Doku | 500 |
| Zeichen pro Zeile in `.cs` und `.xaml` | 140 |
| Code-Dateien pro Verzeichnis (ohne `App.xaml`, `App.xaml.cs`, `EntryPoint.cs`) | 8 |
| Markdown-Dateien pro Doku-Verzeichnis | 8 |
| Verzeichnistiefe unter `NoxVault/` | 4 |
| Zeilen pro Funktion, Konstruktor oder lokaler Funktion | 60 |
| Parameter pro Funktion, Delegat oder Primärkonstruktor | 5 |

- Keine Sammelnamen für Dateien oder Ordner: `utils`, `util`, `helpers`, `helper`, `misc`, `common`, `stuff`, `shared`.
- Ein Konzept pro Datei, ein neues Konzept bekommt sofort eine eigene Datei. Ordner nach Konzept, siehe
  [architecture/overview.md](architecture/overview.md).
- Alle Typen sind `internal`. Die App hat keine öffentliche API.

## Code (geprüft)

- Kein `!` (null-forgiving) außerhalb von `Tests/`. Stattdessen `?? throw` mit einer Meldung, die den Ort nennt.
- `catch` ohne Typ oder mit `Exception` nur mit `throw` im Block oder einem Kommentar, der sagt, warum der Fehler hier
  endet.
- Farben (`#RRGGBB`) nur in `Interface/Theme/`. Schriftgrößen nur aus der Skala 11/12/13/15/17/26, Abstände aus
  0/2/4/6/8/12/16/24/32/40, Radien aus 0/2/4/6/10/12/16. Im Code über `Theme.S1`…`Theme.S9`, `Theme.Size(TextRole)` und
  `Theme.Radius`/`ControlRadius`/`PopoverRadius`/`DialogRadius`.
- Build mit Warnungen als Fehler, Formatierung nach `.editorconfig`.

## Code (Review)

- Logik gehört nicht in Views. Tresor-Zustand und Sitzungen liegen in `Accounts/VaultSession`, Shortcuts in
  `Platform/Hotkeys`, Abfragen in `Accounts/AccountQueries`. `MainWindow` verdrahtet nur.
- Tresorformat, Kryptografie und die Sicherheitsprüfungen beim Ausfüllen ändern sich nur mit eigenem Verifikationsplan
  unter [verification/](verification/) und neuen Selbsttests.
- Fehler nie still schlucken; Nutzerdaten werden nur nach erfolgreichem verschlüsseltem Schreiben ersetzt.
- Kommentare erklären warum, nicht was. Einzeilig, direkt über der Stelle.

## Oberfläche

- Eine Designsprache: [ADR 0003](decisions/0003-design-rahmen-und-buehne.md). Komponenten verwenden nur Tokens
  (`Theme.Bg`, `Theme.Page`, `Theme.Raise1`…), Farben immer als Ressourcenreferenz, damit der Theme-Wechsel greift.
- Tiefe nur über Helligkeit, keine Rahmen, Schatten oder Trennlinien. Genau eine invertierte Primäraktion pro Kontext.
- Bewegung nur über `Motion.Glide` (wandert), `Motion.Settle` (erscheint) und `Motion.Fade` bzw. die Hover-Storyboards
  (140 ms). Reduzierte Bewegung (Windows-Animationen aus) springt ans Ziel.
- Vor „fertig“ beide Themes in den Selbsttest-Renders ansehen.

## Arbeitsweise

1. Vorher [README.md](README.md), diese Datei und den neuesten Eintrag in [handoffs/](handoffs/) lesen.
2. Planen: was sich ändert, wo, wie es geprüft wird. Größeres als Plan unter [plans/](plans/) oder als ADR.
3. In kleinen Schritten ändern, die den Build grün lassen.
4. `dotnet run --project tools/Check` auf Windows, der Selbsttest muss vollständig bestehen.
5. Doku im selben Schritt anpassen. Am Ende einer Sitzung einen datierten Handoff schreiben.

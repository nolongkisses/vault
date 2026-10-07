# Handoff 2026-10-07: neues Design und Umbau nach clean-project

## Stand

- Neues Design „Rahmen + Bühne“ in Dunkel und Hell, Umschalter unter Einstellungen → Allgemein → Darstellung
  ([ADR 0003](../decisions/0003-design-rahmen-und-buehne.md)).
- Code nach Konzepten geordnet, `VaultSession` und `Hotkeys` aus `MainWindow` herausgelöst
  ([ADR 0001](../decisions/0001-struktur-und-pruefung.md)), Doku unter `docs/`.
- `tools/Check`: 16/16 eigene Tests, Format, Build ohne Warnungen, Struktur 98 Dateien ohne Verstoß (unter Linux, mit
  `--skip-self-test`).
- Selbsttest in einer Windows-11-VM (25H2): 230 von 230 bestanden. Alle 223 Prüfungen von 1.2.6 sind unter gleichem Namen
  enthalten, neu sind Theme-Rückfall, Federkurven (je zwei für glide und settle) und „beide Themes rendern“.
- Tresorformat, Kryptografie, `RememberedLogin` und die Ausfüll-Prüfungen sind inhaltlich unverändert, nur verschoben und
  umgebrochen.

## Entscheidungen

- Projekt gehört nolongkisses; der Umbau ist ein Vorschlag. ADRs deshalb `proposed`. Nichts committet.
- Keine Großbuchstaben-Labels (WPF kann nicht sperren), destruktiv fett statt rot, Fenster-Buttons mit Segoe Fluent Icons.
- Zeilenlänge 140 zusätzlich zu clean-project, damit das 500-Zeilen-Limit etwas bedeutet.

## Auffälligkeiten, nicht geändert

- `CheckIdle` sperrt nur in Vorschau- und Testfenstern (`preview`); in der installierten App gibt es seit 1.2.5 keine
  Inaktivitätssperre. Die zugehörigen Tests prüfen damit nur den Testpfad. Entfernen oder wieder einschalten ist eine
  Produktentscheidung.
- Auswahl in der Liste: 0,64–0,87 ms statt 0,37–0,50 ms pro Klick (`performance.txt`, je ein Lauf in der VM),
  Speicher gleich. Ursache sind die zusätzlichen Hover-Ebenen und Ressourcenreferenzen.

## Kleine Korrekturen

- „Technische Details“ nannte Tresorformat v3, richtig ist v4.
- Ausfüllen-Hilfe verwies auf „Ausfüllen einrichten“; den Knopf gab es nicht mehr (`CreateFillAction` war unbenutzt und
  ist entfernt). Der Text nennt jetzt „Riot verbinden“ aus dem Mehr-Menü.
- Die Warnung „Schnellzugriff konnte nicht gespeichert werden“ ging beim Neuaufbau der Ansicht verloren; sie erscheint
  jetzt in der Statuszeile.

## Offen

- `tools/Check` wurde unter Windows nicht als Ganzes ausgeführt (die Test-VM hat kein SDK); der Selbsttest lief über die
  veröffentlichte EXE.
- Nicht gesehen: echte Hover- und Federbewegungen auf einem Bildschirm (die Renders sind Standbilder), Windows 10,
  Bildschirmleser, echter Riot-Login.

## Nächster Schritt

Auf einem Windows-Rechner `dotnet run --project tools/Check` und die App einmal von Hand durchklicken: Theme-Wechsel,
Sidebar einklappen, Kategorie ziehen, Editor, Einstellungen, Win+F8. Danach entscheiden, ob der Stand an nolongkisses
geht.

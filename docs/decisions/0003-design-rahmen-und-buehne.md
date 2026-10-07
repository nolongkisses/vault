# 0003: Designsprache „Rahmen + Bühne“

**Status:** proposed
**Date:** 2026-10-07

## Context
Bis 1.2.6 war die Oberfläche dunkel mit blauem Akzent (#3B82F6), Konturen um Karten und Eingaben, Trennlinien zwischen
Feldern, 76 verschiedenen Farbwerten als Literale in 16 Dateien, Unicode-Zeichen als Icons (▦ ☆ ◇ ↗ ⌄ ✓ ★) und ohne helles
Theme. Gewünscht ist der Look der Apps Calendary, Filyy und Leech: die Designsprache aus clean-project mit den Werten aus
mika-design.

## Options
1. „Standard“ (Calendary, Filyy): Sidebar direkt auf dem Hintergrund, Inhalt auf raise1-Karten.
2. „Rahmen + Bühne“ (Leech, Rewa): der Rahmen füllt Fenster und Sidebar, die Seite liegt als abgerundete Bühne darin.

## Decision
Option 2, weil vault eine eigene Fenster-Chrome zeichnet und mika-design diese Variante für Windows-Apps vorsieht.

- Farben aus Leechs `tokens.css`, beide Themes: `Bg` (Rahmen), `Page` (Bühne), `Raise1`–`Raise3`, `Fg`/`Sub`/`Faint`,
  `ChipOn`/`ChipOnFg` als einzige invertierte Rolle, `Scrim`. R = G = B. Umschalter System/Dunkel/Hell, gespeichert in
  `settings.json`, „System“ folgt Windows live.
- Skalen: Schrift 11/12/13/15/17/26, Abstand 2-px-Raster, Radien 6 (Control) / 10 (Fläche) / 12 (Popover) / 16 (Dialog),
  Control-Höhe 28 (klein 24), Sidebar 224, eingeklappt 64, Bühne 8 px eingerückt.
- Schrift Segoe UI Variable (Text/Display), Rückfall Segoe UI. Fenster-Buttons mit Windows' eigenen Glyphen (Segoe Fluent
  Icons), alle anderen Icons aus Material Design Icons (Apache-2.0) als Pfade in `Icons.cs`.
- Bewegung: Federn glide (0,34 s / 0,82) und settle (0,30 s / 0,86) analytisch, Retarget mit Geschwindigkeit; quick
  140 ms für Hover.
- Kategoriefarben sind Inhalt und erscheinen nur als kleiner Punkt; die Chrome bleibt grau.

Abweichungen von mika-design:
- Keine Großbuchstaben-Labels mit Sperrung: WPF kann Text nicht sperren, ohne Sperrung wirken sie gedrängt. Wie Rewa.
- Destruktive Aktionen fett statt rot (Leech-Variante), auch „Schließen“ in der Titelleiste bleibt beim Hover grau.

## Consequences
- Jede Farbe, Größe und Dauer kommt aus `Interface/Theme`; `tools/Check` lässt Farbliterale und Werte außerhalb der
  Skalen außerhalb dieses Ordners scheitern.
- Der Selbsttest rendert jede Ansicht in beiden Themes.
- Das App-Icon (`Assets/vault.ico`) bleibt unverändert; es ist die Marke von vault, nicht Teil der Designsprache.

# 0001: Struktur nach Konzepten und ein Prüfkommando

**Status:** proposed
**Date:** 2026-10-07

## Context
Bis 1.2.6 lagen 33 Code-Dateien flach in `NoxVault/`. `MainWindow` war eine partielle Klasse über neun Dateien, die
Tresorzustand, Sitzungen, Shortcuts und Ansichten zugleich hielt. Viele Zeilen enthielten mehrere Anweisungen und bis
zu 370 Zeichen, sodass Zeilenlimits nichts über die tatsächliche Größe aussagten. Es gab keinen Format- oder
Strukturcheck; der Selbsttest lief nur über `publish.ps1`.

## Options
1. Nur Verstöße im jeweils berührten Code beheben.
2. Nach Konzepten ordnen, Logik aus `MainWindow` herauslösen und Limits per Werkzeug erzwingen.
3. Umbau auf MVVM mit XAML-Views und Bindings.

## Decision
Option 2. Ordner `Storage`, `Accounts`, `Platform`, `Autofill`, `Interface/*`, `Tests`. `VaultSession` und `Hotkeys`
übernehmen Zustand und Regeln aus `MainWindow`. `tools/Check` (eigenes kleines Konsolenprojekt mit Roslyn) prüft
Format, Build, Struktur- und Code-Regeln und startet den Selbsttest. Zusätzlich zu clean-project gilt eine maximale
Zeilenlänge von 140 Zeichen, weil das 500-Zeilen-Limit sonst durch dichte Zeilen umgangen wird. Ein Namespace
(`NoxVault`) für alles; Ordner gliedern, Namespaces würden nur Usings erzeugen.

Option 3 hätte jede Ansicht neu geschrieben und die Selbsttests, die auf Steuerelemente zugreifen, entwertet, ohne dass
eine Anforderung das verlangt.

## Consequences
- Tresorformat, Kryptografie und Ausfüll-Prüfungen sind inhaltlich unverändert, nur verschoben; alle 223 bisherigen
  Selbsttests laufen weiter unter demselben Namen.
- Der Selbsttest braucht weiterhin Windows. Unter Linux prüft `tools/Check -- --skip-self-test` alles andere.
- Neue Konzepte brauchen eine eigene Datei; ein Ordner mit acht Code-Dateien muss vorher aufgeteilt werden.

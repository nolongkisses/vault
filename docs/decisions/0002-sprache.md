# 0002: Deutsch in Oberfläche und Doku, Englisch im Code

**Status:** proposed
**Date:** 2026-10-07

## Context
clean-project schreibt Englisch für Code, Kommentare und Doku vor, sofern ein Projekt nichts anderes entscheidet. vault
richtet sich an deutschsprachige Nutzer; Oberfläche, README und Pläne waren von Anfang an deutsch, Bezeichner und
Code-Kommentare englisch.

## Options
1. Alles auf Englisch umstellen.
2. Bestehende Aufteilung festschreiben.

## Decision
Option 2: Oberflächentexte, Fehlermeldungen und Doku auf Deutsch; Bezeichner, Code-Kommentare, Testnamen und
Commit-Nachrichten auf Englisch.

## Consequences
Keine Übersetzungsschicht nötig. Eine spätere englische Oberfläche bräuchte ein eigenes ADR und eine Textquelle statt
Literalen im Code.

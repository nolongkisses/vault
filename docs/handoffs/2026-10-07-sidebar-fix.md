# Handoff 2026-10-07: doppelte Sidebar-Icons

## Ursache und Fix

Beim Ein- und Ausklappen überschreibt `BuildSidebar` das Feld `sidebar`. `ToggleSidebar` entfernte dadurch die
neu erzeugte Seitenleiste statt der alten. Alte Navigationen blieben im Fenster und überlagerten Icons und Texte.
Der Austausch hält jetzt die bisherige Seitenleiste fest und entfernt genau diese nach dem Neuaufbau.

## Prüfung

- Der neue Regressionstest schlug vor dem Fix beim ersten Einklappen fehl.
- Nach dem Fix: `tools/Check` unter Windows vollständig bestanden, 16 Prüfwerkzeugtests, Build ohne Warnungen,
  Format und Struktur ohne Befund, 254 App-Selbsttests bestanden.
- Je sechs aufeinanderfolgende Wechsel im dunklen und hellen Theme prüfen die Entfernung der alten Navigation
  und die korrekte Spaltenbreite. Gerenderte eingeklappte und ausgeklappte Ansichten visuell geprüft.
- Ausschließlich synthetische Testdaten; Testberichte und Bilder bleiben außerhalb von Git.

## Stand

Version 1.2.8 enthält den Sidebar-Fix und richtet den Schriftzug im Fenstertitel optisch zur Mitte des Icons aus.
Die Textzeile berücksichtigt dabei die untere Schriftreserve mit einem vorhandenen Abstandstoken.
Speicherformat und Ausfülllogik sind unverändert.

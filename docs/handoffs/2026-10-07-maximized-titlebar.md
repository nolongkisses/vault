# Handoff 2026-10-07: Titelleiste beim Maximieren

## Ursache und Fix

Bei eigener WPF-Fensterkontur lag der maximierte Fensterursprung außerhalb des nutzbaren Monitorbereichs.
Eine oben angeordnete Taskleiste verdeckte dadurch die Titelleiste samt Fensterbuttons. `WindowBounds` setzt
bei `WM_GETMINMAXINFO` Position und Größe auf den Arbeitsbereich des aktuellen Monitors. Die Mindestgröße
bleibt unter Berücksichtigung der Fenster-DPI erhalten. Die vorhandene Titelleiste und DWM-Kontur bleiben bestehen.

## Prüfung

- Vor dem Fix reproduziert: die native Position des Minimieren-Buttons lag beim Maximieren außerhalb des Arbeitsbereichs.
- Nach dem Fix: `tools/Check` unter Windows vollständig bestanden, Build ohne Warnungen, Format und Struktur ohne Befund,
  16 Prüfwerkzeugtests und 270 App-Selbsttests bestanden.
- Zusätzliche native Fensterprüfungen in Hell und Dunkel: Maximieren, Position aller drei Fensterbuttons innerhalb
  des Arbeitsbereichs, Wiederherstellen, Minimieren, erneutes Wiederherstellen und Schließen.
- Tests laufen auf einem separaten Windows-Desktop, der niemals eingeblendet oder aktiviert wird. Ausschließlich
  synthetische Fenster und Daten; kein Zugriff auf den produktiven Tresor.

## Stand

Version 1.2.9 enthält die Korrektur. Testhelfer, Berichte und Build-Ausgaben bleiben außerhalb von Git.
Der lokale Neustart erfolgt im Tray, damit laufende Arbeit nicht durch ein App-Fenster unterbrochen wird.

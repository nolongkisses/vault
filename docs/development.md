# Entwicklung und Prüfungen

## Ein Kommando für alle Prüfungen

```powershell
dotnet run --project tools/Check
```

Läuft der Reihe nach und bricht beim ersten Fehler ab: Tests des Prüfwerkzeugs, `dotnet format --verify-no-changes`,
Build mit Warnungen als Fehler, Struktur- und Code-Regeln aus [conventions.md](conventions.md) und der Selbsttest der
App. Der Selbsttest braucht Windows (WPF, DPAPI). Unter Linux baut und prüft `dotnet run --project tools/Check --
--skip-self-test` alles außer dem Selbsttest und sagt das ausdrücklich dazu; ein übersprungener Selbsttest ist kein
bestandener.

Voraussetzung: .NET 10 SDK. Die Projektdatei setzt `EnableWindowsTargeting`, damit Build und Format auch außerhalb
von Windows laufen; die App selbst startet nur unter Windows.

## Veröffentlichen

Geprüftes lokales Update: `./publish.ps1`. Das Skript baut in ein separates Verzeichnis, testet die fertige Anwendung mit synthetischen Daten und ersetzt die installierte Datei erst nach bestandenen Tests. Die vorherige Version bleibt als `dist/NoxVault/NoxVault.previous.exe` erhalten. `./publish.ps1 -CheckOnly` baut und testet ohne Installation. Vor dem Update laufende Eingaben abschließen; die installierte App wird für den Austausch beendet. Ab App-Version 1.1 beendet sie sich dafür nur ohne offene Dialoge oder laufende Vorgänge; andernfalls bricht das Update ab. Beim einmaligen Wechsel von einer älteren Version gilt noch deren bisheriger Beendigungsablauf.

`publish.ps1` verwendet das lokale SDK 10.0.401 unter `artifacts/dotnet-10.0.401`, falls vorhanden, sonst das installierte `dotnet`.

```powershell
dotnet build NoxVault/NoxVault.csproj -c Release
dotnet publish NoxVault/NoxVault.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist/NoxVault
```

## Selbsttest

Tests mit rein synthetischen Daten; kein Zugriff auf den produktiven Tresor und keine Änderung der realen Zwischenablage:

```powershell
New-Item -ItemType Directory artifacts -Force
$report = Join-Path (Get-Location) 'artifacts\test-results.txt'
Start-Process '.\NoxVault\bin\Release\net10.0-windows\NoxVault.exe' -ArgumentList '--self-test', ('"' + $report + '"') -Wait
Get-Content $report
```

Die Tests prüfen Verschlüsselung, falsche Passwörter, manipulierte Dateien, atomare Validierung, Änderungen und Löschungen, Backup/Restore, Zwischenablage-Besitzprüfung mit einem Testadapter, Passwortgenerator, die Federkurven der Animationen sowie Speichern/Sperren/Entsperren und Suche in der WPF-Oberfläche. Zusätzlich rendert der Lauf jede Ansicht mit synthetischen Daten als PNG neben den Bericht, im dunklen und im hellen Theme (`*-light.png`), bei 1180×780, 980×660 und 1920×1080, die Einstellungen zusätzlich mit 150 % und 200 % Skalierung. Automatische Sperre per Windows-Sitzungsereignis, echter Clipboard-Verlauf und physische globale Tastatureingabe benötigen ergänzend einen manuellen Test.

Weitere Prüfmodi: `--self-test-native <Bericht>` registriert zusätzlich echte globale Shortcuts, `--hotkey-check <Bericht>` und `--frame-check` sind manuelle Prüfungen mit sichtbarem Fenster, `--autofill-ui-test <Bericht> [--auto]` prüft das Ausfüllen gegen einen echten Riot-Login mit einem synthetischen Testaccount.

## Verhalten, das man beim Ändern kennen sollte

Strg+F fokussiert und markiert die Accountsuche. Schnelle Sucheingaben werden über 150 ms gebündelt. Beim Sperren wird eine geplante Suche verworfen. Kann eine gemerkte Sitzung wegen einer Dateisperre nicht gelöscht werden, verhindert eine separate Widerrufsdatei ihre Wiederverwendung. Bei umfassend fehlenden Schreibrechten wird die Oberfläche dennoch gesperrt und ein Hinweis angezeigt; die Wiederanmeldung ist in der laufenden App blockiert.

Die Einstellungen sind in Allgemein, Sicherheit, Sicherungen, Ausfüllen und Über vault gegliedert. Bei wenig Platz erscheint eine kompakte Bereichsauswahl. Änderungen am Shortcut zum Öffnen von vault werden erst mit „Übernehmen“ gespeichert und können vorher verworfen werden.

# Check

Ein Kommando für alle Pflichtprüfungen von vault: eigene Tests, `dotnet format --verify-no-changes`, Build mit
Warnungen als Fehler, Struktur- und Code-Regeln aus [../../docs/conventions.md](../../docs/conventions.md), Selbsttest.

Nicht hier: Tests der App selbst (die liegen in `NoxVault/Tests` und laufen als `--self-test`).

```text
dotnet run --project tools/Check [-- --skip-self-test]
```

Die Regeln sind mit `CheckerTests` abgesichert: jede Regel schlägt bei einem bekannten Verstoß an und schweigt bei
sauberem Code.

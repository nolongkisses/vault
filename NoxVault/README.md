# NoxVault

Die Windows-App vault: Tresordatei, Entsperren über Windows, Oberfläche, Ausfüllen im Riot-Login und der eingebaute
Selbsttest. Aufbau und Datenfluss: [../docs/architecture/overview.md](../docs/architecture/overview.md).

Nicht hier: Prüfwerkzeuge (`../tools/Check`), Veröffentlichung (`../publish.ps1`).

Testen: `NoxVault.exe --self-test <Bericht.txt>` unter Windows, oder alles zusammen mit
`dotnet run --project ../tools/Check`.

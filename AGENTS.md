# Hinweise für Agents

Vor jeder Änderung [docs/conventions.md](docs/conventions.md), [docs/README.md](docs/README.md) und den neuesten
Eintrag in [docs/handoffs/](docs/handoffs/) lesen.

- `dotnet run --project tools/Check` muss bestehen. Der Selbsttest braucht Windows; unter Linux nur mit
  `-- --skip-self-test` und mit dem Hinweis, dass er nicht gelaufen ist.
- Tresorformat (`Storage/Vault.cs`), `RememberedLogin` und die Prüfungen in `Autofill/` nicht ohne Verifikationsplan
  und neue Selbsttests ändern.
- Nur synthetische Testdaten. Nie den echten Tresor unter `%USERPROFILE%\.noxvault` öffnen, lesen oder verändern.
- Oberfläche nur über die Tokens in `Interface/Theme`; beide Themes in den Renders ansehen.

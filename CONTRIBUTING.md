# Contributing

## Ground rules

- Keep changes aligned with the local-first and trust-first design goals.
- Avoid changes that weaken signatures, role enforcement, or auditability.
- Prefer small, focused pull requests.

## Development

```powershell
dotnet build Blueprints.sln
dotnet test Blueprints.sln
```

## Pull requests

- Explain the problem and the chosen approach.
- Note any schema, sync, or signature implications.
- Include tests for behavior changes where practical.

## Scope discipline

- UI customization is project-defined where safe.
- Security-critical behavior is intentionally not freely customizable.

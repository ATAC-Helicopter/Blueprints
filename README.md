<p align="center">
  <img src="docs/assets/blueprints-mark.png" alt="Blueprints logo" width="152">
</p>

<h1 align="center">Blueprints</h1>

<p align="center">
  A local-first release planner for developers and small teams.
  Plan versions, produce changelogs, and trust the files.
</p>

<p align="center">
  <a href="https://github.com/ATAC-Helicopter/Blueprints/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/ATAC-Helicopter/Blueprints/actions/workflows/ci.yml/badge.svg?branch=develop"></a>
  <a href="https://github.com/ATAC-Helicopter/Blueprints/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://github.com/ATAC-Helicopter/Blueprints/actions/workflows/codeql.yml/badge.svg?branch=develop"></a>
  <a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/license-MIT-2f6f5e"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4">
</p>

> [!IMPORTANT]
> Blueprints is pre-release software. The local planning workflow is functional, but packaging, recovery tooling, and multi-user collaboration still need hardening before production use.

## Why Blueprints?

Release notes often live in scattered issues, commit messages, and private documents. Blueprints keeps release intent in a portable workspace that remains readable without a server:

- plan versions and categorized release items;
- generate stable human-readable item keys;
- freeze and release versions with immutable released content;
- export Markdown changelogs, optionally enriched by local Git history;
- validate signed project files and enter read-only mode when trust breaks;
- exchange signed changes through a shared folder with explicit push, pull, and conflict resolution;
- inspect membership, audit history, sync state, and integration boundaries in one desktop app.

## Current status

| Area | Status |
| --- | --- |
| Local project, version, and item workflow | Working |
| Signed persistence and trust validation | Working |
| Markdown changelog export | Working |
| Shared-folder push/pull | Working foundation |
| Conflict and audit diagnostics | Functional, needs UX polish |
| Local Git awareness | Read-only integration available |
| GitHub, GitLab, and VaultSync adapters | Planned |
| Installers and supported releases | Not ready |

The canonical delivery plan is [ROADMAP.md](Roadmap.md). Older planning files remain as design history and are not the active backlog.

## Quick start

Prerequisites:

- the .NET 10 SDK (`10.0.300` or a newer patch);
- Git, if you want local source-change diagnostics.

```sh
git clone https://github.com/ATAC-Helicopter/Blueprints.git
cd Blueprints
./scripts/verify.sh
./scripts/run-app.sh
```

Equivalent commands on any platform:

```sh
dotnet restore Blueprints.sln
dotnet build Blueprints.sln --configuration Release --no-restore
dotnet test Blueprints.Tests/Blueprints.Tests.csproj --configuration Release --no-build
dotnet run --project Blueprints.App/Blueprints.App.csproj
```

See the [development guide](docs/development.md) for platform notes and troubleshooting.

## Architecture at a glance

| Project | Responsibility |
| --- | --- |
| `Blueprints.Core` | Domain models, enums, item-key rules |
| `Blueprints.Security` | Identity, key protection, Ed25519 signatures |
| `Blueprints.Storage` | Canonical JSON and signed workspace persistence |
| `Blueprints.Collaboration` | Sync analysis, manifests, audit chain, shared-folder safety |
| `Blueprints.App` | Avalonia UI, workflows, changelog and Git integration |
| `Blueprints.Tests` | Unit and integration coverage |

Blueprints uses a local workspace as the editable source of truth and a separate shared directory as an exchange layer. See [architecture](docs/architecture.md), [workspace format](docs/workspace-format.md), and [security model](docs/security-model.md).

## Documentation

- [Documentation index](docs/README.md)
- [User guide](docs/user-guide.md)
- [Architecture](docs/architecture.md)
- [Workspace format](docs/workspace-format.md)
- [Security model](docs/security-model.md)
- [Development guide](docs/development.md)
- [Release process](docs/releasing.md)
- [Release history](docs/releases.md)
- [Roadmap](Roadmap.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)

## Contributing

Issues and pull requests are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md), use `develop` as the pull-request base, and keep security reports out of public issues.

## License

Blueprints is available under the [MIT License](LICENSE).

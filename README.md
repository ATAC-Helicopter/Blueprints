# Blueprints

Blueprints is a local-first, version-centric release planning desktop application for developers and small teams. The repository currently contains a working .NET 8 solution with an Avalonia desktop shell, domain models for release planning, canonical JSON storage, detached Ed25519 signatures, and a small test suite around the core infrastructure.

The codebase is still early. Most of the implemented value today is in the domain and storage/security primitives rather than a fully built product workflow.

## What Exists Today

- `Blueprints.App`: Avalonia desktop shell with a sample dashboard-style `MainWindowViewModel`
- `Blueprints.Core`: release-planning domain records, enums, and item key formatting
- `Blueprints.Security`: Ed25519 key generation/signing plus trust-state presentation helpers
- `Blueprints.Storage`: canonical JSON serialization and a file-system signed document store
- `Blueprints.Collaboration`: sync-health enums and summary models
- `Blueprints.Tests`: xUnit coverage for signing, canonical JSON, item key formatting, and signed document round-tripping

## Documentation

Current codebase documentation:

- [Architecture](docs/architecture.md)
- [Domain Model](docs/domain-model.md)
- [Storage and Trust](docs/storage-and-trust.md)
- [Development Guide](docs/development.md)

Planned product documentation:

- [Product Overview](docs/product-overview.md)
- [User Workflows](docs/user-workflows.md)
- [Collaboration and Trust](docs/collaboration-and-trust.md)
- [Release and Changelog Workflow](docs/release-and-changelog.md)

The original product and implementation planning material is still valuable as design context:

- [Plan.md](Plan.md)
- [ImplementationPlan.md](ImplementationPlan.md)
- [GitHubOpsReference.md](GitHubOpsReference.md)

## Stack

- .NET 8
- Avalonia UI 11
- CommunityToolkit.Mvvm
- NSec.Cryptography
- xUnit

## Getting Started

Prerequisites:

- .NET SDK 8.x

Common commands:

```bash
dotnet restore
dotnet build Blueprints.sln
dotnet test Blueprints.sln
dotnet run --project Blueprints.App
```

## Current Implementation Notes

- The desktop app currently renders sample data rather than loading a real workspace.
- The storage layer persists canonical JSON and a detached `.sig` file beside each document.
- Signature validation is per-document and currently demonstrated through tests, not yet through an end-to-end project workflow.
- The design docs describe additional entities such as logs, sync manifests, and invitation flows that are not fully implemented in code yet.

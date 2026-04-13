# Development Guide

## Repository Layout

```text
Blueprints.sln
├── Blueprints.App
├── Blueprints.Collaboration
├── Blueprints.Core
├── Blueprints.Security
├── Blueprints.Storage
└── Blueprints.Tests
```

Top-level supporting documents:

- `Plan.md`: product specification and scope
- `ImplementationPlan.md`: file model, sync architecture, and execution plan
- `GitHubOpsReference.md`: contributor workflow reference

## Requirements

- .NET SDK 8.x

The repository uses SDK-style projects and central shared build settings through `Directory.Build.props`.

## Common Commands

Restore packages:

```bash
dotnet restore
```

Build the solution:

```bash
dotnet build Blueprints.sln
```

Run tests:

```bash
dotnet test Blueprints.sln
```

Launch the desktop app:

```bash
dotnet run --project Blueprints.App
```

## Current Dependencies

Key dependencies in the codebase today:

- `Avalonia`
- `Avalonia.Desktop`
- `Avalonia.Themes.Fluent`
- `Avalonia.Fonts.Inter`
- `CommunityToolkit.Mvvm`
- `NSec.Cryptography`
- `xUnit`

## What Is Implemented

Implemented and test-backed:

- domain records for project, version, item, member, trust, and sync summaries
- deterministic JSON serialization
- detached signature generation and verification
- file-based read/write of signed documents
- item key formatting helpers
- a basic Avalonia shell populated with sample data

Not yet implemented end to end:

- loading a project from disk into the UI
- editing and persisting versions and items through the app
- changelog export
- sync manifests, packs, and conflict resolution
- invitation and membership workflows
- trust evaluation across a full project graph

## Guidance For Contributors

When changing behavior, keep three distinctions clear in the docs and code:

- current implementation
- intended architecture
- open roadmap work

This matters in this repository because the design docs are significantly ahead of the UI/application flow.

Practical suggestions:

- keep `Blueprints.Core` free of infrastructure concerns
- preserve canonical serialization guarantees when evolving document models
- treat signature compatibility as a cross-project concern, not a local refactor
- add tests for any change to JSON shape, signing inputs, or item key behavior

## Suggested Documentation Maintenance

As implementation grows, update docs in this order:

1. Update the relevant `docs/*.md` page that describes current behavior.
2. Update `README.md` if the entry point or project status changed.
3. Update `ImplementationPlan.md` only when the target design itself changed.

That keeps the documentation split clean:

- `README.md` for orientation
- `docs/` for the current codebase
- planning docs for future design

# Blueprints

Blueprints is a local-first, version-centric release planning desktop application for developers and small teams.

## What It Does

Blueprints is being built around a safety-first workflow:

- local signed project workspaces
- Windows DPAPI-backed local identity storage
- detached-signature validation for project files
- shared-folder sync with signed manifest tracking
- version-centric release planning with human-readable item keys

## Stack

- .NET 8
- Avalonia UI
- MVVM

## Current State

The repository currently includes:

- product and implementation planning documents
- local identity management
- signed workspace persistence
- shared-folder sync foundation
- Avalonia desktop shell wired to live workspace and sync state

## Run Locally

From the repository root:

```powershell
dotnet build Blueprints.sln
dotnet test .\Blueprints.Tests\Blueprints.Tests.csproj
dotnet run --project .\Blueprints.App\Blueprints.App.csproj
```

## Branch Workflow

- `main` is the stable branch
- `develop` is the integration branch
- issue work happens on `feature/<number>-<slug>` or `chore/<number>-<slug>`
- work is merged into `develop` first, then promoted intentionally to `main`

## Release Posture

- the repository is public, but the product is still pre-release
- draft prereleases are preferred before anything is published as final
- security-sensitive changes should be reviewed conservatively
- admin bypass remains enabled on protected branches until there is enough maintainer coverage to remove it safely

## Projects

- `Blueprints.App`
- `Blueprints.Core`
- `Blueprints.Storage`
- `Blueprints.Security`
- `Blueprints.Collaboration`
- `Blueprints.Tests`

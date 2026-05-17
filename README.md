# Blueprints

Blueprints is a local-first, version-centric release planning desktop application for developers and small teams.

This is a private, closed-source repository. All rights are reserved; see `LICENSE`.

It is built around a simple product promise:

> Plan releases. Produce changelogs. Trust the files.

## What It Does

Blueprints is being built around a safety-first workflow:

- local signed project workspaces
- platform-aware local identity storage
- detached-signature validation for project files
- shared-folder sync with signed manifest tracking
- version-centric release planning with human-readable item keys

## Stack

- .NET 8
- Avalonia UI
- MVVM

## Platform Support

- Windows uses DPAPI-backed local private key protection.
- Linux and macOS use a local AES-GCM key protector stored under the app data directory with user-only file permissions where supported.
- Windows remains the primary release target for now, but Linux development and test runs are supported.

## Current State

The repository currently includes:

- product and implementation planning documents
- local identity management
- signed workspace persistence
- shared-folder sync foundation
- project create/open workflows
- version and item management
- release workflow and Markdown changelog export
- membership invitation/update workflows
- basic conflict resolution
- audit log and shared-folder safety foundations
- Avalonia desktop shell wired to live workspace and sync state

The next major product direction is to make sync/trust/audit operations visible and coherent in the app UI, then polish the release planning workflow.

## Direction And Handoff

Start here if you are returning to the project after time away:

- `AgentQuickstart.md`
- `CodexHandoff.md`
- `ProductDirection.md`
- `Roadmap.md`
- `IntegrationsStrategy.md`
- `VaultSyncContext.md`
- `TestPlan.md`

## Run Locally

From the repository root:

```powershell
dotnet build Blueprints.sln
dotnet test .\Blueprints.Tests\Blueprints.Tests.csproj
dotnet run --project .\Blueprints.App\Blueprints.App.csproj
```

On Linux, `scripts/run-app.sh` prints a clearer diagnostic when running from a Wayland session without an XWayland `DISPLAY`.

## Internal Branch Workflow

- `main` is the stable branch
- `develop` is the integration branch
- issue work happens on `feature/<number>-<slug>` or `chore/<number>-<slug>`
- work is merged into `develop` first, then promoted intentionally to `main`

## Release Posture

- the repository is private and proprietary
- draft prereleases are preferred before anything is published as final
- security-sensitive changes should be reviewed conservatively
- admin bypass remains enabled on protected branches until there is enough maintainer coverage to remove it safely

## Repository Posture

- closed source
- no public contribution process
- no public issue templates
- no public code-of-conduct workflow
- CI and dependency update automation may remain enabled for private maintenance

## Projects

- `Blueprints.App`
- `Blueprints.Core`
- `Blueprints.Storage`
- `Blueprints.Security`
- `Blueprints.Collaboration`
- `Blueprints.Tests`

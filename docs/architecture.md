# Architecture

## System shape

Blueprints is a layered .NET 8 desktop application.

```text
Blueprints.App
  ├── Blueprints.Collaboration
  ├── Blueprints.Storage
  ├── Blueprints.Security
  └── Blueprints.Core

Blueprints.Collaboration
  ├── Blueprints.Storage
  ├── Blueprints.Security
  └── Blueprints.Core

Blueprints.Storage
  ├── Blueprints.Security
  └── Blueprints.Core

Blueprints.Security
  └── Blueprints.Core
```

`Blueprints.Core` has no project dependencies. Provider integrations belong at the app boundary unless a provider-neutral abstraction becomes part of the domain.

## Runtime composition

`App.axaml.cs` is the current composition root. It constructs:

- identity storage and platform-specific private-key protection;
- canonical JSON and Ed25519 signature services;
- signed workspace storage;
- audit, manifest, sync-state, analysis, validation, and sync services;
- the project coordinator;
- the Avalonia main-window view model.

There is no dependency-injection container. Constructor composition keeps the current graph explicit, but the large coordinator and view model should be split as roadmap work.

## Main flows

### Open

1. Resolve local and shared paths.
2. Load the local identity.
3. Load project, membership, versions, and items.
4. Verify detached signatures with the project member key selected by the current workflow.
5. Validate the signed audit chain.
6. Analyze local/shared changes against local sync state.
7. return a `LocalWorkspaceSession` containing data, trust, sync, safety, and conflict state.

### Mutation

1. Require a trusted workspace and no unresolved conflicts.
2. Validate role or lifecycle rules.
3. create the updated immutable record graph.
4. Write canonical JSON and detached signatures.
5. Append a signed, hash-linked audit entry.
6. Reload the session so displayed state comes from disk.

### Push

1. Build local and shared snapshots.
2. compare both against the last tracked baseline.
3. block if both sides changed the same path differently.
4. copy outgoing documents and signatures.
5. write a signed manifest to the shared root.
6. update local sync state and append an audit entry.

### Pull

1. Validate the shared manifest and incoming signatures.
2. check manifest continuity.
3. analyze conflicts against the baseline.
4. block on any invalid or conflicting input.
5. copy incoming documents and signatures.
6. update local sync state and audit history.

## Design constraints

- Local release planning must work without a network or account.
- Shared state is never applied before validation.
- Released versions are immutable.
- Membership always retains at least one active administrator.
- Hosted providers cannot become authoritative for signed project truth.
- Local-only integration credentials and paths stay outside project files.

## Known structural debt

- `MainWindowViewModel` combines screen state, workflow commands, mapping, diagnostics, and design data.
- `MainWindow.axaml` contains all application sections in one file.
- `ProjectWorkspaceCoordinatorService` combines multiple application use cases.
- file writes are signed but not yet implemented as a transactional workspace commit;
- workspace schema migration and formal compatibility rules do not yet exist;
- automated end-to-end UI and two-user collaboration tests are absent.

These are tracked in [the roadmap](../Roadmap.md).

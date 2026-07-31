# Architecture

## System shape

Blueprints is a layered .NET 10 desktop application.

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
4. Load the optional canvas layout and typed relationship graph, then validate schema, project identity, entity references, and rendering bounds.
5. Verify detached signatures with the project member key selected by the current workflow.
6. Validate the signed audit chain.
7. Analyze local/shared changes against local sync state.
8. Return a `LocalWorkspaceSession` containing data, layout, trust, sync, safety, and conflict state.

### Mutation

1. Require a trusted workspace and no unresolved conflicts.
2. Validate role or lifecycle rules.
3. create the updated immutable record graph.
4. Write canonical JSON and detached signatures.
5. Append a signed, hash-linked audit entry.
6. Reload the session so displayed state comes from disk.

### Canvas layout mutation

1. Collect current project/version/item node positions.
2. Require a trusted workspace with no unresolved conflicts.
3. Reject unknown entities, duplicate node identities, unsupported types, non-finite values, and out-of-range coordinates.
4. Increment the layout revision.
5. Write only `project/canvas-layout.json` and its detached signature.
6. Append a signed audit entry.
7. Reload and project the layout onto current signed entities.

Zoom and scroll offsets follow a separate local-preference flow: validate bounded values, atomically replace `.blueprints/canvas-view.json`, and never include it in signatures, audit entries, manifests, or exchange analysis.

### Source discovery and approval

1. Resolve the linked local Git worktree.
2. Parse bounded changelog and roadmap Markdown locally.
3. Resolve a GitHub or GitLab.com origin and route it through the matching provider-neutral reader. GitHub uses bounded REST plus authenticated GraphQL for Projects; GitLab uses bounded REST for issues, merge requests, releases, and milestones.
4. Normalize source entries into transient `SourceDiscoveryCandidate` values.
5. Present editable `SourceImportProposal` values and flag exact-title duplicates.
6. Require the user to choose inclusion, target version, type, category, title, description, and completion state.
7. Validate every approved proposal against the current trusted workspace before changing disk.
8. Write the approved batch as signed item documents and append one signed `source.import.apply` audit entry.

Discovery never mutates the repository, provider, or Blueprints workspace. Provider data remains an input suggestion and never becomes authoritative without the explicit apply action.

The hosted-provider operation policy allows discovery reads without a write approval. Future provider mutations must present a fresh approval matching one exact provider, repository, operation, and target; approval expires within ten minutes and is consumed once.

### Passive VaultSync health

1. Load the machine-local configured VaultSync destination or metadata path.
2. Resolve only the documented `.vaultsync/meta/vaultsync.meta.db` path forms.
3. Confirm the portable metadata store exists without opening or parsing its SQLite contents.
4. Read the optional sibling `blueprints.status.json` through a schema-1, 1 MiB-bounded adapter.
5. Project reachability, backup, verification, restore-readiness, index, conflict, and warning evidence into the Integrations workspace.
6. Keep all evidence informational and outside signed project truth.

The reader boundary is injectable so a future stable VaultSync CLI/API adapter can replace the file contract without changing integration presentation or Blueprints trust semantics.

Exchange registration is a separate write adapter. It derives the canonical `<destination>/.blueprints/projects/<project-id>/` path from detected metadata, requires a fresh exact-target single-use approval, refuses ambiguous existing content, and writes one atomic registration marker. The adapter does not mutate the current session or its configured shared root.

The release-readiness builder consumes the structured passive-health result rather than parsing display text. It reports absent, risky, incomplete, stale, future-dated, and recent healthy evidence. The default policy is advisory: seven-day freshness and five-minute future clock skew inform the human decision but do not disable release.

The VaultSync recovery drill exercises the boundary without invoking VaultSync: local and registered exchange snapshots are copied independently, relocated, validated, pulled, and then used for another signed publication. This proves Blueprints path, marker, trust, manifest, and continuation behavior while leaving transport-level verification to VaultSync.

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
- Canvas layout is shared signed presentation state; version and item documents remain authoritative content.
- Source-discovery proposals are transient and untrusted; only reviewed, approved, signed Blueprints items become project truth.
- VaultSync metadata enriches recovery diagnostics but cannot establish Blueprints workspace trust.

## Known structural debt

- `MainWindowViewModel` combines screen state, workflow commands, mapping, diagnostics, and design data.
- `MainWindow.axaml` contains all application sections in one file.
- `ProjectWorkspaceCoordinatorService` combines multiple application use cases.
- file writes are signed but not yet implemented as a transactional workspace commit;
- workspace schema migration and formal compatibility rules do not yet exist;
- automated end-to-end UI and two-user collaboration tests are absent.

These are tracked in [the roadmap](../Roadmap.md).

# VaultSync Context For Blueprints

Status: GitHub-derived context
Date: 2026-05-16
Source: `https://github.com/ATAC-Helicopter/VaultSync`

## Repository Summary

VaultSync is a public C#/.NET 8 repository under `ATAC-Helicopter/VaultSync`.

Repository description:

> Transparent, cross-platform backup & sync tool focused on visibility and reliability for NAS, network storage and Local drives.

Default branch:

- `Stable`

Latest release observed:

- `v1.7.3` / `VaultSync 1.7.3`

Primary stack:

- C#
- Avalonia desktop UI
- CLI
- SQLite metadata stores
- rsync/robocopy transfer backends
- Windows/macOS/Linux support

Core product phrase from README:

- Snapshot | Backup | Sync | Verify

## What VaultSync Does

VaultSync is a cross-platform backup and snapshot manager for project folders, NAS workflows, and reliable restores.

Important capabilities:

- project registration
- snapshots
- sync/mirror to destinations
- backup history
- restore workflows
- hash verification
- watch mode
- presets similar to `.gitignore`
- multiple destinations
- destination quota suggestions
- retention simulation
- metadata sync across machines
- encryption policy metadata
- Doctor/repair workflows
- support bundles and diagnostics
- update/patch infrastructure

## Relevant Repository Structure

Important files/directories:

- `VaultSync.sln`
- `src/VaultSync.Core`
- `src/VaultSync.CLI`
- `src/VaultSync.UI`
- `tests/VaultSync.Core.Tests`
- `docs/wiki/Metadata-Sync.md`
- `docs/wiki/Configuration.md`
- `docs/wiki/Network-Shares.md`
- `ROADMAP.md`

Key core files inspected:

- `src/VaultSync.Core/Models/Project.cs`
- `src/VaultSync.Core/Models/Snapshot.cs`
- `src/VaultSync.Core/Models/Backup.cs`
- `src/VaultSync.Core/Config/AppConfig.cs`
- `src/VaultSync.Core/Services/MetadataStore.cs`
- `src/VaultSync.Core/Services/MetadataSyncService.cs`
- `src/VaultSync.Core/Services/DestinationIdentityService.cs`
- `src/VaultSync.Core/Services/NetworkMountService.cs`
- `src/VaultSync.Core/Services/SyncService.cs`
- `src/VaultSync.Core/Services/VerifyService.cs`
- `src/VaultSync.Core/Services/RestoreReadinessService.cs`
- `src/VaultSync.CLI/Commands/ProjectCommands.cs`
- `src/VaultSync.CLI/Commands/SyncCommands.cs`

## Project Model

VaultSync project fields include:

- numeric local ID
- external ID
- name
- root path
- preset
- created timestamp
- restore-needed flag
- preferred destination ID
- encryption policy
- encryption key reference
- restore mode
- verification policy
- tags

Blueprints mapping:

- A Blueprints project can map to a VaultSync project by external ID or configured project name/root.
- Blueprints should not rely on VaultSync numeric local IDs because they are local database identifiers.
- The stable bridge should use:
  - VaultSync project `ExternalId`
  - Blueprints `ProjectId`
  - optional signed Blueprints integration record

## Snapshot Model

VaultSync snapshots include:

- external ID
- project ID
- created timestamp
- file count
- total bytes
- diff added count
- diff modified count
- diff deleted count
- net byte diff
- top changed paths JSON

Blueprints mapping:

- A Blueprints release/version can show latest VaultSync snapshot age, size, and diff summary.
- Before releasing a version, Blueprints can warn if the linked VaultSync project has no recent snapshot.
- Snapshot summaries can enrich Trust/Sync diagnostics, but they are not Blueprints trust authority.

## Backup Model

VaultSync backup history includes:

- external ID
- project ID
- snapshot ID
- created timestamp
- backup type
- backup mode: full/incremental/imported
- total bytes
- relative backup path
- protected/keep flag
- imported flag
- encrypted flag
- non-secret crypto descriptor JSON
- destination path
- destination alias
- origin machine name

Blueprints mapping:

- Blueprints can surface restore-readiness and latest backup context for a project.
- Blueprints can show whether the release workspace has a recent verified/protected backup.
- Blueprints must never treat VaultSync backup metadata as a substitute for Blueprints signatures.

## Metadata Sync

VaultSync exports portable metadata under:

```text
<destination>/.vaultsync/meta/vaultsync.meta.db
```

The metadata store carries:

- project identity
- portable project settings
- snapshot summaries
- backup history fields
- tombstones
- source machine metadata
- non-secret encryption descriptor metadata

It does not carry:

- backup payload contents
- plaintext passwords
- secret material
- full local app configuration
- full destination definitions from another machine

Conflict behavior:

- some imported project settings do not silently overwrite local values
- preferred destination, restore mode, verification policy, and tags can produce metadata conflict records
- conflicts are reviewed through Doctor workflows

Blueprints mapping:

- Blueprints can read VaultSync metadata to understand destination health/history.
- Blueprints should not write secrets to VaultSync metadata.
- Blueprints should expect metadata conflict states and surface them as diagnostics.

## Destinations And Network Shares

VaultSync supports:

- local destinations
- external drive destinations
- network destinations
- SMB auto-mount on Windows/macOS
- pre-mounted NFS workflows
- credential profiles
- advanced destinations
- per-project preferred destination routing
- destination IDs derived from normalized path, credential, and mount mode

Blueprints mapping:

- VaultSync can provide safer destination selection than manually entering a shared folder path.
- Blueprints should prefer VaultSync destination IDs/aliases over raw paths when configured.
- Blueprints can still resolve to an exchange root path for its signed sync files.

## Sync And Verify

VaultSync CLI examples:

```sh
vaultsync init
vaultsync add-project Demo ~/Projects/Demo --preset unity
vaultsync snapshot Demo
vaultsync sync Demo ~/Backup/Demo
vaultsync verify Demo ~/Backup/Demo --full
```

Sync implementation:

- Windows uses `robocopy`
- macOS/Linux use `rsync`
- verify uses deterministic hash sampling or full hash comparison

Blueprints mapping:

- Blueprints should not duplicate VaultSync copy/verify logic when VaultSync is configured.
- Blueprints can call or integrate with VaultSync to prepare/verify destination transport.
- Blueprints still owns signed document validation after transport.

## Restore Readiness

VaultSync computes restore readiness from:

- latest backup age
- verification policy
- selected destination reachability
- backup index consistency

Blueprints mapping:

- Blueprints Trust/Sync view can show "VaultSync restore readiness" for the linked project.
- A release workflow can warn if restore readiness is Risk/Unavailable.

## Integration Contract Recommendation

Blueprints should support VaultSync in phases.

### Phase 1 - Passive Awareness

Read-only integration:

- configure a VaultSync metadata root or destination
- detect `.vaultsync/meta/vaultsync.meta.db`
- read metadata summary
- match VaultSync project to Blueprints project
- show latest snapshot/backup/restore-readiness style diagnostics

No Blueprints project truth changes should be inferred from VaultSync metadata.

### Phase 2 - Managed Exchange Root

Use VaultSync to manage the exchange location:

- Blueprints selects a VaultSync destination/project
- VaultSync prepares/mounts/verifies destination reachability
- Blueprints writes signed exchange files into a Blueprints-specific subfolder
- Blueprints continues to validate manifest, signatures, membership, and audit continuity

Suggested path:

```text
<VaultSyncDestination>/.blueprints/projects/<BlueprintsProjectId>/
```

Alternative if VaultSync project root is the exchange root:

```text
<VaultSyncProjectRoot>/BlueprintsExchange/
```

The first option is better because it avoids mixing Blueprints exchange state into the user project payload.

### Phase 3 - Active VaultSync Registration

Blueprints can optionally register or update a VaultSync project:

- project name
- root path
- preset
- tags
- preferred destination

This should be explicit and reversible.

### Phase 4 - Release Safety Gate

Before release, Blueprints can optionally check:

- VaultSync latest snapshot age
- latest backup age
- verification policy
- destination reachability
- backup index consistency
- metadata conflicts

Release should warn, not silently block by default, unless a project policy later requires it.

## Blueprints Data Boundary

VaultSync can provide:

- transport
- destination identity
- reachability
- backup/snapshot metadata
- restore readiness
- metadata conflict diagnostics

Blueprints must retain authority over:

- project configuration
- release versions
- release items
- members
- signatures
- audit log
- sync manifest
- conflict decisions

VaultSync metadata must never make unsigned Blueprints state trusted.

## Proposed Blueprints Models

Possible future models:

```csharp
public sealed record VaultSyncLink(
    string VaultSyncProjectExternalId,
    string VaultSyncProjectName,
    string? DestinationId,
    string? DestinationAlias,
    string ExchangeRootPolicy);

public sealed record VaultSyncStatusSummary(
    bool MetadataStoreFound,
    string? MetadataStorePath,
    DateTimeOffset? LastMetadataWriteUtc,
    DateTimeOffset? LatestSnapshotUtc,
    DateTimeOffset? LatestBackupUtc,
    string RestoreReadiness,
    IReadOnlyList<string> Warnings);
```

Storage recommendation:

- local VaultSync metadata DB path and local mount path: local-only settings
- VaultSync project external ID and optional destination alias: signed project configuration if the team shares the same intended backup/sync relationship
- secrets and credentials: never in Blueprints signed documents

## Open Questions

- Should Blueprints read VaultSync SQLite metadata directly or ask VaultSync for a small stable CLI/API output?
- Should VaultSync expose a `vaultsync status --json` command for integration consumers?
- Should Blueprints create `.blueprints` under VaultSync destinations, or should VaultSync get a first-class "Blueprints exchange" destination type?
- Should release readiness warnings be advisory or policy-enforced per project?
- Should VaultSync snapshots include Blueprints exchange folders, or should those folders be excluded by preset/default rule?

## Best Next Step

Do not implement direct SQLite coupling first.

Best first slice:

1. define a Blueprints-side `VaultSyncIntegration` abstraction
2. define desired JSON status contract
3. add a passive mock/file-based adapter for tests
4. then decide whether the real adapter reads metadata DB directly or shells out to a future VaultSync CLI command

# Domain Model

## Core Concepts

Blueprints models release planning around a small set of explicit entities:

- project configuration
- membership
- versions
- items
- trust state
- sync state

The records live primarily in `Blueprints.Core`, with identity and sync summaries split into `Blueprints.Security` and `Blueprints.Collaboration`.

## Project Configuration

`ProjectConfigurationDocument` is the top-level project definition. It contains:

- `SchemaVersion`
- `ProjectId`
- `Name`
- `ProjectCode`
- `VersioningScheme`
- `CreatedUtc`
- `DefaultCategories`
- `ItemTypes`
- `ItemKeyRules`
- `ChangelogRules`

This is the document that defines how a project names work, categorizes items, and formats changelog output.

Related records:

- `CategoryDefinition`: category ID and display label
- `ItemTypeDefinition`: item type ID and display label
- `ItemKeyRule`: per-type key prefix and key scope
- `ChangelogRules`: default export preferences

## Membership

`MemberDocument` stores membership state for a project:

- `SchemaVersion`
- `ProjectId`
- `MembershipRevision`
- `Members`

Each `ProjectMember` contains:

- `UserId`
- `DisplayName`
- `PublicKey`
- `Role`
- `JoinedUtc`
- `IsActive`

`MemberRole` currently supports:

- `Viewer`
- `Editor`
- `Admin`

The planning docs describe this file as a trust anchor for collaboration, but the runtime application does not yet load or enforce it.

## Versions

`VersionDocument` represents a single release line:

- `SchemaVersion`
- `ProjectId`
- `VersionId`
- `Name`
- `Status`
- `CreatedUtc`
- `ReleasedUtc`
- `Notes`
- `ManualOrder`

`ReleaseStatus` values:

- `Planned`
- `InProgress`
- `Frozen`
- `Released`

The presence of `ManualOrder` shows the design is intentionally not purely timestamp-sorted; manual sequencing is a first-class concept.

For display, the app uses `VersionSummary`:

- `Name`
- `Status`
- `ItemCount`
- `CompletedItemCount`

## Items

`ItemDocument` is the atomic planning unit in the current model:

- `SchemaVersion`
- `ProjectId`
- `VersionId`
- `ItemId`
- `ItemKey`
- `ItemKeyTypeId`
- `CategoryId`
- `Title`
- `Description`
- `IsDone`
- `Tags`
- `CreatedUtc`
- `UpdatedUtc`
- `LastModifiedByUserId`
- `LastModifiedByName`

Important properties of the current design:

- each item belongs to exactly one version
- items carry both an internal GUID and a human-readable `ItemKey`
- categories and key rules are project-defined
- modification attribution is stored directly on each item

## Item Key Rules

`ItemKeyFormatter` currently implements two strategies:

- `FormatProjectScoped(prefix, sequence)` -> `PREFIX-42`
- `FormatVersionScoped(projectCode, major, minor, sequence)` -> `VS-1601`

The formatter is intentionally simple today. It proves the idea of project-scoped versus version-scoped keys but does not yet implement reservation, persistence, or collision handling.

`ItemKeyScope` values:

- `Project`
- `Version`

## Trust and Identity

Trust is represented in `Blueprints.Core.Enums.TrustState`:

- `Trusted`
- `Untrusted`
- `Corrupt`

Security-facing summaries in `Blueprints.Security.Models` include:

- `IdentitySummary`: display name, user ID, key storage provider
- `TrustReport`: trust state, summary, evaluation timestamp

The app shell currently uses `ProjectSummary` plus `TrustStatePresenter` to render a user-facing trust badge.

## Sync Model

The sync model is still a stub, but the current types already define the intended UI vocabulary.

`SyncHealth` values:

- `Idle`
- `Ready`
- `NeedsAttention`

`SyncSummary` contains:

- `Health`
- `PendingOutgoingChanges`
- `PendingIncomingChanges`
- `ConflictCount`

The implementation plan expands this into a file-based sync architecture, but only the summary-level model is implemented today.

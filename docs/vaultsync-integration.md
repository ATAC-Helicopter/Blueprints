# VaultSync integration

Blueprints treats VaultSync as an optional transport and recovery system. Blueprints continues to own release semantics, signed project documents, membership, manifests, audit continuity, and conflict decisions. Either application remains usable when the other is absent.

## Passive health contract

The VaultSync link is a machine-local path stored with other application integration settings. It is never written into the signed project or shared exchange root.

Blueprints accepts any of these configured locations:

- a destination containing `.vaultsync/meta/vaultsync.meta.db`;
- the destination's `.vaultsync` directory;
- the `.vaultsync/meta` directory;
- the exact `vaultsync.meta.db` file.

Detection is deliberately shallow and deterministic. Blueprints confirms the database exists but does not open or parse SQLite because that would couple this application to VaultSync's private storage schema.

Detailed health is optional. A producer may place this file beside the database:

```text
<destination>/.vaultsync/meta/blueprints.status.json
```

Schema 1:

```json
{
  "schemaVersion": 1,
  "projectExternalId": "stable-vaultsync-project-id",
  "projectName": "Example",
  "destinationAlias": "Studio NAS",
  "destinationReachable": true,
  "latestSnapshotUtc": "2026-07-30T20:00:00Z",
  "latestBackupUtc": "2026-07-30T21:00:00Z",
  "latestVerificationUtc": "2026-07-30T22:00:00Z",
  "backupIndexConsistent": true,
  "restoreReadiness": "Ready",
  "metadataConflictCount": 0,
  "warnings": []
}
```

The document is read-only input. Blueprints accepts at most 1 MiB, JSON depth 16, 32 distinct non-empty warnings, and 256 characters for displayed identity fields. Unsupported or malformed schemas become warnings and never affect workspace trust.

Connection states:

- **Connected:** metadata and a risk-free schema-1 health document are available.
- **Warning:** metadata exists but detailed health is absent, reports risk, contains warnings or conflicts, or says the destination/index is unhealthy.
- **Error:** the configured location does not contain a supported metadata store.
- **Not configured:** no machine-local link is set.

Timestamps and readiness values are evidence reported by the producer. Passive awareness does not independently inspect backup payloads or claim verification.

## Release safety

The release planner treats VaultSync health as an advisory safety gate:

- no configured health link, unavailable metadata, reported risk, and incomplete evidence need attention;
- snapshot, backup, and verification timestamps older than seven days are stale;
- timestamps more than five minutes in the future require a producer clock check;
- reachable, consistent, Ready/Healthy evidence with all three recent timestamps is shown as ready.

These diagnostics do not silently disable the release action. Teams can still make an explicit release decision when VaultSync is absent or when older evidence is intentional. Blueprints reports what the producer claimed and never substitutes that claim for signed workspace validation or a real restore exercise.

## Exchange-root contract

An explicitly registered Blueprints exchange root managed beneath a VaultSync destination uses:

```text
<destination>/.blueprints/projects/<project-id>/
```

`<project-id>` is the lowercase canonical Blueprints project GUID. The directory contains the same signed documents, signatures, and manifest expected from any Blueprints shared root.

The ownership boundary is:

- Blueprints creates and validates content only under its project-specific directory after the user selects **Register exchange** twice.
- VaultSync owns transport, destination reachability, backup, verification, and restore operations for the parent destination.
- Blueprints never mixes exchange state into the backed-up project payload.
- VaultSync metadata cannot make unsigned or invalid Blueprints documents trusted.
- Registration requires a fresh exact-project, exact-destination approval that expires within ten minutes and is consumed once.
- Registration writes only an atomic, bounded `.blueprints-exchange.json` marker. It refuses non-canonical metadata layouts, linked internal directories, and existing unregistered content.
- Registration does not switch the open project's shared root. Reopen the project with the prepared root when ready, so a confirmation can never silently redirect collaboration.
- Future VaultSync commands remain explicit and reversible; passive health detection never writes.

The automated recovery drill backs up the local workspace and registered exchange independently, restores each to a new location, revalidates project trust and shared-manifest continuity, and publishes another signed change through the restored exchange.

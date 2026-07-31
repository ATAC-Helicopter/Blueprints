# Workspace format

This document describes the current schema-1 layout. The format remains pre-release, but Blueprints now inspects it through an explicit compatibility and migration boundary before loading signed project state.

## Local workspace

```text
<workspace>/
├── project/
│   ├── project.json
│   ├── project.sig
│   ├── members.json
│   ├── members.sig
│   ├── canvas-layout.json      # optional until first layout save
│   ├── canvas-layout.sig
│   ├── relationships.json      # optional until first relationship type save
│   └── relationships.sig
├── versions/
│   └── <version-id-without-dashes>/
│       ├── version.json
│       ├── version.sig
│       └── items/
│           ├── <item-id-without-dashes>.json
│           └── <item-id-without-dashes>.sig
├── log/
│   ├── <change-id>.json
│   └── <change-id>.sig
├── sync/
│   ├── state.json
│   ├── inbox/                   # staged pull batches and rollback pairs
│   └── staging/                 # prepared push batches
└── .blueprints/
    ├── canvas-view.json
    ├── trusted-project-keys.json
    ├── archive/                 # recoverable draft version/item archives
    └── recovery/
        └── conflicts/
            └── <recovery-id>/
                ├── resolution.json
                ├── local/       # preserved document/signature pair, when present
                └── shared/      # preserved document/signature pair, when present
```

The collaboration layer also writes a signed manifest in the shared project root. Files under `.blueprints/` and `sync/` are local bookkeeping and are not signed project truth or exchange input.

A VaultSync-prepared shared root also contains `.blueprints-exchange.json` at its top level. This bounded machine-local registration marker identifies the intended Blueprints project but is not signed project truth and is excluded from exchange snapshots. Project documents and the signed manifest retain exactly the same format as any other shared root.

`trusted-project-keys.json` is the machine-local trust-anchor set established when a project is created or a signed project invitation is accepted. It lets documents and audit entries signed by different project members validate without placing trust in the shared folder itself. A verified membership revision may extend this set, but unverified shared membership data cannot.

Before applying a whole-document conflict choice, Blueprints creates a conflict recovery directory. Its metadata records the selected path, choice, presence of each source file, and whether the operation was prepared, applied, or failed. Recovery data stays local and is excluded from exchange manifests.

Archiving a draft version or item first copies its signed data beneath `.blueprints/archive/<archive-id>/`, writes `archive.json`, removes it from active signed project state, and records an audit entry. The UI requires the archive action twice for confirmation. Frozen and released content cannot be archived.

## Serialization

- JSON uses camel-case property names.
- Objects are recursively ordered by property name before writing.
- Output is compact UTF-8 without a byte-order mark.
- Each signed `.json` file has a sibling `.sig`.

Signature files contain:

```json
{
  "algorithm": "Ed25519",
  "keyId": "<identity key id>",
  "signatureBase64": "<detached signature>"
}
```

The signature covers the exact canonical UTF-8 JSON bytes.

## Documents

### `project.json`

Contains the schema version, project identity, name, code, versioning scheme, creation time, categories, item types, item-key rules, and changelog defaults.

### `members.json`

Contains the membership revision and members. A member has a user ID, display name, public key, role, join time, and active flag.

Roles are Viewer, Editor, and Admin.

### `canvas-layout.json`

Contains the shared visual arrangement without duplicating version or item content:

```json
{
  "schemaVersion": 1,
  "projectId": "00000000-0000-0000-0000-000000000000",
  "revision": 1,
  "nodes": [
    {
      "nodeType": "project",
      "entityId": "00000000-0000-0000-0000-000000000000",
      "x": 48,
      "y": 310
    }
  ],
  "updatedUtc": "2026-07-30T00:00:00+00:00",
  "lastModifiedByUserId": "00000000-0000-0000-0000-000000000000",
  "lastModifiedByName": "Local Admin"
}
```

The real project ID replaces the illustrative zero GUIDs. The file is optional for older schema-1 workspaces. Once present, its signature and project identity must validate. Node identities must reference existing signed entities.

See [canvas engine](canvas-engine.md) for validation limits and interaction behavior.

### `relationships.json`

Contains user-defined relationship types and edges without changing the ownership hierarchy:

```json
{
  "schemaVersion": 1,
  "projectId": "00000000-0000-0000-0000-000000000000",
  "revision": 1,
  "types": [
    {
      "typeId": "blocks",
      "name": "Blocks",
      "description": "Must complete first",
      "colorHex": "#E05A47",
      "isDirectional": true
    }
  ],
  "relationships": [
    {
      "relationshipId": "00000000-0000-0000-0000-000000000000",
      "typeId": "blocks",
      "source": { "nodeType": "item", "entityId": "00000000-0000-0000-0000-000000000000" },
      "target": { "nodeType": "version", "entityId": "00000000-0000-0000-0000-000000000000" },
      "label": "Release gate"
    }
  ],
  "updatedUtc": "2026-07-30T00:00:00+00:00",
  "lastModifiedByUserId": "00000000-0000-0000-0000-000000000000",
  "lastModifiedByName": "Local Admin"
}
```

Real entity IDs replace the illustrative zero GUIDs. Type IDs are lowercase slugs, colors use `#RRGGBB`, endpoints must be different existing project/version/item nodes, and duplicate logical edges are rejected. Undirected edges treat A–B and B–A as the same relationship. Archiving an entity removes every edge that references it and advances the relationship revision.

The file is signed, audited, synchronized, and optional for schema-1 compatibility. The complete document is one conflict domain: conflict resolution chooses the local or shared type-and-edge graph as a whole.

### `.blueprints/canvas-view.json`

Contains bounded machine-local zoom and scroll offsets plus the selected canvas mode, search, filters, minimap visibility, and collapsed version-frame IDs. It is deliberately unsigned and excluded from exchange snapshots. Invalid or missing view state falls back to Plan mode, zoom `1`, zero offsets, and no filters without changing workspace trust.

### `version.json`

Contains a version ID, name, lifecycle status, creation/release times, notes, and manual item ordering.

Statuses are Planned, In Progress, Frozen, and Released.

### item document

Contains project/version/item IDs, stable item key, type, category, title, optional description, completion state, optional lifecycle state, tags, timestamps, and last-modifier identity.

The optional `workflowState` uses Planned, In Progress, Review, or Complete. It was added compatibly to schema 1: a missing value maps to Planned when `isDone` is false and Complete when `isDone` is true. Blueprints does not rewrite an old item merely by opening it. New edits keep `isDone` and Complete consistent so changelog export retains its established completion behavior.

### audit entry

Contains a unique change ID, operation, human summary, author, membership revision observed, timestamp, and SHA-256 hash of the previous audit JSON file. The entry and its link are signed.

## Compatibility

Schema 1 is the minimum and current supported schema. Optional schema-1 documents, including the canvas layout and relationship graph, use explicit missing-file compatibility.

Before normal loading, Blueprints reads the bounded `project.json` schema field. A schema newer than the application supports is rejected with an upgrade instruction instead of being interpreted optimistically. Current-schema workspaces are not rewritten.

The migration engine requires an uninterrupted one-version-at-a-time chain. Before a migration it creates a complete ZIP backup in a machine-local sibling migration-backup directory. Each migration runs against a staged workspace, must produce its declared target schema, and is promoted through the atomic workspace transaction only after validation. A failure restores the original directory and removes the incomplete backup.

Do not manually change `schemaVersion`. Project backups do not contain private signing keys; preserve and test an encrypted identity backup separately.

## Files that must never be shared as project data

- private signing keys;
- the local AES-GCM protection key;
- provider access tokens;
- machine-specific integration settings;
- arbitrary repository content.

Signed `*.blueprints-identity.json` and `*.blueprints-project.json` invitation files are deliberate out-of-band handoff artifacts, not workspace documents. Identity invitations prove possession of the included private key. Project invitations bind a target identity, project identity, membership revision, exchange hint, inviter administrator, and trusted member-key set under the inviter's signature.

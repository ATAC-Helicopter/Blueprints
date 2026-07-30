# Workspace format

This document describes the current schema-1 layout. The format is pre-release and may change before v1.0.

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

Contains machine-local zoom and scroll offsets. It is deliberately unsigned and excluded from exchange snapshots. Invalid or missing view state falls back to zoom `1` and zero offsets without changing workspace trust.

### `version.json`

Contains a version ID, name, lifecycle status, creation/release times, notes, and manual item ordering.

Statuses are Planned, In Progress, Frozen, and Released.

### item document

Contains project/version/item IDs, stable item key, type, category, title, optional description, completion state, tags, timestamps, and last-modifier identity.

### audit entry

Contains a unique change ID, operation, human summary, author, membership revision observed, timestamp, and SHA-256 hash of the previous audit JSON file. The entry and its link are signed.

## Compatibility

There is no general migration engine yet. Optional schema-1 documents, including the canvas layout and relationship graph, use explicit missing-file compatibility. Do not manually change `schemaVersion`. Before adopting a future schema, preserve a complete copy of the workspace and identity data.

## Files that must never be shared as project data

- private signing keys;
- the local AES-GCM protection key;
- provider access tokens;
- machine-specific integration settings;
- arbitrary repository content.

Signed `*.blueprints-identity.json` and `*.blueprints-project.json` invitation files are deliberate out-of-band handoff artifacts, not workspace documents. Identity invitations prove possession of the included private key. Project invitations bind a target identity, project identity, membership revision, exchange hint, inviter administrator, and trusted member-key set under the inviter's signature.

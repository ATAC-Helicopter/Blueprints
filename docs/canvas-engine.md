# Canvas engine

The canvas is the primary Blueprints workspace. It visualizes signed project entities without replacing them with a second source of truth.

## Graph projection

The current graph is derived from existing domain relationships:

```text
project
└── version
    └── work item
```

- The project node uses `ProjectConfigurationDocument.ProjectId`.
- A version node uses `VersionDocument.VersionId`.
- A work-item node uses `ItemDocument.ItemId`.
- Connector lines are derived from version ownership; they are not persisted separately.

Changing a node title, state, category, or completion value uses the existing version and item workflows. Moving a node changes only the layout document.

## Interaction model

| Action | Result |
| --- | --- |
| Click a version | Select it and open version fields in the inspector |
| Click a work item | Select it, its owning version, and its item fields |
| Drag a node | Move it and save a new signed layout revision on release |
| Middle-drag empty canvas | Pan without changing shared node positions |
| Scroll the canvas | Save machine-local viewport offsets after a short debounce |
| Ctrl+mouse wheel or zoom buttons | Change and save machine-local zoom |
| Fit view | Use the standard local overview zoom and return to the origin |
| Auto arrange | Rebuild deterministic default positions and save them |
| Undo / redo | Restore the previous or next arrangement and save it as a new signed revision |
| Save layout | Explicitly write the current shared node positions |
| Ctrl+S | Save shared node positions |
| Ctrl+Z | Undo the latest node drag or auto-arrangement |
| Ctrl+Shift+Z or Ctrl+Y | Redo the latest undone layout change |
| Ctrl+0 | Fit the local view |
| Ctrl+plus/minus | Zoom the local view |

Editing and layout controls are disabled when the workspace is untrusted or has unresolved sync conflicts.

## Persistence

Shared node positions are stored in:

```text
project/canvas-layout.json
project/canvas-layout.sig
```

The signed layout document contains:

- schema and project identity;
- monotonically increasing layout revision;
- node type, entity ID, and coordinates;
- update time and last-modifier identity.

The document is optional for schema-1 compatibility. An older workspace without it opens normally and receives deterministic default positions. The first layout save creates revision 1.

Each successful save:

1. reopens and validates the workspace;
2. requires trusted, conflict-free mutable state;
3. verifies every layout node refers to an existing project entity;
4. validates coordinate, uniqueness, and size limits;
5. writes canonical JSON and an Ed25519 detached signature;
6. appends a signed `canvas.layout.save` audit entry;
7. reloads displayed state from disk.

Layout writes do not rewrite version or item documents.

Undo and redo history is held only for the current application session, is capped at 50 layout changes, and is cleared when the workspace or graph structure changes. History snapshots are not a hidden source of truth. Applying one creates a normal signed layout revision and audit entry, preserving the same validation and collaboration guarantees as a direct drag.

Zoom and viewport offsets are machine-local preferences stored in:

```text
.blueprints/canvas-view.json
```

This file is not signed, audited, synchronized, or authoritative. Invalid local values fall back to the default view. Keeping viewport preferences local prevents ordinary scrolling from creating audit noise or collaboration conflicts.

## Validation limits

The schema-1 validator enforces:

- supported node types: `project`, `version`, and `item`;
- non-empty and unique `(nodeType, entityId)` pairs;
- entity membership in the current workspace;
- finite coordinates from `0` through `100000`;
- no more than `10000` nodes;
- matching non-empty project identity;
- schema version `1` and revision of at least `1`.

These limits prevent malformed, non-finite, or unbounded layout input from reaching Avalonia rendering.

The local view-state store separately accepts zoom from `0.6` through `1.5` and non-negative offsets through `100000`.

## Sync and conflicts

The layout lives under `project/`, so the exchange snapshot, manifest, signature validator, push, pull, and conflict analyzer include it automatically.

Two collaborators moving the same canvas from a common baseline may produce a conflict in `project/canvas-layout.json`. Blueprints deliberately treats the complete layout as one document in schema 1. Conflict resolution therefore chooses the local or shared arrangement as a whole.

Version and work-item content remain separate documents. A layout conflict does not imply that their content has conflicted.

## Current limits

- There is one shared release-planning canvas per project.
- Node positions are shared project state, not per-user preferences.
- Connectors represent ownership and cannot yet be created as arbitrary edge types.
- There is no minimap, box selection, grouping, or keyboard-driven node movement yet.
- Auto arrangement is deterministic but not a graph-optimization engine.
- Layout conflict resolution is whole-document.

These are product limitations, not hidden behavior. Planned work belongs in the canonical [roadmap](../Roadmap.md).
